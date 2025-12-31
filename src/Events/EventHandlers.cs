using Furien_Admin.Config;
using Furien_Admin.Database;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;

namespace Furien_Admin.Events;

public class EventHandlers
{
    private readonly ISwiftlyCore _core;
    private readonly BanManager _banManager;
    private readonly MuteManager _muteManager;
    private readonly GagManager _gagManager;
    private readonly AdminDbManager _adminManager;
    private readonly PermissionsConfig _permissions;
    private readonly CommandsConfig _commands;
    
    private readonly Dictionary<int, DateTime> _muteWarnTimestamps = new();
    private readonly Dictionary<int, DateTime> _gagWarnTimestamps = new();
    
    private Guid _chatHookGuid = Guid.Empty;
    private CancellationTokenSource? _expiryCheckCts;

    public event Action<int>? OnPlayerDisconnected;

    public EventHandlers(
        ISwiftlyCore core,
        BanManager banManager,
        MuteManager muteManager,
        GagManager gagManager,
        AdminDbManager adminManager,
        PermissionsConfig permissions,
        CommandsConfig commands)
    {
        _core = core;
        _banManager = banManager;
        _muteManager = muteManager;
        _gagManager = gagManager;
        _adminManager = adminManager;
        _permissions = permissions;
        _commands = commands;
    }
    
    public void RegisterHooks()
    {
        // Hook into chat to block gagged players
        _chatHookGuid = _core.Command.HookClientChat(OnClientChat);
        _core.Logger.LogInformationIfEnabled("[T3-Admin] Chat hook registered");
        
        // Start periodic expiry check (every 30 seconds)
        _expiryCheckCts = _core.Scheduler.RepeatBySeconds(30f, CheckExpiredPunishments);
        _core.Logger.LogInformationIfEnabled("[T3-Admin] Expiry check timer started");
    }
    
    public void UnregisterHooks()
    {
        if (_chatHookGuid != Guid.Empty)
        {
            _core.Command.UnhookClientChat(_chatHookGuid);
            _chatHookGuid = Guid.Empty;
        }
        
        _expiryCheckCts?.Cancel();
        _expiryCheckCts = null;
    }
    
    private void CheckExpiredPunishments()
    {
        foreach (var player in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            var steamId = player.SteamID;
            var playerId = player.PlayerID;
            
            // Check mute expiry
            var cachedMute = _muteManager.GetActiveMuteFromCache(steamId);
            if (cachedMute != null && cachedMute.IsExpired)
            {
                // Mute has expired - notify player and remove mute
                player.SendChat($" \x04{_core.Localizer["prefix"]}\x01 {_core.Localizer["mute_expired"]}");
                player.VoiceFlags = VoiceFlagValue.Normal;
                _muteManager.ClearCache();
                _muteWarnTimestamps.Remove(playerId);
                _core.Logger.LogInformationIfEnabled("[T3-Admin] Mute expired for player {SteamId}", steamId);
            }
            
            // Check gag expiry
            var cachedGag = _gagManager.GetActiveGagFromCache(steamId);
            if (cachedGag != null && cachedGag.IsExpired)
            {
                // Gag has expired - notify player
                player.SendChat($" \x04{_core.Localizer["prefix"]}\x01 {_core.Localizer["gag_expired"]}");
                _gagManager.ClearCache();
                _gagWarnTimestamps.Remove(playerId);
                _core.Logger.LogInformationIfEnabled("[T3-Admin] Gag expired for player {SteamId}", steamId);
            }
        }
        
        // Cleanup expired punishments in database
        _ = Task.Run(async () =>
        {
            await _banManager.CleanupExpiredBansAsync();
            await _muteManager.UpdateExpiredMutesAsync();
            await _gagManager.CleanupExpiredGagsAsync();
        });
    }

    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        var playerId = @event.PlayerId;
        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player == null || !player.IsValid)
            return;

        var steamId = player.SteamID;

        _core.Logger.LogInformationIfEnabled("[T3-Admin] OnClientSteamAuthorize fired for {Name} ({SteamId})", player.Controller.PlayerName, steamId);

        _ = Task.Run(async () =>
        {
            try
            {
                // Cleanup expired bans
                await _banManager.CleanupExpiredBansAsync();

                // Check if player is banned
                var ban = await _banManager.GetActiveBanAsync(steamId);
                if (ban != null && ban.IsActive)
                {
                    var bannedPlayer = _core.PlayerManager.GetPlayer(playerId);
                    if (bannedPlayer?.IsValid == true)
                    {
                        var reason = ban.IsPermanent 
                            ? _core.Localizer["ban_kick_reason_permanent", ban.Reason]
                            : _core.Localizer["ban_kick_reason_minutes", (int)ban.TimeRemaining!.Value.TotalMinutes, ban.Reason];
                        
                        bannedPlayer.Kick(reason, ENetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED);
                    }
                    return;
                }

                // Check if player is muted and apply voice mute
                var mute = await _muteManager.GetActiveMuteAsync(steamId);
                if (mute != null && mute.IsActive)
                {
                    var mutedPlayer = _core.PlayerManager.GetPlayer(playerId);
                    if (mutedPlayer?.IsValid == true)
                    {
                        mutedPlayer.VoiceFlags = VoiceFlagValue.Muted;
                        _core.Logger.LogInformationIfEnabled("[T3-Admin] Applied mute to player {SteamId}", steamId);
                    }
                }

                // Load admin permissions
                var admin = await _adminManager.GetAdminAsync(steamId);
                if (admin != null && admin.IsActive)
                {
                    foreach (var flag in admin.Flags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        _core.Permission.AddPermission(steamId, flag.Trim());
                    }
                    _core.Logger.LogInformationIfEnabled("[T3-Admin] Loaded admin permissions for {SteamId}: {Flags}", steamId, admin.Flags);
                }

                // After permissions are loaded, notify all online admins about this player's history
                await SendJoinPunishmentSummaryAsync(playerId, steamId);
            }
            catch (Exception ex)
            {
                _core.Logger.LogErrorIfEnabled("[T3-Admin] Error in OnClientSteamAuthorize: {Message}", ex.Message);
            }
        });
    }

    private async Task SendJoinPunishmentSummaryAsync(int playerId, ulong steamId)
    {
        try
        {
            var totalBans = await _banManager.GetTotalBansAsync(steamId);
            var totalMutes = await _muteManager.GetTotalMutesAsync(steamId);
            var totalGags = await _gagManager.GetTotalGagsAsync(steamId);

            var activeBan = await _banManager.GetActiveBanAsync(steamId);
            var activeMute = await _muteManager.GetActiveMuteAsync(steamId);
            var activeGag = await _gagManager.GetActiveGagAsync(steamId);

            var player = _core.PlayerManager.GetPlayer(playerId);
            var playerName = player?.Controller.PlayerName ?? _core.Localizer["unknown"];

            _core.Logger.LogInformationIfEnabled("[T3-Admin] Preparing join summary for {Name} ({SteamId})", playerName, steamId);

            var summary = $"Bans:\x10{totalBans}\x01 Mutes:\x10{totalMutes}\x01 Gags:\x10{totalGags}\x01";

            var activeParts = new List<string>();
            if (activeBan != null && activeBan.IsActive) activeParts.Add(_core.Localizer["join_active_ban"]);
            if (activeMute != null && activeMute.IsActive) activeParts.Add(_core.Localizer["join_active_mute"]);
            if (activeGag != null && activeGag.IsActive) activeParts.Add(_core.Localizer["join_active_gag"]);

            var activeLine = activeParts.Count > 0
                ? $"\x10{string.Join(", ", activeParts)}\x01"
                : $"\x09{_core.Localizer["join_active_none"]}\x01";

            var message = $" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["join_summary", playerName, steamId, totalBans, totalMutes, totalGags, activeLine]}";

            _core.Scheduler.NextTick(() =>
            {
                int notified = 0;

                foreach (var p in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient))
                {
                    if (_core.Permission.PlayerHasPermission(p.SteamID, _permissions.ListPlayers))
                    {
                        notified++;
                        p.SendChat(message);
                    }
                }

                _core.Logger.LogInformationIfEnabled("[T3-Admin] Join summary for {Name} ({SteamId}) sent to {Count} admins.", playerName, steamId, notified);
            });
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error sending join punishment summary: {Message}", ex.Message);
        }
    }

    public void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var playerId = @event.PlayerId;
        
        _muteWarnTimestamps.Remove(playerId);
        _gagWarnTimestamps.Remove(playerId);

        // Allow re-join notifications in the future
        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player?.IsValid == true)
        {
        }

        OnPlayerDisconnected?.Invoke(playerId);
    }

    public HookResult OnPlayerSpeak(int playerId)
    {
        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player?.IsValid != true)
            return HookResult.Continue;

        var steamId = player.SteamID;
        var cachedMute = _muteManager.GetActiveMuteFromCache(steamId);

        if (cachedMute != null && cachedMute.IsActive)
        {
            // Show warning message with cooldown
            bool shouldShowMessage = !_muteWarnTimestamps.ContainsKey(playerId) ||
                                   (DateTime.UtcNow - _muteWarnTimestamps[playerId]).TotalSeconds >= 5;

            if (shouldShowMessage)
            {
                if (cachedMute.IsPermanent)
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["muted_warning_permanent"]}");
                }
                else
                {
                    var remainingMinutes = (int)Math.Ceiling(cachedMute.TimeRemaining!.Value.TotalMinutes);
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["muted_warning_minutes", remainingMinutes]}");
                }
                _muteWarnTimestamps[playerId] = DateTime.UtcNow;
            }

            player.VoiceFlags = VoiceFlagValue.Muted;
            return HookResult.Stop;
        }

        // Check database asynchronously for cache miss
        _ = Task.Run(async () =>
        {
            var mute = await _muteManager.GetActiveMuteAsync(steamId);
            if (mute != null && mute.IsActive)
            {
                player.VoiceFlags = VoiceFlagValue.Muted;
            }
            else
            {
                player.VoiceFlags = VoiceFlagValue.Normal;
            }
        });

        return HookResult.Continue;
    }

    public bool CheckGag(ulong steamId, int playerId, IPlayer player)
    {
        var cachedGag = _gagManager.GetActiveGagFromCache(steamId);

        if (cachedGag != null && cachedGag.IsActive)
        {
            // Show warning message with cooldown
            bool shouldShowMessage = !_gagWarnTimestamps.ContainsKey(playerId) ||
                                   (DateTime.UtcNow - _gagWarnTimestamps[playerId]).TotalSeconds >= 5;

            if (shouldShowMessage)
            {
                if (cachedGag.IsPermanent)
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["gagged_warning_permanent"]}");
                }
                else
                {
                    var remainingMinutes = (int)Math.Ceiling(cachedGag.TimeRemaining!.Value.TotalMinutes);
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["gagged_warning_minutes", remainingMinutes]}");
                }
                _gagWarnTimestamps[playerId] = DateTime.UtcNow;
            }

            return true; // Block message
        }

        // Check database asynchronously for cache miss
        _ = Task.Run(async () =>
        {
            await _gagManager.GetActiveGagAsync(steamId);
        });

        return false;
    }

    public HookResult OnRoundStart(EventRoundStart @event)
    {
        // Clear warning timestamps on round start
        _muteWarnTimestamps.Clear();
        _gagWarnTimestamps.Clear();

        return HookResult.Continue;
    }
    
    /// <summary>
    /// Chat hook handler - blocks gagged players from chatting
    /// </summary>
    public HookResult OnClientChat(int playerId, string text, bool teamOnly)
    {
        var player = _core.PlayerManager.GetPlayer(playerId);
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if (!string.IsNullOrWhiteSpace(text) && (text.StartsWith("!") || text.StartsWith("/")))
        {
            var trimmed = text.Trim();
            var withoutPrefix = trimmed.Length > 1 ? trimmed.Substring(1) : "";
            var cmdName = withoutPrefix.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(cmdName))
            {
                bool isAdminChatCommand =
                    _commands.Asay.Any(c => c.Equals(cmdName, StringComparison.OrdinalIgnoreCase)) ||
                    _commands.Say.Any(c => c.Equals(cmdName, StringComparison.OrdinalIgnoreCase)) ||
                    _commands.Psay.Any(c => c.Equals(cmdName, StringComparison.OrdinalIgnoreCase)) ||
                    _commands.Csay.Any(c => c.Equals(cmdName, StringComparison.OrdinalIgnoreCase)) ||
                    _commands.Hsay.Any(c => c.Equals(cmdName, StringComparison.OrdinalIgnoreCase));

                if (isAdminChatCommand)
                    return HookResult.Stop;
            }
        }
        
        var steamId = player.SteamID;
        
        // Check gag from cache first
        var cachedGag = _gagManager.GetActiveGagFromCache(steamId);
        if (cachedGag != null && cachedGag.IsActive)
        {
            // Show warning message with cooldown
            bool shouldShowMessage = !_gagWarnTimestamps.ContainsKey(playerId) ||
                                   (DateTime.UtcNow - _gagWarnTimestamps[playerId]).TotalSeconds >= 5;

            if (shouldShowMessage)
            {
                if (cachedGag.IsPermanent)
                {
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["gagged_chat_warning_permanent"]}");
                }
                else
                {
                    var remainingMinutes = (int)Math.Ceiling(cachedGag.TimeRemaining!.Value.TotalMinutes);
                    player.SendChat($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["gagged_chat_warning_minutes", remainingMinutes]}");
                }
                _gagWarnTimestamps[playerId] = DateTime.UtcNow;
            }

            return HookResult.Stop; // Block the chat message
        }
        
        // Check database asynchronously for cache miss (for next time)
        _ = Task.Run(async () =>
        {
            await _gagManager.GetActiveGagAsync(steamId);
        });

        return HookResult.Continue;
    }
}
