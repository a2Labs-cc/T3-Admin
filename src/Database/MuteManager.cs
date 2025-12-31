using Dapper;
using Furien_Admin.Models;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Furien_Admin.Database;

public class MuteManager
{
    private readonly ISwiftlyCore _core;
    private readonly Dictionary<ulong, Mute> _muteCache = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly AsyncLocal<AdminContext> _currentAdmin = new();

    public MuteManager(ISwiftlyCore core)
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
                CREATE TABLE IF NOT EXISTS `t3_mutes` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `steamid` BIGINT UNSIGNED NOT NULL,
                    `admin_name` VARCHAR(64) NOT NULL,
                    `admin_steamid` BIGINT UNSIGNED NOT NULL,
                    `reason` TEXT NOT NULL,
                    `created_at` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    `expires_at` TIMESTAMP NULL,
                    `status` ENUM('active', 'expired', 'unmuted') DEFAULT 'active',
                    `unmute_admin_name` VARCHAR(64) NULL,
                    `unmute_admin_steamid` BIGINT UNSIGNED NULL,
                    `unmute_reason` TEXT NULL,
                    `unmute_date` TIMESTAMP NULL,
                    INDEX `idx_steamid_status` (`steamid`, `status`),
                    INDEX `idx_expires_status` (`expires_at`, `status`),
                    INDEX `idx_status` (`status`)
                )";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();
            await connection.ExecuteAsync(createTable);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] Mute database initialized successfully");
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error initializing mute database: {Message}", ex.Message);
        }
    }

    public async Task<bool> AddMuteAsync(ulong steamId, int durationMinutes, string reason)
    {
        try
        {
            var admin = _currentAdmin.Value ?? new AdminContext();
            DateTime? expiresAt = durationMinutes > 0 ? DateTime.UtcNow.AddMinutes(durationMinutes) : null;

            const string query = @"
                INSERT INTO t3_mutes (steamid, admin_name, admin_steamid, reason, expires_at, status) 
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
                _muteCache[steamId] = new Mute
                {
                    SteamId = steamId,
                    AdminName = admin.Name,
                    AdminSteamId = admin.SteamId,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    Status = MuteStatus.Active
                };
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error adding mute: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<bool> UnmuteAsync(ulong steamId, string unmuteReason)
    {
        try
        {
            var admin = _currentAdmin.Value ?? new AdminContext();

            const string query = @"
                UPDATE t3_mutes 
                SET status = 'unmuted', 
                    unmute_admin_name = @UnmuteAdminName,
                    unmute_admin_steamid = @UnmuteAdminSteamId,
                    unmute_reason = @UnmuteReason,
                    unmute_date = UTC_TIMESTAMP()
                WHERE steamid = @SteamId AND status = 'active'";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int result = await connection.ExecuteAsync(query, new
            {
                SteamId = steamId,
                UnmuteAdminName = admin.Name,
                UnmuteAdminSteamId = admin.SteamId,
                UnmuteReason = unmuteReason
            });

            if (result > 0)
            {
                _muteCache.Remove(steamId);
            }

            return result > 0;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error unmuting player: {Message}", ex.Message);
            return false;
        }
    }

    public async Task<Mute?> GetActiveMuteAsync(ulong steamId)
    {
        try
        {
            if (_muteCache.TryGetValue(steamId, out Mute? cachedMute) &&
                DateTime.UtcNow - _lastCacheUpdate < _cacheLifetime)
            {
                if (cachedMute.IsExpired || cachedMute.Status != MuteStatus.Active)
                {
                    _muteCache.Remove(steamId);
                    return null;
                }
                return cachedMute;
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
                    unmute_admin_name as UnmuteAdminName,
                    unmute_admin_steamid as UnmuteAdminSteamId,
                    unmute_reason as UnmuteReason,
                    unmute_date as UnmuteDate
                FROM t3_mutes 
                WHERE steamid = @SteamId 
                AND status = 'active'
                AND (expires_at IS NULL OR expires_at > UTC_TIMESTAMP())
                LIMIT 1";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            var mute = await connection.QueryFirstOrDefaultAsync<Mute>(query, new { SteamId = steamId });

            if (mute != null)
            {
                _muteCache[steamId] = mute;
                _lastCacheUpdate = DateTime.UtcNow;
            }
            else
            {
                _muteCache.Remove(steamId);
            }

            return mute;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error checking mute: {Message}", ex.Message);
            return null;
        }
    }

    public Mute? GetActiveMuteFromCache(ulong steamId)
    {
        if (_muteCache.TryGetValue(steamId, out Mute? cachedMute) && cachedMute.IsActive)
        {
            return cachedMute;
        }
        return null;
    }

    public async Task<int> GetTotalMutesAsync(ulong steamId)
    {
        try
        {
            const string query = @"SELECT COUNT(*) FROM t3_mutes WHERE steamid = @SteamId";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            var count = await connection.ExecuteScalarAsync<int>(query, new { SteamId = steamId });
            return count;
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error getting total mutes: {Message}", ex.Message);
            return 0;
        }
    }

    public async Task UpdateExpiredMutesAsync()
    {
        try
        {
            const string query = @"
                UPDATE t3_mutes 
                SET status = 'expired' 
                WHERE status = 'active' 
                AND expires_at IS NOT NULL 
                AND expires_at <= UTC_TIMESTAMP()";

            using var connection = _core.Database.GetConnection("default");
            connection.Open();

            int cleaned = await connection.ExecuteAsync(query);

            if (cleaned > 0)
            {
                _core.Logger.LogInformationIfEnabled("[T3-Admin] Marked {Count} mutes as expired", cleaned);
                _muteCache.Clear();
            }
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error cleaning expired mutes: {Message}", ex.Message);
        }
    }

    public void ClearCache()
    {
        _muteCache.Clear();
    }
}
