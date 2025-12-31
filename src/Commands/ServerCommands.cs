using Furien_Admin.Config;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;

namespace Furien_Admin.Commands;

public class ServerCommands
{
    private readonly ISwiftlyCore _core;
    private readonly PermissionsConfig _permissions;
    private readonly GameMapsConfig _gameMaps;
    private readonly WorkshopMapsConfig _workshopMaps;

    public ServerCommands(
        ISwiftlyCore core, 
        PermissionsConfig permissions,
        GameMapsConfig gameMaps,
        WorkshopMapsConfig workshopMaps)
    {
        _core = core;
        _permissions = permissions;
        _gameMaps = gameMaps;
        _workshopMaps = workshopMaps;
    }

    public void OnMapCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.ChangeMap))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["map_usage"]}");
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["map_available", string.Join(", ", _gameMaps.Maps.Keys)]}");
            return;
        }

        var mapName = context.Args[0].ToLowerInvariant();
        
        // Check if map exists in config
        var matchedMap = _gameMaps.Maps.Keys.FirstOrDefault(m => 
            m.Equals(mapName, StringComparison.OrdinalIgnoreCase));

        if (matchedMap == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["map_not_found", mapName]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var mapDisplayName = _gameMaps.Maps[matchedMap];
        const float changeDelaySeconds = 3f;

        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["map_changing", adminName, mapDisplayName, changeDelaySeconds]}");
        }

        _core.Scheduler.DelayBySeconds(changeDelaySeconds, () =>
        {
            _core.Engine.ExecuteCommand($"changelevel {matchedMap}");
        });

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} changed map to {Map}", adminName, matchedMap);
    }

    public void OnWSMapCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.ChangeWSMap))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["wsmap_usage"]}");
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["wsmap_available", string.Join(", ", _workshopMaps.Maps.Keys)]}");
            return;
        }

        var input = context.Args[0];
        uint workshopId;

        // Try to parse as workshop ID
        if (!uint.TryParse(input, out workshopId))
        {
            // Try to find by name
            var matchedMap = _workshopMaps.Maps.FirstOrDefault(m => 
                m.Key.Contains(input, StringComparison.OrdinalIgnoreCase));

            if (matchedMap.Key == null)
            {
                context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["wsmap_not_found", input]}");
                return;
            }

            workshopId = matchedMap.Value;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        const float changeDelaySeconds = 3f;

        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["wsmap_changing", adminName, workshopId, changeDelaySeconds]}");
        }

        _core.Scheduler.DelayBySeconds(changeDelaySeconds, () =>
        {
            _core.Engine.ExecuteCommand($"host_workshop_map {workshopId}");
        });

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} changed to workshop map {WorkshopId}", adminName, workshopId);
    }

    public void OnRestartCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.RestartGame))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];

        int seconds = 2;
        if (context.Args.Length >= 1 && int.TryParse(context.Args[0], out var parsed) && parsed > 0)
        {
            seconds = parsed;
        }

        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["restart_notification", adminName, seconds]}");
        }

        _core.Engine.ExecuteCommand($"mp_restartgame {seconds}");

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} restarted the game in {Seconds} second(s)", adminName, seconds);
    }

    public void OnRconCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Rcon))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["rcon_usage"]}");
            return;
        }

        var command = string.Join(" ", context.Args);
        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];

        _core.Engine.ExecuteCommand(command);

        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            if (_core.Permission.PlayerHasPermission(player.SteamID, _permissions.Rcon))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["rcon_executed", adminName, command]}");
            }
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} executed rcon: {Command}", adminName, command);
    }

    public void OnCvarCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Cvar))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["cvar_usage"]}");
            return;
        }

        var cvarName = context.Args[0];
        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];

        if (context.Args.Length == 1)
        {
            // Just query the value
            var cvar = _core.ConVar.Find<string>(cvarName);
            if (cvar == null)
            {
                context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["cvar_not_found", cvarName]}");
                return;
            }

            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["cvar_value", cvarName, cvar.Value]}");
        }
        else
        {
            // Set the value
            var value = string.Join(" ", context.Args.Skip(1));
            _core.Engine.ExecuteCommand($"{cvarName} {value}");

            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                if (_core.Permission.PlayerHasPermission(player.SteamID, _permissions.Cvar))
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["cvar_set", adminName, cvarName, value]}");
                }
            }

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} set {Cvar} to {Value}", adminName, cvarName, value);
        }
    }

    private bool HasPermission(ICommandContext context, string permission)
    {
        if (!context.IsSentByPlayer)
            return true;

        return _core.Permission.PlayerHasPermission(context.Sender!.SteamID, permission);
    }
}
