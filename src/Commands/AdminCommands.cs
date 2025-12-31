using Furien_Admin.Config;
using Furien_Admin.Database;
using Furien_Admin.Utils;
using Furien_Admin.Menu;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;

namespace Furien_Admin.Commands;

public class AdminCommands
{
    private readonly ISwiftlyCore _core;
    private readonly AdminDbManager _adminManager;
    private readonly PermissionsConfig _permissions;
    private readonly AdminMenuManager _menuManager;

    public AdminCommands(ISwiftlyCore core, AdminDbManager adminManager, PermissionsConfig permissions, AdminMenuManager menuManager)
    {
        _core = core;
        _adminManager = adminManager;
        _permissions = permissions;
        _menuManager = menuManager;
    }

    public void OnAdminMenuCommand(ICommandContext context)
    {
        if (context.Sender == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_only_command"]}");
            return;
        }

        if (!HasPermission(context, _permissions.AdminMenu))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        _menuManager.OpenAdminMenu(context.Sender);
    }

    public void OnAddAdminCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.AddAdmin))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 3)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["addadmin_usage"]}");
            return;
        }

        if (!PlayerUtils.TryParseSteamId(context.Args[0], out ulong targetSteamId))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["invalid_steamid"]}");
            return;
        }

        var name = context.Args[1];
        var flags = context.Args[2];
        int immunity = 0;
        int? durationDays = null;

        if (context.Args.Length > 3 && int.TryParse(context.Args[3], out int parsedImmunity))
        {
            immunity = parsedImmunity;
        }

        if (context.Args.Length >= 5 && int.TryParse(context.Args[4], out var parsedDuration) && parsedDuration > 0)
        {
            durationDays = parsedDuration;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var adminSteamId = context.Sender?.SteamID;

        _ = Task.Run(async () =>
        {
            var success = await _adminManager.AddAdminAsync(
                targetSteamId, 
                name, 
                flags, 
                immunity, 
                adminName, 
                adminSteamId, 
                durationDays);

            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                if (success)
                {
                    context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["addadmin_success", name, targetSteamId, flags]}");
                    
                    // Add permissions to the player if they're online
                    var onlinePlayer = _core.PlayerManager.GetAllPlayers()
                        .FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                    
                    if (onlinePlayer != null)
                    {
                        foreach (var flag in flags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        {
                            _core.Permission.AddPermission(targetSteamId, flag.Trim());
                        }
                        onlinePlayer.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["addadmin_granted"]}");
                    }
                }
                else
                {
                    context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["addadmin_failed"]}");
                }
            });

            if (success)
            {
                _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} added admin {Name} ({SteamId}) with flags {Flags}", 
                    adminName, name, targetSteamId, flags);
            }
        });
    }

    public void OnRemoveAdminCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.RemoveAdmin))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["removeadmin_usage"]}");
            return;
        }

        if (!PlayerUtils.TryParseSteamId(context.Args[0], out ulong targetSteamId))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["invalid_steamid"]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];

        _ = Task.Run(async () =>
        {
            var existingAdmin = await _adminManager.GetAdminAsync(targetSteamId);
            if (existingAdmin == null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["removeadmin_not_admin", targetSteamId]}"));
                return;
            }

            var success = await _adminManager.RemoveAdminAsync(targetSteamId);
            var existingFlags = existingAdmin.Flags;
            var existingName = existingAdmin.Name;

            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                if (success)
                {
                    context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["removeadmin_success", existingName, targetSteamId]}");
                    
                    // Remove permissions from the player (remove each flag individually)
                    foreach (var flag in existingFlags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        _core.Permission.RemovePermission(targetSteamId, flag.Trim());
                    }

                    var onlinePlayer = _core.PlayerManager.GetAllPlayers()
                        .FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                    
                    if (onlinePlayer != null)
                    {
                        onlinePlayer.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["removeadmin_revoked"]}");
                    }
                }
                else
                {
                    context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["removeadmin_failed"]}");
                }
            });

            if (success)
            {
                _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} removed admin {Name} ({SteamId})", 
                    adminName, existingName, targetSteamId);
            }
        });
    }

    public void OnListAdminsCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.ListAdmins))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        _ = Task.Run(async () =>
        {
            var admins = await _adminManager.GetAllAdminsAsync();

            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                if (admins.Count == 0)
                {
                    context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["listadmins_none"]}");
                    return;
                }

                context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["listadmins_header", admins.Count]}");
                foreach (var admin in admins)
                {
                    var expiryDate = admin.ExpiresAt.HasValue ? admin.ExpiresAt.Value.ToString("yyyy-MM-dd") : "";
                    var expiry = admin.IsPermanent ? _core.Localizer["admin_permanent"] : _core.Localizer["admin_expires", expiryDate];
                    context.Reply($"  {_core.Localizer["listadmins_entry", admin.Name, admin.SteamId, admin.Flags, admin.Immunity, expiry]}");
                }
            });
        });
    }

    private bool HasPermission(ICommandContext context, string permission)
    {
        if (!context.IsSentByPlayer)
            return true;

        return _core.Permission.PlayerHasPermission(context.Sender!.SteamID, permission);
    }
}
