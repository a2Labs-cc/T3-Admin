using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Core.Menus.OptionsBase;
using Furien_Admin.Config;
using Furien_Admin.Menu.Handlers;
using System.Linq;

namespace Furien_Admin.Menu;

public class AdminMenuManager
{
    private readonly ISwiftlyCore _core;
    private readonly PluginConfig _config;
    private readonly Dictionary<string, IAdminMenuHandler> _handlers;

    public AdminMenuManager(ISwiftlyCore core, PluginConfig config)
    {
        _core = core;
        _config = config;
        _handlers = new Dictionary<string, IAdminMenuHandler>();

        // Register handlers
        RegisterHandler("server_management", new ServerManagementHandler(_core, _config));
        RegisterHandler("player_management", new PlayerManagementHandler(_core, _config));
        RegisterHandler("admin_management", new AdminManagementHandler(_core, _config));
        RegisterHandler("fun_commands", new FunCommandsMenuHandler(_core, _config));
    }

    private void RegisterHandler(string key, IAdminMenuHandler handler)
    {
        _handlers[key] = handler;
    }

    public void OpenAdminMenu(IPlayer player)
    {
        // Create a menu builder
        var builder = _core.MenusAPI.CreateBuilder();
        
        // Set Title using the Design API
        string title;
        try
        {
            title = _core.Localizer["menu_admin_title"];
        }
        catch
        {
            title = "Admin Menu";
        }

        // Match Swiftly Admins navigation styling using configurable color
        builder.Design
            .SetMenuTitle(title)
            .Design.SetMenuFooterColor(_config.AdminMenuColor)
            .Design.SetVisualGuideLineColor(_config.AdminMenuColor)
            .Design.SetNavigationMarkerColor(_config.AdminMenuColor);

        // Add Options for each handler
        AddHandlerOption(player, builder, "server_management", "menu_server_management");
        AddHandlerOption(player, builder, "player_management", "menu_player_management");
        AddHandlerOption(player, builder, "fun_commands", "menu_fun_commands");
        AddHandlerOption(player, builder, "admin_management", "menu_admin_management");

        // Build and Open
        var menu = builder.Build();
        _core.MenusAPI.OpenMenuForPlayer(player, menu);
    }

    private void AddHandlerOption(IPlayer player, IMenuBuilderAPI builder, string key, string translationKey)
    {
        if (!_handlers.TryGetValue(key, out var handler))
            return;

        string text;
        try
        {
            text = _core.Localizer[translationKey];
        }
        catch
        {
            // Fallback to a readable version of the key
            text = translationKey.Replace("menu_", "").Replace("_", " ");
            // Capitalize first letter of each word
            text = string.Join(" ", text.Split(' ').Select(word => char.ToUpper(word[0]) + word.Substring(1)));
        }

        builder.AddOption(new SubmenuMenuOption(text, () => handler.CreateMenu(player)));
    }
}
