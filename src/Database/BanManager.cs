using Dapper;
using Furien_Admin.Models;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Furien_Admin.Database;

public class BanManager
{
    private readonly ISwiftlyCore _core;
    private readonly Dictionary<ulong, Ban> _banCache = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly AsyncLocal<AdminContext> _currentAdmin = new();

    public BanManager(ISwiftlyCore core)
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
                CREATE TABLE IF NOT EXISTS `t3_bans` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `steamid` BIGINT UNSIGNED NOT NULL,
                    `admin_name` VARCHAR(64) NOT NULL,
                    `admin_steamid` BIGINT UNSIGNED NOT NULL,
                    `reason` TEXT NOT NULL,
                    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    `expires_at` TIMESTAMP NULL,
                    `status` ENUM('active', 'expired', 'unbanned') DEFAULT 'active',
                    `unban_admin_name` VARCHAR(64) NULL,
                    `unban_admin_steamid` BIGINT UNSIGNED NULL,
                    `unban_reason` TEXT NULL,
                    `unban_date` TIMESTAMP NULL,
                    INDEX `idx_steamid_status` (`steamid`, `status`),
                    INDEX `idx_expires_status` (`expires_at`, `status`),
                    INDEX `idx_created_at` (`created_at`),
                    INDEX `idx_status` (`status`)
                )";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();
            await connection.ExecuteAsync(createTable);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] Ban database initialized successfully");
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error initializing ban database: {Message}", ex.Message);
        }
    }

    public async Task<bool> AddBanAsync(ulong steamId, int durationMinutes, string reason)
    {
        try
        {
            var admin = _currentAdmin.Value ?? new AdminContext();
            DateTime? expiresAt = durationMinutes > 0 ? DateTime.UtcNow.AddMinutes(durationMinutes) : null;

            const string query = @"
                INSERT INTO t3_bans (steamid, admin_name, admin_steamid, reason, expires_at, status) 
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
                _banCache[steamId] = new Ban
                {
                    SteamId = steamId,
                    AdminName = admin.Name,
                    AdminSteamId = admin.SteamId,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    Status = BanStatus.Active
                };
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error adding ban: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> UnbanAsync(ulong steamId, string unbanReason)
    {
        try
        {
            var admin = _currentAdmin.Value ?? new AdminContext();

            const string query = @"
                UPDATE t3_bans 
                SET status = 'unbanned', 
                    unban_admin_name = @UnbanAdminName,
                    unban_admin_steamid = @UnbanAdminSteamId,
                    unban_reason = @UnbanReason,
                    unban_date = UTC_TIMESTAMP()
                WHERE steamid = @SteamId AND status = 'active'";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int result = await connection.ExecuteAsync(query, new
            {
                SteamId = steamId,
                UnbanAdminName = admin.Name,
                UnbanAdminSteamId = admin.SteamId,
                UnbanReason = unbanReason
            });

            if (result > 0)
            {
                _banCache.Remove(steamId);
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error unbanning player: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<Ban?> GetActiveBanAsync(ulong steamId)
    {
        try
        {
            if (_banCache.TryGetValue(steamId, out Ban? cachedBan) &&
                DateTime.UtcNow - _lastCacheUpdate < _cacheLifetime)
            {
                if (cachedBan.IsExpired || cachedBan.Status != BanStatus.Active)
                {
                    _banCache.Remove(steamId);
                    return null;
                }
                return cachedBan;
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
                    unban_admin_name as UnbanAdminName,
                    unban_admin_steamid as UnbanAdminSteamId,
                    unban_reason as UnbanReason,
                    unban_date as UnbanDate
                FROM t3_bans 
                WHERE steamid = @SteamId 
                AND status = 'active'
                AND (expires_at IS NULL OR expires_at > UTC_TIMESTAMP())
                LIMIT 1";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            var ban = await connection.QueryFirstOrDefaultAsync<Ban>(query, new { SteamId = steamId });

            if (ban != null)
            {
                _banCache[steamId] = ban;
                _lastCacheUpdate = DateTime.UtcNow;
            }
            else
            {
                _banCache.Remove(steamId);
            }

            return ban;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error checking ban: {Message}", ex.Message);
            return null;
        }
    }

    public Ban? GetActiveBanFromCache(ulong steamId)
    {
        if (_banCache.TryGetValue(steamId, out Ban? cachedBan) && cachedBan.IsActive)
        {
            return cachedBan;
        }
        return null;
    }

    public async Task<int> GetTotalBansAsync(ulong steamId)
    {
        try
        {
            const string query = @"SELECT COUNT(*) FROM t3_bans WHERE steamid = @SteamId";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            var count = await connection.ExecuteScalarAsync<int>(query, new { SteamId = steamId });
            return count;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error getting total bans: {Message}", ex.Message);
            return 0;
        }
    }

    public async Task CleanupExpiredBansAsync()
    {
        try
        {
            const string query = @"
                UPDATE t3_bans 
                SET status = 'expired' 
                WHERE status = 'active' 
                AND expires_at IS NOT NULL 
                AND expires_at <= UTC_TIMESTAMP()";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int cleaned = await connection.ExecuteAsync(query);

            if (cleaned > 0)
            {
                _core.Logger.LogInformationIfEnabled("[T3-Admin] Marked {Count} bans as expired", cleaned);
                _banCache.Clear();
            }
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error cleaning expired bans: {Message}", ex.Message);
        }
    }

    public void ClearCache()
    {
        _banCache.Clear();
    }
}
