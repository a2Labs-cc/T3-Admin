using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;
using Furien_Admin.Config;

namespace Furien_Admin.Menu.Handlers;

public class ServerManagementHandler : IAdminMenuHandler
{
    private readonly ISwiftlyCore _core;
    private readonly PluginConfig _config;

    public ServerManagementHandler(ISwiftlyCore core, PluginConfig config)
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
            title = _core.Localizer["menu_server_management"];
        }
        catch
        {
            title = "Server Management";
        }
        builder.Design.SetMenuTitle(title);

        // Restart game
        if (_core.Permission.PlayerHasPermission(player.SteamID, _config.Permissions.RestartGame))
        {
            string restartText;
            try
            {
                restartText = _core.Localizer["menu_restart_game"];
            }
            catch
            {
                restartText = "Restart Game";
            }
            builder.AddOption(new SubmenuMenuOption(restartText, () => CreateRestartGameMenu(player)));
        }

        // Change map
        if (_core.Permission.PlayerHasPermission(player.SteamID, _config.Permissions.ChangeMap))
        {
            string mapText;
            try
            {
                mapText = _core.Localizer["menu_change_map"];
            }
            catch
            {
                mapText = "Change Map";
            }
            builder.AddOption(new SubmenuMenuOption(mapText, () => CreateChangeMapMenu(player)));
        }

        // Change workshop map
        if (_core.Permission.PlayerHasPermission(player.SteamID, _config.Permissions.ChangeWSMap))
        {
            string wsMapText;
            try
            {
                wsMapText = _core.Localizer["menu_change_ws_map"];
            }
            catch
            {
                wsMapText = "Change Workshop Map";
            }
            builder.AddOption(new SubmenuMenuOption(wsMapText, () => CreateChangeWSMapMenu(player)));
        }

        return builder.Build();
    }

    private IMenuAPI CreateRestartGameMenu(IPlayer player)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        string title;
        try
        {
            title = _core.Localizer["menu_restart_game"];
        }
        catch
        {
            title = "Restart Game";
        }
        builder.Design.SetMenuTitle(title);

        // Select seconds (1-10)
        string delayText;
        try
        {
            delayText = _core.Localizer["menu_restart_delay"];
        }
        catch
        {
            delayText = "Delay (Seconds)";
        }
        var slider = new SliderMenuOption(delayText, 1, 10, 2, 1);
        builder.AddOption(slider);

        string nowText;
        try
        {
            nowText = _core.Localizer["menu_restart_now"];
        }
        catch
        {
            nowText = "Restart Now";
        }
        var btn = new ButtonMenuOption(nowText) { CloseAfterClick = true };
        btn.Click += (_, args) =>
        {
            var caller = args.Player;
            var seconds = (int)slider.GetValue(caller);
            var cmd = _config.Commands.RestartGame.FirstOrDefault() ?? "rr";
            _core.Scheduler.NextTick(() => caller.ExecuteCommand($"{cmd} {seconds}"));
            return ValueTask.CompletedTask;
        };
        builder.AddOption(btn);

        return builder.Build();
    }

    private IMenuAPI CreateChangeMapMenu(IPlayer player)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        string title;
        try
        {
            title = _core.Localizer["menu_change_map"];
        }
        catch
        {
            title = "Change Map";
        }
        builder.Design.SetMenuTitle(title);

        if (_config.GameMaps.Maps != null)
        {
            foreach (var map in _config.GameMaps.Maps)
            {
                var btn = new ButtonMenuOption(map.Value) { CloseAfterClick = true };
                btn.Click += (_, args) =>
                {
                    var caller = args.Player;
                    var cmd = _config.Commands.ChangeMap.FirstOrDefault() ?? "map";
                    _core.Scheduler.NextTick(() => caller.ExecuteCommand($"{cmd} {map.Key}"));
                    return ValueTask.CompletedTask;
                };
                builder.AddOption(btn);
            }
        }

        return builder.Build();
    }

    private IMenuAPI CreateChangeWSMapMenu(IPlayer player)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        string title;
        try
        {
            title = _core.Localizer["menu_change_ws_map"];
        }
        catch
        {
            title = "Change Workshop Map";
        }
        builder.Design.SetMenuTitle(title);

        if (_config.WorkshopMaps.Maps != null)
        {
            foreach (var map in _config.WorkshopMaps.Maps)
            {
                var displayName = map.Key;
                var workshopId = map.Value;

                var btn = new ButtonMenuOption(displayName) { CloseAfterClick = true };
                btn.Click += (_, args) =>
                {
                    var caller = args.Player;
                    var cmd = _config.Commands.ChangeWSMap.FirstOrDefault() ?? "wsmap";
                    _core.Scheduler.NextTick(() => caller.ExecuteCommand($"{cmd} {workshopId}"));
                    return ValueTask.CompletedTask;
                };
                builder.AddOption(btn);
            }
        }

        return builder.Build();
    }
}
