<p align="center">
  <img src="https://i.imgur.com/ustEuqj.png" alt="ValheimMMOMOD Logo" width="680px" />
</p>

# ValheimMMOMOD

A comprehensive MMO-style mod for **Valheim** that adds a guild system, virtual economy, daily quests, shared map exploration, and a friends system — all accessible through a custom Nordic-themed GUI opened with **F10**.

![ValheimMMOMOD](https://img.shields.io/badge/Valheim-MMOMOD-gold?style=for-the-badge)
![BepInEx](https://img.shields.io/badge/BepInEx-5.x-blue?style=for-the-badge)
![Version](https://img.shields.io/badge/version-1.0.0-green?style=for-the-badge)
![License](https://img.shields.io/badge/license-MIT-lightgrey?style=for-the-badge)

---

## 📋 Table of Contents

- [Features](#-features)
- [Installation](#-installation)
- [Controls](#-controls)
- [GUI Panels](#-gui-panels)
- [Chat Commands](#-chat-commands)
- [Configuration](#-configuration)
- [Localization](#-localization)
- [Architecture](#-architecture)
- [Building from Source](#-building-from-source)
- [Known Issues / TODO](#-known-issues--todo)
- [License](#-license)

---

## ✨ Features

### ⚔️ Guild System
- **Create** your own guild with a custom name
- **Invite** other players to join your guild
- **Guild TAG** displayed above member names (max 5 characters)
- **Member list** with online/offline status tracking
- **Guild roles** (Leader, Officer, Member) — *planned*
- **Map sharing** between guild members via ZRoutedRpc
- Configurable **max members** per guild (default: 20)

### 💰 Virtual Economy
- Earn **gold coins** by killing enemies (scaled by difficulty level)
- Earn **gold** when skills level up
- **Transfer gold** to other players (`/dai`)
- **Withdraw** gold from your balance (`/ritira`)
- **Transaction history** with color-coded entries (+green / -red)
- Fully configurable rewards and multipliers

### 📜 Daily Quests
- Auto-generated **daily quests** that reset every day
- Four quest types: **Kill**, **Gather**, **Explore**, **Level Up**
- Progress tracked automatically via Harmony patches
- **Gold reward** on completion (configurable, default: 100)
- Visual progress bars in the GUI

### 🗺️ Shared Map
- Share explored map tiles with your guild
- **Auto-share** mode with configurable interval
- Waypoint system (planned)
- Map legend with player/base/guild/boss markers

### 👥 Friends System *(UI Ready)*
- Friend list with online/in-game/offline status
- Friend requests (planned)
- Recent players (planned)
- Blocked players (planned)
- Search functionality

### 🎮 Custom GUI
- **Radial wheel menu** opened with F10
- **Nordic-themed** dark wood + gold UI matching Valheim's aesthetic
- **Sidebar navigation** for each panel with sub-tabs
- Pauses the game and opens inventory TAB when activated
- Closes all other Valheim menus (ESC, Chat, etc.) when opened

---

## 📦 Installation

### Requirements
- [Valheim](https://store.steampowered.com/app/892970/Valheim/) (latest version)
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) (64-bit)

### Steps
1. Install BepInEx 5.x into your Valheim directory
2. Download `ValheimMMOMOD.dll` from the [Releases](../../releases) page
3. Place `ValheimMMOMOD.dll` into:
   ```
   <Valheim>/BepInEx/plugins/
   ```
4. Launch Valheim — the mod loads automatically
5. Press **F10** in-game to open the MMOMOD menu

### File Structure
```
Valheim/
└── BepInEx/
    ├── plugins/
    │   └── ValheimMMOMOD.dll
    └── config/
        └── MMOMOD/
            └── mmomod_<PlayerName>.json   ← auto-created save data
```

---

## 🎮 Controls

| Key | Action |
|-----|--------|
| **F10** | Toggle MMOMOD menu (opens/closes) |
| **ESC** | Close MMOMOD menu (when open) |
| **Click** | Select panel from radial wheel or interact with UI |

When the menu is open:
- The game is **paused** (`Time.timeScale = 0`)
- The **cursor is unlocked** and visible
- Player input is **disabled**
- The **inventory TAB** is opened automatically
- Other Valheim menus (ESC menu, Chat) are closed

---

## 🖥️ GUI Panels

### Radial Wheel (Home)
A circular selection wheel with 5 sectors:
- **GILDA** (top) — Guild management
- **ECONOMIA** (right) — Economy & transactions
- **QUEST** (bottom-right) — Daily quests
- **AMICI** (bottom-left) — Friends list
- **MAPPA** (left) — Shared map

### Guild Panel
| Sub-tab | Description |
|---------|-------------|
| **Panoramica** | Guild crest, name, tag, member count, level, XP bar, recent members with status |
| **Membri** | Full member list with online indicators |
| **Invita** | Invite players by name, create new guild |
| **Impostazioni** | Set guild TAG (max 5 chars), leave guild |
| **Ruoli** | Guild roles hierarchy (mockup) |
| **Condividi Mappa** | Share explored map tiles with guild |

### Economy Panel
| Sub-tab | Description |
|---------|-------------|
| **Saldo** | Current balance, quick transfer, recent transactions |
| **Deposita** | Deposit gold |
| **Ritira** | Withdraw gold |
| **Trasferisci** | Send gold to another player |
| **Storico** | Full transaction history |

### Quest Panel
| Sub-tab | Description |
|---------|-------------|
| **Panoramica / Giornaliere** | Active daily quest with progress bar, mockup quests, rewards |
| **Settimanali** | Weekly quests with progress bars (mockup) |
| **Storia** | Quest completion history (mockup) |
| **Completate** | Completed quests archive (mockup) |

### Friends Panel
| Sub-tab | Description |
|---------|-------------|
| **Lista Amici** | Friend list with search, avatars, status, invite buttons |
| **Richieste** | Pending friend requests (mockup) |
| **Giocatori Recenti** | Recently encountered players (mockup) |
| **Bloccati** | Blocked players list (mockup) |

### Map Panel
| Sub-tab | Description |
|---------|-------------|
| **Mappa** | World map placeholder with waypoints and legend |
| **Waypoint** | Saved waypoint list with coordinates |
| **Condividi** | Manual map share button, tile count |
| **Impostazioni** | Auto-share toggle, interval setting |

---

## 💬 Chat Commands

All commands are typed in the Valheim chat (press Enter).

### Guild Commands
| Command | Description |
|---------|-------------|
| `/creagilda <name>` | Create a new guild |
| `/invitagilda <player>` | Invite a player to your guild |
| `/condividimappa` | Share explored map tiles with guild |
| `/gildatag <tag>` | Set your guild TAG (max 5 characters) |
| `/escigilda` | Leave your current guild *(TODO)* |

### Economy Commands
| Command | Description |
|---------|-------------|
| `/saldo` | Check your gold balance |
| `/dai <player> <amount>` | Give gold to another player |
| `/ritira <amount>` | Withdraw gold from your balance |

### Quest Commands
| Command | Description |
|---------|-------------|
| `/quest` | Show current daily quest status |
| `/completaquest` | Claim reward for completed daily quest |

---

## ⚙️ Configuration

All settings are in the BepInEx config file, auto-generated at:
```
BepInEx/config/antonio.valheim.mmomod.cfg
```

### General
| Setting | Default | Description |
|---------|---------|-------------|
| `GuildIconColor` | `#FFD700` | Guild icon color on minimap (hex RGB) |
| `MapShareRadius` | `100` | Map sharing radius between guild members |
| `MaxGuildMembers` | `20` | Maximum members per guild |
| `GuildTag` | `""` | Guild TAG shown above player names |
| `ShowGuildTag` | `true` | Display guild TAG above member names |
| `Language` | `it` | UI language (`it`, `en`, `es`) |
| `AutoMapShare` | `false` | Auto-share map with guild periodically |
| `AutoMapShareInterval` | `30` | Seconds between auto map shares |

### Economy
| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Enable/disable the economy system |
| `GoldBaseKill` | `10` | Base gold per enemy kill |
| `GoldDifficultyMultiplier` | `1.5` | Gold multiplier per enemy difficulty level |
| `GoldLevelUp` | `25` | Gold earned per skill level-up |

### Daily Quest
| Setting | Default | Description |
|---------|---------|-------------|
| `Enabled` | `true` | Enable/disable daily quests |
| `GoldReward` | `100` | Gold reward for completing a daily quest |

---

## 🌍 Localization

The mod supports three languages, configurable via `Language` in the config file:

| Code | Language |
|------|----------|
| `it` | Italiano (default) |
| `en` | English |
| `es` | Español |

All chat messages, feedback text, and quest descriptions are localized. The GUI labels are currently in Italian with plans for full localization.

---

## 🏗️ Architecture

### Tech Stack
- **C# / .NET** — compiled as a BepInEx plugin
- **HarmonyLib** — runtime patching of Valheim assemblies
- **ZRoutedRpc** — multiplayer synchronization via Valheim's built-in RPC system
- **Unity IMGUI** — custom Nordic-themed GUI rendered via `OnGUI()`

### Harmony Patches
| Patch | Target | Purpose |
|-------|--------|---------|
| `ChatInputText_Patch` | `Chat.InputText()` | Intercepts slash commands before chat processing |
| `CharacterOnDeath_Patch` | `Character.OnDeath()` | Awards gold on enemy kills, tracks quest progress |
| `SkillsRaiseSkill_Patch` | `Skills.RaiseSkill()` | Awards gold on skill level-ups, tracks quest progress |
| `PlayerGetHoverText_Patch` | `Player.GetHoverText()` | Prepends guild TAG to member hover names |

### RPC Channels
| RPC Name | Purpose |
|----------|---------|
| `MMOMOD_CreateGuild` | Broadcast guild creation to server |
| `MMOMOD_InviteMember` | Broadcast member invitation |
| `MMOMOD_RemoveMember` | Broadcast member removal |
| `MMOMOD_ShareMapTile` | Share individual explored map tiles |
| `MMOMOD_SendGuildData` | Send full guild data to requesting peer |
| `MMOMOD_RequestGuildData` | Request guild data from other peers |

### Data Persistence
Player data is saved to JSON files at:
```
BepInEx/config/MMOMOD/mmomod_<PlayerName>.json
```
Saved on application quit and OnDestroy. Includes guild state, gold balance, quest progress, and member list.

---

## 🔨 Building from Source

### Prerequisites
- [.NET SDK 6.0+](https://dotnet.microsoft.com/download)
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx) assemblies
- Valheim installation with `assembly_valheim.dll`

### Build
```bash
cd BepInEx/plugins
dotnet build ValheimGuildSystem.csproj -c Release
```

Output: `ValheimMMOMOD.dll` in the plugins directory.

### Project File
The `.csproj` references:
- `BepInEx/core/BepInEx.dll`
- `BepInEx/core/0Harmony.dll`
- `Valheim_Data/Managed/assembly_valheim.dll`
- `Valheim_Data/Managed/UnityEngine.*.dll`

---

## 📝 Known Issues / TODO

- [ ] `/escigilda` — requires `RPC_RemoveMember` server-side implementation
- [ ] Friends system — UI ready, backend RPC not yet implemented
- [ ] Guild roles — UI mockup only, no permission system yet
- [ ] Weekly quests — UI mockup only, no generation logic yet
- [ ] Transaction history — currently shows mockup data
- [ ] Map waypoints — UI mockup only, no save/load yet
- [ ] Guild level/XP — hardcoded display values
- [ ] Full GUI localization — labels currently in Italian

---

## 📄 License

This project is licensed under the **MIT License**. See [LICENSE](LICENSE) for details.

---

## 🙏 Credits

- **Valheim** by [Iron Gate AB](https://irongatestudio.se/)
- **BepInEx** by the BepInEx team
- **HarmonyLib** by [Andreas Pardeike](https://github.com/pardeike/Harmony)

---

> **Note:** This mod is in active development. Some features shown in the GUI are mockups planned for future releases. Multiplayer synchronization works via ZRoutedRpc but requires all players to have the mod installed.
