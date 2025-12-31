using Furien_Admin.Commands;
using Furien_Admin.Config;
using Furien_Admin.Database;
using Furien_Admin.Events;
using Furien_Admin.Menu;
using Furien_Admin.Utils;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Plugins;

namespace T3_Admin;

[PluginMetadata(Id = "T3_Admin", Version = "1.0.1", Name = "T3 Admin", Author = "T3Marius, aga", Description = "A comprehensive admin plugin for CS2.")]
public partial class T3_Admin : BasePlugin
{
    private PluginConfig _config = null!;
    public PluginConfig Config => _config;
    public AdminMenuManager AdminMenuManager { get; private set; } = null!;

    // Database managers
    private BanManager _banManager = null!;
    private MuteManager _muteManager = null!;
    private GagManager _gagManager = null!;
    private AdminDbManager _adminDbManager = null!;

    // Command handlers
    private BanCommands _banCommands = null!;
    private MuteCommands _muteCommands = null!;
    private PlayerCommands _playerCommands = null!;
    private ServerCommands _serverCommands = null!;
    private AdminCommands _adminCommands = null!;
    private ChatCommands _chatCommands = null!;

    // Event handlers
    private EventHandlers _eventHandlers = null!;

    // Utils
    private DiscordWebhook _discord = null!;

    public T3_Admin(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void Load(bool hotReload)
    {
        // Load configuration
        LoadConfiguration();

        Core.Logger.LogInformationIfEnabled("[T3Admin] Loading plugin...");

        // Initialize database managers
        InitializeDatabaseManagers();

        // Initialize Admin Menu Manager
        AdminMenuManager = new AdminMenuManager(Core, Config);

        // Initialize utilities
        _discord = new DiscordWebhook(Core, Config.Discord.Webhook);

        // Initialize command handlers
        InitializeCommandHandlers();

        // Initialize event handlers
        InitializeEventHandlers();

        // Register commands
        RegisterCommands();

        // Register events
        RegisterEvents();

        // Initialize databases
        _ = InitializeDatabasesAsync();

        Core.Logger.LogInformationIfEnabled("[T3Admin] Plugin loaded successfully!");
    }

    public override void Unload()
    {
        Core.Logger.LogInformationIfEnabled("[T3Admin] Unloading plugin...");
        _eventHandlers?.UnregisterHooks();
    }

    private void LoadConfiguration()
    {
        try
        {
            // Initialize config file with model - this will auto-create config.json if it doesn't exist
            Core.Configuration
                .InitializeJsonWithModel<PluginConfig>("config.json", "T3Admin")
                .Configure(builder => builder.AddJsonFile(Core.Configuration.GetConfigPath("config.json"), optional: false, reloadOnChange: true));

            // Bind configuration to our model
            _config = new PluginConfig();
            Core.Configuration.Manager.GetSection("T3Admin").Bind(_config);

            DebugSettings.LoggingEnabled = _config.Debug.Enabled;
            
            Core.Logger.LogInformationIfEnabled("[T3Admin] Configuration loaded from {Path}", Core.Configuration.GetConfigPath("config.json"));
        }
        catch (Exception ex)
        {
            _config = new PluginConfig();

            DebugSettings.LoggingEnabled = _config.Debug.Enabled;
            Core.Logger.LogWarningIfEnabled("[T3Admin] Failed to load config, using defaults: {Message}", ex.Message);
        }
    }

    private void InitializeDatabaseManagers()
    {
        _banManager = new BanManager(Core);
        _muteManager = new MuteManager(Core);
        _gagManager = new GagManager(Core);
        _adminDbManager = new AdminDbManager(Core);
    }

    private void InitializeCommandHandlers()
    {
        _banCommands = new BanCommands(Core, _banManager, _discord, _config.Permissions.Ban, _config.Messages);
        _muteCommands = new MuteCommands(
            Core, 
            _muteManager, 
            _gagManager, 
            _discord, 
            _config.Permissions.Mute,
            _config.Permissions.Gag,
            _config.Permissions.Silence,
            _config.Messages);
        _playerCommands = new PlayerCommands(Core, _discord, _config.Permissions, _config.Messages, _banManager, _muteManager, _gagManager, _adminDbManager);
        _serverCommands = new ServerCommands(Core, _config.Permissions, _config.GameMaps, _config.WorkshopMaps);
        _adminCommands = new AdminCommands(Core, _adminDbManager, _config.Permissions, AdminMenuManager);
        _chatCommands = new ChatCommands(Core, _config.Permissions, _config.Messages);
    }

    private void InitializeEventHandlers()
    {
        _eventHandlers = new EventHandlers(Core, _banManager, _muteManager, _gagManager, _adminDbManager, _config.Permissions, _config.Commands);
        _eventHandlers.OnPlayerDisconnected += playerId => _playerCommands.OnPlayerDisconnect(playerId);
        _eventHandlers.RegisterHooks();
    }

    private async Task InitializeDatabasesAsync()
    {
        await _banManager.InitializeAsync();
        await _muteManager.InitializeAsync();
        await _gagManager.InitializeAsync();
        await _adminDbManager.InitializeAsync();
    }

    private void RegisterCommands()
    {
        // Admin menu commands
        foreach (var cmd in _config.Commands.AdminMenu)
            RegisterCommand(cmd, _adminCommands.OnAdminMenuCommand);

        // Admin communication commands
        foreach (var cmd in _config.Commands.Asay)
            RegisterCommand(cmd, _chatCommands.OnAsayCommand);
        foreach (var cmd in _config.Commands.Say)
            RegisterCommand(cmd, _chatCommands.OnSayCommand);
        foreach (var cmd in _config.Commands.Psay)
            RegisterCommand(cmd, _chatCommands.OnPsayCommand);
        foreach (var cmd in _config.Commands.Csay)
            RegisterCommand(cmd, _chatCommands.OnCsayCommand);
        foreach (var cmd in _config.Commands.Hsay)
            RegisterCommand(cmd, _chatCommands.OnHsayCommand);

        // Ban commands
        foreach (var cmd in _config.Commands.Ban)
            RegisterCommand(cmd, _banCommands.OnBanCommand);
        foreach (var cmd in _config.Commands.AddBan)
            RegisterCommand(cmd, _banCommands.OnAddBanCommand);
        foreach (var cmd in _config.Commands.Unban)
            RegisterCommand(cmd, _banCommands.OnUnbanCommand);

        // Mute/Gag commands
        foreach (var cmd in _config.Commands.Mute)
            RegisterCommand(cmd, _muteCommands.OnMuteCommand);
        foreach (var cmd in _config.Commands.Unmute)
            RegisterCommand(cmd, _muteCommands.OnUnmuteCommand);
        foreach (var cmd in _config.Commands.Gag)
            RegisterCommand(cmd, _muteCommands.OnGagCommand);
        foreach (var cmd in _config.Commands.Ungag)
            RegisterCommand(cmd, _muteCommands.OnUngagCommand);
        foreach (var cmd in _config.Commands.Silence)
            RegisterCommand(cmd, _muteCommands.OnSilenceCommand);
        foreach (var cmd in _config.Commands.Unsilence)
            RegisterCommand(cmd, _muteCommands.OnUnsilenceCommand);

        // Player commands
        foreach (var cmd in _config.Commands.Kick)
            RegisterCommand(cmd, _playerCommands.OnKickCommand);
        foreach (var cmd in _config.Commands.Slay)
            RegisterCommand(cmd, _playerCommands.OnSlayCommand);
        foreach (var cmd in _config.Commands.Respawn)
            RegisterCommand(cmd, _playerCommands.OnRespawnCommand);
        foreach (var cmd in _config.Commands.ChangeTeam)
            RegisterCommand(cmd, _playerCommands.OnTeamCommand);
        foreach (var cmd in _config.Commands.NoClip)
            RegisterCommand(cmd, _playerCommands.OnNoclipCommand);
        foreach (var cmd in _config.Commands.Goto)
            RegisterCommand(cmd, _playerCommands.OnGotoCommand);
        foreach (var cmd in _config.Commands.Bring)
            RegisterCommand(cmd, _playerCommands.OnBringCommand);
        foreach (var cmd in _config.Commands.Freeze)
            RegisterCommand(cmd, _playerCommands.OnFreezeCommand);
        foreach (var cmd in _config.Commands.Unfreeze)
            RegisterCommand(cmd, _playerCommands.OnUnfreezeCommand);
        foreach (var cmd in _config.Commands.ListPlayers)
            RegisterCommand(cmd, _playerCommands.OnPlayersCommand);
        foreach (var cmd in _config.Commands.Who)
            RegisterCommand(cmd, _playerCommands.OnWhoCommand);

        // Server commands
        foreach (var cmd in _config.Commands.ChangeMap)
            RegisterCommand(cmd, _serverCommands.OnMapCommand);
        foreach (var cmd in _config.Commands.ChangeWSMap)
            RegisterCommand(cmd, _serverCommands.OnWSMapCommand);
        foreach (var cmd in _config.Commands.RestartGame)
            RegisterCommand(cmd, _serverCommands.OnRestartCommand);
        foreach (var cmd in _config.Commands.Rcon)
            RegisterCommand(cmd, _serverCommands.OnRconCommand);
        foreach (var cmd in _config.Commands.Cvar)
            RegisterCommand(cmd, _serverCommands.OnCvarCommand);

        // Admin commands
        foreach (var cmd in _config.Commands.AddAdmin)
            RegisterCommand(cmd, _adminCommands.OnAddAdminCommand);
        foreach (var cmd in _config.Commands.RemoveAdmin)
            RegisterCommand(cmd, _adminCommands.OnRemoveAdminCommand);
        foreach (var cmd in _config.Commands.ListAdmins)
            RegisterCommand(cmd, _adminCommands.OnListAdminsCommand);
    }

    private void RegisterCommand(string name, ICommandService.CommandListener handler)
    {
        Core.Command.RegisterCommand(name, handler, registerRaw: true);
    }

    private void RegisterEvents()
    {
        Core.Event.OnClientSteamAuthorize += _eventHandlers.OnClientSteamAuthorize;
        Core.Event.OnClientDisconnected += _eventHandlers.OnClientDisconnected;

        Core.GameEvent.HookPost<EventRoundStart>(_eventHandlers.OnRoundStart);
    }
}