# Claude 2 — SimPlayer Phase 2: Banking Scene + Shadow Hand Guild

**Branch:** `claude2/simplayer-phase2-banking-shadowhand`  
**Base branch:** `main` (Phase 1 full roster already merged)  
**Shard:** Forsaken Britannia | ServUO C# (.NET Framework 4.7+)  
**Auto-compiled on server restart — no manual build step**

---

## Goal

Two deliverables in one branch:

1. **Banking behavior** — all 15 existing SimPlayers periodically visit Britain bank, say "bank", idle with bank-chat ambient lines. This is the highest-ROI feature: Britain bank immediately looks populated.

2. **Shadow Hand guild** — 3 new grey thief SimPlayers added to the roster. They hide in crowds near the bank, flee Silver Wolves, and feel suspicious without actually fighting anyone.

---

## Context: What Phase 1 Built

Phase 1 is fully merged and working. The 15 SimPlayers do:
- Idle ↔ Travelling loop (random nearby points within 20 tiles of home)
- Ambient speech when idle and other mobiles are nearby
- Schedule-based activation (active pool managed by PlayerSimulatorManager)
- Death → cooldown → re-activation lifecycle

What they do NOT yet do (this phase adds it):
- Visit the bank
- Say anything bank-specific
- Hide
- React to rival guild members

---

## Files to Read First

Before writing a single line of code, read these files completely:

```
Scripts/Custom/SimStateMachine.cs     — state enum (add Banking here)
Scripts/Custom/SimPlayer.cs           — base class (main changes here)
Scripts/Custom/SimChatBrain.cs        — speech system (add bank lines + Shadow Hand)
Scripts/Custom/ScheduleProfile.cs     — schedules (add ShadowHand)
Scripts/Custom/SimPlayerGuilds.cs     — guild subclasses (add ShadowHandSimPlayer)
Scripts/Custom/PlayerSimulatorManager.cs — roster manager (add 3 Shadow Hand members)
Scripts/Custom/FBZones.cs             — look up: BountyBoard_Britain_Bank, ShadowHand_Home
Scripts/Custom/ReputationSystem.cs    — look up: FBGuilds.ShadowHand constant string value
```

---

## Part 1 — Banking Behavior (all SimPlayers)

### 1A. SimStateMachine.cs — add Banking state

Add `Banking` to the enum. Do not remove or reorder existing values.

```csharp
public enum SimState
{
    Idle,        // standing still, may speak
    Travelling,  // moving toward next location
    Banking,     // at Britain bank — say "bank", loiter, bank chat
    Dead,        // just died, waiting for cooldown
    OnCooldown   // invisible, not in world
}
```

### 1B. SimPlayer.cs — banking fields, methods, and virtual hook

**New private fields** (add after existing `_idleUntil` field):

```csharp
// Banking
private bool     _inBankingCycle  = false;
private DateTime _nextBankingTime = DateTime.MinValue; // immediately eligible on first activation
private DateTime _bankUntil;
private bool     _travelIsBankTrip = false;
```

**New protected virtual hook** — add this at the end of `TickIdle()`, just before the closing brace. This lets guild subclasses inject idle behavior without overriding the entire method:

```csharp
private void TickIdle()
{
    _chatBrain.TryAmbientSpeech(this);

    // Trigger banking trip if due
    if (!_inBankingCycle && DateTime.UtcNow >= _nextBankingTime)
        StartBankingTrip();

    if (DateTime.UtcNow >= _idleUntil)
        StartTravelling();

    // Guild-specific idle hook (subclasses override)
    OnTickIdle();
}

/// <summary>
/// Called every OnThink tick while state is Idle.
/// Override in guild subclasses for custom idle behaviour (hiding, fleeing, etc.)
/// Base implementation is empty.
/// </summary>
protected virtual void OnTickIdle() { }
```

**New method: StartBankingTrip()**

```csharp
private void StartBankingTrip()
{
    // Pick a tile within ±4 of the Britain bank NPC coord
    Point3D bankBase = FBZones.BountyBoard_Britain_Bank;
    int bx = bankBase.X + Utility.RandomMinMax(-4, 4);
    int by = bankBase.Y + Utility.RandomMinMax(-4, 4);
    int bz = Map.Felucca.GetAverageZ(bx, by);

    _travelIsBankTrip = true;
    _travelDest       = new Point3D(bx, by, bz);
    _travelTimeout    = DateTime.UtcNow + TimeSpan.FromSeconds(120);
    _state            = SimState.Travelling;
    _inBankingCycle   = true;
}
```

**Modify ArriveAtDest()** — switch to Banking instead of Idle when it's a bank trip:

```csharp
private void ArriveAtDest()
{
    if (_travelIsBankTrip)
    {
        _travelIsBankTrip = false;
        _state    = SimState.Banking;
        _bankUntil = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(3 * 60, 8 * 60));
        // Say "bank" after a 1-second delay (feels natural — player walks up then speaks)
        Timer.DelayCall(TimeSpan.FromSeconds(1.0), () => { if (!Deleted && _state == SimState.Banking) this.Say("bank"); });
    }
    else
    {
        _state     = SimState.Idle;
        _idleUntil = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(20, 60));
    }
}
```

**New method: TickBanking()**

```csharp
private void TickBanking()
{
    _chatBrain.TryBankSpeech(this);

    if (DateTime.UtcNow >= _bankUntil)
    {
        // Done banking — resume normal schedule
        _inBankingCycle  = false;
        _nextBankingTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(20, 40));
        _state           = SimState.Idle;
        _idleUntil       = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(5, 20));
    }
}
```

**Update OnThink() switch** — add the Banking case:

```csharp
switch (_state)
{
    case SimState.Idle:       TickIdle();       break;
    case SimState.Travelling: TickTravelling(); break;
    case SimState.Banking:    TickBanking();    break;
}
```

**Add protected helper for subclasses** — ShadowHand needs to start a flee travel. Add this protected method so subclasses don't need to touch private fields:

```csharp
/// <summary>
/// Starts travelling toward the given destination.
/// Used by guild subclasses that need to override travel behaviour (e.g. fleeing).
/// </summary>
protected void StartTravelTo(Point3D dest, TimeSpan timeout)
{
    _travelDest    = dest;
    _travelTimeout = DateTime.UtcNow + timeout;
    _state         = SimState.Travelling;
    _inBankingCycle   = false; // cancel any pending bank trip
    _travelIsBankTrip = false;
}
```

**Update MakeSchedule()** — add ShadowHand case:

```csharp
if (guildName == FBGuilds.ShadowHand)        return ScheduleProfile.ShadowHand(drift);
```
Add this line before the final `return ScheduleProfile.Wanderers(drift);` fallback.

**Update Serialize/Deserialize** — bump version from 0 to 1, persist `_nextBankingTime`:

```csharp
public override void Serialize(GenericWriter writer)
{
    base.Serialize(writer);
    writer.Write(1); // version — bumped from 0 to add _nextBankingTime

    writer.Write(_guildName);
    writer.Write(_memberName);
    writer.Write((int)_state);
    writer.Write(_cooldownUntil);
    writer.Write(_homeLocation);
    writer.Write((int)_homeZone);
    writer.Write(_nextBankingTime); // v1
}

public override void Deserialize(GenericReader reader)
{
    base.Deserialize(reader);
    int version = reader.ReadInt();

    _guildName     = reader.ReadString();
    _memberName    = reader.ReadString();
    _state         = (SimState)reader.ReadInt();
    _cooldownUntil = reader.ReadDateTime();
    _homeLocation  = reader.ReadPoint3D();
    _homeZone      = (SpawnZone)reader.ReadInt();

    if (version >= 1)
        _nextBankingTime = reader.ReadDateTime();
    else
        _nextBankingTime = DateTime.MinValue; // immediately eligible

    // Re-create transient sub-systems
    _schedule  = MakeSchedule(_guildName);
    _chatBrain = new SimChatBrain(_guildName);

    // Restore active state or schedule re-activation
    if (_state == SimState.Idle || _state == SimState.Travelling || _state == SimState.Banking)
    {
        _state     = SimState.Idle; // reset to idle on load — clean slate
        _idleUntil = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        _inBankingCycle = false;
    }
    else
    {
        if (!IsOnCooldown)
            Timer.DelayCall(TimeSpan.FromSeconds(30), Reactivate);
        else
            Timer.DelayCall(_cooldownUntil - DateTime.UtcNow, Reactivate);
    }
}
```

---

## Part 2 — SimChatBrain.cs

### 2A. Add bank speech pool

Add this static array alongside the other guild arrays (e.g., after `SilverWolvesAmbient`):

```csharp
// ── Bank chat — used by all guilds when in Banking state ──────────────────
private static readonly string[] BankAmbient = {
    "WTS 10k iron ingots, 2gp each. PM me.",
    "LFG Hythloth — need one more mage.",
    "Anyone selling a GM katana?",
    "Heard there's a PK camping near Despise.",
    "Nice suit. What hue is that armour?",
    "Selling leather, 5gp per hide.",
    "Is the Britain moongate still clear?",
    "Anyone need a res? I've got bandages.",
    "WTB valorite ore. Good price.",
    "Three Blood Pact in Tier 2 last night. Be careful.",
    "The Silver Wolves were at Destard earlier.",
    "LFG champion spawn — Iron Company forming up.",
    "Anyone know a good house location for sale?",
    "The bank is unusually quiet today.",
    "WTS scrolls — Power, Energy Bolt, Flamestrike.",
};
```

### 2B. Add Shadow Hand ambient pool

```csharp
// ── Shadow Hand ───────────────────────────────────────────────────────────
private static readonly string[] ShadowHandAmbient = {
    "Just passing through.",
    "Nice weather today.",
    "Have you seen the new weapons at the smithy?",
    "I'm looking for someone.",
    "Don't mind me.",
};
```
*(Short, innocent-sounding lines. They blend in. Five is enough — their speech is rare and deliberate.)*

### 2C. Add TryBankSpeech() method

Add this method alongside `TryAmbientSpeech()`:

```csharp
/// <summary>
/// Called every OnThink tick while SimPlayer is in the Banking state.
/// Much lower frequency than ambient — bank chat should be occasional, not constant.
/// </summary>
public void TryBankSpeech(Mobile m)
{
    if (_nextSpeech > DateTime.UtcNow) return;

    // Check for nearby mobiles (speak less often at bank — savour the moment)
    bool anyoneNearby = false;
    foreach (Mobile nearby in m.GetMobilesInRange(8))
    {
        if (nearby != m && !nearby.Deleted)
        {
            anyoneNearby = true;
            break;
        }
    }

    if (!anyoneNearby) return;

    // Bank chat is rarer than ambient — 20% chance per window vs 40%
    if (Utility.RandomDouble() > 0.20) { _nextSpeech = DateTime.UtcNow + SpeechInterval; return; }

    string[] lines = BankAmbient;
    if (lines.Length > 0)
        m.Say(lines[Utility.Random(lines.Length)]);

    _nextSpeech = DateTime.UtcNow + SpeechInterval + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 90));
}
```

### 2D. Update GetAmbientLines()

Add the Shadow Hand case:

```csharp
if (_guildName == FBGuilds.ShadowHand)        return ShadowHandAmbient;
```

---

## Part 3 — ScheduleProfile.cs

Add the Shadow Hand schedule. They work crowds during busy daytime/prime hours:

```csharp
// ── Shadow Hand — town thieves, peak-hours focus ──────────────────────────
public static ScheduleProfile ShadowHand(int driftMinutes = 0)
{
    double[] chance = new double[24];
    for (int h = 0; h < 24; h++)
    {
        if      (h >= 0  && h < 6)  chance[h] = 0.10; // sleeping
        else if (h >= 6  && h < 10) chance[h] = 0.50; // morning shift
        else if (h >= 10 && h < 22) chance[h] = 0.80; // prime picking hours
        else                        chance[h] = 0.30; // late nights
    }
    return new ScheduleProfile(chance, driftMinutes);
}
```

---

## Part 4 — SimPlayerGuilds.cs: ShadowHandSimPlayer

Add this class at the end of the file, after `SilverWolvesSimPlayer`.

**Design rules for Shadow Hand:**
- Template: Thief skills. `Karma = -500` (shows as grey-ish to players).
- Gear: dark clothing (black robe + boots), no weapon visible. Hue 1109 (dark grey).
- `FightMode.None` — they NEVER fight. They only flee.
- They periodically go `Hidden = true` when idle (disappear from view briefly).
- They flee if a `SilverWolvesSimPlayer` is within 12 tiles — start travelling away.

```csharp
// ============================================================
// The Shadow Hand
// Thief/Rogue template — grey, hiding, town operations.
// Never fights. Flees Silver Wolves.
// ============================================================
public class ShadowHandSimPlayer : SimPlayer
{
    private DateTime _nextHideTime = DateTime.MinValue;

    public ShadowHandSimPlayer(string memberName, Point3D home,
                               SpawnZone zone, ScheduleProfile schedule)
        : base(FBGuilds.ShadowHand, memberName, home, zone, schedule) { }

    public ShadowHandSimPlayer(Serial serial) : base(serial) { }

    protected override void ApplyTemplate()
    {
        SetStr(35, 35);
        SetDex(75, 75);
        SetInt(50, 50);
        SetHits(60, 60);

        SetSkill(SkillName.Stealing,      80.0,  80.0);
        SetSkill(SkillName.Hiding,       100.0, 100.0);
        SetSkill(SkillName.Stealth,       90.0,  90.0);
        SetSkill(SkillName.Snooping,      80.0,  80.0);
        SetSkill(SkillName.DetectHidden,  50.0,  50.0);
        SetSkill(SkillName.ItemID,        60.0,  60.0);
        SetSkill(SkillName.Fencing,       40.0,  40.0);
        SetSkill(SkillName.Tactics,       30.0,  30.0);

        VirtualArmor = 5;
        Fame  = 0;
        Karma = -500; // grey alignment
        Kills = 0;
        Hue   = 0;    // normal skin — blend in

        // Dark, inconspicuous clothing
        var robe = new Robe();
        robe.Hue = 1109; // dark charcoal
        AddItem(robe);

        var boots = new Boots();
        boots.Hue = 1109;
        AddItem(boots);
    }

    /// <summary>
    /// Shadow Hand idle hook — hide periodically and flee Silver Wolves.
    /// </summary>
    protected override void OnTickIdle()
    {
        // Periodically go Hidden
        if (DateTime.UtcNow >= _nextHideTime)
        {
            this.Hidden    = true;
            _nextHideTime  = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(20, 60));
        }

        // Flee Silver Wolves on sight
        Mobile wolf = FindNearbyWolf();
        if (wolf != null)
            FleeFrom(wolf);
    }

    private Mobile FindNearbyWolf()
    {
        foreach (Mobile m in GetMobilesInRange(12))
        {
            if (!m.Deleted && m is SilverWolvesSimPlayer)
                return m;
        }
        return null;
    }

    private void FleeFrom(Mobile threat)
    {
        // Pick a point roughly opposite to the threat direction
        int dx = X - threat.X;
        int dy = Y - threat.Y;

        // Extend and jitter
        int fx = X + dx + Utility.RandomMinMax(-8, 8);
        int fy = Y + dy + Utility.RandomMinMax(-8, 8);
        int fz = Map.GetAverageZ(fx, fy);

        if (!Map.CanSpawnMobile(fx, fy, fz))
        {
            // Fallback: random point away from home if no valid tile
            fx = _homeLocation.X + Utility.RandomMinMax(-15, 15);
            fy = _homeLocation.Y + Utility.RandomMinMax(-15, 15);
            fz = Map.GetAverageZ(fx, fy);
        }

        if (Map.CanSpawnMobile(fx, fy, fz))
        {
            this.Hidden = true; // go hidden when fleeing — authentic thief behaviour
            StartTravelTo(new Point3D(fx, fy, fz), TimeSpan.FromSeconds(20));
        }
    }

    public override void Serialize(GenericWriter writer)
    {
        base.Serialize(writer);
        writer.Write(0); // version
    }

    public override void Deserialize(GenericReader reader)
    {
        base.Deserialize(reader);
        reader.ReadInt(); // version
    }
}
```

**Note:** `_homeLocation` is used in `FleeFrom()`. It's `private` in the current SimPlayer.cs. Change it to `protected` in SimPlayer.cs:
```csharp
private Point3D _homeLocation;
→
protected Point3D _homeLocation;
```
This is the only field visibility change needed.

---

## Part 5 — PlayerSimulatorManager.cs

### 5A. Add 3 Shadow Hand members to CreateRosterIfEmpty()

Add after the Silver Wolves block, before `Console.WriteLine($"[SimPlayer] Roster created...")`:

```csharp
// THE SHADOW HAND — 3 members (Phase 2 launch)
Point3D shadowHome = FBZones.ShadowHand_Home;
_allSimPlayers.Add(new ShadowHandSimPlayer("Fingers Malory",
    shadowHome, SpawnZone.Britain_Roads, ScheduleProfile.ShadowHand(0)));
_allSimPlayers.Add(new ShadowHandSimPlayer("The Whisper",
    shadowHome, SpawnZone.Britain_Roads, ScheduleProfile.ShadowHand(25)));
_allSimPlayers.Add(new ShadowHandSimPlayer("Slick Fen",
    shadowHome, SpawnZone.Britain_Roads, ScheduleProfile.ShadowHand(-20)));
```

---

## Compile Checklist

After writing all the code, verify these before committing:

- [ ] `SimState.Banking` referenced in `SimPlayer.OnThink()` switch — no CS0117 missing enum value
- [ ] `FBGuilds.ShadowHand` constant exists in `ReputationSystem.cs` — confirm the exact string before using it
- [ ] `FBZones.ShadowHand_Home` and `FBZones.BountyBoard_Britain_Bank` exist in FBZones.cs (they do — verify with grep before writing)
- [ ] `ShadowHandSimPlayer.FleeFrom()` uses `_homeLocation` — confirm field is now `protected` in SimPlayer.cs
- [ ] `ScheduleProfile.ShadowHand()` added to `ScheduleProfile.cs` AND referenced in `SimPlayer.MakeSchedule()`
- [ ] `TryBankSpeech()` references `_nextSpeech` and `SpeechInterval` — these are private in SimChatBrain.cs. Read the existing `TryAmbientSpeech()` implementation and mirror the pattern exactly.
- [ ] Serialize/Deserialize version bump in SimPlayer.cs is consistent (both write and read use version 1 check)
- [ ] New guild added to PlayerSimulatorManager `CreateRosterIfEmpty()` — Roster count comment should be updated to "18 SimPlayers across 6 guilds"

---

## What This Achieves In-Game

After this phase:

- Any of the 15 existing SimPlayers, after being active for 20-25 minutes, will walk toward Britain bank, say "bank" when they arrive, then idle for 3-8 minutes saying bank-chat style ambient lines ("WTS 10k ingots", "LFG Hythloth", etc.), then resume their normal wander pattern.

- "Fingers Malory", "The Whisper", and "Slick Fen" will spawn as grey-aligned figures in dark robes, hang around Britain bank area, occasionally disappear (go Hidden), and scatter if a Silver Wolves SimPlayer comes within 12 tiles.

- A new player logging into Britain for the first time will see SimPlayers near the bank chatting, and suspicious grey-robed figures lurking. This is the "never log in to an empty server" moment.

---

## Branch and Commit

```bash
git checkout -b claude2/simplayer-phase2-banking-shadowhand
# ... do the work ...
git add Scripts/Custom/SimStateMachine.cs \
        Scripts/Custom/SimPlayer.cs \
        Scripts/Custom/SimChatBrain.cs \
        Scripts/Custom/ScheduleProfile.cs \
        Scripts/Custom/SimPlayerGuilds.cs \
        Scripts/Custom/PlayerSimulatorManager.cs
git commit -m "feat: SimPlayer Phase 2 — banking behavior + Shadow Hand guild (3 members)"
git push origin claude2/simplayer-phase2-banking-shadowhand
```
