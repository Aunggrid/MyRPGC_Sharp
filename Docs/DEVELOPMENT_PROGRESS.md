# Mutant Wasteland - Development Progress & Roadmap

**Last Updated:** January 6, 2026  
**Current Version:** Alpha 0.4  
**Engine:** MonoGame (C# / .NET 9.0)

---

## 🎮 Game Overview

**Mutant Wasteland** is a tactical survival RPG set in post-apocalyptic Orodia, 400 years after "The Severance" tore reality apart. Players control mutant survivors in the Exclusion Zone, navigating faction politics, mastering mutations, and uncovering the truth about The Void.

**Core Inspirations:** Baldur's Gate 3, Rimworld, Caves of Qud

---

## ✅ Completed Systems

### Core Gameplay (100%)
- [x] Grid-based world (50x50 tiles per zone)
- [x] Camera system (WASD pan, Q/E zoom)
- [x] Click-to-move pathfinding (8-directional A*)
- [x] Turn-based combat (AP/MP system, initiative)
- [x] Line of sight and cover system
- [x] Game over / restart

### Character System (100%)
- [x] 6-Attribute system (STR, AGI, END, INT, PER, WIL)
- [x] Character creation with point allocation
- [x] Body part system (individual parts, damage, equipment slots)
- [x] Mutation system (25+ mutations, leveling, rarity-based selection)
- [x] Trait system (backstories + traits with point costs)
- [x] Status effect system with chains (Wet + Lightning = Stunned)
- [x] Dual Science paths (Tinker Science vs Dark Science)

### World & Zones (100%)
- [x] Zone system (20 story zones + 10 free zones = 30 total)
- [x] Zone transitions with exits
- [x] Fog of war and exploration tracking
- [x] Deterministic world generation (stable hash seeding)
- [x] Day/night cycle with seasons

### Combat & Enemies (100%)
- [x] 45 enemy types with unique abilities
- [x] Enemy AI (patrol, chase, attack, abilities)
- [x] Enemy faction associations
- [x] Loot drops from enemies

### Survival Systems (100%)
- [x] Hunger, Thirst, Rest, Temperature
- [x] Survival needs UI (H key)
- [x] Time progression (T to skip time)

### Items & Inventory (100%)
- [x] Inventory system (weight, slots)
- [x] Equipment system (9 slots, stat bonuses)
- [x] ~50 item definitions (weapons, armor, consumables, materials)
- [x] Item pickup from ground (G key)
- [x] Drag and drop inventory management

### Crafting & Research (100%)
- [x] Crafting system (29 recipes, workstations, quality)
- [x] Research system (41 nodes, unlocks recipes/structures)
- [x] Research categories by science path

### Building (100%)
- [x] Base building system (15+ structure types)
- [x] Construction states (Blueprint → Frame → Complete)
- [x] Resource costs for building
- [x] Functional structures (Bed=rest, Campfire=warmth, Storage=capacity)
- [x] Build menu UI (B key)

### Factions (100%)
- [x] 8 factions with reputation system
- [x] Faction standings affect NPC behavior
- [x] Reputation changes from actions

### NPCs & Trading (100%)
- [x] 15 NPC types (merchants, quest givers, service providers)
- [x] Trading system with faction-based pricing
- [x] NPC spawning with walkability checks

### Quests (100%) - *NEWLY EXPANDED*
- [x] **56 quests** (expanded from 9)
- [x] Main story arc (15 quests across 3 acts)
- [x] Faction quest lines (5 quests per faction × 6 factions)
- [x] Bounty quests (repeatable)
- [x] Side quests
- [x] Quest prerequisites (level, faction rep, science path)
- [x] Faction reputation rewards
- [x] Hidden quest discovery

### World Events (100%)
- [x] 13 event types (Rimworld-style random events)
- [x] Zone danger level affects event spawning

### Save/Load (100%)
- [x] Full save system (player, world, factions, research, fog of war)
- [x] Quick save/load (F5/F9)
- [x] Deterministic enemy/NPC spawning on load

---

## 🔧 Recent Session Changes (January 6, 2026)

### Bug Fixes
1. **NPC Spawns in Walls** - Added walkability checks for NPC spawn positions
2. **Map Changes on Restart** - Fixed by implementing stable hash (String.GetHashCode() is non-deterministic in .NET Core)
3. **Enemy Spawns in Walls** - Added walkability validation with spiral fallback
4. **Enemy Positions Non-Deterministic** - Removed DateTime.Now from seeding

### New Features
1. **Quest Expansion** - 56 quests with full story arc
2. **Faction Reputation in Quests** - Quests now grant/remove faction rep
3. **New Quest Objective Types** - KillFaction, ReachLevel, Research
4. **Hidden Quest Discovery** - Void Cult quests discovered in Dark Forest
5. **OnLevelUp Event** - Character stats now fire event for quest tracking
6. **Enemy-to-Faction Mapping** - Helper to determine enemy faction affiliation

### Files Modified
- `Gameplay/Systems/QuestSystem.cs` - Complete rewrite (2,579 lines)
- `Gameplay/Systems/FactionSystem.cs` - Added GetFactionFromEnemyType()
- `Gameplay/Character/CharacterStats.cs` - Added OnLevelUp event
- `Gameplay/World/ZoneManager.cs` - Stable hash, walkability fixes
- `Game1.cs` - Event wiring, faction rep in quest rewards

---

## 📋 Future Development Roadmap

### Phase 1: Polish & Onboarding (HIGH PRIORITY)

#### 1.1 Tutorial System
**Effort:** LOW | **Impact:** HIGH

- [ ] First-time hint overlay system
- [ ] Movement tutorial (WASD camera, click to move)
- [ ] Combat tutorial (turn-based, AP, Tab targeting)
- [ ] Inventory tutorial (I key, G pickup, equipment)
- [ ] Survival tutorial (H key, needs management)
- [ ] Building tutorial (B key, resources)
- [ ] Mutation tutorial (M key, choices)
- [ ] Contextual hints triggered by game state

#### 1.2 UI/UX Improvements
**Effort:** MEDIUM | **Impact:** HIGH

- [ ] Quest tracker on HUD (show active objectives)
- [ ] Better item tooltips
- [ ] Minimap improvements
- [ ] Notification queue system

---

### Phase 2: Content Depth (MEDIUM PRIORITY)

#### 2.1 Dialogue System
**Effort:** MEDIUM | **Impact:** MEDIUM

- [ ] NPC conversation trees
- [ ] Dialogue choices affecting outcomes
- [ ] Lore delivery through dialogue
- [ ] Faction-specific dialogue options

#### 2.2 Boss Encounters
**Effort:** MEDIUM | **Impact:** MEDIUM

- [ ] Unique boss for The Wound (Void Horror variant)
- [ ] Unique boss for The Nursery (Verdant experiment)
- [ ] Unique boss for Vault Omega (Ancient guardian)
- [ ] Unique boss for The Epicenter (Final boss)
- [ ] Boss-specific mechanics and phases

#### 2.3 Zone-Specific Content
**Effort:** MEDIUM | **Impact:** MEDIUM

- [ ] Unique encounters per zone
- [ ] Environmental hazards
- [ ] Zone-specific loot tables
- [ ] Hidden areas and secrets

---

### Phase 3: Immersion (LOWER PRIORITY)

#### 3.1 Sound & Music
**Effort:** HIGH | **Impact:** MEDIUM

- [ ] Combat sound effects
- [ ] UI sound effects
- [ ] Ambient zone sounds
- [ ] Music system (exploration, combat, menus)

#### 3.2 Multiple Endings
**Effort:** MEDIUM | **Impact:** LOW

- [ ] Faction allegiance endings
- [ ] Void embrace ending (Dark Science path)
- [ ] Technology salvation ending (Tinker path)
- [ ] Neutral survivor ending

#### 3.3 Advanced Features
**Effort:** HIGH | **Impact:** MEDIUM

- [ ] Companion system
- [ ] Reputation consequences (faction wars)
- [ ] New Game+ mode

---

## 🗺️ Story Structure

### Act 1: Survival & Discovery (Quests 1-5)
- Awakening (Tutorial)
- Welcome to Rusthollow
- The Basics of Survival
- Into the Ruins
- A Place to Call Home

### Act 2: Faction Allegiance (Quests 6-10)
- The Powers That Be (Meet factions)
- The Purifiers (Sanctum threat)
- The Ancient Ones (Gene Elders)
- The Dual Sciences (Choose path)
- The Bio-Engineers (Verdant Order)

### Act 3: The Void Threat (Quests 11-15)
- Signs of the Void
- Temple of the Consuming Void
- Beyond the Boundary
- The Wound in Reality
- The Epicenter (FINALE)

---

## 🏛️ Faction Quest Lines

| Faction | Quests | Theme |
|---------|--------|-------|
| The Changed | 5 | Mutual aid, survival, Elder's blessing |
| United Sanctum | 5 | Prove worth, tech recovery, uneasy alliance |
| Iron Syndicate | 5 | Trade routes, sabotage, vault heist |
| Verdant Order | 5 | Specimen collection, Nursery secrets |
| Void Cult | 5 | Dark rituals, transcendence (hidden) |
| Gene Elders | 5 | Ancient knowledge, truth of Severance |

---

## 🐛 Known Issues

- [ ] `_selectedResearchIndex` warning (assigned but never used)
- [ ] Equipment stats don't show tooltips yet
- [ ] Enemies can occasionally stack on same tile
- [ ] No diagonal attack (only cardinal directions)

---

## 📁 Project Structure

```
MyRPG/
├── Game1.cs                    # Main game loop (~12,000 lines)
├── GameServices.cs             # Static service locator
├── Data/
│   └── Enums.cs                # All game enumerations
├── Engine/
│   └── Camera2D.cs             # 2D camera
├── Gameplay/
│   ├── Building/               # Structure system
│   ├── Character/              # Stats, Body, Mutations, Traits
│   ├── Combat/                 # Turn-based combat
│   ├── Entities/               # Player, Enemy
│   ├── Items/                  # Items, Inventory
│   ├── Systems/                # Quest, Faction, Research, Crafting, etc.
│   └── World/                  # Tiles, WorldGrid, ZoneManager
└── Content/                    # Assets
```

---

## 🎮 Controls Reference

| Key | Action |
|-----|--------|
| **Click** | Move / Attack / Select |
| **WASD** | Camera pan |
| **Q/E** | Zoom out/in |
| **I** | Toggle inventory |
| **G** | Pick up items |
| **M** | Mutation selection |
| **B** | Toggle build menu |
| **H** | Toggle survival UI |
| **J** | Quest log |
| **R** | Research menu |
| **T** | Skip time (1 hour) |
| **Space** | End turn (combat) |
| **Tab** | Cycle targets |
| **F5** | Quick save |
| **F9** | Quick load |

---

## 📝 Session Notes

### Next Session Priority
1. **Tutorial System** - Critical for new player experience
2. **Quest Tracker HUD** - Show active objectives on screen
3. **Dialogue System** - Enable deeper NPC interactions

### Technical Debt
- Consider splitting Game1.cs into smaller files
- Add unit tests for critical systems
- Document public APIs

---

*Document maintained for development continuity across sessions.*
