# DONE — Defensive Skill Synergies (Step 9)
**Completed:** 2026-05-25

## Files Modified

### Scripts/Custom/SkillSynergies.cs
- Added `SynergyScale(skillValue)` helper (0.0 at <80, 1.0 at 100+)
- Added `CapScale(skillValue, bonusAt100, bonusAt120)` helper
- **Section A** — 5 resist bonus methods (Physical/Fire/Cold/Poison/Energy)
- **Section B** — 5 resist cap raise methods (primary +5/+8, secondary +2/+3)
- **Section C** — `GetDCIBonus` (Tracking +8%, DetectHidden +10%)
- **Section D** — `GetBonusHP` (Herding +20, AnimalTaming +15, Fishing +10)
- **Section E** — `GetBandageHealBonus` (ForensicEval +25%, Veterinary +20%)
- **Section F** — `GetHPRegenMultiplier` (Camping: 1.0→2.0 at skill 80–100)

### Scripts/Mobiles/PlayerMobile.cs
- `GetMaxResistance`: switch block adds skill cap raises per resist type
- `ComputeResistances`: 5 resist bonus lines added before the cap clamp loop
- `HitsMax`: `GetBonusHP(this)` added to strOffs after existing skill mastery bonuses

### Scripts/Misc/RegenRates.cs
- `Mobile_HitsRegenRate`: rate multiplied by `GetHPRegenMultiplier(from)` before returning TimeSpan

### Scripts/Items/Equipment/Weapons/BaseWeapon.cs
- `CheckHit`: DCI bonus added after item DCI cap, additive (outside 45% cap per design)

### Scripts/Items/Resource/Bandage.cs
- `EndHeal`: `GetBandageHealBonus` applied as second multiplicative layer after existing `GetHealBonus`

## Skipped
- Section G (debuff duration via Detect Hidden) — deferred per work instruction

## Commit
`feat: defensive skill synergies — resist/cap/DCI/HP/heal/regen (Step 9)`
