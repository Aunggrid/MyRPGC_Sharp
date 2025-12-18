# Future Features & Remarks

This document tracks future features and design considerations mentioned during development.

---

## 🎮 Core Systems

### Co-op Multiplayer Support
**Priority:** High (Design Consideration)
**Status:** Noted for all features

All features should be designed with future co-op support in mind:
- Player entities should be instance-based, not singleton
- Combat system should support multiple players in combat zone
- Turn order should handle multiple players
- Zone transitions should sync between players
- Inventory/equipment should be per-player
- Quest progress may need to be shared or individual

---

## ⚔️ Combat System

### Attack Animation
**Status:** ✅ Implemented
**Description:** When player attacks or gets attacked, entities lunge toward target
- Entity briefly moves toward target (60% of distance)
- Smooth ease-out on lunge, ease-in on return
- Hit flash effect (white flash) on taking damage
- Works for both player and enemies

### Escape Mechanics (Framework Added)
**Status:** ✅ Framework implemented
- `EnableEscape()` - From items/mutations/abilities
- `EnterStealth()` - Hidden state prevents zone expansion
- `TryEscape()` - Attempt to leave combat
- Items/mutations can grant escape abilities

### Combat Zone Features
**Status:** ✅ Dynamic zone implemented
- Zone expands when player approaches edge
- Max 3 escape attempts before zone locks
- Items/mutations can enable true escape

---

## 🧬 Character System

### More Mutations
**Priority:** High
**Description:** Many creative mutations, attribute-locked
- Physical mutations (STR/END based)
- Mental mutations (INT/WIL based)
- Sensory mutations (PER based)
- Agility mutations (AGI based)

### Free Mutation Choice
**Priority:** Medium
**Description:** Every 4 mutation points → pick from ALL mutations
- Still attribute-locked
- Some traits give 4 choices instead of 3

### Attributes Impact
**Priority:** Medium
**Description:** Attributes should affect more gameplay mechanics
- Dialogue options
- Crafting quality
- Research speed
- Combat special moves
- Movement abilities

### Body Part Equipment
**Priority:** Medium
**Description:** Choose which hand/body part for equipment
- Left hand axe + right hand pistol (dual wield)
- Mutations add new equippable body parts
- Each body part has independent damage/status

---

## 🔬 Research System

### More Research Nodes
**Priority:** High
**Description:** Expand tech tree significantly
- More Tinker Science nodes
- More Dark Science nodes
- Cross-path hybrid research
- Unique blueprints per path

---

## 🌍 World System

### Bigger Zones
**Priority:** Low
**Description:** Rimworld-sized maps
- Current: 50x50 tiles
- Target: 100x100 or larger
- Performance considerations needed

### Passive Mobs
**Status:** ✅ Implemented
- Scavenger (Cowardly)
- Giant Insect (Passive)
- Wild Boar (Passive)
- Mutant Deer (Cowardly)
- Cave Slug (Passive)

---

## 🔊 Audio System

### Sound Effects
**Priority:** Medium
- Combat sounds (hits, misses, deaths)
- UI sounds (menu, inventory)
- Ambient sounds per zone type
- Footsteps

### Music
**Priority:** Low
- Zone-specific ambient music
- Combat music
- Menu music

---

## 📝 Notes for Implementation

When implementing any feature, consider:
1. **Co-op compatibility** - Can this work with multiple players?
2. **Save/Load** - Does this need to be serialized?
3. **Network sync** - For future multiplayer, what needs syncing?
4. **Performance** - Will this scale to larger maps/more players?

---

*Last Updated: December 2024*
