# Claude2 Work Instruction — BaseFBCombatNPC
Date: 2026-05-24
Branch: claude2/base-fb-combat-npc

## Context files to read (in this order)
1. `Design/COWORK_HANDOVER.md`                        ← always first
2. `Design/SystemArchitecture_DesignDoc.txt`           ← BaseFBCombatNPC spec
3. `Design/PKNPCTemplates_AllArchetypes.txt`           ← full stats for all 7 templates
4. `Scripts/Custom/PlayerKillerNPCs.cs`                ← BasePKNPC to refactor
5. `Scripts/Custom/NovicePlayerKiller.cs`              ← NovicePlayerKiller to refactor
6. `Scripts/Custom/HunterWanted.cs`                    ← BaseWantedNPC (extends BasePKNPC — do NOT touch)

---

## Task

Create `Scripts/Custom/BaseFBCombatNPC.cs` — the shared combat foundation for all
Forsaken Britannia AI fighter types (SimPlayers, Pool PKs, Encounter PKs).

Then do two small refactors to wire existing classes into it:
- Edit `PlayerKillerNPCs.cs` — reparent `BasePKNPC` from `BaseCreature` to `BaseFBCombatNPC`
- Edit `NovicePlayerKiller.cs` — reparent from `BaseCreature` to `BaseFBCombatNPC`

No behaviour changes to any existing NPC. Stats, skills, loot, and AI remain identical.

---

## File 1 — CREATE Scripts/Custom/BaseFBCombatNPC.cs

### Class declaration

```csharp
namespace Server.Custom
{
    public abstract class BaseFBCombatNPC : BaseCreature
    {
        ...
    }
}
```

### Usings required
```csharp
using System;
using Server;
using Server.Items;
using Server.Mobiles;
```

### Speech (virtual, override in concrete classes)

```csharp
protected virtual string[] AggroLines => new string[0];
protected virtual string[] KillLines  => new string[0];
```

### Constructor

```csharp
protected BaseFBCombatNPC(AIType ai, FightMode mode, int range)
    : base(ai, mode, range, 1, 0.1, 0.2)
{
    SetupAppearance();
    Kills = 5; // murderer flag — red name
}

public BaseFBCombatNPC(Serial serial) : base(serial) { }
```

### SetupAppearance helper

```csharp
protected void SetupAppearance()
{
    bool female = Utility.RandomBool();
    Body = female ? 0x191 : 0x190;
    Hue  = Utility.RandomSkinHue();
    Name = NameList.RandomName(female ? "female" : "male");
    HairItemID = female
        ? Utility.RandomList(0x203B, 0x203C, 0x2045, 0x204A)
        : Utility.RandomList(0x2044, 0x2045, 0x204A, 0x203C);
    HairHue = Utility.RandomHairHue();
}
```

### InitEncounter — set combatant and start 5-min auto-delete

```csharp
public void InitEncounter(Mobile target)
{
    Timer.DelayCall(TimeSpan.FromMilliseconds(500), () =>
    {
        if (Deleted || target == null || !target.Alive)
            return;

        if (AggroLines.Length > 0)
            Say(AggroLines[Utility.Random(AggroLines.Length)]);

        Combatant = target;
        if (AIObject != null)
            AIObject.Action = ActionType.Combat;

        Timer.DelayCall(TimeSpan.FromMinutes(5.0), () =>
        {
            if (!Deleted && Combatant == null && !Controlled)
                Delete();
        });
    });
}
```

### OnThink — keep combatant lock even if base AI clears it

```csharp
public override void OnThink()
{
    Mobile target = Combatant as Mobile;
    base.OnThink();
    if (Deleted || !Alive) return;
    if (target != null && !target.Deleted && target.Alive && Combatant == null)
        Combatant = target;
}
```

### Combat speech hooks

```csharp
public override void OnGotMeleeAttack(Mobile attacker)
{
    base.OnGotMeleeAttack(attacker);
    if (AggroLines.Length > 0 && Utility.RandomDouble() < 0.20)
        Say(AggroLines[Utility.Random(AggroLines.Length)]);
}

public override void OnDeath(Container c)
{
    base.OnDeath(c);
    if (KillLines.Length > 0)
        Say(KillLines[Utility.Random(KillLines.Length)]);
}
```

### Serialize / Deserialize

**CRITICAL**: BaseFBCombatNPC has NO persistent fields of its own, so it does NOT
override Serialize/Deserialize. Adding them would break every existing BasePKNPC
subclass save file (the read order would shift by one version int). Leave them out
entirely — BaseCreature handles persistence.

### ── EQUIPMENT RULE (MANDATORY — applies to every template below) ──

```
Weapons  : AddItem(new Katana())           — equips to correct layer automatically
Armour   : AddItem(new LeatherChest())     — same
Books    : PackItem(new Spellbook())       — ALWAYS PackItem, NEVER AddItem
             PackItem(new NecromancerSpellbook())
             PackItem(new BookOfChivalry())
```

Reason: Spellbooks and BookOfChivalry share Layer.OneHanded with weapons.
AddItem on a book after AddItem on a weapon = LayerConflict crash in the log.
PackItem bypasses equip logic and places directly in the pack.

---

### Template 1 — ApplyDexxerTemplate(int tier)

Pure warrior. Swords / Tactics / Anatomy / Healing / Parry.

```
Tier 1 (Newbie — StatCap 150, SkillCap 450):
  SetStr(72, 78)  SetDex(47, 53)  SetInt(22, 28)
  SetHits(72, 78) SetStam(47, 53) SetMana(0)
  Skills: Swords 80, Tactics 75, Anatomy 70, Healing 75,
          Parry 70, Hiding 30, MagicResist 50
  Gear:  AddItem(new Katana())
         AddItem(new LeatherChest()) AddItem(new LeatherLegs())
         AddItem(new LeatherArms())  AddItem(new LeatherGorget())
         AddItem(new WoodenShield()) AddItem(new Boots(Utility.RandomNeutralHue()))
  Fame 1500 / Karma -1500 / VirtualArmor 20

Tier 2 (Advanced — StatCap 180, SkillCap 600):
  SetStr(82, 88)  SetDex(62, 68)  SetInt(27, 33)
  SetHits(82, 88) SetStam(62, 68) SetMana(0)
  Skills: Swords 100, Tactics 100, Anatomy 90, Healing 90,
          Parry 100, Hiding 60, MagicResist 60
  Gear:  AddItem(new Katana())
         AddItem(new RingmailChest()) AddItem(new RingmailLegs())
         AddItem(new LeatherArms())   AddItem(new LeatherGorget())
         AddItem(new MetalShield())   AddItem(new Boots(Utility.RandomNeutralHue()))
  Fame 5000 / Karma -5000 / VirtualArmor 35

Tier 3 (Expert — StatCap 225, SkillCap 700):
  SetStr(97, 103) SetDex(77, 83)  SetInt(42, 48)
  SetHits(97, 103) SetStam(77, 83) SetMana(0)
  Skills: Swords 100, Tactics 100, Anatomy 100, Healing 100,
          Parry 100, Hiding 70, MagicResist 70, Chivalry 60
  Gear:  AddItem(new Katana())
         AddItem(new PlateChest())  AddItem(new PlateLegs())
         AddItem(new PlateArms())   AddItem(new PlateGloves())
         AddItem(new PlateHelm())   AddItem(new HeaterShield())
         AddItem(new Boots(Utility.RandomNeutralHue()))
  Fame 12500 / Karma -12500 / VirtualArmor 50
```

---

### Template 2 — ApplyMageTemplate(int tier)

Pure caster. Magery / EvalInt / MagicResist / Meditation / Wrestling.

```
Tier 1 (Newbie):
  SetStr(27, 33)  SetDex(27, 33)  SetInt(87, 93)
  SetHits(27, 33) SetStam(27, 33) SetMana(87, 93)
  Skills: Magery 80, EvalInt 75, MagicResist 70, Meditation 75,
          Wrestling 60, Hiding 40, Tactics 50
  Gear:  AddItem(new Robe(Utility.RandomNeutralHue()))
         AddItem(new Sandals())
         PackItem(new Spellbook())
  Fame 2000 / Karma -2000 / VirtualArmor 10

Tier 2 (Advanced):
  SetStr(32, 38)  SetDex(32, 38)  SetInt(107, 113)
  SetHits(32, 38) SetStam(32, 38) SetMana(107, 113)
  Skills: Magery 100, EvalInt 100, MagicResist 100, Meditation 100,
          Wrestling 80, Focus 60, Hiding 60
  Gear:  AddItem(new Robe(Utility.RandomNeutralHue()))
         AddItem(new Sandals())
         PackItem(new Spellbook())
  Fame 5000 / Karma -5000 / VirtualArmor 10

Tier 3 (Expert):
  SetStr(37, 43)  SetDex(37, 43)  SetInt(142, 148)
  SetHits(37, 43) SetStam(37, 43) SetMana(142, 148)
  Skills: Magery 100, EvalInt 100, MagicResist 100, Meditation 100,
          Wrestling 100, Focus 100, Inscription 100
  Gear:  AddItem(new Robe(Utility.RandomNeutralHue()))
         AddItem(new WizardsHat())
         AddItem(new Sandals())
         PackItem(new Spellbook())
  Fame 12500 / Karma -12500 / VirtualArmor 10
```

---

### Template 3 — ApplyNecroMageTemplate(int tier)

Magery + Necromancy. Wraith Form, Strangle, Wither.
**Both spellbooks must go in pack — never AddItem.**

```
Tier 1 (Newbie):
  SetStr(27, 33)  SetDex(27, 33)  SetInt(87, 93)
  SetHits(27, 33) SetStam(27, 33) SetMana(87, 93)
  Skills: Magery 70, EvalInt 60, MagicResist 60, Necromancy 80,
          SpiritSpeak 70, Meditation 60, Hiding 50
  Gear:  AddItem(new Robe(Utility.RandomNeutralHue()))
         AddItem(new Sandals())
         PackItem(new Spellbook())
         PackItem(new NecromancerSpellbook())
  Fame 2000 / Karma -2000 / VirtualArmor 10

Tier 2 (Advanced):
  SetStr(32, 38)  SetDex(32, 38)  SetInt(107, 113)
  SetHits(32, 38) SetStam(32, 38) SetMana(107, 113)
  Skills: Magery 100, EvalInt 90, MagicResist 90, Necromancy 100,
          SpiritSpeak 100, Meditation 60, Focus 60
  Gear:  AddItem(new BoneChest())  AddItem(new BoneArms())
         AddItem(new Sandals())
         PackItem(new Spellbook())
         PackItem(new NecromancerSpellbook())
  Fame 6000 / Karma -6000 / VirtualArmor 28

Tier 3 (Expert):
  SetStr(47, 53)  SetDex(42, 48)  SetInt(127, 133)
  SetHits(47, 53) SetStam(42, 48) SetMana(127, 133)
  Skills: Magery 100, EvalInt 100, MagicResist 100, Necromancy 100,
          SpiritSpeak 100, Swords 100, Tactics 100
  Gear:  AddItem(new BoneChest())   AddItem(new BoneArms())
         AddItem(new BoneLegs())    AddItem(new BoneGloves())
         AddItem(new BoneHelm())    AddItem(new BoneHarvester())
         AddItem(new Sandals())
         PackItem(new Spellbook())
         PackItem(new NecromancerSpellbook())
  Fame 15000 / Karma -15000 / VirtualArmor 35
```

---

### Template 4 — ApplyNinjaDexxerTemplate(int tier)

Fencing + Ninjitsu + Hiding/Stealth. Ambush and Death Strike.

```
Tier 1 (Newbie):
  SetStr(47, 53)  SetDex(67, 73)  SetInt(27, 33)
  SetHits(47, 53) SetStam(67, 73) SetMana(0)
  Skills: Fencing 75, Tactics 70, Ninjitsu 75, Hiding 75,
          Stealth 75, Anatomy 40, Healing 40
  Gear:  AddItem(new Kryss())
         AddItem(new Kasa())
         AddItem(new LeatherChest()) AddItem(new LeatherLegs())
         AddItem(new Sandals())
  Fame 1500 / Karma -1500 / VirtualArmor 18

Tier 2 (Advanced):
  SetStr(52, 58)  SetDex(82, 88)  SetInt(37, 43)
  SetHits(52, 58) SetStam(82, 88) SetMana(0)
  Skills: Fencing 100, Tactics 100, Ninjitsu 100, Hiding 100,
          Stealth 100, Anatomy 60, Healing 40
  Gear:  AddItem(new Kryss())
         AddItem(new Kasa())
         AddItem(new LeatherChest(0x1)) // dark dye hue 1
         AddItem(new LeatherLegs())
         AddItem(new Sandals())
  Fame 5000 / Karma -5000 / VirtualArmor 22

Tier 3 (Expert):
  SetStr(57, 63)  SetDex(107, 113) SetInt(52, 58)
  SetHits(57, 63) SetStam(107, 113) SetMana(0)
  Skills: Fencing 100, Tactics 100, Ninjitsu 100, Hiding 100,
          Stealth 100, Anatomy 100, Healing 50, Focus 50
  Gear:  AddItem(new Kryss())
         AddItem(new Kasa())
         AddItem(new LeatherChest(0x1))
         AddItem(new LeatherLegs())  AddItem(new LeatherArms())
         AddItem(new LeatherGloves()) AddItem(new Sandals())
  Fame 12500 / Karma -12500 / VirtualArmor 28
```

---

### Template 5 — ApplyPaladinTemplate(int tier)

Swords + Chivalry. Enemy of One, Close Wounds, Divine Fury.
**BookOfChivalry must go in pack — never AddItem.**

```
Tier 1 (Newbie):
  SetStr(67, 73)  SetDex(47, 53)  SetInt(27, 33)
  SetHits(67, 73) SetStam(47, 53) SetMana(27, 33)
  Skills: Swords 75, Tactics 70, Chivalry 80, Healing 70,
          Anatomy 70, Parry 55, Hiding 30
  Gear:  AddItem(new Broadsword())
         AddItem(new StuddedChest())  AddItem(new LeatherLegs())
         AddItem(new LeatherArms())   AddItem(new LeatherGorget())
         AddItem(new WoodenShield())  AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new BookOfChivalry())
  Fame 1500 / Karma -1500 / VirtualArmor 22

Tier 2 (Advanced):
  SetStr(77, 83)  SetDex(57, 63)  SetInt(37, 43)
  SetHits(77, 83) SetStam(57, 63) SetMana(37, 43)
  Skills: Swords 100, Tactics 100, Chivalry 100, Healing 90,
          Anatomy 90, Parry 70, Focus 50
  Gear:  AddItem(new Broadsword())
         AddItem(new RingmailChest())  AddItem(new RingmailLegs())
         AddItem(new RingmailArms())   AddItem(new RingmailGloves())
         AddItem(new MetalShield())    AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new BookOfChivalry())
  Fame 5000 / Karma -5000 / VirtualArmor 38

Tier 3 (Expert):
  SetStr(92, 98)  SetDex(72, 78)  SetInt(52, 58)
  SetHits(92, 98) SetStam(72, 78) SetMana(52, 58)
  Skills: Swords 100, Tactics 100, Chivalry 100, Healing 100,
          Anatomy 100, Parry 100, Focus 100
  Gear:  AddItem(new Broadsword())
         AddItem(new PlateChest())  AddItem(new PlateLegs())
         AddItem(new PlateArms())   AddItem(new PlateGloves())
         AddItem(new PlateHelm())   AddItem(new HeaterShield())
         AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new BookOfChivalry())
         PackItem(new GreaterHealPotion())
         PackItem(new GreaterHealPotion())
         PackItem(new GreaterHealPotion())
  Fame 12500 / Karma -12500 / VirtualArmor 50
```

---

### Template 6 — ApplyArcherTemplate(int tier)

Archery + Hiding/Stealth. Ranged opener, hides after kills.
Arrows use `PackItem(new Arrow(count))`.

```
Tier 1 (Newbie):
  SetStr(47, 53)  SetDex(72, 78)  SetInt(22, 28)
  SetHits(47, 53) SetStam(72, 78) SetMana(0)
  Skills: Archery 80, Tactics 75, Anatomy 70, Healing 75,
          Hiding 75, Stealth 75
  Gear:  AddItem(new Bow())
         AddItem(new LeatherChest()) AddItem(new LeatherArms())
         AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new Arrow(50))
  Fame 1500 / Karma -1500 / VirtualArmor 15

Tier 2 (Advanced):
  SetStr(57, 63)  SetDex(87, 93)  SetInt(27, 33)
  SetHits(57, 63) SetStam(87, 93) SetMana(0)
  Skills: Archery 100, Tactics 100, Anatomy 90, Healing 90,
          Hiding 100, Stealth 100, MagicResist 20
  Gear:  AddItem(new CompositeBow())
         AddItem(new StuddedChest())  AddItem(new StuddedLegs())
         AddItem(new StuddedArms())   AddItem(new StuddedGloves())
         AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new Arrow(100))
  Fame 5000 / Karma -5000 / VirtualArmor 28

Tier 3 (Expert):
  SetStr(67, 73)  SetDex(107, 113) SetInt(42, 48)
  SetHits(67, 73) SetStam(107, 113) SetMana(0)
  Skills: Archery 100, Tactics 100, Anatomy 100, Healing 100,
          Hiding 100, Stealth 100, Ninjitsu 100
  Gear:  AddItem(new ElvenCompositeLongbow())
         AddItem(new Kasa())
         AddItem(new LeatherChest()) AddItem(new LeatherLegs())
         AddItem(new LeatherArms())
         AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new Arrow(150))
  Fame 12500 / Karma -12500 / VirtualArmor 25
```

---

### Template 7 — ApplySampireTemplate(int tier)

Swords + Bushido + Necromancy. Vampiric Embrace life leech.
**NecromancerSpellbook and BookOfChivalry (Tier 3 only) go in pack.**

```
Tier 1 (Newbie):
  SetStr(62, 68)  SetDex(57, 63)  SetInt(22, 28)
  SetHits(62, 68) SetStam(57, 63) SetMana(22, 28)
  Skills: Swords 80, Tactics 75, Bushido 80, Necromancy 80,
          SpiritSpeak 75, Parry 60
  Gear:  AddItem(new Katana())
         AddItem(new LeatherChest())  AddItem(new LeatherLegs())
         AddItem(new LeatherArms())   AddItem(new LeatherGorget())
         AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new NecromancerSpellbook())
  Fame 2000 / Karma -2000 / VirtualArmor 22

Tier 2 (Advanced):
  SetStr(72, 78)  SetDex(72, 78)  SetInt(27, 33)
  SetHits(72, 78) SetStam(72, 78) SetMana(27, 33)
  Skills: Swords 100, Tactics 100, Bushido 100, Necromancy 100,
          SpiritSpeak 100, Parry 100
  Gear:  AddItem(new Katana())
         AddItem(new RingmailChest())  AddItem(new RingmailLegs())
         AddItem(new LeatherArms())    AddItem(new LeatherGorget())
         AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new NecromancerSpellbook())
  Fame 7000 / Karma -7000 / VirtualArmor 35

Tier 3 (Expert):
  SetStr(87, 93)  SetDex(82, 88)  SetInt(47, 53)
  SetHits(87, 93) SetStam(82, 88) SetMana(47, 53)
  Skills: Swords 100, Tactics 100, Bushido 100, Necromancy 100,
          SpiritSpeak 100, Parry 100, Chivalry 100
  Gear:  AddItem(new Katana())
         AddItem(new StuddedChest())  AddItem(new StuddedLegs())
         AddItem(new StuddedArms())   AddItem(new StuddedGloves())
         AddItem(new Boots(Utility.RandomNeutralHue()))
         PackItem(new NecromancerSpellbook())
         PackItem(new BookOfChivalry())
         PackItem(new GreaterHealPotion())
         PackItem(new GreaterHealPotion())
         PackItem(new GreaterHealPotion())
  Fame 15000 / Karma -15000 / VirtualArmor 38
```

---

## File 2 — EDIT Scripts/Custom/PlayerKillerNPCs.cs

### What to change

Change the `BasePKNPC` base class declaration from:
```csharp
public abstract class BasePKNPC : BaseCreature
```
to:
```csharp
public abstract class BasePKNPC : BaseFBCombatNPC
```

Then remove from `BasePKNPC` all the methods that now live in `BaseFBCombatNPC`:
- `SetupAppearance()` — delete it (now in base)
- `InitEncounter(Mobile target)` — delete it (now in base)
- `OnThink()` — delete it (now in base)
- `OnGotMeleeAttack(Mobile attacker)` — delete it (now in base)
- `OnDeath(Container c)` — delete it (now in base)
- The `AggroLines` and `KillLines` virtual properties — delete them (now in base)
- The `Kills = 5` line in the constructor — delete it (now in base)
- The `SetupAppearance()` call in the constructor — delete it (now in base)

Keep in `BasePKNPC`:
- Constructor — call `base(ai, mode, range)` — that's it
- Serial constructor
- Serialize/Deserialize (version 0, no fields) — KEEP these untouched

The constructor becomes:
```csharp
protected BasePKNPC(AIType ai, FightMode mode, int range)
    : base(ai, mode, range)
{
    // All shared setup (appearance, Kills=5) now in BaseFBCombatNPC
}

public BasePKNPC(Serial serial) : base(serial) { }
```

**DO NOT touch any of the 21 concrete PK classes.** They extend BasePKNPC and
will automatically inherit BaseFBCombatNPC through the chain.

---

## File 3 — EDIT Scripts/Custom/NovicePlayerKiller.cs

### What to change

Change the class declaration from:
```csharp
public class NovicePlayerKiller : BaseCreature
```
to:
```csharp
public class NovicePlayerKiller : BaseFBCombatNPC
```

Remove `OnGotMeleeAttack` and `OnDeath` overrides — the base class now handles
speech via `AggroLines` / `KillLines`. Add the two property overrides instead:

```csharp
protected override string[] AggroLines => AggroSpeech;
protected override string[] KillLines  => KillSpeech;
```

In the constructor, the appearance setup and `Kills = 5` are now handled by
`BaseFBCombatNPC` — **remove** these lines from the constructor:
```csharp
// REMOVE these — base handles them now:
bool female = Utility.RandomBool();
Body  = female ? 0x191 : 0x190;
Hue   = Utility.RandomSkinHue();
Name  = NameList.RandomName(female ? "female" : "male");
HairItemID = ...
HairHue = ...
```
Keep: `Title = "the ruffian"` — that's NovicePlayerKiller-specific.
Keep: All SetStr/SetDex/SetInt/SetSkill/AddItem/etc. — no stat changes.
Keep: The encounter-mode timer block with `PortalSpeech` — that's specific to this class.
Keep: The `GenerateLoot()` override.
Keep: Serialize/Deserialize (version 0, no fields) — unchanged.

The constructor changes from `base(AIType..., FightMode..., range, 1, 0.2, 0.4)` to
`base(AIType..., FightMode..., range)` because BaseFBCombatNPC passes the trailing
speed params itself: `base(ai, mode, range, 1, 0.1, 0.2)`.

---

## Must NOT touch

- `Scripts/Custom/HunterWanted.cs` — `BaseWantedNPC` extends `BasePKNPC`. It will
  automatically inherit `BaseFBCombatNPC` once BasePKNPC is reparented. No edit needed.
- `Scripts/Custom/HunterSystem.cs`
- `Scripts/Custom/HunterCreatures.cs`
- `Scripts/Custom/FBZones.cs`
- `Scripts/Custom/FBEventBus.cs`
- `Scripts/Custom/PKEncounterSystem.cs`
- `Scripts/Custom/GraveyardPKEncounter.cs`
- Any file in `Server/` or `Scripts/` (outside `Scripts/Custom/`)

---

## Interfaces to respect

- `namespace Server.Custom` — all three files
- `BaseFBCombatNPC` lives in `Scripts/Custom/BaseFBCombatNPC.cs`
- The `InitEncounter(Mobile target)` method signature is used by
  `GraveyardPKEncounter.cs` and `PKEncounterSystem.cs` — do not change it
- `BasePKNPC.AggroLines` and `BasePKNPC.KillLines` are overridden by all 21
  concrete classes — they must exist on `BaseFBCombatNPC` with matching signatures:
  `protected virtual string[] AggroLines => new string[0];`
  `protected virtual string[] KillLines  => new string[0];`

---

## Definition of done

- [ ] `BaseFBCombatNPC.cs` compiles with no errors
- [ ] All 7 `Apply*Template` methods are present and implement the equipment rule
      (books via PackItem, weapons/armour via AddItem)
- [ ] `BasePKNPC` extends `BaseFBCombatNPC` — duplicate methods removed
- [ ] All 21 concrete PK classes in `PlayerKillerNPCs.cs` are UNTOUCHED
- [ ] `NovicePlayerKiller` extends `BaseFBCombatNPC` — appearance code removed,
      `AggroLines`/`KillLines` properties added
- [ ] `BaseWantedNPC` in `HunterWanted.cs` is UNTOUCHED (inherits BaseFBCombatNPC
      automatically via the BasePKNPC chain)
- [ ] No `Serialize`/`Deserialize` override in `BaseFBCombatNPC` (no fields, would
      break existing save files)
- [ ] `namespace Server.Custom` throughout
- [ ] `using Server.Spells.Necromancy` is NOT needed — no necro spells are cast
      directly in this file

---

## Done signal

Commit this file as your final commit:
  `ToDo Cowork/DONE - Claude2 - BaseFBCombatNPC - 2026-05-24.md`

Contents: `Done: BaseFBCombatNPC. Files created/edited: Scripts/Custom/BaseFBCombatNPC.cs (CREATE), Scripts/Custom/PlayerKillerNPCs.cs (EDIT BasePKNPC parent), Scripts/Custom/NovicePlayerKiller.cs (EDIT parent + cleanup).`
