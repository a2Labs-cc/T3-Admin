using Furien_Admin.Config;
using Furien_Admin.Utils;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;

namespace Furien_Admin.Commands;

public class ChatCommands
{
    private readonly ISwiftlyCore _core;
    private readonly PermissionsConfig _permissions;
    private readonly MessagesConfig _messages;

    public ChatCommands(ISwiftlyCore core, PermissionsConfig permissions, MessagesConfig messages)
    {
        _core = core;
        _permissions = permissions;
        _messages = messages;
    }

    public void OnAsayCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Asay))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["asay_usage"]}");
            return;
        }

        var messageText = string.Join(" ", context.Args);
        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var prefix = _core.Localizer["asay_prefix"];
        var msg = $" \x04{prefix}\x01 \x10{adminName}\x01: {messageText}";

        int notified = 0;
        foreach (var p in GetOnlineAdmins(_permissions.Asay))
        {
            notified++;
            p.SendChat(msg);
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] ASAY from {Admin} delivered to {Count} admins: {Message}", adminName, notified, messageText);
    }

    public void OnSayCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Say))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["say_usage"]}");
            return;
        }

        var messageText = string.Join(" ", context.Args);
        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var prefix = _core.Localizer["say_prefix"];
        var msg = $" \x04{prefix}\x01 \x10{adminName}\x01: {messageText}";

        foreach (var p in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            p.SendChat(msg);
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] SAY from {Admin}: {Message}", adminName, messageText);
    }

    public void OnPsayCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Psay))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 2)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["psay_usage"]}");
            return;
        }

        var target = PlayerUtils.FindPlayerByTarget(_core, context.Args[0]);
        if (target == null || !target.IsValid)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["player_not_found"]}");
            return;
        }

        var messageText = string.Join(" ", context.Args.Skip(1));
        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var prefix = _core.Localizer["psay_prefix"];

        target.SendChat($" \x04{prefix}\x01 \x10{adminName}\x01: {messageText}");
        context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["psay_sent", target.Controller.PlayerName]}");

        _core.Logger.LogInformationIfEnabled("[T3-Admin] PSAY from {Admin} to {Target}: {Message}", adminName, target.Controller.PlayerName, messageText);
    }

    public void OnCsayCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Csay))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["csay_usage"]}");
            return;
        }

        var messageText = string.Join(" ", context.Args);
        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var htmlPrefix = _core.Localizer["csay_html_prefix"];
        var chatPrefix = _core.Localizer["csay_prefix"];

        var html = $"{htmlPrefix} <font color='#ffcc00'>{adminName}</font><br><font color='#ffffff'>{messageText}</font>";
        var chat = $" \x04{chatPrefix}\x01 \x10{adminName}\x01: {messageText}";

        foreach (var p in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            PlayerUtils.SendNotification(p, _messages, html, chat);
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] CSAY from {Admin}: {Message}", adminName, messageText);
    }

    public void OnHsayCommand(ICommandContext context)
    {
        if (!HasPermission(context, _permissions.Hsay))
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["no_permission"]}");
            return;
        }

        if (context.Args.Length < 1)
        {
            context.Reply($" \x02{_core.Localizer["prefix"]}\x01 {_core.Localizer["hsay_usage"]}");
            return;
        }

        var messageText = string.Join(" ", context.Args);
        var adminName = context.Sender?.Controller.PlayerName ?? _core.Localizer["console_name"];
        var htmlPrefix = _core.Localizer["hsay_html_prefix"];
        var chatPrefix = _core.Localizer["hsay_prefix"];

        var html = $"{htmlPrefix} <font color='#ffcc00'>{adminName}</font><br><font color='#ffffff'>{messageText}</font>";
        var chat = $" \x04{chatPrefix}\x01 \x10{adminName}\x01: {messageText}";

        foreach (var p in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid))
        {
            PlayerUtils.SendNotification(p, _messages, html, chat);
        }

        _core.Logger.LogInformationIfEnabled("[T3-Admin] HSAY from {Admin}: {Message}", adminName, messageText);
    }

    private bool HasPermission(ICommandContext context, string permission)
    {
        if (!context.IsSentByPlayer)
            return true;

        return _core.Permission.PlayerHasPermission(context.Sender!.SteamID, permission);
    }

    private IEnumerable<IPlayer> GetOnlineAdmins(string permission)
    {
        foreach (var p in _core.PlayerManager.GetAllPlayers().Where(p => p.IsValid && !p.IsFakeClient))
        {
            if (_core.Permission.PlayerHasPermission(p.SteamID, permission))
                yield return p;
        }
    }
}
