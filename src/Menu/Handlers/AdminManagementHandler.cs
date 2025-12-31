using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;
using Furien_Admin.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Furien_Admin.Menu.Handlers;

public class AdminManagementHandler : IAdminMenuHandler
{
    private readonly ISwiftlyCore _core;
    private readonly PluginConfig _config;

    public AdminManagementHandler(ISwiftlyCore core, PluginConfig config)
    {
        _core = core;
        _config = config;
    }

    public IMenuAPI CreateMenu(IPlayer player)
    {
        var builder = _core.MenusAPI.CreateBuilder();

        string title;
        try
        {
            title = _core.Localizer["menu_admin_management"];
        }
        catch
        {
            title = "Admin Management";
        }
        builder.Design.SetMenuTitle(title);

        if (HasPermission(player, _config.Permissions.AddAdmin))
        {
            string addAdminText;
            try
            {
                addAdminText = _core.Localizer["menu_add_admin"];
            }
            catch
            {
                addAdminText = "Add Admin";
            }
            builder.AddOption(new SubmenuMenuOption(addAdminText, () => BuildAddAdminMenu(player)));
        }

        if (HasPermission(player, _config.Permissions.RemoveAdmin))
        {
            string removeAdminText;
            try
            {
                removeAdminText = _core.Localizer["menu_remove_admin"];
            }
            catch
            {
                removeAdminText = "Remove Admin";
            }
            builder.AddOption(new SubmenuMenuOption(removeAdminText, () => BuildRemoveAdminMenu(player)));
        }

        if (HasPermission(player, _config.Permissions.ListAdmins))
        {
            string listAdminsText;
            try
            {
                listAdminsText = _core.Localizer["menu_list_admins"];
            }
            catch
            {
                listAdminsText = "List Admins";
            }
            var listBtn = new ButtonMenuOption(listAdminsText) { CloseAfterClick = true };
            listBtn.Click += (_, args) =>
            {
                var caller = args.Player;
                _core.Scheduler.NextTick(() => ExecuteListAdmins(caller));
                return ValueTask.CompletedTask;
            };
            builder.AddOption(listBtn);
        }

        return builder.Build();
    }

    private bool HasPermission(IPlayer player, string permission)
    {
        return _core.Permission.PlayerHasPermission(player.SteamID, permission);
    }

    private IMenuAPI BuildAddAdminMenu(IPlayer admin)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        string title;
        try
        {
            title = _core.Localizer["menu_select_player_add"];
        }
        catch
        {
            title = "Select Player to Add";
        }
        builder.Design.SetMenuTitle(title);

        var players = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
        foreach (var target in players)
        {
            var fallbackName = "Player " + target.PlayerID;
            try
            {
                fallbackName = _core.Localizer["player_fallback_name", target.PlayerID];
            }
            catch
            {
                // Use default fallback
            }
            var btn = new ButtonMenuOption(target.Controller.PlayerName ?? fallbackName) { CloseAfterClick = false };
            btn.Click += (_, args) =>
            {
                var adminPlayer = args.Player;
                _core.Scheduler.NextTick(() => OpenAddAdminFlagsMenu(adminPlayer, target));
                return ValueTask.CompletedTask;
            };
            builder.AddOption(btn);
        }

        return builder.Build();
    }

    private void OpenAddAdminFlagsMenu(IPlayer admin, IPlayer target)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        string title;
        try
        {
            title = _core.Localizer["menu_select_permission_group"];
        }
        catch
        {
            title = "Select Permission Group";
        }
        builder.Design.SetMenuTitle(title);

        var groups = GetPermissionGroupKeys();
        foreach (var group in groups)
        {
            var groupBtn = new ButtonMenuOption(group) { CloseAfterClick = true };
            groupBtn.Click += (_, args) =>
            {
                var adminPlayer = args.Player;
                _core.Scheduler.NextTick(() => ExecuteAddAdmin(adminPlayer, target, group));
                return ValueTask.CompletedTask;
            };
            builder.AddOption(groupBtn);
        }

        string flagsText;
        try
        {
            flagsText = _core.Localizer["menu_custom_flags"];
        }
        catch
        {
            flagsText = "Custom Flags";
        }
        var input = new InputMenuOption(flagsText);
        input.ValueChanged += (_, args) =>
        {
            var value = args.NewValue;
            var adminPlayer = admin;
            _core.Scheduler.NextTick(() => ExecuteAddAdmin(adminPlayer, target, value));
        };
        builder.AddOption(input);

        _core.MenusAPI.OpenMenuForPlayer(admin, builder.Build());
    }

    private List<string> GetPermissionGroupKeys()
    {
        try
        {
            var permissionManager = _core.Permission;
            var method = permissionManager.GetType().GetMethod("GetSubPermissions", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                return new List<string>();

            var result = method.Invoke(permissionManager, null) as Dictionary<string, List<string>>;
            if (result == null)
                return new List<string>();

            return result.Keys
                .Where(k => !string.Equals(k, "__default", StringComparison.OrdinalIgnoreCase))
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private IMenuAPI BuildRemoveAdminMenu(IPlayer admin)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        string title;
        try
        {
            title = _core.Localizer["menu_select_admin_remove"];
        }
        catch
        {
            title = "Select Admin to Remove";
        }
        builder.Design.SetMenuTitle(title);

        var players = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
        foreach (var target in players)
        {
            var fallbackName = "Player " + target.PlayerID;
            try
            {
                fallbackName = _core.Localizer["player_fallback_name", target.PlayerID];
            }
            catch
            {
                // Use default fallback
            }
            var btn = new ButtonMenuOption(target.Controller.PlayerName ?? fallbackName) { CloseAfterClick = true };
            btn.Click += (_, args) =>
            {
                var adminPlayer = args.Player;
                _core.Scheduler.NextTick(() => ExecuteRemoveAdmin(adminPlayer, target));
                return ValueTask.CompletedTask;
            };
            builder.AddOption(btn);
        }

        return builder.Build();
    }

    private void ExecuteAddAdmin(IPlayer admin, IPlayer target, string flagsRaw)
    {
        if (string.IsNullOrWhiteSpace(flagsRaw))
            return;

        var fallbackName = "Player " + target.PlayerID;
        try
        {
            fallbackName = _core.Localizer["player_fallback_name", target.PlayerID];
        }
        catch
        {
            // Use default fallback
        }
        var safeName = (target.Controller.PlayerName ?? fallbackName).Replace(' ', '_');
        var steamId = target.SteamID;
        var flags = flagsRaw.Trim();

        var cmd = _config.Commands.AddAdmin.FirstOrDefault() ?? "addadmin";
        _core.Scheduler.NextTick(() => admin.ExecuteCommand($"{cmd} {steamId} {safeName} {flags}"));
    }

    private void ExecuteRemoveAdmin(IPlayer admin, IPlayer target)
    {
        var steamId = target.SteamID;
        var cmd = _config.Commands.RemoveAdmin.FirstOrDefault() ?? "removeadmin";
        _core.Scheduler.NextTick(() => admin.ExecuteCommand($"{cmd} {steamId}"));
    }

    private void ExecuteListAdmins(IPlayer admin)
    {
        var cmd = _config.Commands.ListAdmins.FirstOrDefault() ?? "listadmins";
        _core.Scheduler.NextTick(() => admin.ExecuteCommand(cmd));
    }
}
