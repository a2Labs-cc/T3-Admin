using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;
using Furien_Admin.Config;

namespace Furien_Admin.Menu.Handlers;

public class PlayerManagementHandler : IAdminMenuHandler
{
    private readonly ISwiftlyCore _core;
    private readonly PluginConfig _config;

    public PlayerManagementHandler(ISwiftlyCore core, PluginConfig config)
    {
        _core = core;
        _config = config;
    }

    public IMenuAPI CreateMenu(IPlayer player)
    {
        var builder = _core.MenusAPI.CreateBuilder();

        string title = _core.Localizer["menu_player_management"];
        builder.Design.SetMenuTitle(title);

        AddTargetAction(builder, player, "menu_ban", "Ban", "ban", _config.Permissions.Ban);
        AddTargetAction(builder, player, "menu_mute", "Mute", "mute", _config.Permissions.Mute);
        AddTargetAction(builder, player, "menu_gag", "Gag", "gag", _config.Permissions.Gag);
        AddTargetAction(builder, player, "menu_silence", "Silence", "silence", _config.Permissions.Silence);
        AddTargetAction(builder, player, "menu_kick", "Kick", "kick", _config.Permissions.Kick);

        return builder.Build();
    }

    private void AddTargetAction(IMenuBuilderAPI builder, IPlayer admin, string translationKey, string fallback, string action, string permission)
    {
        if (!_core.Permission.PlayerHasPermission(admin.SteamID, permission))
            return;

        string text = _core.Localizer[translationKey];

        builder.AddOption(new SubmenuMenuOption(text, () => BuildSelectPlayerMenu(admin, action)));
    }

    private IMenuAPI BuildSelectPlayerMenu(IPlayer admin, string action)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(_core.Localizer["menu_select_player"]);

        var players = _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && p.PlayerID != admin.PlayerID).ToList();
        foreach (var target in players)
        {
            var button = new ButtonMenuOption(target.Controller.PlayerName ?? _core.Localizer["player_fallback_name", target.PlayerID]) { CloseAfterClick = false };
            button.Click += (_, args) =>
            {
                var adminPlayer = args.Player;
                _core.Scheduler.NextTick(() => OpenReasonMenu(adminPlayer, target, action));
                return ValueTask.CompletedTask;
            };
            builder.AddOption(button);
        }

        return builder.Build();
    }

    private void OpenReasonMenu(IPlayer admin, IPlayer target, string action)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(_core.Localizer["menu_select_reason"]);

        var reasons = GetReasonsForAction(action);
        foreach (var reason in reasons)
        {
            var option = new ButtonMenuOption(reason) { CloseAfterClick = true };
            option.Click += (_, args) =>
            {
                var adminPlayer = admin;
                _core.Scheduler.NextTick(() =>
                {
                    if (action == "kick")
                    {
                        ExecuteKick(adminPlayer, target, reason);
                    }
                    else
                    {
                        OpenDurationMenu(adminPlayer, target, action, reason);
                    }
                });
                return ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        _core.MenusAPI.OpenMenuForPlayer(admin, builder.Build());
    }

    private void OpenDurationMenu(IPlayer admin, IPlayer target, string action, string reason)
    {
        var builder = _core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(_core.Localizer["menu_select_duration"]);

        foreach (var item in _config.Sanctions.Durations)
        {
            var label = item.Name;
            var minutes = item.Minutes;
            var option = new ButtonMenuOption(label) { CloseAfterClick = true };
            option.Click += (_, args) =>
            {
                var adminPlayer = admin;
                _core.Scheduler.NextTick(() => ExecuteTimedAction(adminPlayer, target, action, minutes, reason));
                return ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        _core.MenusAPI.OpenMenuForPlayer(admin, builder.Build());
    }

    private IReadOnlyList<string> GetReasonsForAction(string action)
    {
        return action switch
        {
            "ban" => _config.Sanctions.BanReasons,
            "kick" => _config.Sanctions.KickReasons,
            "mute" => _config.Sanctions.MuteReasons,
            "gag" => _config.Sanctions.GagReasons,
            "silence" => _config.Sanctions.SilenceReasons,
            _ => _config.Sanctions.BanReasons
        };
    }

    private void ExecuteKick(IPlayer admin, IPlayer target, string reason)
    {
        var targetId = target.PlayerID;
        var cmd = _config.Commands.Kick.FirstOrDefault() ?? "kick";
        admin.ExecuteCommand($"{cmd} {targetId} {reason}");
    }

    private void ExecuteTimedAction(IPlayer admin, IPlayer target, string action, int minutes, string reason)
    {
        var duration = minutes < 0 ? 0 : minutes;
        var targetId = target.PlayerID;

        string? cmdName = action switch
        {
            "ban" => _config.Commands.Ban.FirstOrDefault(),
            "mute" => _config.Commands.Mute.FirstOrDefault(),
            "gag" => _config.Commands.Gag.FirstOrDefault(),
            "silence" => _config.Commands.Silence.FirstOrDefault(),
            _ => null
        };

        if (string.IsNullOrEmpty(cmdName))
            return;

        admin.ExecuteCommand($"{cmdName} {targetId} {duration} {reason}");
    }
}
