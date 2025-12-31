using Furien_Admin.Config;
using Furien_Admin.Database;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Misc;
using System.Text.Json;

namespace Furien_Admin.Commands;

public class PlayerCommands
{
    private readonly ISwiftlyCore _core;
    private readonly DiscordWebhook _discord;
    private readonly PermissionsConfig _permissions;
    private readonly MessagesConfig _messagesConfig;
    private readonly BanManager _banManager;
    private readonly MuteManager _muteManager;
    private readonly GagManager _gagManager;
    private readonly AdminDbManager _adminDbManager;
    private readonly HashSet<int> _noclipPlayers = new();
    private readonly HashSet<int> _frozenPlayers = new();

    private record PlayerListEntry(
        int Id,
        string Name,
        string SteamId,
        int Team,
        string TeamName,
        int Score,
        int Ping,
        bool IsAlive,
        string Ip
    );

    public PlayerCommands(
        ISwiftlyCore core,
        DiscordWebhook discord,
        PermissionsConfig permissions,
        MessagesConfig messagesConfig,
        BanManager banManager,
        MuteManager muteManager,
        GagManager gagManager,
        AdminDbManager adminDbManager)
    {
        _core = core;
        _discord = discord;
        _permissions = permissions;
        _messagesConfig = messagesConfig;
        _banManager = banManager;
        _muteManager = muteManager;
        _gagManager = gagManager;
        _adminDbManager = adminDbManager;
    }

    public void OnKickCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Kick))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["kick_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        string reason = context.Args.Length > 1 
            ? string.Join(" ", context.Args.Skip(1)) 
            : _core.Localizer["no_reason"];

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var targetName = target.Controller.PlayerName;

        // Broadcast to all players
        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["kicked_notification", adminName, targetName, reason]}");
        }
        
        // Personal message to target
        PlayerUtils.SendNotification(target, _messagesConfig,
            _core.Localizer["kicked_personal_html", reason],
            $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["kicked_personal_chat", reason]}");

        // Kick after short delay to show message
        var targetSteamId = target.SteamID;
        _core.Scheduler.DelayBySeconds(2f, () =>
        {
            var playerToKick = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
            playerToKick?.Kick(reason, ENetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
        });

        _ = _discord.SendKickNotificationAsync(adminName, targetName, reason);

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} kicked {Target}. Reason: {Reason}", 
            adminName, targetName, reason);
    }

    public void OnSlayCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Slay))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["slay_usage"]}");
            return;
        }

        var targets = PlayerUtils.FindPlayersByTarget(_core, context.Args[0], includeDeadPlayers: false);
        if (targets.Count == 0)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_valid_targets"]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];

        foreach (var target in targets)
        {
            if (target.PlayerPawn?.IsValid == true)
            {
                target.PlayerPawn.CommitSuicide(false, true);
            }
        }

        // Personal message to targets
        foreach (var target in targets)
        {
            PlayerUtils.SendNotification(target, _messagesConfig,
                _core.Localizer["slayed_personal_html", adminName],
                $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["slayed_personal_chat", adminName]}");
        }

        if (targets.Count == 1)
        {
            var targetName = targets[0].Controller.PlayerName;
            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["slayed_notification_single", adminName, targetName]}");
            }
        }
        else
        {
            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["slayed_notification_multiple", adminName, targets.Count]}");
            }
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} slayed {Count} player(s)", adminName, targets.Count);
    }

    public void OnRespawnCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Respawn))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["respawn_usage"]}");
            return;
        }

        var targets = PlayerUtils.FindPlayersByTarget(_core, context.Args[0]);
        if (targets.Count == 0)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_valid_targets"]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];

        foreach (var target in targets)
        {
            if (target.Controller.TeamNum >= 2) // T or CT
            {
                target.Respawn();
            }
        }

        // Personal message to targets
        foreach (var target in targets)
        {
            PlayerUtils.SendNotification(target, _messagesConfig,
                _core.Localizer["respawned_personal_html", adminName],
                $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["respawned_personal_chat", adminName]}");
        }

        if (targets.Count == 1)
        {
            var targetName = targets[0].Controller.PlayerName;
            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["respawned_notification_single", adminName, targetName]}");
            }
        }
        else
        {
            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["respawned_notification_multiple", adminName, targets.Count]}");
            }
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} respawned {Count} player(s)", adminName, targets.Count);
    }

    public void OnTeamCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.ChangeTeam))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["team_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        var team = PlayerUtils.ParseTeam(context.Args[1]);
        if (team == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["invalid_team"]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var targetName = target.Controller.PlayerName;
        var teamName = PlayerUtils.GetTeamName((int)team.Value, _core.Localizer);

        target.ChangeTeam(team.Value);
        
        // Personal message to target
        PlayerUtils.SendNotification(target, _messagesConfig,
            _core.Localizer["team_changed_personal_html", teamName, adminName],
            $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["team_changed_personal_chat", teamName, adminName]}");

        // Broadcast to all players
        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["team_changed_notification", adminName, targetName, teamName]}");
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} moved {Target} to {Team}", adminName, targetName, teamName);
    }

    public void OnGotoCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Goto))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (!context.IsSentByPlayer || context.Sender == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_only_command"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["goto_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        var admin = context.Sender;

        if (admin.PlayerID == target.PlayerID)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["cannot_target_self_goto"]}");
            return;
        }

        var adminPawn = admin.PlayerPawn;
        var targetPawn = target.PlayerPawn;

        if (adminPawn?.IsValid != true || targetPawn?.IsValid != true)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["both_must_be_alive"]}");
            return;
        }

        // Teleport admin near the target, facing them, to avoid getting stuck inside each other
        var targetPos = targetPawn.AbsOrigin ?? adminPawn.AbsOrigin ?? new Vector(0, 0, 0);
        var adminPos = adminPawn.AbsOrigin ?? targetPos;

        var dx = targetPos.X - adminPos.X;
        var dy = targetPos.Y - adminPos.Y;

        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance < 0.001f)
        {
            // If we're already very close, just pick an arbitrary horizontal direction
            dx = 1f;
            dy = 0f;
            distance = 1f;
        }

        dx /= distance;
        dy /= distance;

        const float offset = 50f; // units away from the target

        var destX = targetPos.X - dx * offset;
        var destY = targetPos.Y - dy * offset;
        var destZ = targetPos.Z;

        var destPos = new Vector(destX, destY, destZ);

        // Calculate yaw so the admin looks at the target
        var lookDx = targetPos.X - destX;
        var lookDy = targetPos.Y - destY;
        var yawRad = MathF.Atan2(lookDy, lookDx);
        var yawDeg = yawRad * (180f / MathF.PI);

        var destRot = new QAngle(0, yawDeg, 0);

        var velocity = adminPawn.AbsVelocity;
        adminPawn.Teleport(destPos, destRot, velocity);

        var adminName = admin.Controller.PlayerName ?? _core.Localizer["console_name"];
        var targetName = target.Controller.PlayerName;

        admin.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["goto_success", targetName]}");

        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["goto_notification", adminName, targetName]}");
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} teleported to {Target}", adminName, targetName);
    }

    public void OnBringCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Bring))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (!context.IsSentByPlayer || context.Sender == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_only_command"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["bring_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        var admin = context.Sender;

        if (admin.PlayerID == target.PlayerID)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["cannot_target_self_bring"]}");
            return;
        }

        var targetPawn = target.PlayerPawn;
        if (targetPawn?.IsValid != true)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["target_must_be_alive"]}");
            return;
        }

        // Use an eye trace from the admin's view to determine the destination position
        var destPos = GetAimPosition(admin);
        if (!destPos.HasValue)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_valid_position"]}");
            return;
        }

        // Teleport target to the traced position, keeping their rotation/velocity
        var destRot = targetPawn.AbsRotation;
        var velocity = targetPawn.AbsVelocity;

        targetPawn.Teleport(destPos.Value, destRot, velocity);

        var adminName = admin.Controller.PlayerName ?? _core.Localizer["console_name"];
        var targetName = target.Controller.PlayerName;

        target.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["bring_success", adminName]}");

        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["bring_notification", adminName, targetName]}");
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} brought {Target}", adminName, targetName);
    }

    private Vector? GetAimPosition(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn == null)
            return null;

        var eyePos = pawn.EyePosition;
        if (!eyePos.HasValue)
            return null;

        pawn.EyeAngles.ToDirectionVectors(out var forward, out _, out _);

        var startPos = new Vector(eyePos.Value.X, eyePos.Value.Y, eyePos.Value.Z);
        var endPos = startPos + forward * 8192;

        var trace = new CGameTrace();
        _core.Trace.SimpleTrace(
            startPos,
            endPos,
            RayType_t.RAY_TYPE_LINE,
            RnQueryObjectSet.Static | RnQueryObjectSet.Dynamic,
            MaskTrace.Solid | MaskTrace.Player,
            MaskTrace.Empty,
            MaskTrace.Empty,
            CollisionGroup.Player,
            ref trace,
            pawn
        );

        if (trace.Fraction < 1.0f)
        {
            // Offset the hit position back along the trace direction to avoid spawning inside walls
            var hitPos = trace.EndPos;
            var traceDir = endPos - startPos;
            var traceDirLen = MathF.Sqrt(traceDir.X * traceDir.X + traceDir.Y * traceDir.Y + traceDir.Z * traceDir.Z);
            if (traceDirLen > 0.001f)
            {
                // Normalize and offset back by 32 units (player hull radius)
                var nx = traceDir.X / traceDirLen;
                var ny = traceDir.Y / traceDirLen;
                var nz = traceDir.Z / traceDirLen;
                const float wallOffset = 32f;
                hitPos = new Vector(hitPos.X - nx * wallOffset, hitPos.Y - ny * wallOffset, hitPos.Z - nz * wallOffset);
            }
            // Add small Z offset so player doesn't clip into the ground
            return new Vector(hitPos.X, hitPos.Y, hitPos.Z + 10);
        }

        return null;
    }

    public void OnNoclipCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.NoClip))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["noclip_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var targetName = target.Controller.PlayerName;

        bool isEnabled = _noclipPlayers.Contains(target.PlayerID);
        
        if (isEnabled)
        {
            PlayerUtils.SetNoclip(_core, target, false);
            _noclipPlayers.Remove(target.PlayerID);
        }
        else
        {
            PlayerUtils.SetNoclip(_core, target, true);
            _noclipPlayers.Add(target.PlayerID);
        }

        var state = !isEnabled ? $"\x04{_core.Localizer["noclip_on"]}\x01" : $"\x02{_core.Localizer["noclip_off"]}\x01";
        
        // Personal message to target
        var stateText = !isEnabled ? _core.Localizer["noclip_on"] : _core.Localizer["noclip_off"];
        var stateColor = !isEnabled ? "#00ff00" : "#ff0000";
        PlayerUtils.SendNotification(target, _messagesConfig,
            _core.Localizer["noclip_toggled_personal_html", stateColor, stateText, adminName],
            $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["noclip_toggled_personal_chat", state, adminName]}");

        // Broadcast to all players
        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["noclip_toggled_notification", adminName, state, targetName]}");
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} toggled noclip {State} for {Target}", 
            adminName, !isEnabled ? "ON" : "OFF", targetName);
    }

    public void OnFreezeCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Freeze))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["freeze_usage"]}");
            return;
        }

        var targets = PlayerUtils.FindPlayersByTarget(_core, context.Args[0], includeDeadPlayers: false);
        if (targets.Count == 0)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_valid_targets"]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];

        int? durationSeconds = null;
        if (context.Args.Length >= 2 && int.TryParse(context.Args[1], out var parsedSeconds) && parsedSeconds > 0)
        {
            durationSeconds = parsedSeconds;
        }

        foreach (var target in targets)
        {
            PlayerUtils.Freeze(target);
            _frozenPlayers.Add(target.PlayerID);

            if (durationSeconds.HasValue)
            {
                var playerId = target.PlayerID;
                _core.Scheduler.DelayBySeconds(durationSeconds.Value, () =>
                {
                    var player = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.PlayerID == playerId);
                    if (player == null)
                        return;

                    if (_frozenPlayers.Contains(playerId))
                    {
                        PlayerUtils.Unfreeze(player);
                        _frozenPlayers.Remove(playerId);
                    }
                });
            }
        }

        // Personal message to targets
        foreach (var target in targets)
        {
            PlayerUtils.SendNotification(target, _messagesConfig,
                _core.Localizer["frozen_personal_html", adminName],
                $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["frozen_personal_chat", adminName]}");
        }

        if (targets.Count == 1)
        {
            var targetName = targets[0].Controller.PlayerName;
            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["freeze_notification_single", adminName, targetName]}");
            }
        }
        else
        {
            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["freeze_notification_multiple", adminName, targets.Count]}");
            }
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} froze {Count} player(s)", adminName, targets.Count);
    }

    public void OnUnfreezeCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Unfreeze))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unfreeze_usage"]}");
            return;
        }

        var targets = PlayerUtils.FindPlayersByTarget(_core, context.Args[0]);
        if (targets.Count == 0)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_valid_targets"]}");
            return;
        }

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];

        foreach (var target in targets)
        {
            PlayerUtils.Unfreeze(target);
            _frozenPlayers.Remove(target.PlayerID);
        }

        // Personal message to targets
        foreach (var target in targets)
        {
            PlayerUtils.SendNotification(target, _messagesConfig,
                _core.Localizer["unfrozen_personal_html", adminName],
                $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unfrozen_personal_chat", adminName]}");
        }

        if (targets.Count == 1)
        {
            var targetName = targets[0].Controller.PlayerName;
            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unfreeze_notification_single", adminName, targetName]}");
            }
        }
        else
        {
            foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
            {
                player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unfreeze_notification_multiple", adminName, targets.Count]}");
            }
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} unfroze {Count} player(s)", adminName, targets.Count);
    }

    public void OnPlayersCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.ListPlayers))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        var isJson = context.Args.Length >= 1 && string.Equals(context.Args[0], "-json", StringComparison.OrdinalIgnoreCase);

        var players = _core.PlayerManager
            .GetAllPlayers()
            .Where(p => p.IsValid)
            .OrderBy(p => p.Controller.PlayerName)
            .ToList();

        if (!isJson)
        {
            var lines = new List<string>
            {
                _core.Localizer["players_list_header"]
            };

            foreach (var player in players)
            {
                var teamNum = player.Controller.TeamNum;
                var teamName = PlayerUtils.GetTeamName(teamNum, _core.Localizer);
                var score = player.Controller.Score;
                var ping = (int)player.Controller.Ping;
                var isAlive = player.PlayerPawn?.IsValid == true && player.PlayerPawn.Health > 0;
                var ip = (player.IPAddress ?? _core.Localizer["unknown"]).Split(':')[0];

                lines.Add(
                    _core.Localizer["players_list_entry", 
                        player.PlayerID, 
                        player.Controller.PlayerName, 
                        teamName, 
                        teamNum, 
                        score, 
                        ping, 
                        isAlive ? _core.Localizer["players_yes"] : _core.Localizer["players_no"], 
                        ip, 
                        player.SteamID]);
            }

            lines.Add(_core.Localizer["players_list_footer"]);

            var output = string.Join('\n', lines);

            if (context.IsSentByPlayer && context.Sender != null)
            {
                context.Sender.SendConsole(output);

                if (context.Sender.IsValid && !context.Sender.IsFakeClient)
                {
                    context.Sender.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["players_list_console"]}");
                }
            }
            else
            {
                _core.Logger.LogInformationIfEnabled("{PlayerList}", output);
            }
        }
        else
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var entries = players
                .Select(p =>
                {
                    var teamNum = p.Controller.TeamNum;
                    var teamName = PlayerUtils.GetTeamName(teamNum, _core.Localizer);
                    var score = p.Controller.Score;
                    var ping = (int)p.Controller.Ping;
                    var isAlive = p.PlayerPawn?.IsValid == true && p.PlayerPawn.Health > 0;
                    var ip = (p.IPAddress ?? _core.Localizer["unknown"]).Split(':')[0];

                    return new PlayerListEntry(
                        p.PlayerID,
                        p.Controller.PlayerName,
                        p.SteamID.ToString(),
                        teamNum,
                        teamName,
                        score,
                        ping,
                        isAlive,
                        ip
                    );
                })
                .ToList();

            var json = JsonSerializer.Serialize(entries, options);

            if (context.IsSentByPlayer && context.Sender != null)
            {
                context.Sender.SendConsole(json);

                if (context.Sender.IsValid && !context.Sender.IsFakeClient)
                {
                    context.Sender.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["players_list_json_console"]}");
                }
            }
            else
            {
                _core.Logger.LogInformationIfEnabled("{PlayerListJson}", json);
            }
        }
    }

    public void OnWhoCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Who))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["who_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        var steamId64 = target.SteamID;

        _ = Task.Run(async () =>
        {
            var admin = await _adminDbManager.GetAdminAsync(steamId64);
            var ban = await _banManager.GetActiveBanAsync(steamId64);
            var mute = await _muteManager.GetActiveMuteAsync(steamId64);
            var gag = await _gagManager.GetActiveGagAsync(steamId64);
            var totalBans = await _banManager.GetTotalBansAsync(steamId64);
            var totalMutes = await _muteManager.GetTotalMutesAsync(steamId64);
            var totalGags = await _gagManager.GetTotalGagsAsync(steamId64);

            _core.Scheduler.NextTick(() =>
            {
                var name = target.Controller.PlayerName ?? _core.Localizer["player_fallback_name", target.PlayerID];
                var userId = target.PlayerID;
                var ip = (target.IPAddress ?? _core.Localizer["who_unknown"]).Split(':')[0];
                var ping = (int)target.Controller.Ping;
                var teamNum = target.Controller.TeamNum;
                var teamName = PlayerUtils.GetTeamName(teamNum, _core.Localizer);
                var isAlive = target.PlayerPawn?.IsValid == true && target.PlayerPawn.Health > 0;

                var lines = new List<string>
                {
                    _core.Localizer["who_header", name],
                    _core.Localizer["who_name", name],
                    _core.Localizer["who_userid", userId],
                    _core.Localizer["who_steamid", steamId64],
                    _core.Localizer["who_team", teamName, teamNum],
                    _core.Localizer["who_ip", ip],
                    _core.Localizer["who_ping", ping],
                    _core.Localizer["who_alive", isAlive ? _core.Localizer["players_yes"] : _core.Localizer["players_no"]]
                };

                if (admin != null)
                {
                    var flags = string.IsNullOrWhiteSpace(admin.Flags) ? _core.Localizer["who_none"] : admin.Flags;
                    lines.Add(_core.Localizer["who_admin_flags", flags, admin.Immunity]);
                }

                if (ban != null && ban.IsActive)
                {
                    var expires = ban.IsPermanent ? _core.Localizer["duration_permanent"] : ban.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? _core.Localizer["who_unknown"];
                    lines.Add(_core.Localizer["who_active_ban_yes", ban.Reason, expires]);
                }
                else
                {
                    lines.Add(_core.Localizer["who_active_ban_no"]);
                }

                if (mute != null && mute.IsActive)
                {
                    var expires = mute.IsPermanent ? _core.Localizer["duration_permanent"] : mute.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? _core.Localizer["who_unknown"];
                    lines.Add(_core.Localizer["who_active_mute_yes", mute.Reason, expires]);
                }
                else
                {
                    lines.Add(_core.Localizer["who_active_mute_no"]);
                }

                if (gag != null && gag.IsActive)
                {
                    var expires = gag.IsPermanent ? _core.Localizer["duration_permanent"] : gag.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? _core.Localizer["who_unknown"];
                    lines.Add(_core.Localizer["who_active_gag_yes", gag.Reason, expires]);
                }
                else
                {
                    lines.Add(_core.Localizer["who_active_gag_no"]);
                }

                lines.Add(_core.Localizer["who_total_bans", totalBans]);
                lines.Add(_core.Localizer["who_total_mutes", totalMutes]);
                lines.Add(_core.Localizer["who_total_gags", totalGags]);

                lines.Add(_core.Localizer["who_footer", name]);

                var output = string.Join('\n', lines);

                if (context.IsSentByPlayer && context.Sender != null)
                {
                    context.Sender.SendConsole(output);

                    if (context.Sender.IsValid && !context.Sender.IsFakeClient)
                    {
                        context.Sender.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["who_console"]}");
                    }
                }
                else
                {
                    _core.Logger.LogInformationIfEnabled("{WhoInfo}", output);
                }
            });
        });
    }

    public void OnPlayerDisconnect(int playerId)
    {
        _noclipPlayers.Remove(playerId);
        _frozenPlayers.Remove(playerId);
    }

    private bool HasPermission(ICommandContext context, string permission)
    {
        if (!context.IsSentByPlayer)
            return true;

        return _core.Permission.PlayerHasPermission(context.Sender!.SteamID, permission);
    }
}
