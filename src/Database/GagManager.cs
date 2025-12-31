using Dapper;
using Furien_Admin.Models;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Furien_Admin.Database;

public class GagManager
{
    private readonly ISwiftlyCore _core;
    private readonly Dictionary<ulong, Gag> _gagCache = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly AsyncLocal<AdminContext> _currentAdmin = new();

    public GagManager(ISwiftlyCore core)
    {
        _core = core;
    }

    public void SetAdminContext(string? adminName, ulong? adminSteamId)
    {
        _currentAdmin.Value = new AdminContext
        {
            Name = adminName ?? _core.Localizer["console_name"],
            SteamId = adminSteamId ?? 0
        };
    }

    public async Task InitializeAsync()
    {
        try
        {
            const string createTable = @"
                CREATE TABLE IF NOT EXISTS `t3_gags` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `steamid` BIGINT UNSIGNED NOT NULL,
                    `admin_name` VARCHAR(64) NOT NULL,
                    `admin_steamid` BIGINT UNSIGNED NOT NULL,
                    `reason` TEXT NOT NULL,
                    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    `expires_at` TIMESTAMP NULL,
                    `status` ENUM('active', 'expired', 'ungagged') DEFAULT 'active',
                    `ungag_admin_name` VARCHAR(64) NULL,
                    `ungag_admin_steamid` BIGINT UNSIGNED NULL,
                    `ungag_reason` TEXT NULL,
                    `ungag_date` TIMESTAMP NULL,
                    INDEX `idx_steamid_status` (`steamid`, `status`),
                    INDEX `idx_expires_status` (`expires_at`, `status`),
                    INDEX `idx_status` (`status`)
                )";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();
            await connection.ExecuteAsync(createTable);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] Gag database initialized successfully");
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error initializing gag database: {Message}", ex.Message);
        }
    }

    public async Task<bool> AddGagAsync(ulong steamId, int durationMinutes, string reason)
    {
        try
        {
            var admin = _currentAdmin.Value ?? new AdminContext();
            DateTime? expiresAt = durationMinutes > 0 ? DateTime.UtcNow.AddMinutes(durationMinutes) : null;

            const string query = @"
                INSERT INTO t3_gags (steamid, admin_name, admin_steamid, reason, expires_at, status) 
                VALUES (@SteamId, @AdminName, @AdminSteamId, @Reason, @ExpiresAt, 'active')";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int result = await connection.ExecuteAsync(query, new
            {
                SteamId = steamId,
                AdminName = admin.Name,
                AdminSteamId = admin.SteamId,
                Reason = reason,
                ExpiresAt = expiresAt
            });

            if (result > 0)
            {
                _gagCache[steamId] = new Gag
                {
                    SteamId = steamId,
                    AdminName = admin.Name,
                    AdminSteamId = admin.SteamId,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    Status = GagStatus.Active
                };
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error adding gag: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> UngagAsync(ulong steamId, string ungagReason)
    {
        try
        {
            var admin = _currentAdmin.Value ?? new AdminContext();

            const string query = @"
                UPDATE t3_gags 
                SET status = 'ungagged', 
                    ungag_admin_name = @UngagAdminName,
                    ungag_admin_steamid = @UngagAdminSteamId,
                    ungag_reason = @UngagReason,
                    ungag_date = UTC_TIMESTAMP()
                WHERE steamid = @SteamId AND status = 'active'";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int result = await connection.ExecuteAsync(query, new
            {
                SteamId = steamId,
                UngagAdminName = admin.Name,
                UngagAdminSteamId = admin.SteamId,
                UngagReason = ungagReason
            });

            if (result > 0)
            {
                _gagCache.Remove(steamId);
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error ungagging player: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<Gag?> GetActiveGagAsync(ulong steamId)
    {
        try
        {
            if (_gagCache.TryGetValue(steamId, out Gag? cachedGag) &&
                DateTime.UtcNow - _lastCacheUpdate < _cacheLifetime)
            {
                if (cachedGag.IsExpired || cachedGag.Status != GagStatus.Active)
                {
                    _gagCache.Remove(steamId);
                    return null;
                }
                return cachedGag;
            }

            const string query = @"
                SELECT 
                    id as Id,
                    steamid as SteamId,
                    admin_name as AdminName,
                    admin_steamid as AdminSteamId,
                    reason as Reason,
                    created_at as CreatedAt,
                    expires_at as ExpiresAt,
                    status as Status,
                    ungag_admin_name as UngagAdminName,
                    ungag_admin_steamid as UngagAdminSteamId,
                    ungag_reason as UngagReason,
                    ungag_date as UngagDate
                FROM t3_gags 
                WHERE steamid = @SteamId 
                AND status = 'active'
                AND (expires_at IS NULL OR expires_at > UTC_TIMESTAMP())
                LIMIT 1";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            var gag = await connection.QueryFirstOrDefaultAsync<Gag>(query, new { SteamId = steamId });

            if (gag != null)
            {
                _gagCache[steamId] = gag;
                _lastCacheUpdate = DateTime.UtcNow;
            }
            else
            {
                _gagCache.Remove(steamId);
            }

            return gag;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error checking gag: {Message}", ex.Message);
            return null;
        }
    }

    public Gag? GetActiveGagFromCache(ulong steamId)
    {
        if (_gagCache.TryGetValue(steamId, out Gag? cachedGag) && cachedGag.IsActive)
        {
            return cachedGag;
        }
        return null;
    }

    public async Task<int> GetTotalGagsAsync(ulong steamId)
    {
        try
        {
            const string query = @"SELECT COUNT(*) FROM t3_gags WHERE steamid = @SteamId";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            var count = await connection.ExecuteScalarAsync<int>(query, new { SteamId = steamId });
            return count;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error getting total gags: {Message}", ex.Message);
            return 0;
        }
    }

    public async Task CleanupExpiredGagsAsync()
    {
        try
        {
            const string query = @"
                UPDATE t3_gags 
                SET status = 'expired' 
                WHERE status = 'active' 
                AND expires_at IS NOT NULL 
                AND expires_at <= UTC_TIMESTAMP()";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int cleaned = await connection.ExecuteAsync(query);

            if (cleaned > 0)
            {
                _core.Logger.LogInformationIfEnabled("[T3-Admin] Marked {Count} gags as expired", cleaned);
                _gagCache.Clear();
            }
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error cleaning expired gags: {Message}", ex.Message);
        }
    }

    public void ClearCache()
    {
        _gagCache.Clear();
    }
}
