using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;

namespace Furien_Admin.Utils;

public class DiscordWebhook
{
    private readonly ISwiftlyCore _core;
    private readonly string _webhookUrl;
    private readonly HttpClient _httpClient;

    public DiscordWebhook(ISwiftlyCore core, string webhookUrl)
    {
        _core = core;
        _webhookUrl = webhookUrl;
        _httpClient = new HttpClient();
    }

    public async Task SendBanNotificationAsync(string adminName, string targetName, int duration, string reason)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
            return;

        try
        {
            var embed = new
            {
                title = _core.Localizer["discord_ban_title"],
                color = 15158332, // Red
                fields = new[]
                {
                    new { name = _core.Localizer["discord_admin"], value = adminName, inline = true },
                    new { name = _core.Localizer["discord_player"], value = targetName, inline = true },
                    new { name = _core.Localizer["discord_duration"], value = duration == 0 ? _core.Localizer["discord_permanent"] : _core.Localizer["discord_minutes", duration], inline = true },
                    new { name = _core.Localizer["discord_reason"], value = reason, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            await SendEmbedAsync(embed);
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error sending ban notification: {Message}", ex.Message);
        }
    }

    public async Task SendUnbanNotificationAsync(string adminName, string targetSteamId, string reason)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
            return;

        try
        {
            var embed = new
            {
                title = _core.Localizer["discord_unban_title"],
                color = 3066993, // Green
                fields = new[]
                {
                    new { name = _core.Localizer["discord_admin"], value = adminName, inline = true },
                    new { name = _core.Localizer["discord_steamid"], value = targetSteamId, inline = true },
                    new { name = _core.Localizer["discord_reason"], value = reason, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            await SendEmbedAsync(embed);
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error sending unban notification: {Message}", ex.Message);
        }
    }

    public async Task SendMuteNotificationAsync(string adminName, string targetName, int duration, string reason)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
            return;

        try
        {
            var embed = new
            {
                title = _core.Localizer["discord_mute_title"],
                color = 15105570, // Orange
                fields = new[]
                {
                    new { name = _core.Localizer["discord_admin"], value = adminName, inline = true },
                    new { name = _core.Localizer["discord_player"], value = targetName, inline = true },
                    new { name = _core.Localizer["discord_duration"], value = duration == 0 ? _core.Localizer["discord_permanent"] : _core.Localizer["discord_minutes", duration], inline = true },
                    new { name = _core.Localizer["discord_reason"], value = reason, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            await SendEmbedAsync(embed);
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error sending mute notification: {Message}", ex.Message);
        }
    }

    public async Task SendGagNotificationAsync(string adminName, string targetName, int duration, string reason)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
            return;

        try
        {
            var embed = new
            {
                title = _core.Localizer["discord_gag_title"],
                color = 15105570, // Orange
                fields = new[]
                {
                    new { name = _core.Localizer["discord_admin"], value = adminName, inline = true },
                    new { name = _core.Localizer["discord_player"], value = targetName, inline = true },
                    new { name = _core.Localizer["discord_duration"], value = duration == 0 ? _core.Localizer["discord_permanent"] : _core.Localizer["discord_minutes", duration], inline = true },
                    new { name = _core.Localizer["discord_reason"], value = reason, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            await SendEmbedAsync(embed);
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error sending gag notification: {Message}", ex.Message);
        }
    }

    public async Task SendKickNotificationAsync(string adminName, string targetName, string reason)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
            return;

        try
        {
            var embed = new
            {
                title = _core.Localizer["discord_kick_title"],
                color = 15844367, // Gold
                fields = new[]
                {
                    new { name = _core.Localizer["discord_admin"], value = adminName, inline = true },
                    new { name = _core.Localizer["discord_player"], value = targetName, inline = true },
                    new { name = _core.Localizer["discord_reason"], value = reason, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            await SendEmbedAsync(embed);
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error sending kick notification: {Message}", ex.Message);
        }
    }

    public async Task SendSilenceNotificationAsync(string adminName, string targetName, int duration, string reason)
    {
        if (string.IsNullOrEmpty(_webhookUrl))
            return;

        try
        {
            var embed = new
            {
                title = _core.Localizer["discord_silence_title"],
                color = 10181046, // Purple
                fields = new[]
                {
                    new { name = _core.Localizer["discord_admin"], value = adminName, inline = true },
                    new { name = _core.Localizer["discord_player"], value = targetName, inline = true },
                    new { name = _core.Localizer["discord_duration"], value = duration == 0 ? _core.Localizer["discord_permanent"] : _core.Localizer["discord_minutes", duration], inline = true },
                    new { name = _core.Localizer["discord_reason"], value = reason, inline = false }
                },
                timestamp = DateTime.UtcNow.ToString("o")
            };

            await SendEmbedAsync(embed);
        }
        catch (Exception ex)
        {
            _core.Logger.LogErrorIfEnabled("[T3-Admin] Error sending silence notification: {Message}", ex.Message);
        }
    }

    private async Task SendEmbedAsync(object embed)
    {
        var payload = new { embeds = new[] { embed } };
        var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        await _httpClient.PostAsync(_webhookUrl, content);
    }
}
