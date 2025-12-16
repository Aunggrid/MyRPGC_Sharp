# Mutant Wasteland - Game Design Document

## Overview
**Genre:** Tactical Survival RPG  
**Inspirations:** Rimworld, Baldur's Gate 3, Caves of Qud  
**Perspective:** Top-down 2D grid-based  
**Players:** Single player (co-op planned for future)

---

## Lore Summary
200 years ago, a mysterious power destroyed one faction completely, leaving behind a zone of dark energy. While the rest of the world continued to thrive with technology and civilization, the survivors of this catastrophe began mutating over generations. You play as one of these mutants, surviving in the wasteland while the "normal" world watches with fear and fascination.

---

## Character Creation

### Starting Points
- **Base mutation points:** 4
- **Traits/Backstory** modify points:
  - Bad traits: +1 or +2 points
  - Good traits: -1 or -2 points
  - **Range:** 2-6 points maximum

### Science Path (Choose One - Permanent)
| Path | Description | Crafting Style |
|------|-------------|----------------|
| **Tinker Science** | Conventional technology | Implants, guns, electronics, machines |
| **Dark Science** | Anomaly/ritual based | Monster parts, rituals, transmutation |

### Traits (Permanent)
Selected at creation, can gain more from quests.
- Affect communication, disguise, combat, survival
- Examples: Can't Speak, Paranoid, Quick Learner, Night Blind

---

## Mutation System

### Acquisition
- **Level up** → Gain skill points
- **1 point** = Level up 1 mutation
- **Choice system:** 3 random mutations shown (perks can increase choices)
- **Free pick:** Every 4 levels, choose any mutation freely

### Mutation Properties
- Some mutations: Max level 1 (binary - have it or don't)
- Some mutations: Can level multiple times (scaling effect)
- **No limit** on total mutations

### Example Mutations
| Mutation | Max Level | Effect per Level |
|----------|-----------|------------------|
| Extra Arms | 1 | +2 arm body parts |
| Night Vision | 3 | +10m dark sight range per level |
| Claws | 5 | +2 melee damage per level |
| Thick Hide | 3 | +5% damage resistance per level |
| Complex Brain | 3 | +10% research speed per level |
| Tree Jump | 1 | Can jump between trees, +evasion in forests |
| Regeneration | 5 | Heal 1 HP per 10 seconds per level |

---

## Body Part System

### Structure (Literal Parts)
```
Body
├── Head
│   ├── Brain
│   ├── Left Eye
│   ├── Right Eye
│   ├── Nose
│   └── Jaw
├── Torso
│   ├── Heart
│   ├── Left Lung
│   ├── Right Lung
│   └── Stomach
├── Left Arm → Left Hand
├── Right Arm → Right Hand
├── Left Leg → Left Foot
└── Right Leg → Right Foot
```

### Mutations Add Parts
- "Extra Arms" → Adds LeftArm2, RightArm2, LeftHand2, RightHand2
- Each part is REAL: Can be injured, can hold items, can have implants

### Part States
- **Healthy** - Full function
- **Injured** - Reduced function (cuts, bruises)
- **Broken** - Severely reduced function
- **Missing** - Gone (prosthetic possible)
- **Infected** - Degrading, needs treatment
- **Prosthetic** - Replaced with implant

### Implant Slots
- Each body part can have 0-N implant slots (depends on part)
- More arms = more implant capacity

---

## Status Effect System

### Categories
| Category | Examples |
|----------|----------|
| **Elemental** | Wet, Burning, Frozen, Electrified |
| **Physical** | Bleeding, Broken Bone, Exhausted |
| **Mental** | Panicked, Focused, Berserk, Dazed |
| **Environmental** | Muddy, Irradiated, Poisoned |

### Effect Chains
```
Wet + Lightning → Stunned (3 turns)
Wet + Cold → Hypothermia
Oiled + Fire → Burning (intense)
Bleeding + Shark Zone → Aggro predators
Wet + Electric Tile → Damage + Stunned
```

### Effect Properties
- **Duration:** Turns or seconds
- **Stacking:** Some stack intensity, some refresh duration
- **Severity:** Minor → Major → Critical
- **Interactions:** Defined chain reactions

---

## Survival Mechanics

### Needs
| Need | Drain Rate | Effects When Low |
|------|------------|------------------|
| **Hunger** | ~1 day to hungry | -damage, -speed, eventually organ damage |
| **Thirst** | ~0.5 day to thirsty | -vision, hallucinations, collapse |
| **Rest** | ~1 day to tired | -accuracy, -reaction, mistakes |
| **Temperature** | Varies by weather | Hypothermia or Heatstroke chains |

### Seasons
- **Spring:** Mild, good foraging
- **Summer:** Hot, dehydration risk, abundant food
- **Autumn:** Cooling, harvest time, prep for winter
- **Winter:** Cold, scarce food, hypothermia risk

### Day/Night
- **Day:** Normal visibility, most NPCs active
- **Night:** Reduced vision (unless Night Vision), dangerous creatures, stealth bonus

---

## Combat System

### Mode Trigger
- **Exploration:** Real-time movement and actions
- **Combat:** Turn-based when enemies engaged

### Action Points (per turn)
- **Base AP:** 3 (modified by traits, mutations, status)
- **Move:** 1 AP per tile (terrain modifies)
- **Attack:** 2 AP (weapons may vary)
- **Use Item:** 1 AP
- **Special Ability:** 1-3 AP

### Initiative
- Based on Speed stat + modifiers
- Higher initiative = act first
- Can delay turn to act later

### Systems
- **Stealth:** Pre-combat positioning, sneak attacks
- **Cover:** Reduces ranged hit chance (half/full cover)
- **Height:** Advantage for ranged from high ground

---

## World Structure

### Semi-Procedural
- **Fixed Locations:** Objectives, trading posts, key story areas
- **Random Locations:** Everything else (swamps, forests, ruins)
- **Progression Lock:** Further zones locked behind objectives

### Map Example
```
[Zone ???] [Zone ???] [OBJECTIVE 2]
    |          |           |
[Zone ???] [Zone ???] [OBJECTIVE 1]
    |          |           |
[Random]   [SPAWN]    [Trading Post]
    |          |           |
[Random]   [Random]   [Random]
```

### Travel
- Exit map edge to move to adjacent zone
- Requires food for journey
- Travel events possible (ambush, discovery, weather)

---

## Base Building (Medium Complexity)

### Structures
- Walls, doors, floors
- Furniture (beds, tables, storage)
- Workbenches (crafting stations)
- Power system (generators, wiring)
- Defense (turrets, traps)

### Room Types
- Bedroom (rest quality)
- Lab (research speed)
- Kitchen (food prep)
- Storage (item capacity)
- Workshop (crafting)

---

## Faction Relations

### Player Status
- Mutants are outsiders to normal society
- Traits affect interaction (Can Speak, Disguise, etc.)
- Reputation per faction

### Interaction Types
- **Trading:** Higher prices for obvious mutants
- **Quests:** Dirty work normals won't do
- **Hostility:** Some factions hunt mutants
- **Allies:** Other mutant groups, outcasts

---

## Tech Trees

### Tinker Science
```
Basic Electronics
├── Simple Circuits → Advanced Circuits → AI Modules
├── Basic Implants → Combat Implants → Neural Interface
└── Crude Weapons → Firearms → Energy Weapons
```

### Dark Science
```
Void Basics
├── Corpse Transmutation → Material Conversion → Gold Synthesis
├── Monster Harvesting → Anomaly Crafting → Eldritch Weapons
└── Minor Rituals → Major Rituals → Forbidden Rites
```

---

## Development Priority

### Phase 1: Core Loop (Current)
- [x] Grid movement + pathfinding
- [x] Basic status effects
- [ ] Body part system
- [ ] Basic combat (attack, HP, death)
- [ ] Simple enemy AI

### Phase 2: Character Depth
- [ ] Mutation system (5-10 mutations)
- [ ] Trait system
- [ ] Survival needs (hunger, thirst)
- [ ] Inventory system

### Phase 3: World
- [ ] Multiple map zones
- [ ] Zone transitions
- [ ] Basic base building
- [ ] Save/Load system

### Phase 4: Content
- [ ] Trading NPCs
- [ ] Quest system
- [ ] Full mutation tree
- [ ] Science research trees
- [ ] More enemy types

---

## File Structure (Planned)
```
MyRPG/
├── Engine/
│   └── Camera2D.cs
├── Gameplay/
│   ├── Character/
│   │   ├── Body.cs
│   │   ├── BodyPart.cs
│   │   ├── Mutation.cs
│   │   ├── Trait.cs
│   │   └── CharacterStats.cs
│   ├── Combat/
│   │   ├── CombatManager.cs
│   │   ├── TurnManager.cs
│   │   └── ActionPoint.cs
│   ├── Entities/
│   │   ├── PlayerEntity.cs
│   │   └── EnemyEntity.cs
│   ├── Systems/
│   │   ├── Pathfinding.cs
│   │   ├── StatusSystem.cs
│   │   ├── SurvivalSystem.cs
│   │   └── TimeSystem.cs
│   └── World/
│       ├── WorldGrid.cs
│       ├── Tile.cs
│       └── WorldMap.cs
├── Data/
│   ├── Enums.cs
│   ├── MutationDatabase.cs
│   └── TraitDatabase.cs
└── UI/
    ├── HUD.cs
    └── CharacterSheet.cs
```
