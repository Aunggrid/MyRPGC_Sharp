# 💰 MUTANT WASTELAND - Currency Guide

## Overview

In the world of Orodia, different factions use different currencies. While **Gold** is universally accepted, trading with a faction's preferred currency gives you **better exchange rates**!

---

## 🪙 Currency Types

### Universal Currencies (Accepted Everywhere)

| Currency | Value | Weight | Description |
|----------|-------|--------|-------------|
| **Gold Nugget** 🥇 | 10 | 0.1 | Pure gold. The Iron Syndicate trade standard. |
| **Gold Bar** 🥇 | 100 | 1.0 | Worth 10 Gold Nuggets. Compact wealth. |
| **Aethelgard Coin** 🏛️ | 100 | 0.05 | 400-year-old coins. Never tarnish. |
| **Aethelgard Ingot** 🏛️ | 500 | 0.5 | Pre-Severance metal. Crafting material too! |

---

### The Changed (Mutant Society)

| Currency | Value | Weight | Special |
|----------|-------|--------|---------|
| **Void Mushroom** 🍄 | 5 | 0.05 | **EDIBLE: +1 XP when eaten!** Restores 5 hunger. |
| **Void Mushroom Cluster** 🍄 | 50 | 0.3 | Bundle of 10 mushrooms. Pulses with light. |

**Tip:** Can't decide whether to spend or eat your mushrooms? That's the gameplay choice!

---

### Void Cult (Dark Science)

| Currency | Value | Weight | Special |
|----------|-------|--------|---------|
| **Void Shard** 🔮 | 15 | 0.05 | Also used in Dark Science crafting! |
| **Pure Void Shard** 🔮 | 150 | 0.1 | Essential for powerful rituals. |

---

### United Sanctum (Tech Kingdom)

| Currency | Value | Weight | Description |
|----------|-------|--------|-------------|
| **Sanctum Coin** 🎴 | 20 | 0.08 | Heavy rectangular coins shaped like ID cards. |
| **Sanctum Credit Chip** 🎴 | 100 | 0.01 | Digital currency worth 50 coins. |

**Note:** These are heavy! The Sanctum doesn't care about convenience.

---

### Iron Syndicate (Trade Kingdom)

| Currency | Value | Weight | Description |
|----------|-------|--------|-------------|
| **Syndicate Scrip** 📜 | 8 | 0.01 | Paper trade notes. "Good as gold" |
| **Syndicate Bond** 📜 | 80 | 0.02 | Worth 100 scrip. |

**Tip:** The Syndicate *says* scrip is good as gold, but they prefer actual gold...

---

### Verdant Order (Bio-Religion)

| Currency | Value | Weight | Special |
|----------|-------|--------|---------|
| **Bio-Token** 🧬 | 25 | 0.03 | **⚠️ DECAYS OVER TIME!** Spend them fast! |
| **Verdant Tithe** 🧬 | 250 | 0.1 | Sacred tokens. Also decay but slower. |

**Warning:** Bio-Tokens are LIVING currency. They lose value if you hold them too long!

---

### Traders Guild

| Currency | Value | Weight | Description |
|----------|-------|--------|-------------|
| **Trade Bead** 📿 | 3 | 0.01 | Super lightweight glass beads. |
| **Bead String** 📿 | 150 | 0.2 | 50 beads on wire. |

**Tip:** Trade Beads are the lightest currency - great for long journeys!

---

### Gene-Elders (Tribal Leaders)

| Currency | Value | Weight | Special |
|----------|-------|--------|---------|
| **Elder's Token** 🦴 | 75 | 0.02 | **CANNOT BE BOUGHT!** Quest rewards only. |

**How to get:** Complete quests for Gene-Elders, or... loot them from enemies who have them.

---

## 📊 Exchange Rate Table

Base value relative to Gold (1.0):

| Currency | Rate | Notes |
|----------|------|-------|
| Gold | 1.0 | Base standard |
| Void Mushroom | 0.5 | Common, cheap |
| Void Shard | 1.5 | Valuable |
| Sanctum Credits | 2.0 | Premium tech |
| Syndicate Scrip | 0.8 | Paper discount |
| Verdant Tithes | 2.5 | Living = premium |
| Trade Beads | 0.3 | Low value each |
| Ancient Relic | 10.0 | Very valuable |
| Mutant Favor | 7.5 | Quest rewards |

---

## 🎯 Faction Bonuses

Using a faction's **preferred currency** gives you better deals!

| Faction | Preferred Currency | Bonus |
|---------|-------------------|-------|
| **The Changed** | Void Mushroom | +20% |
| **Gene-Elders** | Elder's Token | +50% |
| **Gene-Elders** | Void Mushroom | +30% |
| **Void Cult** | Void Shard | +50% |
| **United Sanctum** | Sanctum Credits | +30% |
| **Iron Syndicate** | Gold | +10% |
| **Iron Syndicate** | Syndicate Scrip | +20% |
| **Verdant Order** | Bio-Token | +40% |
| **Traders** | Trade Beads | +20% |

---

## 💡 Tips & Tricks

1. **Void Mushrooms for XP:** Early game, eat mushrooms for +1 XP each. Late game, spend them.

2. **Bio-Tokens decay:** If you have Verdant currency, spend it soon or convert to Gold!

3. **Elder's Tokens are rare:** They're valuable but you can only get them from quests or combat.

4. **Trade Beads for travel:** Lightest currency = best for exploration.

5. **Ancient Relics:** Worth a fortune to everyone. Don't spend these frivolously!

6. **Match currency to faction:** Always trade with a faction's preferred currency for better rates.

---

## 🔧 Technical Notes

### For Modders

Currency items are defined in `Gameplay/Items/CurrencyItems.cs`

Key properties:
- `IsCurrency` - marks item as currency
- `CurrencyType` - enum for exchange rates
- `AcceptedFactions` - which factions accept this
- `XPBonus` - XP granted when consumed (mushrooms)
- `DecaysOverTime` - whether currency loses value (bio-tokens)
- `IsQuestReward` - cannot be purchased (elder tokens)

---

*"Gold talks, but Void Mushrooms... they sing."* — Old Trader saying
