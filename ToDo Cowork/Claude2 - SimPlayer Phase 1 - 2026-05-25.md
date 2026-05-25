# Claude 2 Work Instruction — SimPlayer Phase 1

**Branch:** `claude2/simplayer-phase1`  
**Depends on:** BaseFBCombatNPC.cs (done), FBZones.cs (done), FBEventBus.cs (done), ReputationSystem.cs (done)

---

## Goal

Build the SimPlayer system — Phase 1 only. Phase 1 means: **The Wanderers guild, 4 members, Idle + Travelling states, ambient speech.** No combat AI, no crafting, no PvP. The goal is to get 4 AI characters visibly moving around the Britain area and chatting, so we can verify performance and correctness before expanding.

---

## Files to Create

| File | Purpose |
|------|---------|
| `Scripts/Custom/SimPlayer.cs` | Base class for all AI guild members |
| `Scripts/Custom/PlayerSimulatorManager.cs` | Lifecycle + active pool management |
| `Scripts/Custom/SimStateMachine.cs` | State enum + transition logic |
| `Scripts/Custom/ScheduleProfile.cs` | Daily schedule — when SimPlayers are active |
| `Scripts/Custom/SimChatBrain.cs` | Ambient speech logic |

Do **not** modify any existing file.

---

## Read These Before Starting

1. `Design/PlayerSimulator_DesignDoc.txt` — full architecture
2. `Design/GuildSystem_FullDesignDoc.txt` — Wanderers section (top of file)
3. `Scripts/Custom/BaseFBCombatNPC.cs` — what you extend
4. `Scripts/Custom/FBZones.cs` — home locations and zone data
5. `Scripts/Custom/FBEventBus.cs` — events to fire and subscribe to
6. `CLAUDE.md` — ServUO patterns (serialization, Initialize(), equipment rule)

---

## What to Build — Detailed Spec

---

### 1. SimStateMachine.cs

Simple enum + state data. No logic here — logic lives in SimPlayer.

```csharp
namespace Server.Custom
{
    public enum SimState
    {
        Idle,        // standing still, may speak
        Travelling,  // moving toward next location
        Dead,        // just died, waiting for cooldown
        OnCooldown   // invisible, not in world
    }
}
```

That's it for Phase 1. Do not implement Working, Hunting, Combat, etc. yet — those are Phase 2+.

---

### 2. ScheduleProfile.cs

Defines when a SimPlayer is active based on server hour.

```csharp
namespace Server.Custom
{
    public class ScheduleProfile
    {
        // ActivityChance[hour 0-23] = 0.0 to 1.0
        private readonly double[] _hourlyChance;
        private readonly int _driftMinutes; // per-member random drift, set at construction

        public ScheduleProfile(double[] hourlyChance, int driftMinutes = 0)
        {
            _hourlyChance = hourlyChance;
            _driftMinutes = driftMinutes;
        }

        public bool ShouldBeActive(DateTime utcNow)
        {
            int adjustedMinute = (int)(utcNow.Hour * 60 + utcNow.Minute + _driftMinutes) % (24 * 60);
            int hour = adjustedMinute / 60;
            return Utility.RandomDouble() < _hourlyChance[hour];
        }

        // Wanderers schedule — friendly, daytime-focused
        public static ScheduleProfile Wanderers(int driftMinutes = 0)
        {
            double[] chance = new double[24];
            for (int h = 0; h < 24; h++)
            {
                if      (h >= 0  && h < 8)  chance[h] = 0.20; // night
                else if (h >= 8  && h < 12) chance[h] = 0.60; // morning
                else if (h >= 12 && h < 18) chance[h] = 0.70; // afternoon
                else if (h >= 18 && h < 23) chance[h] = 0.90; // prime time
                else                        chance[h] = 0.40; // late night
            }
            return new ScheduleProfile(chance, driftMinutes);
        }
    }
}
```

---

### 3. SimChatBrain.cs

Manages ambient speech. Phase 1: ambient lines only. No reactive/combat speech yet.

```csharp
namespace Server.Custom
{
    public class SimChatBrain
    {
        private static readonly string[] WandererAmbient = {
            "Quite a journey getting here...",
            "Anyone know where I can find a good healer?",
            "This shard has been busy lately.",
            "The roads to the dungeons are dangerous these days.",
            "I heard there are hunters tracking rare creatures.",
            "Have you seen any Blood Pact around here?",
            "Good day, traveller.",
            "Stay safe out there.",
            "The Britain bank is always a good place to meet people.",
            "I could use a few more bandages before heading out.",
        };

        private readonly string _guildName;
        private DateTime _nextSpeechTime;

        public SimChatBrain(string guildName)
        {
            _guildName = guildName;
            _nextSpeechTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(15, 45));
        }

        // Called by SimPlayer.OnThink when state == Idle
        // Returns true if speech was fired
        public bool TryAmbientSpeech(Mobile speaker)
        {
            if (DateTime.UtcNow < _nextSpeechTime) return false;
            if (speaker == null || speaker.Deleted || speaker.Map == null || speaker.Map == Map.Internal)
                return false;

            // Only speak if a player or mobile is within 10 tiles
            bool anyoneNearby = false;
            foreach (Mobile m in speaker.GetMobilesInRange(10))
            {
                if (m != speaker && !m.Deleted && m.Alive)
                { anyoneNearby = true; break; }
            }
            if (!anyoneNearby) return false;

            string[] lines = GetAmbientLines();
            if (lines == null || lines.Length == 0) return false;

            speaker.Say(lines[Utility.Random(lines.Length)]);
            _nextSpeechTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 90));
            return true;
        }

        private string[] GetAmbientLines()
        {
            if (_guildName == FBGuilds.Wanderers) return WandererAmbient;
            // Other guilds added in later phases
            return WandererAmbient; // fallback
        }
    }
}
```

---

### 4. SimPlayer.cs

The base class for all AI guild members. Phase 1 behaviour: Idle → Travelling → Idle loop around Britain area.

#### Key design decisions:
- Extends `BaseFBCombatNPC`
- `FightMode.None` for Phase 1 Wanderers — they do NOT fight
- Ticks via `OnThink()` override (ServUO calls this on all BaseCreature on their AI think cycle)
- Uses `Map.Internal` to hide inactive SimPlayers (invisible to world)
- Home location comes from `FBZones.SimPlayerHomes`

```csharp
using System;
using Server;
using Server.Custom;
using Server.Mobiles;

namespace Server.Custom
{
    public class SimPlayer : BaseFBCombatNPC
    {
        // ── Identity ──────────────────────────────────────────────────────
        private string _guildName;
        private string _memberName;

        [CommandProperty(AccessLevel.GameMaster)]
        public string GuildName  { get => _guildName;  set { _guildName  = value; InvalidateProperties(); } }

        [CommandProperty(AccessLevel.GameMaster)]
        public string MemberName { get => _memberName; set { _memberName = value; InvalidateProperties(); } }

        // ── State ─────────────────────────────────────────────────────────
        private SimState _state = SimState.OnCooldown;
        private DateTime _cooldownUntil;
        private Point3D  _homeLocation;
        private SpawnZone _homeZone;

        [CommandProperty(AccessLevel.GameMaster)]
        public SimState State => _state;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IsOnCooldown => DateTime.UtcNow < _cooldownUntil;

        // ── AI sub-systems ────────────────────────────────────────────────
        private ScheduleProfile _schedule;
        private SimChatBrain    _chatBrain;

        // Travelling state
        private Point3D  _travelDest;
        private DateTime _travelTimeout;
        private DateTime _idleUntil;

        // ── Cooldown durations by guild tier ──────────────────────────────
        private static readonly TimeSpan Tier1Cooldown = TimeSpan.FromMinutes(5);

        // ── Constructor (for PlayerSimulatorManager) ──────────────────────
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

            // Wanderers — Warrior template (Phase 1)
            SetStr(60, 60);
            SetDex(55, 55);
            SetInt(35, 35);
            SetHits(80, 80);
            SetSkill(SkillName.Swords,   65.0, 65.0);
            SetSkill(SkillName.Tactics,  60.0, 60.0);
            SetSkill(SkillName.Anatomy,  55.0, 55.0);
            SetSkill(SkillName.Healing,  60.0, 60.0);
            VirtualArmor = 15;
            Fame  = 0;
            Karma = 1000;
            Kills = 0; // blue — Wanderers are not red

            // Equipment — leather armour + sword
            AddItem(new LeatherChest());
            AddItem(new LeatherLegs());
            AddItem(new LeatherGorget());
            AddItem(new LeatherArms());
            AddItem(new LeatherGloves());
            AddItem(new Boots());
            AddItem(new Longsword());

            // Start hidden in internal map — manager will activate
            MoveToWorld(Point3D.Zero, Map.Internal);
            _state = SimState.OnCooldown;
            _cooldownUntil = DateTime.UtcNow; // immediately eligible
        }

        public SimPlayer(Serial serial) : base(serial) { }

        // ── OnThink — main AI tick ────────────────────────────────────────
        // ServUO calls this every ActiveSpeed seconds while the creature is active.
        public override void OnThink()
        {
            base.OnThink();

            if (Map == Map.Internal) return; // inactive — don't tick

            switch (_state)
            {
                case SimState.Idle:    TickIdle();       break;
                case SimState.Travelling: TickTravelling(); break;
            }
        }

        private void TickIdle()
        {
            _chatBrain.TryAmbientSpeech(this);

            if (DateTime.UtcNow >= _idleUntil)
                StartTravelling();
        }

        private void StartTravelling()
        {
            // Pick a random nearby point (within ~20 tiles of home) as destination
            Point3D dest = GetRandomNearbyPoint();
            if (dest == Point3D.Zero) return;

            _travelDest    = dest;
            _travelTimeout = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            _state         = SimState.Travelling;

            // Use built-in movement — set CurrentSpeed to walking
            CurrentSpeed = ActiveSpeed;
        }

        private void TickTravelling()
        {
            // Check if arrived
            if (GetDistanceToSqrt(_travelDest) < 3.0)
            {
                ArriveAtDest();
                return;
            }

            // Timeout — give up and go idle
            if (DateTime.UtcNow > _travelTimeout)
            {
                ArriveAtDest();
                return;
            }

            // Walk toward destination
            this.Move(this.GetDirectionTo(_travelDest));
        }

        private void ArriveAtDest()
        {
            _state     = SimState.Idle;
            _idleUntil = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(20, 60));
        }

        // Returns a random walkable point within ~20 tiles of home on the same map
        private Point3D GetRandomNearbyPoint()
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                int x = _homeLocation.X + Utility.RandomMinMax(-20, 20);
                int y = _homeLocation.Y + Utility.RandomMinMax(-20, 20);
                int z = Map.GetAverageZ(x, y);
                if (Map.CanSpawnMobile(x, y, z))
                    return new Point3D(x, y, z);
            }
            return Point3D.Zero;
        }

        // ── Death ─────────────────────────────────────────────────────────
        public override void OnDeath(Container c)
        {
            base.OnDeath(c);

            _state         = SimState.Dead;
            _cooldownUntil = DateTime.UtcNow + Tier1Cooldown;

            // Fire event so ReputationSystem can react
            // (killer is tracked by ServUO in LastKiller)
            FBEventBus.Fire_SimPlayerDeactivated(this);

            // Move to internal map after brief delay (let corpse/loot stay)
            Timer.DelayCall(TimeSpan.FromSeconds(5.0), () =>
            {
                if (!Deleted)
                {
                    MoveToWorld(Point3D.Zero, Map.Internal);
                    _state = SimState.OnCooldown;
                }
            });

            // Schedule re-activation
            Timer.DelayCall(_cooldownUntil - DateTime.UtcNow + TimeSpan.FromSeconds(5), Reactivate);
        }

        // Called after cooldown expires — returns to home
        private void Reactivate()
        {
            if (Deleted) return;
            if (!_schedule.ShouldBeActive(DateTime.UtcNow))
            {
                // Schedule says stay offline — try again in 5 minutes
                Timer.DelayCall(TimeSpan.FromMinutes(5.0), Reactivate);
                return;
            }

            Resurrect();
            MoveToWorld(_homeLocation, Map.Felucca);
            _state     = SimState.Idle;
            _idleUntil = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(5, 20));

            FBEventBus.Fire_SimPlayerActivated(this);
        }

        // ── Properties ────────────────────────────────────────────────────
        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            if (!string.IsNullOrEmpty(_guildName))
                list.Add($"[{_guildName}]");
        }

        // ── Serialization ─────────────────────────────────────────────────
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version

            writer.Write(_guildName);
            writer.Write(_memberName);
            writer.Write((int)_state);
            writer.Write(_cooldownUntil);
            writer.Write(_homeLocation);
            writer.Write((int)_homeZone);
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

            // Re-create transient sub-systems
            _schedule  = ScheduleProfile.Wanderers(Utility.RandomMinMax(-30, 30));
            _chatBrain = new SimChatBrain(_guildName);

            // If we were active before save, re-activate after load
            if (_state == SimState.Idle || _state == SimState.Travelling)
            {
                _state     = SimState.Idle;
                _idleUntil = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            }
            else if (_state == SimState.OnCooldown || _state == SimState.Dead)
            {
                if (!IsOnCooldown)
                    Timer.DelayCall(TimeSpan.FromSeconds(30), Reactivate);
                else
                    Timer.DelayCall(_cooldownUntil - DateTime.UtcNow, Reactivate);
            }
        }
    }
}
```

---

### 5. PlayerSimulatorManager.cs

Singleton Item that lives at a fixed world location. Creates all SimPlayers on first load, manages the active pool.

#### Pattern: model after BountyQuestSpawner.cs (singleton item that persists).

```csharp
namespace Server.Custom
{
    public class PlayerSimulatorManager : Item
    {
        // ── Singleton ─────────────────────────────────────────────────────
        private static PlayerSimulatorManager _instance;
        public static PlayerSimulatorManager Instance => _instance;

        // Fixed world location for the manager item (invisible, internal map)
        private static readonly Point3D ManagerLocation = new Point3D(0, 0, 0);

        // ── Config ────────────────────────────────────────────────────────
        public const int MaxActiveSimultaneous = 30;

        // ── SimPlayer roster ──────────────────────────────────────────────
        // All 144 (Phase 1: 4) SimPlayers. Persisted via item serialization.
        private List<SimPlayer> _allSimPlayers = new List<SimPlayer>();

        // ── Initialize() — called at server startup ────────────────────────
        public static void Initialize()
        {
            // Find or create the singleton
            if (_instance == null)
            {
                // Search world items for existing instance
                foreach (Item item in World.Items.Values)
                {
                    if (item is PlayerSimulatorManager mgr)
                    {
                        _instance = mgr;
                        break;
                    }
                }
            }

            if (_instance == null)
            {
                _instance = new PlayerSimulatorManager();
                _instance.MoveToWorld(ManagerLocation, Map.Internal);
                Console.WriteLine("[SimPlayer] PlayerSimulatorManager created.");
            }

            _instance.CreateRosterIfEmpty();
            _instance.ActivateEligibleSimPlayers();

            // Hourly tick to manage active pool
            Timer.DelayCall(TimeSpan.FromMinutes(1.0), _instance.OnManageTick);
        }

        // ── Constructor ───────────────────────────────────────────────────
        [Constructable]
        public PlayerSimulatorManager() : base(0x1)
        {
            Name    = "PlayerSimulatorManager";
            Visible = false;
            Movable = false;
        }

        public PlayerSimulatorManager(Serial serial) : base(serial) { }

        // ── Roster creation — runs once on first world load ───────────────
        private void CreateRosterIfEmpty()
        {
            if (_allSimPlayers.Count > 0) return;

            Console.WriteLine("[SimPlayer] Creating Phase 1 roster...");

            // THE WANDERERS — 4 members
            // Home location: Britain bank area (FBZones.Wanderers_Home)
            Point3D wandHome = FBZones.SimPlayerHomes.Wanderers_Home;

            CreateSimPlayer(FBGuilds.Wanderers, "Erik the Wanderer",    wandHome, SpawnZone.Britain_Roads, 0);
            CreateSimPlayer(FBGuilds.Wanderers, "Mira of the Road",     wandHome, SpawnZone.Britain_Roads, 15);
            CreateSimPlayer(FBGuilds.Wanderers, "Old Thomas",           wandHome, SpawnZone.Britain_Roads, -20);
            CreateSimPlayer(FBGuilds.Wanderers, "Lena Farwalker",       wandHome, SpawnZone.Britain_Roads, 10);

            Console.WriteLine($"[SimPlayer] Roster created: {_allSimPlayers.Count} SimPlayers.");
        }

        private void CreateSimPlayer(string guild, string memberName, Point3D home,
                                     SpawnZone zone, int scheduleDriftMinutes)
        {
            var sp = new SimPlayer(guild, memberName, home, zone,
                                   ScheduleProfile.Wanderers(scheduleDriftMinutes));
            _allSimPlayers.Add(sp);
        }

        // ── Active pool management ────────────────────────────────────────
        private void ActivateEligibleSimPlayers()
        {
            int active = CountActive();
            foreach (SimPlayer sp in _allSimPlayers)
            {
                if (active >= MaxActiveSimultaneous) break;
                if (sp.Deleted) continue;
                if (sp.Map != Map.Internal) continue;         // already active
                if (sp.IsOnCooldown) continue;
                if (!sp.Schedule_ShouldBeActive()) continue;  // expose via property — see below

                sp.Activate();
                active++;
            }
        }

        private int CountActive()
        {
            int count = 0;
            foreach (SimPlayer sp in _allSimPlayers)
                if (!sp.Deleted && sp.Map != Map.Internal) count++;
            return count;
        }

        // Runs every minute to manage the pool
        private void OnManageTick()
        {
            if (Deleted) return;
            ActivateEligibleSimPlayers();
            Timer.DelayCall(TimeSpan.FromMinutes(1.0), OnManageTick);
        }

        // ── Serialization ─────────────────────────────────────────────────
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version

            writer.Write(_allSimPlayers.Count);
            foreach (SimPlayer sp in _allSimPlayers)
                writer.Write(sp);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                SimPlayer sp = reader.ReadMobile() as SimPlayer;
                if (sp != null)
                    _allSimPlayers.Add(sp);
            }

            _instance = this;
        }
    }
}
```

---

## Required SimPlayer additions for Manager integration

The manager calls two things on SimPlayer that need to be public methods/properties:

Add to SimPlayer.cs:
```csharp
// Called by manager to check schedule
public bool Schedule_ShouldBeActive() => _schedule.ShouldBeActive(DateTime.UtcNow);

// Called by manager to activate
public void Activate()
{
    if (Deleted) return;
    Resurrect();
    MoveToWorld(_homeLocation, Map.Felucca);
    _state     = SimState.Idle;
    _idleUntil = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(5, 30));
    FBEventBus.Fire_SimPlayerActivated(this);
}
```

---

## FBEventBus wiring (fire these, already declared in FBEventBus.cs)

- `FBEventBus.Fire_SimPlayerActivated(simPlayer)` — when SimPlayer enters world
- `FBEventBus.Fire_SimPlayerDeactivated(simPlayer)` — when SimPlayer dies and leaves world

When a player kills a SimPlayer, fire `FBEventBus.Fire_PlayerKilledSimPlayer(killer, this)` from SimPlayer.OnDeath — use `LastKiller` for the killer reference:

```csharp
Mobile killer = LastKiller;
if (killer != null)
    FBEventBus.Fire_PlayerKilledSimPlayer(killer, this);
```

---

## What NOT to Build in Phase 1

- Do NOT implement Hunting, Combat, Looting, Banking states
- Do NOT implement SimPersonality.cs (Phase 2)
- Do NOT create Craftsmen's League, Iron Company, Silver Wolves, Arcane Brotherhood members
- Do NOT implement the gate-following mechanic yet
- Do NOT implement PvP/PKing
- The Wanderers have `FightMode.None` — they should NOT attack anyone, ever

---

## Verification Checklist

After building, mentally trace:

1. Server starts → `PlayerSimulatorManager.Initialize()` called → roster created with 4 Wanderers → eligible ones activated
2. In-game: 4 named Wanderers visible near Britain bank area walking around
3. `[props` on a Wanderer → shows GuildName = "The Wanderers", State = Idle/Travelling
4. `[worldsave` + server restart → Wanderers still exist, reactivate on schedule
5. Kill a Wanderer → `FBEventBus.Fire_PlayerKilledSimPlayer` fires → ReputationSystem awards +10 SilverWolves +15 PaladinOrder to killer → Wanderer vanishes from world → returns after 5 minutes
6. `[repgump` on killer → Silver Wolves and Paladin Order standings updated

---

## Compile Notes

- `using System.Collections.Generic;` required in PlayerSimulatorManager.cs
- `FBGuilds` is the correct class name (not `Guilds` — namespace conflict with Server.Guilds)
- `FBZones.SimPlayerHomes.Wanderers_Home` is already defined in FBZones.cs
- `SpawnZone.Britain_Roads` already exists in FBZones.cs
- `BaseFBCombatNPC` constructor signature: `(AIType ai, FightMode mode, int range)`
- Wander movement: use `this.Move(this.GetDirectionTo(dest))` for simple step-by-step movement

---

## Signal Completion

Commit all 5 files to branch `claude2/simplayer-phase1`, then commit a final file:
`ToDo Cowork/DONE - Claude2 - SimPlayer Phase 1 - 2026-05-25.md`
