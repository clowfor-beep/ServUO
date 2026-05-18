# UO Shard — Change Log

All changes to the shard are documented here.
Format: [YYYY-MM-DD] | Category | File(s) | Description

---

## [2026-05-17] — Skill Synergy System

**Category:** Core Systems — Skills
**Status:** ✅ Implemented — pending compile test

### New Files
- `Scripts/Custom/SkillSynergies.cs`
  - New standalone skill synergy system inspired by UO Outlands
  - All bonus values configurable at the top of the file — no C# knowledge needed to tune
  - Implements the following passive synergies:

### Weapon Damage Synergies (all weapon types)
| Skill | Bonus at GM | Notes |
|---|---|---|
| Camping | +10% damage | Universal — all weapon types |
| Forensic Evaluation | +10% damage | Universal — all weapon types |
| Tracking | +10% damage | Universal — all weapon types |

### Weapon Damage Synergies (type-specific)
| Skill | Bonus at GM | Applies To |
|---|---|---|
| Blacksmithy | +10% damage | Metal weapons (Axe, Slashing, Bashing, Piercing, Polearm) |
| Carpentry | +10% damage | Wooden weapons (Staff, Ranged) |
| Mining | +10% damage | Bashing weapons only |
| Lumberjacking | +10% damage | Axe weapons only (stacks with Blacksmithy) |

### Combat Synergies
| Skill | Bonus | Notes |
|---|---|---|
| Stealth | +30% backstab damage | First hit only, attacker must be hidden |

### Healing Synergies
| Skills | Bonus | Notes |
|---|---|---|
| Anatomy + Healing | +50% heal amount | Scales by lower of the two skills |
| Animal Lore + Veterinary | +50% vet heal | Defined, not yet hooked in |

### Barding Synergies
| Skill | Bonus | Notes |
|---|---|---|
| Forensic Evaluation | +10% barding effectiveness | Defined, not yet hooked in |
| Tracking | +10% barding effectiveness | Defined, not yet hooked in |
| Carpentry | +10% instrument bonus | Defined, not yet hooked in |

### Modified Files
- `Scripts/Items/Equipment/Weapons/BaseWeapon.cs`
  - Added two lines to `ScaleDamageAOS` method (~line 3134)
  - Calls `SkillSynergies.GetWeaponDamageBonus(attacker, this)` for weapon synergy bonus
  - Calls `SkillSynergies.GetBackstabBonus(attacker)` for stealth backstab bonus
  - Both are added to `totalBonus` before final damage is returned

### To Do — Pending Hooks
- [ ] Hook `GetHealBonus()` into Healing.cs bandage apply logic
- [ ] Hook `GetBardingBonus()` into Discordance.cs, Peacemaking.cs, Provocation.cs
- [ ] Test all synergies in-game and tune values
- [ ] Consider adding Resisting Spells → spell damage reduction synergy

---

## Shard Design Decisions

### Emulator
- ServUO (C# / .NET)
- Client: ClassicUO

### World Structure (planned)
- Tier 1: Safe Lands — PvE, quests, crafting
- Tier 2: Contested Zones — PvP enabled, champion spawns
- Tier 3: Cursed Lands — Full loot, world bosses, rare artifacts

### Skill System Philosophy
- Based on UO Outlands synergy model
- Skills gain passive bonuses when combined with complementary skills
- No codex system yet — synergies are always-on passive bonuses
- All values configurable in SkillSynergies.cs config section

---
