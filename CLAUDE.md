# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Mutant Wasteland** is a tactical survival RPG built with MonoGame (C#/.NET 9.0). Players control mutant survivors in a post-apocalyptic wasteland, featuring turn-based combat, mutation-based character progression, crafting, base building, and faction systems. Inspired by Baldur's Gate 3, Rimworld, and Caves of Qud.

## Build & Run Commands

```bash
# Build the project
dotnet build

# Run the game
dotnet run

# Clean build artifacts
dotnet clean
```

The game uses MonoGame Framework DesktopGL 3.8. Target framework is .NET 9.0.

## High-Level Architecture

### Service Locator Pattern

The codebase uses a static service locator (`GameServices.cs`) that must be initialized at startup:

```csharp
GameServices.Initialize();  // Called once at game start
```

All major systems are accessed via `GameServices`:
- `GameServices.Mutations` - Mutation system
- `GameServices.Traits` - Character traits/backstories
- `GameServices.StatusEffects` - Status effect definitions
- `GameServices.Building` - Base building structures
- `GameServices.SurvivalSystem` - Hunger/thirst/rest
- `GameServices.Quests` - Quest system (56 quests)
- `GameServices.Research` - Tech tree system
- `GameServices.Crafting` - Crafting recipes
- `GameServices.Factions` - 8-faction reputation system
- `GameServices.FogOfWar` - Exploration tracking
- `GameServices.Tutorial` - Tutorial hint system

**Critical:** Systems must be initialized before use. Call `GameServices.Initialize()` in `Game1.Initialize()`.

### Game State Machine

The game operates in distinct states (`GameState` enum in `Game1.cs`):
- `CharacterCreation` - Science path selection (Tinker vs Dark Science)
- `AttributeSelect` - Attribute point allocation on level-up
- `Playing` - Normal gameplay (exploration or combat)
- `MutationSelect` - Choosing mutations on level-up
- `VitalOrganChoice` - Relocating vital organ damage
- `GameOver` - Player death
- `Paused` - Game paused (future use)

### World Structure

The game world consists of zones (`ZoneManager.cs`):
- **30 total zones** - 20 story zones + 10 free exploration zones
- **Deterministic generation** - Uses stable hashing (custom GetStableHashCode) to ensure same zone layout on reload
- **50x50 tile grid per zone** - Each zone is a `WorldGrid` with 64px tiles
- **Zone transitions** - Move to adjacent zones via exit tiles

**Important:** Do NOT use `String.GetHashCode()` for world generation as it's non-deterministic in .NET Core. Use `ZoneManager.GetStableHashCode()` instead.

### Entity System

Three main entity types:
- `PlayerEntity` - Player character with `CharacterStats`, body parts, mutations, traits
- `EnemyEntity` - 45 enemy types with AI (patrol, chase, attack)
- `NPCEntity` - 15 NPC types (merchants, quest givers, services)

**Entity spawning must check tile walkability:**
```csharp
// Always validate spawn positions
if (world.GetTile(x, y).IsWalkable())
{
    // Spawn here
}
```

### Character Stats Architecture

`CharacterStats` is the core of character progression:
- **6 Attributes** - STR, AGI, END, INT, PER, WIL
- **Body System** - Individual body parts with health, injuries, equipment slots
- **Mutations** - 25+ mutations with levels (acquired via leveling)
- **Traits** - Permanent modifiers from backstory
- **Status Effects** - Dynamic effects with chains (Wet + Lightning → Stunned)

**Character builds** are created via `CharacterBuild` and applied with a `SciencePath` (Tinker or Dark):
```csharp
var build = GameServices.Traits.GenerateRandomBuild();
player.Initialize(build, SciencePath.Tinker);
```

### Combat System

Turn-based combat (`CombatManager.cs`):
- **Action Points (AP)** - Default 3 per turn
- **Movement Points (MP)** - Separate from AP
- **Initiative order** - Based on Speed stat
- **Line of sight** - `WorldGrid.HasLineOfSight()` checks terrain AND structures

Combat enters turn-based mode when enemies are within aggro range.

### Save System

Full persistence via `SaveSystem.cs`:
- **Quick save/load** - F5/F9 keys
- **JSON serialization** - Version 3 format (includes Tutorial progress)
- **Saves include:** Player, World state, Enemies, NPCs, Items, Factions, Research, Fog of War, Quests, Tutorial

**Important:** When adding new systems, update `GameSaveData` structure and increment version number.

## Key Code Patterns

### Adding New Systems

1. Create system class in `Gameplay/Systems/`
2. Add property to `GameServices.cs`
3. Initialize in `GameServices.Initialize()`
4. Add save/load support in `SaveSystem.cs`
5. Update `GameSaveData` version if needed

### Working with Mutations

Mutations are defined in `MutationSystem.cs` and have:
- **Rarity** - Common, Uncommon, Rare, Legendary
- **Max level** - Some mutations level multiple times
- **Cost** - Mutation points required
- **Effects** - Stat modifiers, new body parts, abilities

Use `GameServices.Mutations.GetMutation(type)` to access definitions.

### Quest System

56 quests across 3 acts + faction quest lines:
- **Prerequisites** - Level, faction rep, science path requirements
- **Objectives** - Kill enemies, collect items, explore zones, research nodes, reach level
- **Rewards** - XP, gold, Esper points, faction reputation, items
- **Hidden quests** - Discovered by entering specific zones

Quest progression fires events that systems can subscribe to.

### Faction Reputation

8 factions with reputation levels (-100 to +100):
- **Enemy faction mapping** - Use `FactionSystem.GetFactionFromEnemyType()` to determine which faction an enemy belongs to
- **Reputation affects** - Trading prices, NPC behavior, quest availability
- **Quest rewards** - Quests grant/reduce faction reputation

## Data Organization

All enums are centralized in `Data/Enums.cs`:
- `AttributeType` - Character attributes
- `BodyPartType` - Body part definitions
- `StatusEffectType` - Status effects
- `MutationType` - Mutation types
- `TraitType` - Character traits
- `FactionType` - Game factions
- `TileType` - Terrain types
- `ItemType` - Item categories
- `EnemyType` - Enemy variants
- And many more...

## Current Known Issues

- `Game1.cs` is extremely large (~12,000 lines) - consider splitting when making major changes
- Equipment stat tooltips not yet implemented
- Enemies can occasionally stack on same tile
- Diagonal attacks not supported (only cardinal directions)
- `_selectedResearchIndex` warning in codebase (assigned but never used)

## Development Guidelines

### Deterministic Systems

World generation and entity spawning MUST be deterministic:
- Use stable hash functions, not `String.GetHashCode()`
- Don't use `DateTime.Now` in seeding
- Validate walkability for all spawns with fallback logic

### UI Theme

UI uses consistent color scheme defined in `Game1.UITheme`:
- Panel backgrounds: `new Color(20, 25, 35)`
- Text primary: `new Color(220, 225, 230)`
- Selections: `new Color(60, 80, 120)`

Follow existing patterns for consistency.

### Science Paths

Game has dual progression paths:
- **Tinker Science** - Technology, implants, firearms, electronics
- **Dark Science** - Void powers, rituals, transmutation, monster parts

Many systems (crafting, research, mutations) branch based on chosen path.

## Testing the Game

Key things to verify after changes:
1. Save/Load still works (F5/F9)
2. World generation is deterministic (restart game, check same layout)
3. Entity spawns don't place in walls
4. Combat turn order works correctly
5. Quest progression triggers properly
6. Faction reputation persists across saves

## Entry Point

Execution flow:
1. `Program.cs` - Creates `Game1` instance and runs
2. `Game1.Initialize()` - Calls `GameServices.Initialize()`
3. `Game1.LoadContent()` - Loads assets
4. Game loop begins

The main game logic is in `Game1.cs` which handles all rendering, input, and game state management.
