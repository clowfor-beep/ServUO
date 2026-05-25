# Claude 2 Work Instruction — Defensive Skill Synergies (Step 9)
**Date:** 2026-05-25  
**Branch:** work directly on `main`  
**Estimated complexity:** High — touches 4+ engine files

---

## Context

The shard already has offensive skill synergies implemented in `Scripts/Custom/SkillSynergies.cs`. Step 9 adds the **defensive** side: resist bonuses, resist cap raises, DCI bonuses, HP bonuses, bandage heal bonuses, HP regen multiplier, and debuff duration reduction.

The full design spec is in `Design/SkillSynergies_DesignDoc.txt` — **read it first**. The relevant section is PART 2 (sections A–G).

---

## What to Build

### 1. Add defensive methods to `Scripts/Custom/SkillSynergies.cs`

Add the following **private helpers** first:

```csharp
// Scale 0.0 at skill 80, 1.0 at skill 100+
private static double SynergyScale(double skillValue)
{
    if (skillValue < 80.0) return 0.0;
    return Math.Min(1.0, (skillValue - 80.0) / 20.0);
}

// Cap raise: bonusAt100 at skill 100, bonusAt120 at skill 120
private static int CapScale(double skillValue, int bonusAt100, int bonusAt120)
{
    if (skillValue < 100.0) return 0;
    if (skillValue >= 120.0) return bonusAt120;
    return bonusAt100 + (int)((skillValue - 100.0) / 20.0 * (bonusAt120 - bonusAt100));
}
```

Then add these **public static methods** — all take `Mobile m`, check `m is PlayerMobile`, read skill values via `m.Skills[SkillName.X].Value`:

#### Section A — Resist bonuses (return int = % points)
```
GetPhysicalResistBonus(Mobile m)  — Blacksmithy*10, Mining*5, Lumberjacking*5, Carpentry*3, Camping*5, Animal Taming*5, Wrestling*8
GetFireResistBonus(Mobile m)      — Blacksmithy*5, Mining*5, Alchemy*12, Taste ID*5
GetColdResistBonus(Mobile m)      — Lumberjacking*8, Carpentry*5, Camping*8, Herding*5, Fishing*5, Inscription*5
GetPoisonResistBonus(Mobile m)    — Taste ID*12, Herding*8, Animal Taming*5, Alchemy*5
GetEnergyResistBonus(Mobile m)    — Inscription*12, Fishing*3
```
All scale via `SynergyScale`. Sum all contributing skills then return as int.

#### Section B — Resist cap raises (return int = additional % cap)
Primary raises (+5 at 100, +8 at 120):
```
GetPhysicalResistCap(Mobile m) — Blacksmithy primary
GetFireResistCap(Mobile m)     — Alchemy primary
GetColdResistCap(Mobile m)     — Camping primary
GetPoisonResistCap(Mobile m)   — Taste ID primary
GetEnergyResistCap(Mobile m)   — Inscription primary
```
Secondary raises (+2 at 100, +3 at 120) — add to the same methods:
```
Physical: +Lumberjacking, +Carpentry, +Camping
Fire:     +Blacksmithy secondary, +Mining, +Taste ID secondary
Cold:     +Lumberjacking, +Herding, +Inscription secondary
Poison:   +Alchemy secondary, +Herding, +Animal Taming
Energy:   +Fishing
```
Use `CapScale(skill, 2, 3)` for secondaries, `CapScale(skill, 5, 8)` for primaries.

#### Section C — DCI bonus (return double = fraction, e.g. 0.08 = +8%)
```
GetDCIBonus(Mobile m) — Tracking*0.08, Detect Hidden*0.10
```

#### Section D — HP bonus (return int)
```
GetBonusHP(Mobile m) — Herding+20, Animal Taming+15, Fishing+10 (all scale via SynergyScale)
```

#### Section E — Bandage heal bonus (return double = multiplier, e.g. 0.25 = +25%)
```
GetBandageHealBonus(Mobile m) — Forensic Eval*0.25, Veterinary*0.20 (SynergyScale)
```

#### Section F — HP regen multiplier (return double, 1.0 = no change)
```
GetHPRegenMultiplier(Mobile m) — Camping: returns 2.0 at skill 100+, scales from 1.0 at 80
```

#### Section G — Debuff duration multiplier (return double, 1.0 = no change)
```
GetDebuffDurationMultiplier(Mobile m) — Detect Hidden: returns 0.75 at skill 100 (25% reduction), scales from 1.0 at 80
```

---

### 2. Hook locations

#### `Scripts/Mobiles/PlayerMobile.cs`

**Resist bonuses — `ComputeResistances()`**  
Find where `SetResistance` or resist values are finalized. After the base computation, add:
```csharp
SetResistance(ResistanceType.Physical, GetResistance(ResistanceType.Physical) + SkillSynergies.GetPhysicalResistBonus(this));
SetResistance(ResistanceType.Fire,     GetResistance(ResistanceType.Fire)     + SkillSynergies.GetFireResistBonus(this));
SetResistance(ResistanceType.Cold,     GetResistance(ResistanceType.Cold)     + SkillSynergies.GetColdResistBonus(this));
SetResistance(ResistanceType.Poison,   GetResistance(ResistanceType.Poison)   + SkillSynergies.GetPoisonResistBonus(this));
SetResistance(ResistanceType.Energy,   GetResistance(ResistanceType.Energy)   + SkillSynergies.GetEnergyResistBonus(this));
```

**Resist cap raises — `GetMaxResistance(ResistanceType type)`**  
Find this method (returns int, typically 70). Add skill cap bonus:
```csharp
int skillCap = 0;
switch (type)
{
    case ResistanceType.Physical: skillCap = SkillSynergies.GetPhysicalResistCap(this); break;
    case ResistanceType.Fire:     skillCap = SkillSynergies.GetFireResistCap(this);     break;
    case ResistanceType.Cold:     skillCap = SkillSynergies.GetColdResistCap(this);     break;
    case ResistanceType.Poison:   skillCap = SkillSynergies.GetPoisonResistCap(this);   break;
    case ResistanceType.Energy:   skillCap = SkillSynergies.GetEnergyResistCap(this);   break;
}
return baseMax + skillCap;
```

**HP bonus — `HitsMax` getter**  
Find `public override int HitsMax`. After the base calculation, add:
```csharp
value += SkillSynergies.GetBonusHP(this);
```

**HP regen — `GetHitsRegenRate()`**  
Find this method. Multiply the return value:
```csharp
return (int)(base * SkillSynergies.GetHPRegenMultiplier(this));
```

---

#### `Scripts/Items/Weapons/BaseWeapon.cs` (already patched — be careful)

**DCI bonus — `CheckHit()`**  
Find where `defChance` or defender's DCI is calculated. After the item DCI is applied, add:
```csharp
defChance += SkillSynergies.GetDCIBonus(defender);
```
This is additive on top of the item DCI cap — it does NOT count against the 45% cap per the design.

---

#### `Scripts/Skills/Healing.cs` (or `Bandage.cs`)

**Bandage heal bonus**  
Find where the final heal amount is calculated (`toHeal` variable). Multiply it:
```csharp
toHeal = (int)(toHeal * (1.0 + SkillSynergies.GetBandageHealBonus(healer)));
```

---

#### Section G — Debuff duration (skip for now)

The design says to apply `-25% debuff duration` for Detect Hidden to Evil Omen, Curse, Strangle, Pain Spike. These are in individual spell files. **Skip this section for now** — hook the others first, debuff duration can be a separate task.

---

## Important Rules

- **PlayerMobile only** — all synergy methods check `m is PlayerMobile` and return 0 if not. SimPlayers use fixed `SetResistance()` in their constructors.
- **Do not change the existing offensive synergy methods** in SkillSynergies.cs.
- **Do not reorder or remove any existing `Deserialize` reads** in any file you touch.
- Before modifying PlayerMobile.cs, read the section you're changing carefully — it's a large file. Make minimal, targeted edits.
- Test compile after each file. Fix any errors before moving to the next hook.

## Deliverables

1. `Scripts/Custom/SkillSynergies.cs` — all defensive methods added (Sections A–F)
2. `Scripts/Mobiles/PlayerMobile.cs` — resist bonuses, resist cap, HP bonus, HP regen hooked
3. `Scripts/Items/Weapons/BaseWeapon.cs` — DCI bonus hooked in CheckHit
4. `Scripts/Skills/Healing.cs` or `Bandage.cs` — bandage heal bonus hooked
5. Build must succeed with 0 errors before committing
6. Commit message: `feat: defensive skill synergies — resist/cap/DCI/HP/heal/regen (Step 9)`
