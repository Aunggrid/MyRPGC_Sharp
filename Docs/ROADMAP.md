# Development Roadmap

## Current State
You have:
- ✅ Grid-based world with tile types
- ✅ Camera and basic rendering
- ✅ Click-to-move pathfinding
- ✅ Basic status effect framework (Wet/Stunned)

## Architecture Created (This Session)
- ✅ Comprehensive Enums (Data/Enums.cs)
- ✅ Body Part System (Gameplay/Character/BodyPart.cs, Body.cs)
- ✅ Mutation System (Gameplay/Character/Mutation.cs)
- ✅ Trait System (Gameplay/Character/Trait.cs)
- ✅ Status Effect System with chains (Gameplay/Systems/StatusEffectSystem.cs)
- ✅ Unified Character Stats (Gameplay/Character/CharacterStats.cs)
- ✅ Game Design Document (Docs/GAME_DESIGN.md)

---

## Phase 1: Playable Core Loop (Next 2-4 weeks)

### Priority 1: Integrate New Systems
Replace your current PlayerEntity with CharacterStats integration:

```csharp
// In PlayerEntity.cs, add:
public CharacterStats Stats { get; private set; }

// Initialize in constructor or Initialize method
Stats = new CharacterStats(mutationSystem, traitSystem, statusSystem);
```

**Tasks:**
1. [ ] Create singleton/service for MutationSystem, TraitSystem, StatusEffectSystem
2. [ ] Update PlayerEntity to use CharacterStats for speed, etc.
3. [ ] Update status effect handling to use new StatusEffectSystem
4. [ ] Test body damage affecting movement

### Priority 2: Basic Combat
**Files to create:**
- `Gameplay/Combat/CombatManager.cs` - Manages turn flow
- `Gameplay/Combat/TurnManager.cs` - Initiative and turn order
- `Gameplay/Entities/EnemyEntity.cs` - Basic enemy with AI

**Combat MVP Features:**
1. [ ] Detect when player is near enemy → Trigger combat mode
2. [ ] Turn-based initiative order
3. [ ] Move action (1 AP per tile)
4. [ ] Attack action (2 AP, basic melee)
5. [ ] End turn
6. [ ] Combat ends when one side is dead/fled

### Priority 3: Simple Enemy AI
Start with basic behavior:
```
If (can see player) {
    If (in melee range) Attack()
    Else MoveToward(player)
} Else {
    Patrol or Idle
}
```

### Priority 4: Health & Death
1. [ ] Show health bars above entities
2. [ ] Death state (remove from world or show corpse)
3. [ ] Player death → Game over screen

---

## Phase 2: Character Depth (Weeks 4-8)

### Mutation UI
1. [ ] Level up notification
2. [ ] Mutation choice screen (show 3 random options)
3. [ ] Mutation effects visible on character sprite (optional but cool)

### Character Creation Screen
1. [ ] Show backstory + traits
2. [ ] "Reroll" button
3. [ ] Show mutation points
4. [ ] Choose Science Path (Tinker/Dark)
5. [ ] "Start Game" button

### Basic Survival
**File to create:** `Gameplay/Systems/SurvivalSystem.cs`

1. [ ] Hunger meter (0-100)
2. [ ] Hunger drains over time
3. [ ] Low hunger = debuffs
4. [ ] Food items restore hunger
5. [ ] (Later: Thirst, Rest)

### Basic Inventory
**File to create:** `Gameplay/Items/Inventory.cs`

1. [ ] Item base class
2. [ ] Inventory container (list of items)
3. [ ] Pick up items
4. [ ] Use consumables
5. [ ] (Later: Equipment slots)

---

## Phase 3: World Expansion (Weeks 8-12)

### Multiple Zones
**Files to create:**
- `Gameplay/World/WorldMap.cs` - Manages multiple zones
- `Gameplay/World/Zone.cs` - Individual zone data

1. [ ] Save/load current zone
2. [ ] Exit map edge → Transition to adjacent zone
3. [ ] Generate new zones procedurally
4. [ ] Fixed zones for objectives/trading posts

### Base Building
**Files to create:**
- `Gameplay/Building/Structure.cs`
- `Gameplay/Building/BuildingSystem.cs`

1. [ ] Place walls (blueprint → build)
2. [ ] Doors
3. [ ] Storage containers
4. [ ] Workbench (crafting station)
5. [ ] Bed (rest)

### Save System
1. [ ] Serialize character (Stats, Body, Mutations, Traits)
2. [ ] Serialize world state
3. [ ] Serialize base
4. [ ] Load game

---

## Phase 4: Content & Polish (Weeks 12+)

### More Enemies
- Different enemy types (melee, ranged, special)
- Enemy mutations/abilities
- Boss enemies at objectives

### Science Trees
- Tinker research tree
- Dark Science research tree
- Crafting recipes

### Trading & NPCs
- Trader AI
- Dialogue system
- Reputation/faction system

### More Mutations & Traits
- Fill out the remaining mutations
- Quest-reward traits
- Negative mutation possibilities

---

## File Structure (Target)

```
MyRPG/
├── Data/
│   └── Enums.cs                    ✅ DONE
├── Docs/
│   └── GAME_DESIGN.md              ✅ DONE
├── Engine/
│   └── Camera2D.cs                 ✅ EXISTS
├── Gameplay/
│   ├── Building/
│   │   ├── Structure.cs            ⬜ PHASE 3
│   │   └── BuildingSystem.cs       ⬜ PHASE 3
│   ├── Character/
│   │   ├── Body.cs                 ✅ DONE
│   │   ├── BodyPart.cs             ✅ DONE
│   │   ├── CharacterStats.cs       ✅ DONE
│   │   ├── Mutation.cs             ✅ DONE
│   │   └── Trait.cs                ✅ DONE
│   ├── Combat/
│   │   ├── CombatManager.cs        ⬜ PHASE 1
│   │   └── TurnManager.cs          ⬜ PHASE 1
│   ├── Entities/
│   │   ├── PlayerEntity.cs         ✅ EXISTS (needs update)
│   │   └── EnemyEntity.cs          ⬜ PHASE 1
│   ├── Items/
│   │   ├── Item.cs                 ⬜ PHASE 2
│   │   └── Inventory.cs            ⬜ PHASE 2
│   ├── Systems/
│   │   ├── Pathfinding.cs          ✅ EXISTS
│   │   ├── StatusEffectSystem.cs   ✅ DONE
│   │   ├── SurvivalSystem.cs       ⬜ PHASE 2
│   │   └── TimeSystem.cs           ⬜ PHASE 2
│   └── World/
│       ├── Tile.cs                 ✅ EXISTS
│       ├── WorldGrid.cs            ✅ EXISTS
│       ├── WorldMap.cs             ⬜ PHASE 3
│       └── Zone.cs                 ⬜ PHASE 3
├── UI/
│   ├── HUD.cs                      ⬜ PHASE 1
│   ├── CharacterSheet.cs           ⬜ PHASE 2
│   └── MutationChoiceUI.cs         ⬜ PHASE 2
├── Game1.cs                        ✅ EXISTS (needs update)
└── Program.cs                      ✅ EXISTS
```

---

## Immediate Next Steps (This Week)

### 1. Copy New Files Into Your Project
Copy these files from this session into your Visual Studio project:
- Data/Enums.cs
- Gameplay/Character/BodyPart.cs
- Gameplay/Character/Body.cs
- Gameplay/Character/Mutation.cs
- Gameplay/Character/Trait.cs
- Gameplay/Character/CharacterStats.cs
- Gameplay/Systems/StatusEffectSystem.cs
- Docs/GAME_DESIGN.md

### 2. Create Service Container
Create a simple way to access systems:

```csharp
// GameServices.cs
public static class GameServices
{
    public static MutationSystem Mutations { get; private set; }
    public static TraitSystem Traits { get; private set; }
    public static StatusEffectSystem StatusEffects { get; private set; }
    
    public static void Initialize()
    {
        Mutations = new MutationSystem();
        Traits = new TraitSystem();
        StatusEffects = new StatusEffectSystem();
    }
}
```

### 3. Update PlayerEntity
Replace basic status handling with the new system:

```csharp
// In PlayerEntity.cs
public CharacterStats Stats { get; private set; }

public void Initialize()
{
    Stats = new CharacterStats(
        GameServices.Mutations,
        GameServices.Traits,
        GameServices.StatusEffects
    );
    
    // Create a test build
    var build = GameServices.Traits.GenerateRandomBuild();
    Stats.ApplyCharacterBuild(build, SciencePath.Tinker);
}

// In Update, use Stats.Speed instead of fixed Speed
float currentSpeed = Stats.Speed;
```

### 4. Test the Systems
Add debug output to verify everything works:

```csharp
// In Game1.cs Initialize or Update
if (keyboard.IsKeyDown(Keys.F1))
{
    System.Diagnostics.Debug.WriteLine(_player.Stats.GetStatusReport());
}

if (keyboard.IsKeyDown(Keys.F2))
{
    System.Diagnostics.Debug.WriteLine(_player.Stats.Body.GetStatusReport());
}

if (keyboard.IsKeyDown(Keys.F3))
{
    // Test mutation
    _player.Stats.AddXP(100);
}
```

---

## Questions for Next Session

When you're ready to continue, let me know:
1. Did the files integrate without errors?
2. What would you like to work on first - Combat, UI, or something else?
3. Any systems that need clarification or modification?

Good luck, and have fun building your mutant wasteland survival game! 🧬
