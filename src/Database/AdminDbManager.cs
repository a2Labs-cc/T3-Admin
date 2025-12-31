using Dommel;
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
            using var connection = _core.Database.GetConnection("default");
            MigrationRunner.RunMigrations(connection);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] Admin database initialized successfully");
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error initializing admin database: {Message}", ex.Message);
        }
    }

    public async Task<bool> AddAdminAsync(ulong steamId, string name, string flags, int immunity, string? addedBy, ulong? addedBySteamId, int? durationDays = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                DateTime? expiresAt = durationDays.HasValue && durationDays.Value > 0 
                    ? DateTime.UtcNow.AddDays(durationDays.Value) 
                    : null;

                using var connection = _core.Database.GetConnection("default");
                var existingAdmin = connection.FirstOrDefault<Admin>(a => a.SteamId == steamId);

                if (existingAdmin != null)
                {
                    existingAdmin.Name = name;
                    existingAdmin.Flags = flags;
                    existingAdmin.Immunity = immunity;
                    existingAdmin.ExpiresAt = expiresAt;
                    existingAdmin.AddedBy = addedBy;
                    existingAdmin.AddedBySteamId = addedBySteamId;
                    connection.Update(existingAdmin);
                    _adminCache[steamId] = existingAdmin;
                }
                else
                {
                    var admin = new Admin
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
                    var id = connection.Insert(admin);
                    admin.Id = Convert.ToInt32(id);
                    _adminCache[steamId] = admin;
                }

                return true;
            }
            catch (Exception ex)
            {
                _core.Logger.LogErrorIfEnabled("[T3-Admin] Error adding admin: {Message}", ex.Message);
                return false;
            }
        });
    }

    public async Task<bool> RemoveAdminAsync(ulong steamId)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var connection = _core.Database.GetConnection("default");
                var admin = connection.FirstOrDefault<Admin>(a => a.SteamId == steamId);
                if (admin == null) return false;

                connection.Delete(admin);
                _adminCache.Remove(steamId);

                return true;
            }
            catch (Exception ex)
            {
                _core.Logger.LogErrorIfEnabled("[T3-Admin] Error removing admin: {Message}", ex.Message);
                return false;
            }
        });
    }

    public async Task<Admin?> GetAdminAsync(ulong steamId)
    {
        return await Task.Run(() =>
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

                using var connection = _core.Database.GetConnection("default");
                var admin = connection.FirstOrDefault<Admin>(a => 
                    a.SteamId == steamId && 
                    (a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow));

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
        });
    }

    public async Task<List<Admin>> GetAllAdminsAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var connection = _core.Database.GetConnection("default");
                var admins = connection.Select<Admin>(a => 
                    a.ExpiresAt == null || a.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(a => a.Immunity)
                    .ThenBy(a => a.Name)
                    .ToList();
                return admins;
            }
            catch (Exception ex)
            {
                _core.Logger.LogErrorIfEnabled("[T3-Admin] Error getting all admins: {Message}", ex.Message);
                return new List<Admin>();
            }
        });
    }

    public async Task CleanupExpiredAdminsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var connection = _core.Database.GetConnection("default");
                var expiredAdmins = connection.Select<Admin>(a => 
                    a.ExpiresAt != null && 
                    a.ExpiresAt <= DateTime.UtcNow);

                int cleaned = 0;
                foreach (var admin in expiredAdmins)
                {
                    connection.Delete(admin);
                    cleaned++;
                }

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
        });
    }

    public void ClearCache()
    {
        _adminCache.Clear();
    }
}
