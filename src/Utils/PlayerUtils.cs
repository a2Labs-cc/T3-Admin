using System.Text.RegularExpressions;
using Furien_Admin.Config;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Translation;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace Furien_Admin.Utils;

public static class PlayerUtils
{
    public static bool TryParseSteamId(string input, out ulong steamId)
    {
        steamId = 0;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Direct SteamID64
        if (ulong.TryParse(input, out steamId) && steamId > 76561197960265728)
            return true;

        // STEAM_X:Y:Z format
        var steamIdMatch = Regex.Match(input, @"STEAM_(\d):(\d):(\d+)");
        if (steamIdMatch.Success)
        {
            ulong y = ulong.Parse(steamIdMatch.Groups[2].Value);
            ulong z = ulong.Parse(steamIdMatch.Groups[3].Value);
            steamId = 76561197960265728 + z * 2 + y;
            return true;
        }

        // [U:1:X] format
        var steam3Match = Regex.Match(input, @"\[U:1:(\d+)\]");
        if (steam3Match.Success)
        {
            ulong accountId = ulong.Parse(steam3Match.Groups[1].Value);
            steamId = 76561197960265728 + accountId;
            return true;
        }

        return false;
    }

    public static IPlayer? FindPlayerByTarget(ISwiftlyCore core, string target)
    {
        var players = core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();

        // Try by player ID/slot
        if (int.TryParse(target, out int playerId))
        {
            return players.FirstOrDefault(p => p.PlayerID == playerId);
        }

        // Try by SteamID
        if (TryParseSteamId(target, out ulong steamId))
        {
            return players.FirstOrDefault(p => p.SteamID == steamId);
        }

        // Try by name (partial match)
        var targetLower = target.ToLowerInvariant();
        
        // Exact match first
        var exactMatch = players.FirstOrDefault(p => 
            p.Controller.PlayerName?.Equals(target, StringComparison.OrdinalIgnoreCase) == true);
        if (exactMatch != null)
            return exactMatch;

        // Partial match
        var partialMatches = players.Where(p => 
            p.Controller.PlayerName?.Contains(target, StringComparison.OrdinalIgnoreCase) == true).ToList();
        
        if (partialMatches.Count == 1)
            return partialMatches[0];

        return null;
    }

    public static List<IPlayer> FindPlayersByTarget(ISwiftlyCore core, string target, bool includeDeadPlayers = true)
    {
        var players = core.PlayerManager.GetAllPlayers().Where(p => p.IsValid).ToList();

        if (!includeDeadPlayers)
        {
            players = players.Where(p => p.PlayerPawn?.IsValid == true && p.PlayerPawn.Health > 0).ToList();
        }

        // Special targets
        switch (target.ToLowerInvariant())
        {
            case "@all":
                return players;
            case "@t":
            case "@terrorists":
                return players.Where(p => p.Controller.TeamNum == 2).ToList();
            case "@ct":
            case "@counterterrorists":
                return players.Where(p => p.Controller.TeamNum == 3).ToList();
            case "@alive":
                return players.Where(p => p.PlayerPawn?.IsValid == true && p.PlayerPawn.Health > 0).ToList();
            case "@dead":
                return players.Where(p => p.PlayerPawn?.IsValid != true || p.PlayerPawn.Health <= 0).ToList();
            case "@bots":
                return players.Where(p => p.IsFakeClient).ToList();
            case "@humans":
                return players.Where(p => !p.IsFakeClient).ToList();
        }

        // Single player target
        var player = FindPlayerByTarget(core, target);
        return player != null ? new List<IPlayer> { player } : new List<IPlayer>();
    }

    public static void Freeze(IPlayer player)
    {
        if (player.PlayerPawn?.IsValid == true)
        {
            player.PlayerPawn.MoveType = MoveType_t.MOVETYPE_NONE;
            player.PlayerPawn.ActualMoveType = MoveType_t.MOVETYPE_NONE;
            player.PlayerPawn.MoveTypeUpdated();
            
            // Zero out velocity
            player.PlayerPawn.AbsVelocity = new Vector(0, 0, 0);
        }
    }

    public static void Unfreeze(IPlayer player)
    {
        if (player.PlayerPawn?.IsValid == true)
        {
            // Remove FL_FROZEN flag
            player.PlayerPawn.Flags &= ~(uint)Flags_t.FL_FROZEN;
            player.PlayerPawn.FlagsUpdated();
            
            // Restore MoveType to WALK
            player.PlayerPawn.MoveType = MoveType_t.MOVETYPE_WALK;
            player.PlayerPawn.ActualMoveType = MoveType_t.MOVETYPE_WALK;
            player.PlayerPawn.MoveTypeUpdated();
        }
    }

    public static void SetNoclip(ISwiftlyCore core, IPlayer player, bool enabled)
    {
        // Get sv_cheats convar
        var svCheats = core.ConVar.Find<bool>("sv_cheats");
        if (svCheats == null) return;
        
        // Store original value
        var originalCheats = svCheats.Value;
        
        // Enable cheats temporarily if needed
        if (!originalCheats)
        {
            svCheats.Value = true;
        }
        
        // Execute noclip command
        var currentlyEnabled = IsNoclipEnabled(player);
        if (enabled != currentlyEnabled)
        {
            player.ExecuteCommand("noclip");
        }
        
        // Restore original sv_cheats value after a short delay
        if (!originalCheats)
        {
            core.Scheduler.NextTick(() =>
            {
                svCheats.Value = false;
            });
        }
    }

    public static bool IsNoclipEnabled(IPlayer player)
    {
        return player.Controller?.NoClipEnabled == true || 
               player.PlayerPawn?.MoveType == MoveType_t.MOVETYPE_NOCLIP;
    }

    public static string GetTeamName(int teamNum, ILocalizer localizer)
    {
        return teamNum switch
        {
            0 => localizer["team_unassigned"],
            1 => localizer["team_spectator"],
            2 => localizer["team_terrorist"],
            3 => localizer["team_ct"],
            _ => localizer["team_unknown"]
        };
    }

    public static Team? ParseTeam(string input)
    {
        return input.ToLowerInvariant() switch
        {
            "t" or "terrorist" or "terrorists" or "2" => Team.T,
            "ct" or "counterterrorist" or "counterterrorists" or "3" => Team.CT,
            "spec" or "spectator" or "spectators" or "1" => Team.Spectator,
            _ => null
        };
    }

    /// <summary>
    /// Sends a notification message to a player, using CenterHTML if enabled in config, otherwise chat.
    /// </summary>
    public static void SendNotification(IPlayer player, MessagesConfig config, string htmlMessage, string chatMessage)
    {
        if (config.EnableCenterHtmlMessages)
        {
            player.SendCenterHTML(htmlMessage, config.CenterHtmlDurationMs);
        }
        else
        {
            player.SendChat(chatMessage);
        }
    }
}
