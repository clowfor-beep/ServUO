# UO Shard — Change Log

All changes to the shard are documented here.
Format: [YYYY-MM-DD] | Category | File(s) | Description

---

## [2026-05-17] — Backstab Tuning

**Category:** Core Systems — Skills
**Status:** ✅ Implemented

### Changed
- `Scripts/Custom/SkillSynergies.cs`
  - `StealthBackstabBonus` changed from `0.30` (+30%) to `2.00` (+200%)
  - Backstab now deals 3x total damage at GM Stealth (was 1.3x)
  - Updated `GetBackstabBonus` signature to accept defender
  - Added PvM-only check — zero bonus against PlayerMobile
  - Added minimum skill requirement — requires 80+ Hiding AND 80+ Stealth

- `Scripts/Items/Equipment/Weapons/BaseWeapon.cs`
  - Removed backstab from `ScaleDamageAOS` (that method has no defender reference)
  - Moved backstab multiplier to `ComputeDamageAOS` where defender is available
  - Applied as a multiplier on final damage: `damage * (1.0 + backstabBonus)`

### Backstab Behaviour Summary
| Stealth Skill | Bonus | Total Damage Multiplier |
|---|---|---|
| 80 (minimum) | +160% | 2.6x |
| 90 | +180% | 2.8x |
| 100 (GM) | +200% | 3.0x |
| 120 (with mastery) | +240% | 3.4x |

---

## [2026-05-17] — Skill Synergy System

**Category:** Core Systems — Skills
**Status:** ✅ Implemented

### New Files
- `Scripts/Custom/SkillSynergies.cs`
  - Outlands-style passive skill synergy system
  - All values configurable in CONFIG section at top of file

### Weapon Damage Synergies (all weapon types)
| Skill | Bonus at GM |
|---|---|
| Camping | +10% |
| Forensic Evaluation | +10% |
| Tracking | +10% |

### Weapon Damage Synergies (type-specific)
| Skill | Bonus at GM | Weapon Type |
|---|---|---|
| Blacksmithy | +10% | Metal (Axe, Slashing, Bashing, Piercing, Polearm) |
| Carpentry | +10% | Wooden (Staff, Ranged) |
| Mining | +10% | Bashing only |
| Lumberjacking | +10% | Axe only |

### Modified Files
- `Scripts/Items/Equipment/Weapons/BaseWeapon.cs`
  - `ScaleDamageAOS` — calls `SkillSynergies.GetWeaponDamageBonus`
  - `ComputeDamageAOS` — applies backstab multiplier with PvM check

### To Do — Pending Hooks
- [ ] Hook `GetHealBonus()` into bandage apply logic
- [ ] Hook `GetBardingBonus()` into Discordance, Peacemaking, Provocation
- [ ] Test all synergies in-game and tune values
- [ ] Add Resisting Spells damage reduction synergy

---

## Shard Design Decisions

### Emulator
ServUO (C# / .NET) | Client: ClassicUO

### World Structure (planned)
- Tier 1: Safe Lands — PvE, quests, crafting
- Tier 2: Contested Zones — PvP enabled, champion spawns
- Tier 3: Cursed Lands — Full loot, world bosses, rare artifacts

### Skill System Philosophy
- Based on UO Outlands synergy model
- Passive bonuses when skills are combined — always-on, no codex required yet
- All values configurable in SkillSynergies.cs
- Backstab: +200% PvM only, scales with Stealth skill, requires 80+ Hiding/Stealth
