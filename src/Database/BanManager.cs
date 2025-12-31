using Dommel;
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
            using var connection = _core.Database.GetConnection("default");
            MigrationRunner.RunMigrations(connection);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] Ban database initialized successfully");
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error initializing ban database: {Message}", ex.Message);
        }
    }

    public async Task<bool> AddBanAsync(ulong steamId, int durationMinutes, string reason)
    {
        return await Task.Run(() =>
        {
            try
            {
                var admin = _currentAdmin.Value ?? new AdminContext();
                DateTime? expiresAt = durationMinutes > 0 ? DateTime.UtcNow.AddMinutes(durationMinutes) : null;

                var ban = new Ban
                {
                    SteamId = steamId,
                    AdminName = admin.Name,
                    AdminSteamId = admin.SteamId,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    Status = BanStatus.Active
                };

                using var connection = _core.Database.GetConnection("default");
                var id = connection.Insert(ban);
                ban.Id = Convert.ToInt32(id);
                _banCache[steamId] = ban;

                return true;
            }
            catch (Exception ex)
            {
                _core.Logger.LogErrorIfEnabled("[T3-Admin] Error adding ban: {Message}", ex.Message);
                return false;
            }
        });
    }

    public async Task<bool> UnbanAsync(ulong steamId, string unbanReason)
    {
        return await Task.Run(() =>
        {
            try
            {
                var admin = _currentAdmin.Value ?? new AdminContext();
                using var connection = _core.Database.GetConnection("default");
                
                var ban = connection.FirstOrDefault<Ban>(b => b.SteamId == steamId && b.Status == BanStatus.Active);
                if (ban == null) return false;

                ban.Status = BanStatus.Unbanned;
                ban.UnbanAdminName = admin.Name;
                ban.UnbanAdminSteamId = admin.SteamId;
                ban.UnbanReason = unbanReason;
                ban.UnbanDate = DateTime.UtcNow;

                connection.Update(ban);
                _banCache.Remove(steamId);

                return true;
            }
            catch (Exception ex)
            {
                _core.Logger.LogErrorIfEnabled("[T3-Admin] Error unbanning player: {Message}", ex.Message);
                return false;
            }
        });
    }

    public async Task<Ban?> GetActiveBanAsync(ulong steamId)
    {
        return await Task.Run(() =>
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

                using var connection = _core.Database.GetConnection("default");
                var ban = connection.FirstOrDefault<Ban>(b => 
                    b.SteamId == steamId && 
                    b.Status == BanStatus.Active &&
                    (b.ExpiresAt == null || b.ExpiresAt > DateTime.UtcNow));

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
        });
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
        return await Task.Run(() =>
        {
            try
            {
                using var connection = _core.Database.GetConnection("default");
                var bans = connection.Select<Ban>(b => b.SteamId == steamId);
                return bans.Count();
            }
            catch (Exception ex)
            {
                _core.Logger.LogErrorIfEnabled("[T3-Admin] Error getting total bans: {Message}", ex.Message);
                return 0;
            }
        });
    }

    public async Task CleanupExpiredBansAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var connection = _core.Database.GetConnection("default");
                var expiredBans = connection.Select<Ban>(b => 
                    b.Status == BanStatus.Active && 
                    b.ExpiresAt != null && 
                    b.ExpiresAt <= DateTime.UtcNow);

                int cleaned = 0;
                foreach (var ban in expiredBans)
                {
                    ban.Status = BanStatus.Expired;
                    connection.Update(ban);
                    cleaned++;
                }

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
        });
    }

    public void ClearCache()
    {
        _banCache.Clear();
    }
}
