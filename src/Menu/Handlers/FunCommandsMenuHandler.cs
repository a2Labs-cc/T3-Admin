using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;
using Furien_Admin.Config;

namespace Furien_Admin.Menu.Handlers;

public class FunCommandsMenuHandler : IAdminMenuHandler
{
    private readonly ISwiftlyCore _core;
    private readonly PluginConfig _config;

    private enum FunAction
    {
        Slay,
        Respawn,
        Team,
        Noclip,
        Freeze,
        Unfreeze
    }

    public FunCommandsMenuHandler(ISwiftlyCore core, PluginConfig config)
    {
        _core = core;
        _config = config;
    }

    public IMenuAPI CreateMenu(IPlayer player)
    {
        var builder = _core.MenusAPI.CreateBuilder();

        string title = _core.Localizer["menu_fun_commands"];
        builder.Design.SetMenuTitle(title);

        if (HasPermission(player, _config.Permissions.Slay))
            builder.AddOption(new SubmenuMenuOption(_core.Localizer["menu_slay"], () => BuildPlayerSelectMenu(player, FunAction.Slay)));

        if (HasPermission(player, _config.Permissions.Respawn))
            builder.AddOption(new SubmenuMenuOption(_core.Localizer["menu_respawn"], () => BuildPlayerSelectMenu(player, FunAction.Respawn)));

        if (HasPermission(player, _config.Permissions.ChangeTeam))
            builder.AddOption(new SubmenuMenuOption(_core.Localizer["menu_team"], () => BuildPlayerSelectMenu(player, FunAction.Team)));

        if (HasPermission(player, _config.Permissions.NoClip))
            builder.AddOption(new SubmenuMenuOption(_core.Localizer["menu_noclip"], () => BuildPlayerSelectMenu(player, FunAction.Noclip)));

        if (HasPermission(player, _config.Permissions.Freeze))
            builder.AddOption(new SubmenuMenuOption(_core.Localizer["menu_freeze"], () => BuildPlayerSelectMenu(player, FunAction.Freeze)));

        if (HasPermission(player, _config.Permissions.Unfreeze))
            builder.AddOption(new SubmenuMenuOption(_core.Localizer["menu_unfreeze"], () => BuildPlayerSelectMenu(player, FunAction.Unfreeze)));

        return builder.Build();
    }

    private bool HasPermission(IPlayer player, string permission)
    {
        return _core.Permission.PlayerHasPermission(player.SteamID, permission);
    }

    private IMenuAPI BuildPlayerSelectMenu(IPlayer admin, FunAction action)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(_core.Localizer["menu_select_player"]);

        var players = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();
        foreach (var target in players)
        {
            var btn = new ButtonMenuOption(target.Controller.PlayerName ?? _core.Localizer["player_fallback_name", target.PlayerID]) { CloseAfterClick = true };
            btn.Click += (_, args) =>
            {
                var adminPlayer = args.Player;
                _core.Scheduler.NextTick(() =>
                {
                    if (action == FunAction.Team)
                    {
                        OpenTeamSelectMenu(adminPlayer, target);
                    }
                    else
                    {
                        ExecuteFunAction(adminPlayer, target, action);
                    }
                });
                return ValueTask.CompletedTask;
            };
            builder.AddOption(btn);
        }

        return builder.Build();
    }

    private void OpenTeamSelectMenu(IPlayer admin, IPlayer target)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(_core.Localizer["menu_select_team"]);

        AddTeamButton(builder, admin, target, _core.Localizer["team_t"], "t");
        AddTeamButton(builder, admin, target, _core.Localizer["team_ct"], "ct");
        AddTeamButton(builder, admin, target, _core.Localizer["team_spec"], "spec");

        _core.MenusAPI.OpenMenuForPlayer(admin, builder.Build());
    }

    private void AddTeamButton(IMenuBuilderAPI builder, IPlayer admin, IPlayer target, string label, string teamArg)
    {
        var option = new ButtonMenuOption(label) { CloseAfterClick = true };
        option.Click += (_, args) =>
        {
            var caller = args.Player;
            var cmd = _config.Commands.ChangeTeam.FirstOrDefault() ?? "team";
            var targetId = target.PlayerID;
            _core.Scheduler.NextTick(() => caller.ExecuteCommand($"{cmd} {targetId} {teamArg}"));
            return ValueTask.CompletedTask;
        };
        builder.AddOption(option);
    }

    private void ExecuteFunAction(IPlayer admin, IPlayer target, FunAction action)
    {
        var targetId = target.PlayerID;

        switch (action)
        {
            case FunAction.Slay:
            {
                var cmd = _config.Commands.Slay.FirstOrDefault() ?? "slay";
                _core.Scheduler.NextTick(() => admin.ExecuteCommand($"{cmd} {targetId}"));
                break;
            }
            case FunAction.Respawn:
            {
                var cmd = _config.Commands.Respawn.FirstOrDefault() ?? "respawn";
                _core.Scheduler.NextTick(() => admin.ExecuteCommand($"{cmd} {targetId}"));
                break;
            }
            case FunAction.Noclip:
            {
                var cmd = _config.Commands.NoClip.FirstOrDefault() ?? "noclip";
                _core.Scheduler.NextTick(() => admin.ExecuteCommand($"{cmd} {targetId}"));
                break;
            }
            case FunAction.Freeze:
            {
                var cmd = _config.Commands.Freeze.FirstOrDefault() ?? "freeze";
                _core.Scheduler.NextTick(() => admin.ExecuteCommand($"{cmd} {targetId}"));
                break;
            }
            case FunAction.Unfreeze:
            {
                var cmd = _config.Commands.Unfreeze.FirstOrDefault() ?? "unfreeze";
                _core.Scheduler.NextTick(() => admin.ExecuteCommand($"{cmd} {targetId}"));
                break;
            }
        }
    }
}
