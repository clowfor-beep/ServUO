# Claude2 Work Instruction — SummonerSynergySystem
Date: 2026-05-28
Branch: claude2/summoner-synergy-system

## Context files to read (in this order)
1. `Design/COWORK_HANDOVER.md`                                 ← always first
2. `Scripts/Custom/SkillSynergies.cs`                          ← existing synergy pattern to follow
3. `Scripts/Spells/Eighth/EarthElemental.cs`                   ← SpellHelper.Summon pattern (no ref returned)
4. `Scripts/Spells/Eighth/SummonDaemon.cs`                     ← SpellHelper.Summon pattern (ref already saved)
5. `Scripts/Spells/Fifth/BladeSpirits.cs`                      ← BaseCreature.Summon inline pattern
6. `Scripts/Spells/Necromancy/AnimateDeadSpell.cs`              ← deferred summon in callback
7. `Scripts/Spells/Necromancy/SummonFamiliar.cs`                ← summon inside gump OnResponse
8. `Scripts/Mobiles/Normal/BaseCreature.cs`                    ← AlterMeleeDamageTo / AlterMeleeDamageFrom hooks

## Background / What this enables

This system makes the **Necro Pet Herder** template viable on our shard (Necromancy + Spirit Speak + Herding + Magery).

In vanilla ServUO:
- Spirit Speak has zero effect on summoned creature stats.
- Herding has zero combat utility (only herds animals to a tile).

This system adds two independent bonuses:

**1. Spirit Speak → Summoned Follower Scaling**
All summoned followers (elementals, daemon, blade spirits, energy vortex, familiars, animate dead) gain
stat bonuses based on the summoner's Spirit Speak skill at the moment of summoning.

**2. Herding → Follower Damage + Resistance**
All controlled AND summoned followers deal extra damage and take less damage, provided their
controller has a Shepherd's Crook anywhere in their backpack.

---

## Task

Build **SummonerSynergySystem.cs** and hook it into the relevant spell files and BaseCreature.cs.

Also extend **SkillSynergies.cs** with two new Herding methods and hook those into **BaseCreature.cs**.

---

## Architecture

### File 1 — Scripts/Custom/SummonerSynergySystem.cs (CREATE)

Single static class. Called by each summoning spell immediately after the creature is placed.

```csharp
namespace Server.Custom
{
    public static class SummonerSynergySystem
    {
        // ── Tuning constants ────────────────────────────────────────────────
        // Based on UO Outlands formula, scaled to our shard's power level.
        // At 100 Spirit Speak:  HP ×2.5, Damage ×1.5, Wrestling +50, VirtualArmor +25, MagicResist +50
        // At 120 Spirit Speak:  HP ×2.8, Damage ×1.6, Wrestling +60, VirtualArmor +30, MagicResist +60

        private const double HpMultiplierPerSS      = 1.5;   // +150% HP   per SS/100
        private const double DamageMultiplierPerSS  = 0.5;   // +50%  dmg  per SS/100
        private const double WrestlingBonusPerSS    = 50.0;  // +50   wrestling per SS/100
        private const double VirtualArmorBonusPerSS = 25.0;  // +25   armor    per SS/100
        private const double MagicResistBonusPerSS  = 50.0;  // +50   resist   per SS/100

        /// <summary>
        /// Apply Spirit Speak scaling to a freshly summoned creature.
        /// Call this AFTER SpellHelper.Summon / BaseCreature.Summon returns.
        /// </summary>
        public static void ApplyBonuses(BaseCreature creature, Mobile caster)
        {
            if (creature == null || caster == null || creature.Deleted) return;

            double ss = caster.Skills[SkillName.SpiritSpeak].Value;
            if (ss < 20.0) return;  // no bonus below 20 SS

            double factor = ss / 100.0;

            // ── Hit Points ─────────────────────────────────────────────────
            if (creature.HitsMaxSeed > 0)
            {
                int bonus = (int)(creature.HitsMaxSeed * HpMultiplierPerSS * factor);
                creature.HitsMaxSeed = creature.HitsMaxSeed + bonus;
                creature.Hits = creature.HitsMaxSeed; // full HP on summon
            }

            // ── Damage ─────────────────────────────────────────────────────
            creature.DamageMin = creature.DamageMin + (int)(creature.DamageMin * DamageMultiplierPerSS * factor);
            creature.DamageMax = creature.DamageMax + (int)(creature.DamageMax * DamageMultiplierPerSS * factor);

            // ── Wrestling ──────────────────────────────────────────────────
            double newWrestling = Math.Min(120.0, creature.Skills[SkillName.Wrestling].Base + WrestlingBonusPerSS * factor);
            creature.SetSkill(SkillName.Wrestling, newWrestling);

            // ── Virtual Armor ──────────────────────────────────────────────
            creature.VirtualArmor = creature.VirtualArmor + (int)(VirtualArmorBonusPerSS * factor);

            // ── Magic Resist ───────────────────────────────────────────────
            double newResist = Math.Min(120.0, creature.Skills[SkillName.MagicResist].Base + MagicResistBonusPerSS * factor);
            creature.SetSkill(SkillName.MagicResist, newResist);
        }
    }
}
```

**Namespace:** `Server.Custom`  
**Using directives needed:** `using Server; using Server.Mobiles; using System;`  
**No Initialize() needed** — this is a pure helper, called by spells.  
**No Serialize/Deserialize** — no persistent state.

---

### File 2 — Scripts/Custom/SkillSynergies.cs (EDIT — append to existing file)

Add three new methods at the bottom of the existing `SkillSynergies` class (before the closing `}`):

```csharp
// ============================================================
// HERDING SYNERGIES — follower damage and resistance bonuses
// ============================================================

// PvM formula: +22% damage per (Herding/100) while controller has a Shepherd's Crook
private const double HerdingDamageBonusPerPoint  = 0.22;
// PvM formula: +11% resistance per (Herding/100)
private const double HerdingResistBonusPerPoint  = 0.11;

/// <summary>
/// Call from BaseCreature.AlterMeleeDamageTo when this creature is a follower attacking.
/// Multiplies outgoing melee damage by the Herding bonus.
/// </summary>
public static void ApplyHerdingDamageBonus(Mobile master, ref int damage)
{
    if (master == null || !master.Alive) return;

    double herding = master.Skills[SkillName.Herding].Value;
    if (herding < 30.0) return;
    if (!HasShepherdsCrook(master)) return;

    double bonus = HerdingDamageBonusPerPoint * (herding / 100.0);
    damage = (int)(damage * (1.0 + bonus));

    // Passive Herding skill gain: 5% chance per successful follower hit
    if (Utility.RandomDouble() < 0.05)
        master.CheckSkill(SkillName.Herding, 50.0, 120.0);
}

/// <summary>
/// Call from BaseCreature.AlterMeleeDamageFrom when this creature is a follower being hit.
/// Reduces incoming melee damage by the Herding resist bonus.
/// </summary>
public static void ApplyHerdingResistBonus(Mobile master, ref int damage)
{
    if (master == null || !master.Alive) return;

    double herding = master.Skills[SkillName.Herding].Value;
    if (herding < 30.0) return;
    if (!HasShepherdsCrook(master)) return;

    double reduction = HerdingResistBonusPerPoint * (herding / 100.0);
    damage = (int)(damage * (1.0 - reduction));
}

/// <summary>Returns true if the mobile has a Shepherd's Crook equipped or in backpack.</summary>
private static bool HasShepherdsCrook(Mobile m)
{
    if (m.FindItemOnLayer(Layer.TwoHanded) is ShepherdsCrook) return true;
    if (m.Backpack != null && m.Backpack.FindItemByType(typeof(ShepherdsCrook)) != null) return true;
    return false;
}
```

**Note:** `ShepherdsCrook` is in `Server.Items` — add `using Server.Items;` if not already present at the top of SkillSynergies.cs.

---

### File 3 — Scripts/Mobiles/Normal/BaseCreature.cs (EDIT — two small additions)

**Read this file first. It is large (~6000 lines). Only edit the two methods shown below.**

#### Addition A — AlterMeleeDamageTo (follower outgoing damage bonus)

Find the existing `AlterMeleeDamageTo` method. It currently looks like:

```csharp
public virtual void AlterMeleeDamageTo(Mobile to, ref int damage)
{
    if (m_TempDamageBonus > 0 && TastyTreat.UnderInfluence(this))
        damage += damage / m_TempDamageBonus;
}
```

Add the Herding bonus call at the END of this method (after the TastyTreat block):

```csharp
// Herding bonus — applies when this creature is a player-controlled follower
if ((Controlled || Summoned) && damage > 0)
{
    Mobile master = ControlMaster ?? SummonMaster;
    Server.Custom.SkillSynergies.ApplyHerdingDamageBonus(master, ref damage);
}
```

#### Addition B — AlterMeleeDamageFrom (follower incoming damage reduction)

Find the existing `AlterMeleeDamageFrom` method. It currently ends with:

```csharp
if (m_TempDamageAbsorb > 0 && VialofArmorEssence.UnderInfluence(this))
    damage -= damage / m_TempDamageAbsorb;
```

Add the Herding resist call at the END of this method (after the VialOfArmorEssence block):

```csharp
// Herding resist bonus — applies when this creature is a player-controlled follower
if ((Controlled || Summoned) && damage > 0)
{
    Mobile master = ControlMaster ?? SummonMaster;
    Server.Custom.SkillSynergies.ApplyHerdingResistBonus(master, ref damage);
}
```

---

### Files 4–11 — Summoning spell edits

Each spell gets a single `SummonerSynergySystem.ApplyBonuses(creature, Caster)` call added immediately after the creature is summoned.

**Add this using directive** to every spell file you edit:
```csharp
using Server.Custom;
```

---

#### Scripts/Spells/Eighth/EarthElemental.cs

The creature is created inline without saving a reference. Extract it first:

Replace:
```csharp
SpellHelper.Summon(new SummonedEarthElemental(), Caster, 0x217, duration, false, false);
```
With:
```csharp
var summon = new SummonedEarthElemental();
SpellHelper.Summon(summon, Caster, 0x217, duration, false, false);
SummonerSynergySystem.ApplyBonuses(summon, Caster);
```

#### Scripts/Spells/Eighth/FireElemental.cs

Same pattern — read the file first to confirm the creature type name.
Replace the inline `SpellHelper.Summon(new SummonedFireElemental(), ...)` with the same extract-then-apply pattern.

#### Scripts/Spells/Eighth/AirElemental.cs

Same pattern for `SummonedAirElemental`.

#### Scripts/Spells/Eighth/WaterElemental.cs

Same pattern for `SummonedWaterElemental`.

#### Scripts/Spells/Eighth/SummonDaemon.cs

The reference `m_Daemon` is already saved. Just add after `SpellHelper.Summon(m_Daemon, ...)`:
```csharp
SummonerSynergySystem.ApplyBonuses(m_Daemon, Caster);
```

#### Scripts/Spells/Fifth/BladeSpirits.cs

Find in `Target()`:
```csharp
BaseCreature.Summon(new BladeSpirits(true), false, Caster, new Point3D(p), 0x212, TimeSpan.FromSeconds(120));
```
Replace with:
```csharp
var spirit = new BladeSpirits(true);
BaseCreature.Summon(spirit, false, Caster, new Point3D(p), 0x212, TimeSpan.FromSeconds(120));
SummonerSynergySystem.ApplyBonuses(spirit, Caster);
```

#### Scripts/Spells/Eighth/EnergyVortex.cs

Same pattern as BladeSpirits:
```csharp
var vortex = new EnergyVortex(true);
BaseCreature.Summon(vortex, false, Caster, new Point3D(p), 0x212, TimeSpan.FromSeconds(90));
SummonerSynergySystem.ApplyBonuses(vortex, Caster);
```

#### Scripts/Spells/Necromancy/AnimateDeadSpell.cs

In `SummonDelay_Callback`, add after the `BaseCreature.Summon(summoned, ...)` call and before `summoned.Fame = 0`:
```csharp
SummonerSynergySystem.ApplyBonuses(summoned, caster);
```

**Note:** `summoned` is the local variable. `caster` is the caster Mobile. Both are in scope at that line.

#### Scripts/Spells/Necromancy/SummonFamiliar.cs

In `SummonFamiliarGump.OnResponse`, inside the `if (BaseCreature.Summon(bc, ...))` block,
add after `SummonFamiliarSpell.Table[m_From] = bc;`:
```csharp
SummonerSynergySystem.ApplyBonuses(bc, m_From);
```

---

## Files to create or edit

| Action | File | What changes |
|--------|------|-------------|
| CREATE | `Scripts/Custom/SummonerSynergySystem.cs` | New static class, ApplyBonuses method |
| EDIT   | `Scripts/Custom/SkillSynergies.cs` | Append Herding methods + HasShepherdsCrook |
| EDIT   | `Scripts/Mobiles/Normal/BaseCreature.cs` | Add Herding hooks in AlterMeleeDamageTo + AlterMeleeDamageFrom |
| EDIT   | `Scripts/Spells/Eighth/EarthElemental.cs` | Extract creature ref, call ApplyBonuses |
| EDIT   | `Scripts/Spells/Eighth/FireElemental.cs` | Extract creature ref, call ApplyBonuses |
| EDIT   | `Scripts/Spells/Eighth/AirElemental.cs` | Extract creature ref, call ApplyBonuses |
| EDIT   | `Scripts/Spells/Eighth/WaterElemental.cs` | Extract creature ref, call ApplyBonuses |
| EDIT   | `Scripts/Spells/Eighth/SummonDaemon.cs` | Call ApplyBonuses on m_Daemon |
| EDIT   | `Scripts/Spells/Fifth/BladeSpirits.cs` | Extract creature ref, call ApplyBonuses |
| EDIT   | `Scripts/Spells/Eighth/EnergyVortex.cs` | Extract creature ref, call ApplyBonuses |
| EDIT   | `Scripts/Spells/Necromancy/AnimateDeadSpell.cs` | Call ApplyBonuses in SummonDelay_Callback |
| EDIT   | `Scripts/Spells/Necromancy/SummonFamiliar.cs` | Call ApplyBonuses in OnResponse |

## Must NOT touch
- `Scripts/Custom/ReputationSystem.cs`
- `Scripts/Custom/PlayerSimulatorManager.cs`
- Any other file not listed above
- Server/ core engine files

---

## Viability at our 700 skill cap

The template fits cleanly at 700 without any cap changes:

```
120 Necromancy
120 Spirit Speak
100 Herding
100 Magery
100 Meditation
 80 Discordance
 80 Musicianship
= 700 total
```

At these values:
- Summons get: HP ×2.8, Damage ×1.6, Wrestling +60, Armor +30, MagicResist +60
- Herding adds: +22% follower damage, +11% follower resistance
- Discordance provides enemy defense/attack debuff
- Total effective DPS improvement over vanilla summons: roughly 3-4× before Discordance

---

## Definition of done

- [ ] `SummonerSynergySystem.cs` compiles with no errors
- [ ] All 9 summoning spells have ApplyBonuses calls added
- [ ] Earth/Air/Fire/Water Elemental and BladeSpirits/EnergyVortex all use the extract-then-apply pattern (no inline `new X()` inside Summon call)
- [ ] AnimateDeadSpell: ApplyBonuses called on `summoned` in `SummonDelay_Callback`
- [ ] SummonFamiliar: ApplyBonuses called on `bc` in `OnResponse` gump handler
- [ ] SkillSynergies.cs: ApplyHerdingDamageBonus, ApplyHerdingResistBonus, HasShepherdsCrook added
- [ ] BaseCreature.cs: Herding hooks added to AlterMeleeDamageTo and AlterMeleeDamageFrom
- [ ] ShepherdsCrook type resolves — check it exists in `Scripts/Items/` before referencing. If the class name differs, match it exactly.
- [ ] Passive Herding skill gain fires via `master.CheckSkill` inside ApplyHerdingDamageBonus
- [ ] namespace Server.Custom used throughout SummonerSynergySystem.cs and SkillSynergies.cs additions
- [ ] No compile errors — read every file carefully before editing

## Done signal
Commit this file as your final commit:
  `ToDo Cowork/DONE - Claude2 - SummonerSynergySystem - 2026-05-28.md`

Contents: "Done: SummonerSynergySystem. Files created: SummonerSynergySystem.cs. Files edited: SkillSynergies.cs, BaseCreature.cs, EarthElemental.cs, FireElemental.cs, AirElemental.cs, WaterElemental.cs, SummonDaemon.cs, BladeSpirits.cs, EnergyVortex.cs, AnimateDeadSpell.cs, SummonFamiliar.cs."
