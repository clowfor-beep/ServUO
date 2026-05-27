# Claude2 Work Instruction — SimGuilds Tier 2 and Tier 3
Date: 2026-05-27
Branch: claude2/simguilds-tier2-tier3

## Context files to read (in this order)
1. `Design/COWORK_HANDOVER.md`                              ← always first
2. `Design/GuildSystem_FullDesignDoc.txt`                   ← all 12 guild templates
3. `Scripts/Custom/SimPlayerGuilds.cs`                      ← existing guild classes to match pattern
4. `Scripts/Custom/PlayerSimulatorManager.cs`               ← where to register new members
5. `Scripts/Custom/ScheduleProfile.cs`                      ← schedule factory methods to add
6. `Scripts/Custom/SimChatBrain.cs`                         ← ambient speech arrays to extend
7. `Scripts/Custom/ReputationSystem.cs`                     ← FBGuilds constants (all 12 already defined)
8. `Scripts/Custom/FBZones.cs`                              ← home Point3D for every guild (all 12 already defined)

## Task

Implement the **6 remaining SimGuild subclasses** (Guilds 7–12) and register **3 members each** in
`PlayerSimulatorManager.cs`. Also add the matching `ScheduleProfile` factory methods and `SimChatBrain`
ambient speech arrays.

The 6 guilds to implement:

| # | Class name                  | FBGuilds constant         | Alignment | Tier |
|---|-----------------------------|---------------------------|-----------|------|
| 7 | `PaladinOrderSimPlayer`     | `FBGuilds.PaladinOrder`   | Blue      | 3    |
| 8 | `DeadWatchersSimPlayer`     | `FBGuilds.DeadWatchers`   | Perm Grey | 3    |
| 9 | `DreadHuntersSimPlayer`     | `FBGuilds.DreadHunters`   | Blue/Grey | 3    |
|10 | `BloodPactSimPlayer`        | `FBGuilds.BloodPact`      | Red       | 3    |
|11 | `TheVoidSimPlayer`          | `FBGuilds.TheVoid`        | Red       | 3    |
|12 | `ShadowbladeSimPlayer`      | `FBGuilds.Shadowblade`    | Red/Grey  | 3    |

---

### Templates — read GuildSystem_FullDesignDoc.txt carefully for each guild

Below are the key numbers pulled from the design doc. Use the **first template listed** for each guild
as the single `ApplyTemplate()` implementation (one class per guild is enough for Phase 1 — not one
class per template variant):

**Guild 7 — Paladin Order** (StatCap 225)
- Skills: Swords 100, Tactics 100, Anatomy 100, Healing 100, Parry 100, Chivalry 100, MagicResist 100
- Stats: Str 90, Dex 80, Int 55 | Hits 200
- Gear: PlateChest, PlateLegs, PlateArms, PlateGloves, PlateHelm, HeaterShield (white/silver hue ~1153)
- BookOfChivalry → `PackItem()` NOT `AddItem()`
- Karma 5000, Fame 10000, Kills 0
- AlwaysAttackable = false, AlwaysInnocent = true (blue — override BaseFBCombatNPC defaults)

**Guild 8 — Dead Watchers** (StatCap 225)
- Skills: Swords 100, Tactics 100, Anatomy 100, Healing 100, Necromancy 100, SpiritSpeak 100, Parry 100
- Stats: Str 90, Dex 80, Int 55 | Hits 200
- Gear: BoneChest, BoneLegs, BoneArms, BoneGloves, BoneHelm (Hue 0x455 — dark)
- NecromancerSpellbook → `PackItem()`
- Karma -100 (perm grey), Fame 8000, Kills 0
- AlwaysAttackable = true (grey — fights reds and greys, neutral to blues)
- Note from design doc: "Blue-testing mechanic SAVED FOR LATER — do not implement"

**Guild 9 — Dread Hunters** (StatCap 300, skill cap 880)
- Skills: Swords 120, Tactics 120, Anatomy 120, Healing 120, Parry 120, MagicResist 100, Chivalry 100, Focus 80
- Stats: Str 125, Dex 125, Int 50 | Hits 300
- Gear: PlateChest, PlateLegs, PlateArms, PlateGloves, PlateHelm, HeaterShield (dark grey hue ~0x497)
- BookOfChivalry → `PackItem()`
- Karma 1000, Fame 15000, Kills 0
- AlwaysAttackable = false, AlwaysInnocent = true

**Guild 10 — Blood Pact** (StatCap 250, skill cap 800)
- Use NecroMage template (most common per design doc):
  Magery 100, EvalInt 100, MagicResist 100, Necromancy 100, SpiritSpeak 100, Meditation 100, Wrestling 100, Hiding 100
- Stats: Str 50, Dex 75, Int 125 | Hits 150
- Gear: black Robe (Hue 0x455), Sandals
- Spellbook → `PackItem()`, NecromancerSpellbook → `PackItem()`
- Karma -5000, Fame 10000, Kills 10 (red — murderer flag)
- AlwaysAttackable = true
- AlwaysMurderer = true (override — red NPC)
- Home zone: `FBZones.BloodPact_Home` (Destard outskirts — NOT a city)

**Guild 11 — The Void** (StatCap 270, skill cap 850)
- All 12 members identical (design doc says so):
  Magery 120, EvalInt 120, MagicResist 120, Necromancy 120, SpiritSpeak 120, Meditation 100, Wrestling 100, Hiding 50
- Stats: Str 60, Dex 85, Int 125 | Hits 170
- Gear: black Robe (Hue 0x455), Sandals
- Spellbook → `PackItem()`, NecromancerSpellbook → `PackItem()`
- Karma -8000, Fame 15000, Kills 15 (red — murderer)
- AlwaysAttackable = true
- AlwaysMurderer = true
- Home zone: `FBZones.TheVoid_Home` (Deceit outskirts)

**Guild 12 — Shadowblade** (StatCap 250, skill cap 800)
- Use Shadow Assassin template:
  Ninjitsu 100, Hiding 100, Stealth 100, Fencing 100, Tactics 100, Anatomy 100, Healing 100, DetectHidden 100
- Stats: Str 75, Dex 125, Int 50 | Hits 160
- Gear: black NinjaSuit (Hue 0x455), NinjaHood, Boots; weapon: Kryss or AssassinSpike
- Karma -500 (grey), Fame 5000, Kills 0 (grey — not red)
- AlwaysAttackable = true
- AlwaysMurderer = false (grey, not red)
- Home zone: `FBZones.Shadowblade_Home` (Wrong outskirts)

---

### ScheduleProfile factory methods to add (in ScheduleProfile.cs)

Follow the exact same pattern as existing methods. Add:

```
PaladinOrder(driftMinutes)  — heavy prime time, late nights: on 16:00-02:00, off 02:00-16:00
DeadWatchers(driftMinutes)  — late night hunters: on 20:00-04:00, off 04:00-20:00
DreadHunters(driftMinutes)  — extreme prime time: on 17:00-01:00, off 01:00-17:00
BloodPact(driftMinutes)     — prime time predators: on 18:00-00:00, off 00:00-18:00
TheVoid(driftMinutes)       — all hours, loner: on 00:00-23:59 (always)
Shadowblade(driftMinutes)   — opportunistic, prime time: on 17:00-23:00, off 23:00-17:00
```

---

### SimChatBrain ambient lines to add (in SimChatBrain.cs)

Follow the exact same pattern as existing arrays. Add one array per guild (4-6 lines each).
Wire them in `GetAmbientLines()` with `if (_guildName == FBGuilds.X) return XAmbient;`.

Suggested tone:
- PaladinOrder: honour, duty, hunting evil, "Evil will not stand while we draw breath"
- DeadWatchers: grim, cryptic, observe and judge, "We watch. We wait. We end."
- DreadHunters: matter-of-fact professional hunters, "The deep dungeons hold no mysteries for us"
- BloodPact: menacing, predatory, calculating, "They should not have come here alone"
- TheVoid: chaotic, barely coherent, "Everything burns. Everything ends."
- Shadowblade: cold, mercenary, precise, "Contract accepted."

---

### PlayerSimulatorManager.cs — add 3 members per new guild

Add after the existing ShadowHand block. 3 members per guild, using the guild's home zone from FBZones
and the new ScheduleProfile factory method. Use immersive fantasy names consistent with each guild's tone.

Example pattern (copy from existing guilds):
```csharp
// PALADIN ORDER -- 3 members
Point3D paladinHome = FBZones.PaladinOrder_Home;
_allSimPlayers.Add(new PaladinOrderSimPlayer("Commander Aldric",
    paladinHome, SpawnZone.Britain_Roads, ScheduleProfile.PaladinOrder(0)));
...
```

Update the `Console.WriteLine` count at the bottom to reflect the new total (was 18, becomes 36).

---

## File(s) to create or edit

- `Scripts/Custom/SimPlayerGuilds.cs`   (EDIT — append 6 new classes at the bottom)
- `Scripts/Custom/ScheduleProfile.cs`   (EDIT — append 6 new factory methods)
- `Scripts/Custom/SimChatBrain.cs`      (EDIT — append 6 ambient arrays + wire in GetAmbientLines)
- `Scripts/Custom/PlayerSimulatorManager.cs` (EDIT — add 18 new registrations, update count log)

## Must NOT touch
- `Scripts/Custom/SimPlayer.cs`           (base class — no changes)
- `Scripts/Custom/ReputationSystem.cs`    (FBGuilds already complete)
- `Scripts/Custom/FBZones.cs`             (all home points already defined)
- Any file in `Server/` or vanilla `Scripts/`

## Interfaces to respect

```csharp
// Guild subclass constructor signature (match exactly):
public XxxSimPlayer(string memberName, Point3D home, SpawnZone zone, ScheduleProfile schedule)
    : base(FBGuilds.Xxx, memberName, home, zone, schedule) { }

public XxxSimPlayer(Serial serial) : base(serial) { }

// ScheduleProfile factory (match existing pattern):
public static ScheduleProfile XxxGuild(int driftMinutes = 0) { ... }

// SimChatBrain wiring:
if (_guildName == FBGuilds.Xxx) return XxxAmbient;

// AlwaysMurderer override required for red guilds (Blood Pact, The Void):
public override bool AlwaysMurderer => true;

// AlwaysInnocent override required for blue guilds (Paladin Order, Dread Hunters):
public override bool AlwaysInnocent => true;
public override bool AlwaysAttackable => false;
```

## Definition of done

- [ ] 6 new SimPlayer subclasses in SimPlayerGuilds.cs, each with Serialize/Deserialize versioned at 0
- [ ] Equipment rule followed: all spellbooks and books of chivalry via `PackItem()` not `AddItem()`
- [ ] AlwaysMurderer = true on Blood Pact and The Void (red guilds)
- [ ] AlwaysInnocent = true / AlwaysAttackable = false on Paladin Order and Dread Hunters (blue)
- [ ] Dead Watchers, Shadowblade: AlwaysAttackable = true (grey — attackable but not murderer)
- [ ] 6 ScheduleProfile factory methods added
- [ ] 6 ambient speech arrays added + wired in GetAmbientLines()
- [ ] 18 new SimPlayer registrations in PlayerSimulatorManager.cs (3 per guild)
- [ ] Console.WriteLine count updated
- [ ] Red guild home zones use FBZones.BloodPact_Home / TheVoid_Home / Shadowblade_Home (NOT city zones)
- [ ] Code compiles (read it carefully — check all SkillName values are valid ServUO names)
- [ ] Follows namespace Server.Custom convention throughout

## Done signal
Commit this file as your final commit:
  `ToDo Cowork/DONE - Claude2 - SimGuilds Tier2 and Tier3 - 2026-05-27.md`

Contents: "Done: SimGuilds Tier 2 and Tier 3. Files edited: SimPlayerGuilds.cs, ScheduleProfile.cs, SimChatBrain.cs, PlayerSimulatorManager.cs."
