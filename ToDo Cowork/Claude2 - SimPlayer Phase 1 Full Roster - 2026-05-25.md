# Claude 2 Work Instruction — SimPlayer Phase 1 Full Roster

**Branch:** `claude2/simplayer-phase1-fullroster`  
**Depends on:** SimPlayer Phase 1 (done — Wanderers working in prod)

---

## Goal

Expand the SimPlayer system from 4 Wanderers to the full Phase 1 roster of 15 members across 5 guilds. The Wanderers are already working — do not touch them. Add the remaining 11 members across 4 guilds.

---

## Read These Before Starting

1. `Design/GuildSystem_FullDesignDoc.txt` — guild templates, stats, skills, gear
2. `Design/PlayerSimulator_DesignDoc.txt` — Phase 1 launch roster (page: LAUNCH ROSTER)
3. `Scripts/Custom/SimPlayer.cs` — existing base class (read carefully before modifying)
4. `Scripts/Custom/PlayerSimulatorManager.cs` — roster creation (where to add new members)
5. `Scripts/Custom/SimChatBrain.cs` — add chat lines here
6. `Scripts/Custom/ScheduleProfile.cs` — add schedule profiles here
7. `Scripts/Custom/FBZones.cs` — home locations already defined, do NOT modify
8. `CLAUDE.md` — ServUO patterns

---

## Phase 1 Full Roster

| Guild | Members to Add |
|-------|---------------|
| The Wanderers | DONE — do not touch |
| Craftsmen's League | Garrett the Smith, Fisherman Pete, Woodcutter Bram |
| Iron Company | Sergeant Vale, Brother Kael, Ironhide |
| Arcane Brotherhood | Scholar Aldric, Mistress Verna, The Recluse |
| Silver Wolves | Captain Rowena, Scout Finn |

---

## Step 1 — Refactor SimPlayer to support multiple guild templates

The current `SimPlayer` constructor hardcodes Wanderer warrior stats. You need to refactor it so each guild can define its own stats, skills, and gear.

**Add a protected virtual method `ApplyTemplate()` to SimPlayer:**

```csharp
// Called from constructor after identity fields are set.
// Subclasses override this to apply guild-specific stats, skills, gear.
protected virtual void ApplyTemplate()
{
    // Default: Wanderer Warrior template
    SetStr(60, 60);
    SetDex(55, 55);
    SetInt(35, 35);
    SetHits(80, 80);
    SetSkill(SkillName.Swords,  65.0, 65.0);
    SetSkill(SkillName.Tactics, 60.0, 60.0);
    SetSkill(SkillName.Anatomy, 55.0, 55.0);
    SetSkill(SkillName.Healing, 60.0, 60.0);
    VirtualArmor = 15;
    Fame  = 0;
    Karma = 1000;
    Kills = 0;

    AddItem(new LeatherChest());
    AddItem(new LeatherLegs());
    AddItem(new LeatherGorget());
    AddItem(new LeatherArms());
    AddItem(new LeatherGloves());
    AddItem(new Boots());
    AddItem(new Longsword());
}
```

**Remove the hardcoded stats/skills/gear block from the SimPlayer constructor body** and replace it with a single call to `ApplyTemplate()`.

The constructor should now look like:
```csharp
public SimPlayer(string guildName, string memberName, Point3D homeLocation,
                 SpawnZone homeZone, ScheduleProfile schedule)
    : base(AIType.AI_Melee, FightMode.None, 10)
{
    _guildName    = guildName;
    _memberName   = memberName;
    _homeLocation = homeLocation;
    _homeZone     = homeZone;
    _schedule     = schedule;
    _chatBrain    = new SimChatBrain(guildName);

    Name  = memberName;
    Title = guildName;

    ApplyTemplate(); // <-- replaces all the hardcoded stat/skill/gear blocks

    MoveToWorld(Point3D.Zero, Map.Internal);
    _state         = SimState.OnCooldown;
    _cooldownUntil = DateTime.UtcNow;
}
```

---

## Step 2 — Create guild subclasses

Create a new file: `Scripts/Custom/SimPlayerGuilds.cs`

Each class extends `SimPlayer` and overrides `ApplyTemplate()`. Stats come from `Design/GuildSystem_FullDesignDoc.txt`.

**IMPORTANT — equipment rule (from CLAUDE.md):**
- Weapons / armour: `AddItem(...)` 
- Spellbooks: `PackItem(...)` — NEVER AddItem

### Craftsmen's League

```csharp
public class CraftsmensLeagueSimPlayer : SimPlayer
{
    public CraftsmensLeagueSimPlayer(string memberName, Point3D home, SpawnZone zone, ScheduleProfile schedule)
        : base(FBGuilds.CraftsmenLeague, memberName, home, zone, schedule) { }

    public CraftsmensLeagueSimPlayer(Serial serial) : base(serial) { }

    protected override void ApplyTemplate()
    {
        // Blacksmith template (use for all 3 Phase 1 members)
        SetStr(70, 70);
        SetDex(50, 50);
        SetInt(30, 30);
        SetHits(80, 80);
        SetSkill(SkillName.Blacksmith, 100.0, 100.0);
        SetSkill(SkillName.Mining,     100.0, 100.0);
        SetSkill(SkillName.ArmsLore,    80.0,  80.0);
        SetSkill(SkillName.Tinkering,   60.0,  60.0);
        SetSkill(SkillName.Swords,      30.0,  30.0); // flee only
        VirtualArmor = 10;
        Fame  = 0;
        Karma = 1000;
        Kills = 0;
        ActiveSpeed = 0.3; // moves slower — working pace

        AddItem(new LeatherChest());
        AddItem(new LeatherLegs());
        AddItem(new Boots());
        AddItem(new SmithHammer());
    }
}
```

### Iron Company

```csharp
public class IronCompanySimPlayer : SimPlayer
{
    public IronCompanySimPlayer(string memberName, Point3D home, SpawnZone zone, ScheduleProfile schedule)
        : base(FBGuilds.IronCompany, memberName, home, zone, schedule) { }

    public IronCompanySimPlayer(Serial serial) : base(serial) { }

    protected override void ApplyTemplate()
    {
        // Heavy Warrior template
        SetStr(85, 85);
        SetDex(65, 65);
        SetInt(30, 30);
        SetHits(120, 120);
        SetSkill(SkillName.Swords,       100.0, 100.0);
        SetSkill(SkillName.Tactics,      100.0, 100.0);
        SetSkill(SkillName.Anatomy,       90.0,  90.0);
        SetSkill(SkillName.Healing,       90.0,  90.0);
        SetSkill(SkillName.Parry,        100.0, 100.0);
        SetSkill(SkillName.MagicResist,   80.0,  80.0);
        SetSkill(SkillName.Chivalry,      80.0,  80.0);
        VirtualArmor = 40;
        Fame  = 2000;
        Karma = 2000;
        Kills = 0;

        AddItem(new PlateChest());
        AddItem(new PlateLegs());
        AddItem(new PlateArms());
        AddItem(new PlateGloves());
        AddItem(new PlateGorget());
        AddItem(new PlateHelm());
        AddItem(new Boots());
        AddItem(new Longsword());
        AddItem(new HeaterShield());
        PackItem(new BookOfChivalry());
    }
}
```

### Arcane Brotherhood

```csharp
public class ArcaneBrotherhoodSimPlayer : SimPlayer
{
    public ArcaneBrotherhoodSimPlayer(string memberName, Point3D home, SpawnZone zone, ScheduleProfile schedule)
        : base(FBGuilds.ArcaneBrotherhood, memberName, home, zone, schedule) { }

    public ArcaneBrotherhoodSimPlayer(Serial serial) : base(serial) { }

    protected override void ApplyTemplate()
    {
        // War Mage template
        SetStr(30, 30);
        SetDex(35, 35);
        SetInt(115, 115);
        SetHits(60, 60);
        SetSkill(SkillName.Magery,      100.0, 100.0);
        SetSkill(SkillName.EvalInt,     100.0, 100.0);
        SetSkill(SkillName.MagicResist, 100.0, 100.0);
        SetSkill(SkillName.Meditation,   90.0,  90.0);
        SetSkill(SkillName.Wrestling,    80.0,  80.0);
        SetSkill(SkillName.Focus,        70.0,  70.0);
        SetSkill(SkillName.Inscribe,     60.0,  60.0);
        VirtualArmor = 10;
        Fame  = 2000;
        Karma = 2000;
        Kills = 0;

        AddItem(new Robe());
        AddItem(new Sandals());
        AddItem(new WizardsHat());
        PackItem(new Spellbook()); // always PackItem for spellbooks
    }
}
```

### Silver Wolves

```csharp
public class SilverWolvesSimPlayer : SimPlayer
{
    public SilverWolvesSimPlayer(string memberName, Point3D home, SpawnZone zone, ScheduleProfile schedule)
        : base(FBGuilds.SilverWolves, memberName, home, zone, schedule) { }

    public SilverWolvesSimPlayer(Serial serial) : base(serial) { }

    protected override void ApplyTemplate()
    {
        // Wolf Warrior template (used for both Phase 1 members)
        SetStr(80, 80);
        SetDex(65, 65);
        SetInt(35, 35);
        SetHits(110, 110);
        SetSkill(SkillName.Swords,       100.0, 100.0);
        SetSkill(SkillName.Tactics,      100.0, 100.0);
        SetSkill(SkillName.Anatomy,       90.0,  90.0);
        SetSkill(SkillName.Healing,       90.0,  90.0);
        SetSkill(SkillName.Parry,         90.0,  90.0);
        SetSkill(SkillName.MagicResist,   80.0,  80.0);
        SetSkill(SkillName.DetectHidden,  50.0,  50.0);
        VirtualArmor = 35;
        Fame  = 2000;
        Karma = 3000;
        Kills = 0;

        AddItem(new ChainChest());
        AddItem(new ChainLegs());
        AddItem(new LeatherGorget());
        AddItem(new LeatherArms());
        AddItem(new LeatherGloves());
        AddItem(new Boots());
        AddItem(new Longsword());
        AddItem(new HeaterShield());
    }
}
```

---

## Step 3 — Add schedule profiles to ScheduleProfile.cs

Add these static factory methods to `ScheduleProfile`:

```csharp
// Craftsmen's League — workday schedule, daytime focused
public static ScheduleProfile CraftsmensLeague(int driftMinutes = 0)
{
    double[] chance = new double[24];
    for (int h = 0; h < 24; h++)
    {
        if      (h >= 0  && h < 7)  chance[h] = 0.10; // sleeping
        else if (h >= 7  && h < 18) chance[h] = 0.85; // working day
        else if (h >= 18 && h < 21) chance[h] = 0.50; // evening wind-down
        else                        chance[h] = 0.20; // night
    }
    return new ScheduleProfile(chance, driftMinutes);
}

// Iron Company — prime time heavy, always has some members on
public static ScheduleProfile IronCompany(int driftMinutes = 0)
{
    double[] chance = new double[24];
    for (int h = 0; h < 24; h++)
    {
        if      (h >= 0  && h < 8)  chance[h] = 0.30; // skeleton crew
        else if (h >= 8  && h < 18) chance[h] = 0.60; // operational
        else if (h >= 18 && h < 23) chance[h] = 0.95; // prime ops
        else                        chance[h] = 0.40; // late night
    }
    return new ScheduleProfile(chance, driftMinutes);
}

// Arcane Brotherhood — scholarly, late night heavy
public static ScheduleProfile ArcaneBrotherhood(int driftMinutes = 0)
{
    double[] chance = new double[24];
    for (int h = 0; h < 24; h++)
    {
        if      (h >= 0  && h < 4)  chance[h] = 0.70; // late night research
        else if (h >= 4  && h < 10) chance[h] = 0.20; // sleeping
        else if (h >= 10 && h < 18) chance[h] = 0.50; // afternoon study
        else if (h >= 18 && h < 24) chance[h] = 0.80; // evening sessions
    }
    return new ScheduleProfile(chance, driftMinutes);
}

// Silver Wolves — patrol schedule, consistent presence throughout day
public static ScheduleProfile SilverWolves(int driftMinutes = 0)
{
    double[] chance = new double[24];
    for (int h = 0; h < 24; h++)
    {
        if      (h >= 0  && h < 6)  chance[h] = 0.40; // night watch
        else if (h >= 6  && h < 22) chance[h] = 0.75; // patrol hours
        else                        chance[h] = 0.50; // evening patrol
    }
    return new ScheduleProfile(chance, driftMinutes);
}
```

---

## Step 4 — Add chat lines to SimChatBrain.cs

Add these line tables and extend `GetAmbientLines()`:

```csharp
private static readonly string[] CraftsmensLeagueAmbient = {
    "These ingots won't smelt themselves.",
    "Anyone need repairs? I can fix that armour.",
    "The forge has been busy today.",
    "Good ore is getting harder to find near Britain.",
    "I heard the mines near Wrong are rich but dangerous.",
    "A well-made blade lasts a lifetime. Most people forget that.",
    "Selling leather? I'll pay fair.",
    "My back's killing me from the anvil work.",
};

private static readonly string[] IronCompanyAmbient = {
    "Hold the line!",
    "Blood Pact spotted near Destard yesterday.",
    "Anyone heading to a champion spawn? We're forming up.",
    "Support on my position.",
    "Keep formation. Always.",
    "We lost two at the last spawn. Not again.",
    "Iron Company doesn't run. Remember that.",
    "The spawn resets in an hour — be ready.",
};

private static readonly string[] ArcaneBrotherhoodAmbient = {
    "The deeper circles require absolute focus.",
    "These reagent prices in Moonglow are robbery.",
    "I've been studying a new inscription pattern.",
    "The Void grows bolder. We must remain vigilant.",
    "Magic is precision. Never forget that.",
    "Deceit level three is cleaner since we swept it last week.",
    "Have you read the latest transcription from the archive?",
    "Mana recovery has been slower than usual today.",
};

private static readonly string[] SilverWolvesAmbient = {
    "Stay alert. Blood Pact could be anywhere.",
    "We protect the blue. That's the oath.",
    "If you see red names near Britain, call it out.",
    "The Shadow Hand was spotted near the bank earlier.",
    "Always move in threes. Never alone.",
    "Someone's been watching the bank. Not one of ours.",
    "Clear skies mean nothing in this city.",
    "We run toward danger, not away from it.",
};
```

Update `GetAmbientLines()`:
```csharp
private string[] GetAmbientLines()
{
    if (_guildName == FBGuilds.Wanderers)         return WandererAmbient;
    if (_guildName == FBGuilds.CraftsmenLeague)   return CraftsmensLeagueAmbient;
    if (_guildName == FBGuilds.IronCompany)       return IronCompanyAmbient;
    if (_guildName == FBGuilds.ArcaneBrotherhood) return ArcaneBrotherhoodAmbient;
    if (_guildName == FBGuilds.SilverWolves)      return SilverWolvesAmbient;
    return WandererAmbient; // fallback
}
```

---

## Step 5 — Add 11 new members to PlayerSimulatorManager.cs

Add these after the Wanderers block in `CreateRosterIfEmpty()`.  
Home locations come from `FBZones.*_Home` — already defined.

```csharp
// THE CRAFTSMEN'S LEAGUE — 3 members
Point3D craftHome = FBZones.CraftsmensLeague_Home;
_allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Garrett the Smith",
    craftHome, SpawnZone.Britain_Roads, ScheduleProfile.CraftsmensLeague(0)));
_allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Fisherman Pete",
    craftHome, SpawnZone.Britain_Roads, ScheduleProfile.CraftsmensLeague(20)));
_allSimPlayers.Add(new CraftsmensLeagueSimPlayer("Woodcutter Bram",
    craftHome, SpawnZone.Britain_Roads, ScheduleProfile.CraftsmensLeague(-15)));

// IRON COMPANY — 3 members
Point3D ironHome = FBZones.IronCompany_Home;
_allSimPlayers.Add(new IronCompanySimPlayer("Sergeant Vale",
    ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(0)));
_allSimPlayers.Add(new IronCompanySimPlayer("Brother Kael",
    ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(10)));
_allSimPlayers.Add(new IronCompanySimPlayer("Ironhide",
    ironHome, SpawnZone.Britain_Roads, ScheduleProfile.IronCompany(-10)));

// ARCANE BROTHERHOOD — 3 members
Point3D arcaneHome = FBZones.ArcaneBrotherhood_Home;
_allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Scholar Aldric",
    arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(0)));
_allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("Mistress Verna",
    arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(25)));
_allSimPlayers.Add(new ArcaneBrotherhoodSimPlayer("The Recluse",
    arcaneHome, SpawnZone.Britain_Roads, ScheduleProfile.ArcaneBrotherhood(-30)));

// SILVER WOLVES — 2 members
Point3D wolvesHome = FBZones.SilverWolves_Home;
_allSimPlayers.Add(new SilverWolvesSimPlayer("Captain Rowena",
    wolvesHome, SpawnZone.Britain_Roads, ScheduleProfile.SilverWolves(0)));
_allSimPlayers.Add(new SilverWolvesSimPlayer("Scout Finn",
    wolvesHome, SpawnZone.Britain_Roads, ScheduleProfile.SilverWolves(15)));
```

---

## Important notes

**Do NOT modify:**
- `FBZones.cs` — home locations already exist
- `FBEventBus.cs`
- The Wanderers in `PlayerSimulatorManager.cs` — leave them exactly as-is
- Any vanilla ServUO files

**Equipment rule (mandatory):**
- Spellbooks → `PackItem(new Spellbook())` — NEVER `AddItem`
- BookOfChivalry → `PackItem(new BookOfChivalry())` — NEVER `AddItem`
- All armour/weapons → `AddItem(...)`

**Serialization:**
- The new subclasses call `base(serial)` which chains to `SimPlayer(Serial serial)` then `BaseFBCombatNPC(Serial serial)`
- No new fields in the subclasses = no new Serialize/Deserialize needed
- The guild template is re-applied at construction time, not saved

**Roster creation vs. deserialization:**
- `CreateRosterIfEmpty()` only runs once (when `_allSimPlayers.Count == 0`)
- After the first world save, SimPlayers persist and are loaded via `reader.ReadMobile()`
- The subclass type is preserved by the world save system — deserialization calls the correct subclass `Serial` constructor

---

## Verification checklist

1. Server starts → log shows `[SimPlayer] Roster created: 15 SimPlayers.`
2. In-game: members from all 5 guilds visible near Britain bank area
3. `[props` on each type → correct GuildName shown
4. Iron Company members wearing plate armour
5. Arcane Brotherhood members in robes
6. Silver Wolves in chain
7. `[worldsave` + restart → all 15 members return after load
8. Kill a Silver Wolf → `[repgump` shows Silver Wolves standing decreased, Paladin Order decreased

---

## Signal completion

Commit all changed files to branch `claude2/simplayer-phase1-fullroster`, then commit:
`ToDo Cowork/DONE - Claude2 - SimPlayer Phase 1 Full Roster - 2026-05-25.md`
