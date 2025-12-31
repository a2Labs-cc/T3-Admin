using Furien_Admin.Config;
using Furien_Admin.Database;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Furien_Admin.Commands;

public class BanCommands
{
    private readonly ISwiftlyCore _core;
    private readonly BanManager _banManager;
    private readonly DiscordWebhook _discord;
    private readonly string _banPermission;
    private readonly MessagesConfig _messagesConfig;

    public BanCommands(ISwiftlyCore core, BanManager banManager, DiscordWebhook discord, string banPermission, MessagesConfig messagesConfig)
    {
        _core = core;
        _banManager = banManager;
        _discord = discord;
        _banPermission = banPermission;
        _messagesConfig = messagesConfig;
    }

    public void OnBanCommand(ICommandContext context)
    {
        if (!HasPermission(context))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["ban_usage"]}");
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
            var existingBan = await _banManager.GetActiveBanAsync(targetSteamId);
            if (existingBan != null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_already_banned", targetName]}"));
                return;
            }

            _banManager.SetAdminContext(adminName, adminSteamId);
            await _banManager.AddBanAsync(targetSteamId, duration, reason);

            var durationText = duration == 0 ? _core.Localizer["duration_permanently"] : _core.Localizer["duration_for_minutes", duration];
            
            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                // Notify all players
                foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["banned_notification", adminName, targetName, durationText, reason]}");
                }

                // Show ban message to target and kick
                var targetPlayer = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                if (targetPlayer != null)
                {
                    var durationDisplay = duration == 0 ? _core.Localizer["duration_permanent"] : _core.Localizer["duration_minutes", duration];
                    PlayerUtils.SendNotification(targetPlayer, _messagesConfig,
                        _core.Localizer["banned_personal_html", durationDisplay, reason],
                        $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["banned_personal_chat", durationText, reason]}");
                    
                    // Kick after 3 seconds (use DelayBySeconds for proper timing)
                    _core.Scheduler.DelayBySeconds(3f, () =>
                    {
                        var playerToKick = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                        playerToKick?.Kick($"Banned: {reason}", ENetworkDisconnectionReason.NETWORK_DISCONNECT_BANADDED);
                    });
                }
            });

            // Discord notification
            await _discord.SendBanNotificationAsync(adminName, targetName, duration, reason);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} banned {Target} for {Duration} minutes. Reason: {Reason}", 
                adminName, targetName, duration, reason);
        });
    }

    public void OnAddBanCommand(ICommandContext context)
    {
        if (!HasPermission(context))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["addban_usage"]}");
            return;
        }

        if (!PlayerUtils.TryParseSteamId(context.Args[0], out ulong targetSteamId))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["invalid_steamid"]}");
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

        _ = Task.Run(async () =>
        {
            var existingBan = await _banManager.GetActiveBanAsync(targetSteamId);
            if (existingBan != null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["steamid_already_banned", targetSteamId]}"));
                return;
            }

            _banManager.SetAdminContext(adminName, adminSteamId);
            await _banManager.AddBanAsync(targetSteamId, duration, reason);

            // Schedule game operations on main thread
            _core.Scheduler.NextTick(() =>
            {
                var durationDisplay = duration == 0 ? _core.Localizer["duration_permanent"] : _core.Localizer["duration_minutes", duration];
                context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["addban_success", targetSteamId, durationDisplay]}");

                // Check if player is online and kick them
                var onlinePlayer = _core.PlayerManager.GetAllPlayers()
                    .FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                
                if (onlinePlayer != null)
                {
                    var durationText2 = duration == 0 ? _core.Localizer["duration_permanently"] : _core.Localizer["duration_for_minutes", duration];
                    var durationDisplay2 = duration == 0 ? _core.Localizer["duration_permanent"] : _core.Localizer["duration_minutes", duration];
                    PlayerUtils.SendNotification(onlinePlayer, _messagesConfig,
                        _core.Localizer["banned_personal_html", durationDisplay2, reason],
                        $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["banned_personal_chat", durationText2, reason]}");
                    
                    // Kick after 3 seconds
                    _core.Scheduler.DelayBySeconds(3f, () =>
                    {
                        var playerToKick = _core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && p.SteamID == targetSteamId);
                        playerToKick?.Kick($"Banned: {reason}", ENetworkDisconnectionReason.NETWORK_DISCONNECT_BANADDED);
                    });
                }
            });

            await _discord.SendBanNotificationAsync(adminName, targetSteamId.ToString(), duration, reason);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} added ban for SteamID {SteamId}. Duration: {Duration} minutes. Reason: {Reason}", 
                adminName, targetSteamId, duration, reason);
        });
    }

    public void OnUnbanCommand(ICommandContext context)
    {
        if (!HasPermission(context))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unban_usage"]}");
            return;
        }

        if (!PlayerUtils.TryParseSteamId(context.Args[0], out ulong targetSteamId))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["invalid_steamid"]}");
            return;
        }

        string reason = context.Args.Length > 1 
            ? string.Join(" ", context.Args.Skip(1)) 
            : _core.Localizer["no_reason"];

        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var adminSteamId = context.Sender?.SteamID ?? 0;

        _ = Task.Run(async () =>
        {
            var existingBan = await _banManager.GetActiveBanAsync(targetSteamId);
            if (existingBan == null)
            {
                _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["steamid_not_banned", targetSteamId]}"));
                return;
            }

            _banManager.SetAdminContext(adminName, adminSteamId);
            await _banManager.UnbanAsync(targetSteamId, reason);

            _core.Scheduler.NextTick(() => context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["unbanned_success", targetSteamId, reason]}"));

            await _discord.SendUnbanNotificationAsync(adminName, targetSteamId.ToString(), reason);

            _core.Logger.LogInformationIfEnabled("[T3-Admin] {Admin} unbanned SteamID {SteamId}. Reason: {Reason}", 
                adminName, targetSteamId, reason);
        });
    }

    private bool HasPermission(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
            return true;

        return _core.Permission.PlayerHasPermission(context.Sender!.SteamID, _banPermission);
    }
}
