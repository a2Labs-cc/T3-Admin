<div align="center">

# [SwiftlyS2] T3 Admin

[![GitHub Release](https://img.shields.io/github/v/release/a2Labs-cc/T3-Admin?color=FFFFFF&style=flat-square)](https://github.com/a2Labs-cc/T3-Admin/releases/latest)
[![GitHub Issues](https://img.shields.io/github/issues/a2Labs-cc/T3-Admin?color=FF0000&style=flat-square)](https://github.com/a2Labs-cc/T3-Admin/issues)
[![GitHub Downloads](https://img.shields.io/github/downloads/a2Labs-cc/T3-Admin/total?color=blue&style=flat-square)](https://github.com/a2Labs-cc/T3-Admin/releases)
[![GitHub Stars](https://img.shields.io/github/stars/a2Labs-cc/T3-Admin?style=social)](https://github.com/a2Labs-cc/T3-Admin/stargazers)<br/>
  <sub>Made by <a href="https://github.com/agasking1337" rel="noopener noreferrer" target="_blank">aga</a> and <a href="https://github.com/T3Marius" rel="noopener noreferrer" target="_blank">T3Marius</a></sub>
  <br/>

</div>

## Overview

**T3-Admin** is a SwiftlyS2 plugin that provides a classic admin workflow:
- Admin management via commands and in-game menus
- Punishment system (ban / mute / gag / silence) backed by a database
- Moderation utilities (kick, slay, respawn, team, bring/goto, freeze, noclip)
- Admin chat commands (asay/say/psay/csay/hsay)
- Optional Discord webhook logging for moderation actions
- Translations via `resources/translations/*.jsonc`

It includes:

- **Admin Menu** (`!admin`) with:
  - Server Management
  - Player Management
  - Fun Commands
  - Admin Management
- **Database tables auto-created on startup** for bans, mutes, gags, admins
- **Configurable command aliases and permissions** via `config.json`

## Download Shortcuts
<ul>
  <li>
    <code>📦</code>
    <strong>&nbspDownload Latest Plugin Version</strong> ⇢
    <a href="https://github.com/a2Labs-cc/T3-Admin/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>⚙️</code>
    <strong>&nbspDownload Latest SwiftlyS2 Version</strong> ⇢
    <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
</ul>

## Installation

1. Download/build the plugin.
2. Copy the published plugin folder to your server:

```
.../game/csgo/addons/swiftlys2/plugins/T3_Admin/
```

3. Ensure the plugin has its `resources/` folder alongside the DLL (translations, templates).
4. Start/restart the server.

## Configuration

The plugin uses SwiftlyS2’s JSON config system.

- **File name**: `config.json`
- **Section**: `T3Admin`

On first run the config will be created automatically. The exact resolved path is logged on startup:

```
[T3Admin] Configuration loaded from .../config.json
```

Useful config fields (non-exhaustive):

- `T3Admin.Discord.Webhook`
- `T3Admin.Messages.EnableCenterHtmlMessages`
- `T3Admin.Messages.CenterHtmlDurationMs`
- `T3Admin.Commands.*` (command aliases)
- `T3Admin.Permissions.*` (permission strings)
- `T3Admin.GameMaps.Maps`
- `T3Admin.WorkshopMaps.Maps`

## Map configs

T3-Admin does not use per-map JSON files.

- Map display names and workshop IDs are configured in `config.json` under:
  - `T3Admin.GameMaps.Maps`
  - `T3Admin.WorkshopMaps.Maps`

## Commands

### Admin / Root

| Command | Description | Permission |
| :--- | :--- | :--- |
| `!admin` | Opens the admin menu. | `admin.menu` |
| `!addadmin <steamid> <name> <flags> [immunity] [duration_days]` | Adds/updates an admin in the DB. | `admin.root` |
| `!removeadmin <steamid>` | Removes an admin from the DB. | `admin.root` |
| `!listadmins` / `!admins` | Lists stored admins. | `admin.menu` |
| `!ban <target> <minutes> [reason]` | Bans a player. | `admin.ban` |
| `!addban <steamid> <minutes> [reason]` | Adds an offline ban by SteamID. | `admin.ban` |
| `!unban <steamid> [reason]` | Removes a ban by SteamID. | `admin.unban` |
| `!mute` / `!unmute` | Mutes/unmutes a player. | `admin.mute` |
| `!gag` / `!ungag` | Gags/ungags a player. | `admin.chat` |
| `!silence` / `!unsilence` | Mutes + gags in one action. | `admin.silence` |
| `!map <mapname>` | Changes map (configured maps list). | `admin.map` |
| `!wsmap <workshop_id|name>` | Changes to workshop map (configured list). | `admin.map` |
| `!rr` / `!restart` | Restarts the game after a delay prompt. | `admin.generic` |
| `!rcon <command>` | Executes an RCON command. | `admin.rcon` |
| `!cvar <cvar> [value]` | Reads/sets a cvar. | `admin.cvar` |

### Punishment

| Command | Description |
| :--- | :--- |
| `!kick <target> [reason]` | Kicks a player. |
| `!slay <target>` | Slays one or more targets. |
| `!respawn <target>` | Respawns one or more targets. |
| `!team <target> <t/ct/spec>` | Changes player team. |
| `!goto <target>` | Teleports you near the target. |
| `!bring <target>` | Brings the target to your aim position. |
| `!freeze <target> [seconds]` | Freezes the target(s). |
| `!unfreeze <target>` | Unfreezes the target(s). |
| `!noclip <target>` | Toggles noclip for the target. |

### Player

| Command | Description |
| :--- | :--- |
| `!who <target>` | Prints info about a player (SteamID, team, admin flags/immunity, active punishments). |
| `!players` / `!list` | Prints a player list to console (supports `-json`). |

### Logging

T3-Admin supports optional Discord logging for moderation actions.

- Configure `T3Admin.Discord.Webhook` in `config.json` to enable it.
- Configure `T3Admin.Debug.Enabled` in `config.json` to enable debug logging.
- Message text can be customized via `resources/translations/en.jsonc`.

## Building

```bash
dotnet build
```

## Credits
- Readme template by [criskkky](https://github.com/criskkky)
- Release workflow based on [K4ryuu/K4-Guilds-SwiftlyS2 release workflow](https://github.com/K4ryuu/K4-Guilds-SwiftlyS2/blob/main/.github/workflows/release.yml)
- Authors:
  - [T3Marius](https://github.com/T3Marius)
  - [aga](https://github.com/agasking1337)