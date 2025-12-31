namespace Furien_Admin.Config;

public class PluginConfig
{
    public DiscordConfig Discord { get; set; } = new();
    public MessagesConfig Messages { get; set; } = new();
    public DebugConfig Debug { get; set; } = new();
    public CommandsConfig Commands { get; set; } = new();
    public PermissionsConfig Permissions { get; set; } = new();
    public GameMapsConfig GameMaps { get; set; } = new();
    public WorkshopMapsConfig WorkshopMaps { get; set; } = new();
    public SanctionMenuConfig Sanctions { get; set; } = new();
    public string AdminMenuColor { get; set; } = "#00FEED";
}

public class DebugConfig
{
    public bool Enabled { get; set; } = false;
}

public class MessagesConfig
{
    public bool EnableCenterHtmlMessages { get; set; } = true;
    public int CenterHtmlDurationMs { get; set; } = 5000;
}

public class DiscordConfig
{
    public string Webhook { get; set; } = "";
}

public class CommandsConfig
{
    public List<string> AdminMenu { get; set; } = ["admin"];
    public List<string> Asay { get; set; } = ["asay"];
    public List<string> Say { get; set; } = ["say"];
    public List<string> Psay { get; set; } = ["psay"];
    public List<string> Csay { get; set; } = ["csay"];
    public List<string> Hsay { get; set; } = ["hsay"];
    public List<string> AddAdmin { get; set; } = ["addadmin"];
    public List<string> RemoveAdmin { get; set; } = ["removeadmin"];
    public List<string> ListAdmins { get; set; } = ["listadmins", "admins"];
    public List<string> Ban { get; set; } = ["ban"];
    public List<string> AddBan { get; set; } = ["addban"];
    public List<string> Unban { get; set; } = ["unban"];
    public List<string> Kick { get; set; } = ["kick"];
    public List<string> Gag { get; set; } = ["gag"];
    public List<string> Ungag { get; set; } = ["ungag"];
    public List<string> Mute { get; set; } = ["mute"];
    public List<string> Unmute { get; set; } = ["unmute"];
    public List<string> Silence { get; set; } = ["silence"];
    public List<string> Unsilence { get; set; } = ["unsilence"];
    public List<string> Slay { get; set; } = ["slay"];
    public List<string> Respawn { get; set; } = ["respawn", "1up"];
    public List<string> ChangeTeam { get; set; } = ["team"];
    public List<string> NoClip { get; set; } = ["noclip"];
    public List<string> Goto { get; set; } = ["goto"];
    public List<string> Bring { get; set; } = ["bring"];
    public List<string> Freeze { get; set; } = ["freeze"];
    public List<string> Unfreeze { get; set; } = ["unfreeze"];
    public List<string> ChangeMap { get; set; } = ["map"];
    public List<string> ChangeWSMap { get; set; } = ["wsmap"];
    public List<string> RestartGame { get; set; } = ["rr", "restart"];
    public List<string> Rcon { get; set; } = ["rcon"];
    public List<string> Cvar { get; set; } = ["cvar"];
    public List<string> ListPlayers { get; set; } = ["players", "list"];
    public List<string> Who { get; set; } = ["who"];
}

public class PermissionsConfig
{
    public string AdminMenu { get; set; } = "admin.menu";
    public string Asay { get; set; } = "admin.chat";
    public string Say { get; set; } = "admin.chat";
    public string Psay { get; set; } = "admin.chat";
    public string Csay { get; set; } = "admin.chat";
    public string Hsay { get; set; } = "admin.chat";
    public string AddAdmin { get; set; } = "admin.root";
    public string RemoveAdmin { get; set; } = "admin.root";
    public string ListAdmins { get; set; } = "admin.menu";
    public string Ban { get; set; } = "admin.ban";
    public string AddBan { get; set; } = "admin.ban";
    public string Unban { get; set; } = "admin.unban";
    public string Kick { get; set; } = "admin.kick";
    public string Gag { get; set; } = "admin.chat";
    public string Ungag { get; set; } = "admin.chat";
    public string Mute { get; set; } = "admin.mute";
    public string Unmute { get; set; } = "admin.mute";
    public string Silence { get; set; } = "admin.silence";
    public string Unsilence { get; set; } = "admin.silence";
    public string Slay { get; set; } = "admin.slay";
    public string Respawn { get; set; } = "admin.respawn";
    public string ChangeTeam { get; set; } = "admin.team";
    public string NoClip { get; set; } = "admin.cheats";
    public string Goto { get; set; } = "admin.goto";
    public string Bring { get; set; } = "admin.bring";
    public string Freeze { get; set; } = "admin.freeze";
    public string Unfreeze { get; set; } = "admin.freeze";
    public string ChangeMap { get; set; } = "admin.map";
    public string ChangeWSMap { get; set; } = "admin.map";
    public string RestartGame { get; set; } = "admin.generic";
    public string Rcon { get; set; } = "admin.rcon";
    public string Cvar { get; set; } = "admin.cvar";
    public string ListPlayers { get; set; } = "admin.generic";
    public string Who { get; set; } = "admin.generic";
}

public class GameMapsConfig
{
    public Dictionary<string, string> Maps { get; set; } = new()
    {
        { "de_dust2", "Dust 2" },
        { "de_mirage", "Mirage" },
        { "de_inferno", "Inferno" },
        { "de_ancient", "Ancient" },
        { "de_anubis", "Anubis" },
        { "de_train", "Train" },
        { "de_nuke", "Nuke" },
        { "de_overpass", "Overpass" },
        { "de_vertigo", "Vertigo" }
    };
}

public class WorkshopMapsConfig
{
    public Dictionary<string, uint> Maps { get; set; } = new()
    {
        { "Inferno Online", 3549919360 }
    };
}

public class SanctionDurationConfigItem
{
    public string Name { get; set; } = "";
    public int Minutes { get; set; }
}

public class SanctionMenuConfig
{
    public List<string> BanReasons { get; set; } =
    [
        "Hacking",
        "Obscene language",
        "Insult players",
        "Admin disrespect",
        "Other"
    ];

    public List<string> KickReasons { get; set; } =
    [
        "Obscene language",
        "Insult players",
        "AFK",
        "Admin disrespect",
        "Other"
    ];

    public List<string> MuteReasons { get; set; } =
    [
        "Obscene language",
        "Insult players",
        "Spamming",
        "Admin disrespect",
        "Other"
    ];

    public List<string> GagReasons { get; set; } =
    [
        "Obscene language",
        "Insult players",
        "Spamming",
        "Admin disrespect",
        "Other"
    ];

    public List<string> SilenceReasons { get; set; } =
    [
        "Obscene language",
        "Insult players",
        "Spamming",
        "Admin disrespect",
        "Other"
    ];

    public List<SanctionDurationConfigItem> Durations { get; set; } = new()
    {
        new() { Name = "5 minutes", Minutes = 5 },
        new() { Name = "30 minutes", Minutes = 30 },
        new() { Name = "1 hour", Minutes = 60 },
        new() { Name = "2 hours", Minutes = 120 },
        new() { Name = "1 day", Minutes = 60 * 24 },
        new() { Name = "1 week", Minutes = 60 * 24 * 7 },
        new() { Name = "Permanent", Minutes = 0 }
    };
}
