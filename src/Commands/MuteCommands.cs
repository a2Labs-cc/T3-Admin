using Furien_Admin.Config;
using Furien_Admin.Database;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace Furien_Admin.Commands;

public class MuteCommands
{
    private readonly ISwiftlyCore _core;
    private readonly MuteManager _muteManager;
    private readonly GagManager _gagManager;
    private readonly DiscordWebhook _discord;
    private readonly string _mutePermission;
    private readonly string _gagPermission;
    private readonly string _silencePermission;
    private readonly MessagesConfig _messagesConfig;

    public MuteCommands(
        ISwiftlyCore core, 
        MuteManager muteManager, 
        GagManager gagManager,
        DiscordWebhook discord, 
        string mutePermission,
        string gagPermission,
        string silencePermission,
        MessagesConfig messagesConfig)
    {
        _core = core;
        _muteManager = muteManager;
        _gagManager = gagManager;
        _discord = discord;
        _mutePermission = mutePermission;
        _gagPermission = gagPermission;
        _silencePermission = silencePermission;
        _messagesConfig = messagesConfig;
    }

    public void OnMuteCommand(ICommandContext context)
    {
        if (!HasPermission(context, _mutePermission))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["mute_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        if (!int.TryParse(context.Args[1], out int duration) || duration < 0)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["invalid_duration"]}");
            return;
        }

        string reason = context.Args.Length > 2 
            ? string.Join(" ", context.Args.Skip(2)) 
            : _core.Localizer["no_reason"];

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var adminSteamId = context.Sender?.SteamID ?? 0;
        var targetName = target.Controller.PlayerName;
        var targetSteamId = target.SteamID;

        _ = Task.Run(async () =>
        {
            var existingMute = await _muteManager.GetActiveMuteAsync(targetSteamId);
            if (existingMute != null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_already_muted", targetName]}"));
                return;
            }

            _muteManager.SetAdminContext(adminName, adminSteamId);
            await _muteManager.AddMuteAsync(targetSteamId, duration, reason);

            var durationText = duration == 0 ? _core.Localizer["duration_permanently"] : _core.Localizer["duration_for_minutes", duration];
            
            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                // Broadcast to all players
                foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["muted_notification", adminName, targetName, durationText, reason]}");
                }
                
                // Personal message to target
                var targetPlayer = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                if (targetPlayer != null)
                {
                    var durationDisplay = duration == 0 ? _core.Localizer["duration_permanent"] : _core.Localizer["duration_minutes", duration];
                    PlayerUtils.SendNotification(targetPlayer, _messagesConfig,
                        _core.Localizer["muted_personal_html", durationDisplay, reason],
                        $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["muted_personal_chat", durationText, reason]}");
                    targetPlayer.VoiceFlags = VoiceFlagValue.Muted;
                }
            });

            await _discord.SendMuteNotificationAsync(adminName, targetName, duration, reason);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} muted {Target} for {Duration} minutes. Reason: {Reason}", 
                adminName, targetName, duration, reason);
        });
    }

    public void OnUnmuteCommand(ICommandContext context)
    {
        if (!HasPermission(context, _mutePermission))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unmute_usage"]}");
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
        var adminSteamId = context.Sender?.SteamID ?? 0;
        var targetName = target.Controller.PlayerName;
        var targetSteamId = target.SteamID;

        _ = Task.Run(async () =>
        {
            var existingMute = await _muteManager.GetActiveMuteAsync(targetSteamId);
            if (existingMute == null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_muted", targetName]}"));
                return;
            }

            _muteManager.SetAdminContext(adminName, adminSteamId);
            await _muteManager.UnmuteAsync(targetSteamId, reason);

            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                // Broadcast to all players
                foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unmuted_notification", adminName, targetName, reason]}");
                }
                
                // Personal message to target
                var targetPlayer = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                if (targetPlayer != null)
                {
                    PlayerUtils.SendNotification(targetPlayer, _messagesConfig,
                        _core.Localizer["unmuted_personal_html", reason],
                        $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unmuted_personal_chat", reason]}");
                    targetPlayer.VoiceFlags = VoiceFlagValue.Normal;
                }
            });

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} unmuted {Target}. Reason: {Reason}", 
                adminName, targetName, reason);
        });
    }

    public void OnGagCommand(ICommandContext context)
    {
        if (!HasPermission(context, _gagPermission))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["gag_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        if (!int.TryParse(context.Args[1], out int duration) || duration < 0)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["invalid_duration"]}");
            return;
        }

        string reason = context.Args.Length > 2 
            ? string.Join(" ", context.Args.Skip(2)) 
            : _core.Localizer["no_reason"];

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var adminSteamId = context.Sender?.SteamID ?? 0;
        var targetName = target.Controller.PlayerName;
        var targetSteamId = target.SteamID;

        _ = Task.Run(async () =>
        {
            var existingGag = await _gagManager.GetActiveGagAsync(targetSteamId);
            if (existingGag != null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_already_gagged", targetName]}"));
                return;
            }

            _gagManager.SetAdminContext(adminName, adminSteamId);
            await _gagManager.AddGagAsync(targetSteamId, duration, reason);

            var durationText = duration == 0 ? _core.Localizer["duration_permanently"] : _core.Localizer["duration_for_minutes", duration];
            
            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                // Broadcast to all players
                foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["gagged_notification", adminName, targetName, durationText, reason]}");
                }
                
                // Personal message to target
                var targetPlayer = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                if (targetPlayer != null)
                {
                    var durationDisplay = duration == 0 ? _core.Localizer["duration_permanent"] : _core.Localizer["duration_minutes", duration];
                    PlayerUtils.SendNotification(targetPlayer, _messagesConfig,
                        _core.Localizer["gagged_personal_html", durationDisplay, reason],
                        $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["gagged_personal_chat", durationText, reason]}");
                }
            });

            await _discord.SendGagNotificationAsync(adminName, targetName, duration, reason);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} gagged {Target} for {Duration} minutes. Reason: {Reason}", 
                adminName, targetName, duration, reason);
        });
    }

    public void OnUngagCommand(ICommandContext context)
    {
        if (!HasPermission(context, _gagPermission))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["ungag_usage"]}");
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
        var adminSteamId = context.Sender?.SteamID ?? 0;
        var targetName = target.Controller.PlayerName;
        var targetSteamId = target.SteamID;

        _ = Task.Run(async () =>
        {
            var existingGag = await _gagManager.GetActiveGagAsync(targetSteamId);
            if (existingGag == null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_gagged", targetName]}"));
                return;
            }

            _gagManager.SetAdminContext(adminName, adminSteamId);
            await _gagManager.UngagAsync(targetSteamId, reason);

            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                // Broadcast to all players
                foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["ungagged_notification", adminName, targetName, reason]}");
                }
                
                // Personal message to target
                var targetPlayer = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                if (targetPlayer != null)
                {
                    PlayerUtils.SendNotification(targetPlayer, _messagesConfig,
                        _core.Localizer["ungagged_personal_html", reason],
                        $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["ungagged_personal_chat", reason]}");
                }
            });

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} ungagged {Target}. Reason: {Reason}", 
                adminName, targetName, reason);
        });
    }

    public void OnSilenceCommand(ICommandContext context)
    {
        if (!HasPermission(context, _silencePermission))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["silence_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        if (!int.TryParse(context.Args[1], out int duration) || duration < 0)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["invalid_duration"]}");
            return;
        }

        string reason = context.Args.Length > 2 
            ? string.Join(" ", context.Args.Skip(2)) 
            : _core.Localizer["no_reason"];

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var adminSteamId = context.Sender?.SteamID ?? 0;
        var targetName = target.Controller.PlayerName;
        var targetSteamId = target.SteamID;

        _ = Task.Run(async () =>
        {
            _muteManager.SetAdminContext(adminName, adminSteamId);
            _gagManager.SetAdminContext(adminName, adminSteamId);

            var existingMute = await _muteManager.GetActiveMuteAsync(targetSteamId);
            var existingGag = await _gagManager.GetActiveGagAsync(targetSteamId);

            if (existingMute != null && existingGag != null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_already_silenced", targetName]}"));
                return;
            }

            if (existingMute == null)
                await _muteManager.AddMuteAsync(targetSteamId, duration, reason);
            
            if (existingGag == null)
                await _gagManager.AddGagAsync(targetSteamId, duration, reason);

            var durationText = duration == 0 ? _core.Localizer["duration_permanently"] : _core.Localizer["duration_for_minutes", duration];
            
            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                // Broadcast to all players
                foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["silenced_notification", adminName, targetName, durationText, reason]}");
                }
                
                // Personal message to target
                var targetPlayer = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                if (targetPlayer != null)
                {
                    var durationDisplay = duration == 0 ? _core.Localizer["duration_permanent"] : _core.Localizer["duration_minutes", duration];
                    PlayerUtils.SendNotification(targetPlayer, _messagesConfig,
                        _core.Localizer["silenced_personal_html", durationDisplay, reason],
                        $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["silenced_personal_chat", durationText, reason]}");
                    targetPlayer.VoiceFlags = VoiceFlagValue.Muted;
                }
            });

            await _discord.SendSilenceNotificationAsync(adminName, targetName, duration, reason);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} silenced {Target} for {Duration} minutes. Reason: {Reason}", 
                adminName, targetName, duration, reason);
        });
    }

    public void OnUnsilenceCommand(ICommandContext context)
    {
        if (!HasPermission(context, _silencePermission))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unsilence_usage"]}");
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
        var adminSteamId = context.Sender?.SteamID ?? 0;
        var targetName = target.Controller.PlayerName;
        var targetSteamId = target.SteamID;

        _ = Task.Run(async () =>
        {
            _muteManager.SetAdminContext(adminName, adminSteamId);
            _gagManager.SetAdminContext(adminName, adminSteamId);

            var existingMute = await _muteManager.GetActiveMuteAsync(targetSteamId);
            var existingGag = await _gagManager.GetActiveGagAsync(targetSteamId);

            if (existingMute == null && existingGag == null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_silenced", targetName]}"));
                return;
            }

            if (existingMute != null)
                await _muteManager.UnmuteAsync(targetSteamId, reason);
            
            if (existingGag != null)
                await _gagManager.UngagAsync(targetSteamId, reason);

            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                // Broadcast to all players
                foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unsilenced_notification", adminName, targetName, reason]}");
                }
                
                // Personal message to target
                var targetPlayer = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                if (targetPlayer != null)
                {
                    PlayerUtils.SendNotification(targetPlayer, _messagesConfig,
                        _core.Localizer["unsilenced_personal_html", reason],
                        $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unsilenced_personal_chat", reason]}");
                    targetPlayer.VoiceFlags = VoiceFlagValue.Normal;
                }
            });

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} unsilenced {Target}. Reason: {Reason}", 
                adminName, targetName, reason);
        });
    }

    private bool HasPermission(ICommandContext context, string permission)
    {
        if (!context.IsSentByPlayer)
            return true;

        return _core.Permission.PlayerHasPermission(context.Sender!.SteamID, permission);
    }
}
