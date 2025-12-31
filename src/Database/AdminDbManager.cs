using Dapper;
using Furien_Admin.Models;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Furien_Admin.Database;

public class AdminDbManager
{
    private readonly ISwiftlyCore _core;
    private readonly Dictionary<ulong, Admin> _adminCache = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);

    public AdminDbManager(ISwiftlyCore core)
    {
        _core = core;
    }

    public async Task InitializeAsync()
    {
        try
        {
            const string createTable = @"
                CREATE TABLE IF NOT EXISTS `t3_admins` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `steamid` BIGINT UNSIGNED NOT NULL UNIQUE,
                    `name` VARCHAR(64) NOT NULL,
                    `flags` VARCHAR(255) NOT NULL,
                    `immunity` INT DEFAULT 0,
                    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    `expires_at` TIMESTAMP NULL,
                    `added_by` VARCHAR(64) NULL,
                    `added_by_steamid` BIGINT UNSIGNED NULL,
                    INDEX `idx_steamid` (`steamid`),
                    INDEX `idx_expires` (`expires_at`)
                )";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();
            await connection.ExecuteAsync(createTable);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] Admin database initialized successfully");
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error initializing admin database: {Message}", ex.Message);
        }
    }

    public async Task<bool> AddAdminAsync(ulong steamId, string name, string flags, int immunity, string? addedBy, ulong? addedBySteamId, int? durationDays = null)
    {
        try
        {
            DateTime? expiresAt = durationDays.HasValue && durationDays.Value > 0 
                ? DateTime.UtcNow.AddDays(durationDays.Value) 
                : null;

            const string query = @"
                INSERT INTO t3_admins (steamid, name, flags, immunity, expires_at, added_by, added_by_steamid) 
                VALUES (@SteamId, @Name, @Flags, @Immunity, @ExpiresAt, @AddedBy, @AddedBySteamId)
                ON DUPLICATE KEY UPDATE 
                    name = @Name, 
                    flags = @Flags, 
                    immunity = @Immunity, 
                    expires_at = @ExpiresAt,
                    added_by = @AddedBy,
                    added_by_steamid = @AddedBySteamId";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int result = await connection.ExecuteAsync(query, new
            {
                SteamId = steamId,
                Name = name,
                Flags = flags,
                Immunity = immunity,
                ExpiresAt = expiresAt,
                AddedBy = addedBy,
                AddedBySteamId = addedBySteamId
            });

            if (result > 0)
            {
                _adminCache[steamId] = new Admin
                {
                    SteamId = steamId,
                    Name = name,
                    Flags = flags,
                    Immunity = immunity,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    AddedBy = addedBy,
                    AddedBySteamId = addedBySteamId
                };
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error adding admin: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> RemoveAdminAsync(ulong steamId)
    {
        try
        {
            const string query = "DELETE FROM t3_admins WHERE steamid = @SteamId";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int result = await connection.ExecuteAsync(query, new { SteamId = steamId });

            if (result > 0)
            {
                _adminCache.Remove(steamId);
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error removing admin: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<Admin?> GetAdminAsync(ulong steamId)
    {
        try
        {
            if (_adminCache.TryGetValue(steamId, out Admin? cachedAdmin) &&
                DateTime.UtcNow - _lastCacheUpdate < _cacheLifetime)
            {
                if (cachedAdmin.IsExpired)
                {
                    _adminCache.Remove(steamId);
                    return null;
                }
                return cachedAdmin;
            }

            const string query = @"
                SELECT 
                    id as Id,
                    steamid as SteamId,
                    name as Name,
                    flags as Flags,
                    immunity as Immunity,
                    created_at as CreatedAt,
                    expires_at as ExpiresAt,
                    added_by as AddedBy,
                    added_by_steamid as AddedBySteamId
                FROM t3_admins 
                WHERE steamid = @SteamId 
                AND (expires_at IS NULL OR expires_at > UTC_TIMESTAMP())
                LIMIT 1";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            var admin = await connection.QueryFirstOrDefaultAsync<Admin>(query, new { SteamId = steamId });

            if (admin != null)
            {
                _adminCache[steamId] = admin;
                _lastCacheUpdate = DateTime.UtcNow;
            }
            else
            {
                _adminCache.Remove(steamId);
            }

            return admin;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error getting admin: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<List<Admin>> GetAllAdminsAsync()
    {
        try
        {
            const string query = @"
                SELECT 
                    id as Id,
                    steamid as SteamId,
                    name as Name,
                    flags as Flags,
                    immunity as Immunity,
                    created_at as CreatedAt,
                    expires_at as ExpiresAt,
                    added_by as AddedBy,
                    added_by_steamid as AddedBySteamId
                FROM t3_admins 
                WHERE expires_at IS NULL OR expires_at > UTC_TIMESTAMP()
                ORDER BY immunity DESC, name ASC";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            var admins = await connection.QueryAsync<Admin>(query);
            return admins.ToList();
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error getting all admins: {Message}", ex.Message);
            return new List<Admin>();
        }
    }

    public async Task CleanupExpiredAdminsAsync()
    {
        try
        {
            const string query = @"
                DELETE FROM t3_admins 
                WHERE expires_at IS NOT NULL 
                AND expires_at <= UTC_TIMESTAMP()";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int cleaned = await connection.ExecuteAsync(query);

            if (cleaned > 0)
            {
                _core.Logger.LogInformationIfEnabled("[T3-Admin] Removed {Count} expired admins", cleaned);
                _adminCache.Clear();
            }
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error cleaning expired admins: {Message}", ex.Message);
        }
    }

    public void ClearCache()
    {
        _adminCache.Clear();
    }
}
