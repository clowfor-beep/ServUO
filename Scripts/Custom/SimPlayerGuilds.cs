// ============================================================
// SimPlayerGuilds.cs
// Scripts/Custom/SimPlayerGuilds.cs
//
// Guild-specific SimPlayer subclasses.
// Each overrides ApplyTemplate() with the correct stats,
// skills, and gear for that guild.
//
// Equipment rule (from CLAUDE.md):
//   Weapons / armour  --> AddItem(...)
//   Spellbooks        --> PackItem(...)  NEVER AddItem
//   BookOfChivalry    --> PackItem(...)  NEVER AddItem
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Custom;
using Server.Engines.CannedEvil;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // Craftsmen's League
    // Blacksmith template -- slow moving, trade-focused
    // ============================================================
    public class CraftsmensLeagueSimPlayer : SimPlayer
    {
        public CraftsmensLeagueSimPlayer(string memberName, Point3D home,
                                         SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.CraftsmenLeague, memberName, home, zone, schedule) { }

        public CraftsmensLeagueSimPlayer(Serial serial) : base(serial) { }

        protected override void ApplyTemplate()
        {
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
            ActiveSpeed = 0.3; // working pace -- moves slower

            AddItem(new LeatherChest());
            AddItem(new LeatherLegs());
            AddItem(new Boots());
            AddItem(new SmithHammer());
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

    // ============================================================
    // Iron Company
    // Heavy Warrior template -- plate armour, chivalry.
    // Periodically finds a real ChampionSpawn on Felucca, travels
    // to it, activates it if needed, fights through all tiers,
    // targets the champion directly when it spawns, and returns
    // home when the champion dies (or after a 2-hour hard cap).
    // ============================================================
    public class IronCompanySimPlayer : SimPlayer
    {
        // -- Champion run phase (transient — not serialized) -----------
        // Flow: None → GatherAtBank (walk to local bank) → WaitingAtBank (2-min rally)
        //       → AtSpawn (Sacred Journey teleport, then fight) → back to None (Sacred Journey home)
        private enum ChampPhase { None, GatherAtBank, WaitingAtBank, AtSpawn }

        private ChampPhase    _champPhase       = ChampPhase.None;
        private bool          _champScheduleSet = false;
        private DateTime      _nextChampRun     = DateTime.MinValue;
        private DateTime      _departAt         = DateTime.MinValue; // when to Sacred Journey
        private DateTime      _leaveSpawnAt     = DateTime.MinValue; // 2-hour hard cap
        private ChampionSpawn _targetSpawn      = null;
        private int           _lastKnownLevel   = -1;
        private bool          _champAnnounced   = false;
        private Timer         _bossBattlecryTimer = null;

        // -- Dungeon patrol (persistent: _dungeonGroupId) -----------------
        // Groups 1 and 2 each contain 3 members.  When idle (no champ run),
        // each group picks a dungeon together, travels there, fights for
        // ~90 minutes, then withdraws back home.  Group 0 = no dungeon patrol.
        private int            _dungeonGroupId      = 0;  // saved

        private enum DungeonPhase { None, Traveling, Fighting, Withdrawing }
        private DungeonPhase   _dungeonPhase        = DungeonPhase.None;
        private bool           _dungeonScheduleSet  = false;
        private DateTime       _nextDungeonRun      = DateTime.MinValue;
        private DateTime       _leaveDungeonAt      = DateTime.MinValue;
        private Point3D        _dungeonAnchor       = Point3D.Zero;
        private Map            _dungeonAnchorMap    = Map.Felucca;

        // Shared state: group ID → dungeon target (picked once per cycle, shared by group)
        private class DungeonTarget
        {
            public Point3D  Loc;
            public Map      DMap;
            public string   Name;
            public DateTime SetAt;
            public DungeonTarget(Point3D loc, Map map, string name)
            {
                Loc = loc; DMap = map; Name = name; SetAt = DateTime.UtcNow;
            }
        }
        private static readonly Dictionary<int, DungeonTarget> _groupDungeonTarget
            = new Dictionary<int, DungeonTarget>();

        private struct DungeonEntry
        {
            public Point3D Loc;
            public Map     DMap;
            public string  Name;
            public DungeonEntry(Point3D loc, Map map, string name)
            { Loc = loc; DMap = map; Name = name; }
        }
        private static readonly DungeonEntry[] DungeonLocations =
        {
            new DungeonEntry(new Point3D(354,  110,  -1), Map.Malas,   "Doom"),
            new DungeonEntry(new Point3D(2509, 997,   0), Map.Felucca, "Covetous"),
            new DungeonEntry(new Point3D(2004, 145,  -1), Map.Felucca, "the Ice Dungeon"),
            new DungeonEntry(new Point3D(2929, 3430, -1), Map.Felucca, "the Fire Dungeon"),
            new DungeonEntry(new Point3D(1170, 1663, -25), Map.Felucca, "the Orc Cave"),
            new DungeonEntry(new Point3D(2817, 1130, -20), Map.Felucca, "Terathan Keep"),
        };

        // -- Headquarters idle hangout ------------------------------------
        // When both champ and dungeon phases are None, members periodically
        // visit the Iron Company castle HQ near Luna to rest and gossip.
        private static readonly Point3D IronCompanyHQ     = new Point3D(854, 557, -90);
        private static readonly Map     IronCompanyHQMap  = Map.Malas;

        private enum HQPhase { None, AtHQ }
        private HQPhase  _hqPhase       = HQPhase.None;
        private DateTime _nextHQVisit   = DateTime.MinValue;  // when to next travel to HQ
        private DateTime _leaveHQAt     = DateTime.MinValue;  // when to head home
        private DateTime _nextHQSpeech  = DateTime.MinValue;  // throttle gossip lines
        private bool     _hqInitialized = false;

        private static readonly string[] HQGossip =
        {
            // Dungeon victories
            "You should've seen the Doom run — we wiped the floor with three Lich Lords.",
            "Covetous Level 5 was crawling with mongbats. We cleared it in under an hour.",
            "Three Balrons down in the Fire Dungeon. Still got the burns to prove it.",
            "The Ice Dungeon nearly cost us Kael — Frost Liche got him good. He made it.",
            "Orc Cave stunk worse than usual. Thirty dead Orcs by the time we left.",
            "Terathan Keep's queens are no joke. Had to pull back twice before we broke through.",
            "Doom's gauntlet rooms are mine now. Go ahead — test me.",
            "We've cleared every floor of Covetous this month. Nobody else can say that.",
            // Champion spawn victories
            "The Abyss spawn tried to turn us. Four waves — we held every one.",
            "Trammel lord fell in under twenty minutes. Iron Company efficiency.",
            "The spawn last week barely had a chance. We were organised, they weren't.",
            "Two Blood Pact players tried to muscle in on our spawn. They regretted it.",
            "Champion's crown went to Sergeant Vale. Rightfully earned.",
            "We ran three spawns back-to-back last week. Nobody does that but us.",
            "I counted forty-eight kills at the last champion. Personal record.",
            // General HQ banter
            "Good to be back at the castle. My feet needed the rest.",
            "Luna market's just down the road. Grab reagents while you can.",
            "Next run in a few hours. Get comfortable while it lasts.",
            "Anyone want to spar in the courtyard? Kael's been getting sloppy.",
            "This castle's worth every bit of gold we spent on it.",
            "Fill your flasks. We ride again soon.",
        };

        // -- Combat healing + mobility (transient) ------------------------
        private DateTime _nextHealAt          = DateTime.MinValue;
        private Point3D  _spawnEntryPoint     = Point3D.Zero;
        private double   _baseActiveSpeed     = 0.0; // saved so we can restore after spawn
        private Serial   _lastCombatantSerial = Serial.Zero;
        private DateTime _combatantSince      = DateTime.MinValue;
        private DateTime _nextReturnAt        = DateTime.MinValue;

        // -- Retaliation tracking (transient) --------------------------------
        // If a blue (innocent) player attacks us, we fight back for 30 seconds.
        // Outside that window we never auto-acquire innocent players as targets.
        private Serial   _retaliateSerial  = Serial.Zero;
        private DateTime _retaliateExpires = DateTime.MinValue;

        private bool IsRetaliationTarget(Mobile m)
            => m != null && m.Serial == _retaliateSerial && DateTime.UtcNow < _retaliateExpires;

        // -- Stuck resolution (3-phase) -----------------------------------
        // None → Teleported (6s stuck) → CrossbowOut (5s after teleport still stuck)
        // → give up after 15s with crossbow → return to entry + re-equip melee
        private enum StuckPhase { None, Teleported, CrossbowOut }
        private StuckPhase _stuckPhase   = StuckPhase.None;
        private DateTime   _stuckPhaseAt = DateTime.MinValue;

        // -- Victory gold collection dedup --------------------------------
        // Tracks which champion spawn serials have already had gold collected
        // this run, so only the first member to call BeginWithdraw(true) fires it.
        private static readonly HashSet<Serial> _goldCollectedForSpawn = new HashSet<Serial>();

        // -- Unreachable target blacklist ----------------------------------
        // Targets we gave up on are skipped for 3 minutes so we don't
        // immediately re-acquire an unreachable ledge monster.
        private readonly Dictionary<Serial, DateTime> _blockedTargets
            = new Dictionary<Serial, DateTime>();
        private static readonly TimeSpan BlockDuration = TimeSpan.FromMinutes(3);

        // Used when no Felucca ChampionSpawn is found in the world at all
        private static readonly Point3D[] FallbackDestinations =
        {
            new Point3D(1296, 1263, 0),   // Despise entrance (Felucca)
            new Point3D(533,  1566, 0),   // Shame entrance (Felucca)
        };

        // -- Downed / resurrection state -----------------------------------
        // When an Iron Company member would die, OnBeforeDeath intercepts it.
        // They are left downed (alive at 1 HP, invulnerable, immobile) until a
        // fellow guild member comes within 10 tiles and resurrects them.
        private bool     _isDowned        = false;
        private DateTime _nextRezCheckAt  = DateTime.MinValue;
        private DateTime _nextGuildHealAt = DateTime.MinValue;

        private static readonly string[] DownedLines =
        {
            "*collapses with a grunt*",
            "*falls to the ground*",
            "I need... help...",
            "*staggers and falls*",
        };
        private static readonly string[] RezLines =
        {
            "Stay with me!",
            "On your feet, soldier!",
            "I've got you — hold on!",
            "Don't you die on me!",
        };
        private static readonly string[] BackUpLines =
        {
            "*rises shakily* Back in the fight!",
            "Thank you, brother.",
            "*gasps* That was close...",
            "I owe you one.",
        };

        // -- Speech pools ----------------------------------------------
        private static readonly string[] DepartureSpeech = {
            "Iron Company, move out.",
            "We have a spawn to clear. Let's go.",
            "Form up. We march now.",
            "Time to earn our pay. Move.",
        };

        private static readonly string[] GatherSpeech = {
            "Iron Company, rally at the bank!",
            "Form up here. Sacred Journey in one minute.",
            "Everyone gear up. We depart shortly.",
            "Hold at the bank. Move out soon.",
        };

        private static readonly string[] ArrivalSpeech = {
            "Iron Company — hold this floor!",
            "Form up! We clear this spawn!",
            "Nobody leaves until this is done.",
            "Support on my position.",
        };

        // Indexed 0-3 for level 1-4 transitions
        private static readonly string[] TierSpeech = {
            "First skulls lit! Keep pushing!",
            "Second tier! Stay focused!",
            "Third tier! Champion is close!",
            "Final tier! CHAMPION INCOMING!",
        };

        // Index 0 = victory line (champion killed); 1-3 = timeout withdraw lines
        private static readonly string[] WithdrawSpeech = {
            "Champion down! Iron Company stands!",
            "Good work. Fall back.",
            "Clear. We're heading back.",
            "Iron Company, withdraw.",
        };

        // -- Constructors ----------------------------------------------
        // Iron Company are hardened warriors — immune to Succubus life drain
        public override bool IsImmuneToLifeDrain => true;

        public IronCompanySimPlayer(string memberName, Point3D home,
                                    SpawnZone zone, ScheduleProfile schedule,
                                    int dungeonGroupId = 0)
            : base(FBGuilds.IronCompany, memberName, home, zone, schedule)
        {
            _dungeonGroupId = dungeonGroupId;
        }

        public IronCompanySimPlayer(Serial serial) : base(serial) { }

        // -- Health / monitor overrides --------------------------------

        public override SimPlayer.SimHealthStatus GetHealth()
        {
            // Inherit base checks (Map.Internal, travel timeout, etc.)
            SimPlayer.SimHealthStatus base_ = base.GetHealth();
            if (base_ == SimPlayer.SimHealthStatus.Stuck) return base_;

            // Orphaned champ phase: AtSpawn but spawn is gone
            if (_champPhase == ChampPhase.AtSpawn
                && (_targetSpawn == null || _targetSpawn.Deleted))
                return SimPlayer.SimHealthStatus.Stuck;

            // Downed for > 10 min (rez should have come)
            if (_isDowned && DateTime.UtcNow - LastStateChange > TimeSpan.FromMinutes(10))
                return SimPlayer.SimHealthStatus.Warning;

            return base_;
        }

        public override void AutoFix()
        {
            // Reset all guild-specific phases before calling base
            StopBossBattlecryTimer();
            _champPhase      = ChampPhase.None;
            _targetSpawn     = null;
            _dungeonPhase    = DungeonPhase.None;
            _dungeonAnchor   = Point3D.Zero;
            _dungeonAnchorMap = Map.Felucca;
            _hqPhase         = HQPhase.None;
            _isDowned        = false;
            FightMode        = FightMode.None;
            Combatant        = null;

            base.AutoFix();
        }

        // -- Serialization ---------------------------------------------

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1); // version
            writer.Write(_dungeonGroupId);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            if (version >= 1)
                _dungeonGroupId = reader.ReadInt();

            // Transient combat state must not persist across restarts.
            // If the world was saved mid-champ-run, FightMode could be
            // Closest and Team could be 1, causing the base AI to run in
            // combat mode while _champPhase is reset to None — breaking patrol.
            FightMode = FightMode.None;
            Combatant = null;
            Team      = 0;
        }

        // -- Overrides -------------------------------------------------

        // While downed we are invulnerable — prevents the killing blow repeating
        public override bool IsInvulnerable => _isDowned;

        /// <summary>
        /// Controls who the AI considers an enemy at the champ spawn.
        /// — Uncontrolled non-SimPlayer creatures (spawn monsters): always enemy (grey or blue).
        /// — Red players (murderers): always enemy at spawn.
        /// — Any player/pet who attacked us first: enemy for 30s retaliation window.
        /// — Innocent (blue) players who haven't attacked us: never enemy.
        /// Outside the spawn phase falls back to base behaviour.
        /// </summary>
        public override bool IsEnemy(Mobile m)
        {
            // Retaliation targets are always fair game regardless of notoriety.
            if (IsRetaliationTarget(m))
                return true;

            // If a retaliation target owns the pet, fight the pet too.
            if (m is BaseCreature retPet && retPet.Controlled
                && IsRetaliationTarget(retPet.ControlMaster))
                return true;

            // Never attack innocent player pets unless they or their owner attacked us first.
            if (m is BaseCreature petCheck && petCheck.Controlled
                && petCheck.ControlMaster is PlayerMobile petOwner
                && petOwner.Kills < 5)
                return false;

            if (_champPhase == ChampPhase.AtSpawn)
            {
                // All uncontrolled non-SimPlayer creatures are enemies — grey or blue.
                if (m is BaseCreature bc && !bc.Controlled && !bc.Summoned && !(m is SimPlayer))
                    return true;

                // Always fight red players (murderers) at spawn.
                if (m is PlayerMobile redPm && redPm.Kills >= 5)
                    return true;

                // Never auto-attack innocent (blue) players who haven't hit us.
                if (m is PlayerMobile pm && pm.Kills < 5)
                    return false;
            }

            return base.IsEnemy(m);
        }

        /// <summary>
        /// Allow attacking uncontrolled spawn creatures regardless of their notoriety.
        /// Base CanBeHarmful rejects blue innocents — without this override the target
        /// scan skips blue Lord of Oaks creatures entirely.
        /// </summary>
        public override bool CanBeHarmful(IDamageable target, bool message, bool ignoreEvilType)
        {
            if (_champPhase == ChampPhase.AtSpawn
                && target is BaseCreature spawnBc
                && !spawnBc.Controlled && !spawnBc.Summoned && !(target is SimPlayer))
                return true;

            return base.CanBeHarmful(target, message, ignoreEvilType);
        }

        /// <summary>
        /// Intercept death: instead of dying, go downed. A guild member will
        /// resurrect us when they come within range.
        /// </summary>
        public override bool OnBeforeDeath()
        {
            if (_isDowned) return false; // already downed, ignore

            _isDowned  = true;
            Hits       = 1;
            Combatant  = null;
            FightMode  = FightMode.None;
            _stuckPhase = StuckPhase.None;
            StopBossBattlecryTimer();

            Animate(22, 5, 1, true, false, 0); // fall animation
            Say(DownedLines[Utility.Random(DownedLines.Length)]);

            return false; // returning false prevents actual death
        }

        /// <summary>Pause SimState movement ticks while fighting, rallying at bank, or fighting at spawn.</summary>
        protected override bool SkipStateTick => Combatant != null
            || _champPhase == ChampPhase.WaitingAtBank
            || _champPhase == ChampPhase.AtSpawn
            || _dungeonPhase == DungeonPhase.Fighting;

        /// <summary>No banking while on a champ run.</summary>
        protected override bool CanBank => _champPhase == ChampPhase.None
            && _dungeonPhase == DungeonPhase.None
            && _hqPhase == HQPhase.None;

        public override void OnThink()
        {
            // While downed: check if a guild member is nearby to rez us,
            // then do nothing else (no movement, no combat, no state ticks).
            if (_isDowned)
            {
                // nothing — rez is driven by living members scanning outward
                return;
            }

            // Living member: scan for downed or injured colleagues within 10 tiles
            CheckRezNearby();
            CheckHealNearby();

            // Champ state machine always runs — even during combat / SkipStateTick
            ManageChampPhase();

            // Dungeon patrol state machine — only when idle from champ runs
            if (_dungeonGroupId > 0 && _champPhase == ChampPhase.None)
                ManageDungeonPhase();

            // HQ hangout — all Iron Company members, when both other phases are idle
            if (_champPhase == ChampPhase.None && _dungeonPhase == DungeonPhase.None)
                ManageHQIdle();

            // Self-heal when fighting at the spawn
            if (_champPhase == ChampPhase.AtSpawn && Alive)
                TrySelfHeal();

            // Keep all equipped gear at full durability (indestructible)
            RepairEquippedGear();

            base.OnThink();

            // AFTER base AI: immediately clear any innocent player pet it may have
            // re-acquired as combatant. Must run after base.OnThink() so the base
            // AI can't re-acquire it again this tick.
            if (Combatant is BaseCreature combatPet && combatPet.Controlled
                && combatPet.ControlMaster is PlayerMobile combatOwner
                && combatOwner.Kills < 5
                && !IsRetaliationTarget(combatPet)
                && !IsRetaliationTarget(combatOwner))
                Combatant = null;
        }

        /// <summary>
        /// Scan nearby mobiles for downed Iron Company members and help them up.
        /// Throttled to once every 3 seconds.
        /// </summary>
        private void CheckRezNearby()
        {
            if (DateTime.UtcNow < _nextRezCheckAt) return;
            _nextRezCheckAt = DateTime.UtcNow + TimeSpan.FromSeconds(3);

            foreach (Mobile m in GetMobilesInRange(10))
            {
                if (m == this || m.Deleted) continue;
                if (!(m is IronCompanySimPlayer ic)) continue;
                if (!ic._isDowned) continue;

                PerformRez(ic);
                break; // one at a time
            }
        }

        private void PerformRez(IronCompanySimPlayer target)
        {
            Combatant = null; // pause our own combat briefly
            Say(RezLines[Utility.Random(RezLines.Length)]);
            Animate(11, 5, 1, true, false, 0); // cast/heal animation

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (Deleted || target.Deleted || !target._isDowned) return;

                target._isDowned = false;
                target.Hits      = target.HitsMax / 2;
                target.Stam      = target.StamMax;
                target.Mana      = target.ManaMax / 2;

                // Restore fight mode if still at spawn
                if (target._champPhase == ChampPhase.AtSpawn)
                    target.FightMode = FightMode.Closest;

                Effects.SendLocationParticles(
                    EffectItem.Create(target.Location, target.Map, EffectItem.DefaultDuration),
                    0x376A, 9, 32, 5023);
                target.PlaySound(0x1F2);
                target.Say(BackUpLines[Utility.Random(BackUpLines.Length)]);
            });
        }

        /// <summary>
        /// If anyone damages us while at the spawn, register them as a
        /// retaliation target for 30 seconds — even blue innocent players.
        /// This is the only way an innocent player can become our combatant.
        /// </summary>

        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            base.OnDamage(amount, from, willKill);

            if (from == null || !from.Alive || from == this) return;

            // Register the responsible party as a retaliation target for 30 seconds.
            // If a pet attacked us, register the owner — that way both the pet combatant
            // check (IsRetaliationTarget(petOwner)) and the blue player combatant check
            // (IsRetaliationTarget(bluePm)) in OnThink pass correctly.
            Mobile responsible = from;
            if (from is BaseCreature attackPet && attackPet.Controlled && attackPet.ControlMaster != null)
                responsible = attackPet.ControlMaster;

            _retaliateSerial  = responsible.Serial;
            _retaliateExpires = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Scan nearby Iron Company members below 50% HP and heal the most
        /// injured one. Throttled to once every 8 seconds.
        /// Priority: downed rez > guild heal > self-heal.
        /// </summary>
        private void CheckHealNearby()
        {
            if (DateTime.UtcNow < _nextGuildHealAt) return;

            IronCompanySimPlayer needsHeal = null;
            int lowestHitsPct = 50; // only heal if below 50%

            foreach (Mobile m in GetMobilesInRange(10))
            {
                if (m == this || m.Deleted) continue;
                if (!(m is IronCompanySimPlayer ic)) continue;
                if (ic._isDowned) continue;  // handled by rez system
                if (ic.HitsMax <= 0) continue;

                int pct = (ic.Hits * 100) / ic.HitsMax;
                if (pct < lowestHitsPct)
                {
                    lowestHitsPct = pct;
                    needsHeal     = ic;
                }
            }

            if (needsHeal == null) return;

            _nextGuildHealAt = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(7, 10));

            // Heal animation + sound on the healer
            Animate(11, 5, 1, true, false, 0);
            PlaySound(0x57);

            // Compute heal amount using our own Healing + Anatomy
            double healSkill = Skills[SkillName.Healing].Value;
            double anatSkill = Skills[SkillName.Anatomy].Value;
            int amount = (int)((healSkill + anatSkill) * 0.3 + Utility.RandomMinMax(10, 25));

            needsHeal.Hits = Math.Min(needsHeal.HitsMax, needsHeal.Hits + amount);
            needsHeal.FixedEffect(0x375A, 10, 15);
            needsHeal.PlaySound(0x1F2);
        }

        /// <summary>
        /// Simulates bandage + chivalry healing. Fires every ~12 seconds when
        /// HP drops below 75%. Amount is based on Healing + Anatomy skills.
        /// </summary>
        private void TrySelfHeal()
        {
            if (DateTime.UtcNow < _nextHealAt) return;
            if (Hits >= (int)(HitsMax * 0.75))  return;

            double healSkill = Skills[SkillName.Healing].Value;
            double anatSkill = Skills[SkillName.Anatomy].Value;
            int amount = (int)((healSkill + anatSkill) * 0.3 + Utility.RandomMinMax(5, 15));

            Hits = Math.Min(HitsMax, Hits + amount);
            PlaySound(0x57);
            FixedEffect(0x375A, 10, 15);

            _nextHealAt = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(10, 14));
        }

        // -- Champion run state machine --------------------------------

        private void ManageChampPhase()
        {
            if (Map == Map.Internal) return;

            // One-time init: stagger first run 2-8 min after activation so they
            // don't all depart at once, but don't sit idle for an hour on a fresh
            // reset.  Subsequent runs are scheduled 60-120 min apart (in BeginWithdraw).
            if (!_champScheduleSet)
            {
                _champScheduleSet = true;
                _nextChampRun = DateTime.UtcNow
                    + TimeSpan.FromMinutes(Utility.RandomMinMax(2, 8));
            }

            switch (_champPhase)
            {
                case ChampPhase.None:
                    if (State == SimState.Idle && DateTime.UtcNow >= _nextChampRun)
                        StartChampRun();
                    break;

                case ChampPhase.GatherAtBank:
                    // Transition fires via Timer.DelayCall fallback (90s) in StartChampRun/ForceChampRunAt.
                    // Also trigger immediately if they happen to already be Idle (fast arrival).
                    if (State == SimState.Idle)
                        ArriveAtBank();
                    break;

                case ChampPhase.WaitingAtBank:
                    // SkipStateTick holds them still while waiting
                    if (DateTime.UtcNow >= _departAt)
                        SacredJourneyToSpawn();
                    break;

                case ChampPhase.AtSpawn:
                    TickAtSpawn();
                    break;
            }
        }

        // ----------------------------------------------------------------
        // Dungeon patrol state machine
        // ----------------------------------------------------------------

        private void ManageDungeonPhase()
        {
            if (Map == Map.Internal) return;

            // One-time init: stagger first dungeon run 10-30 min after activation
            if (!_dungeonScheduleSet)
            {
                _dungeonScheduleSet = true;
                _nextDungeonRun = DateTime.UtcNow
                    + TimeSpan.FromMinutes(Utility.RandomMinMax(10, 30));
            }

            switch (_dungeonPhase)
            {
                case DungeonPhase.None:
                    if (State == SimState.Idle && DateTime.UtcNow >= _nextDungeonRun)
                        StartDungeonRun();
                    break;

                case DungeonPhase.Traveling:
                    // Wait until we're close enough to the dungeon anchor
                    if (_dungeonAnchor != Point3D.Zero
                        && Map == _dungeonAnchorMap
                        && InRange(_dungeonAnchor, 8))
                    {
                        _dungeonPhase   = DungeonPhase.Fighting;
                        _leaveDungeonAt = DateTime.UtcNow
                            + TimeSpan.FromMinutes(Utility.RandomMinMax(60, 120));
                        FightMode  = FightMode.Closest;
                        CurrentSpeed = ActiveSpeed;
                    }
                    break;

                case DungeonPhase.Fighting:
                    TickAtDungeon();
                    break;

                case DungeonPhase.Withdrawing:
                    if (Map == Map.Felucca && InRange(Home, 10))
                    {
                        _dungeonPhase     = DungeonPhase.None;
                        _dungeonAnchor    = Point3D.Zero;
                        _dungeonAnchorMap = Map.Felucca;
                        FightMode         = FightMode.None;
                        Combatant         = null;
                        _nextDungeonRun   = DateTime.UtcNow
                            + TimeSpan.FromMinutes(Utility.RandomMinMax(30, 60));
                    }
                    break;
            }
        }

        private void StartDungeonRun()
        {
            // Check if our group already has a target picked this cycle (within 3 hours)
            DungeonTarget target;
            DungeonTarget existing;

            if (_groupDungeonTarget.TryGetValue(_dungeonGroupId, out existing)
                && DateTime.UtcNow - existing.SetAt < TimeSpan.FromHours(3))
            {
                target = existing;
            }
            else
            {
                DungeonEntry pick = DungeonLocations[Utility.Random(DungeonLocations.Length)];
                target = new DungeonTarget(pick.Loc, pick.DMap, pick.Name);
                _groupDungeonTarget[_dungeonGroupId] = target;
            }

            _dungeonAnchor    = target.Loc;
            _dungeonAnchorMap = target.DMap;
            _dungeonPhase     = DungeonPhase.Traveling;

            // Leave HQ if we're currently there
            if (_hqPhase != HQPhase.None)
            {
                _hqPhase     = HQPhase.None;
                _nextHQVisit = DateTime.UtcNow + TimeSpan.FromMinutes(60);
            }

            string[] lines =
            {
                string.Format("Time to move out — we're heading to {0}.", target.Name),
                string.Format("Brothers, to {0}! Let's hunt.", target.Name),
                string.Format("The Iron Company rides to {0}!", target.Name),
                string.Format("Fall in — {0} awaits.", target.Name),
            };
            PublicOverheadMessage(MessageType.Regular, 0x3B2, false,
                lines[Utility.Random(lines.Length)]);

            MoveToWorld(target.Loc, target.DMap);
        }

        private void TickAtDungeon()
        {
            if (DateTime.UtcNow >= _leaveDungeonAt)
            {
                BeginDungeonWithdraw();
                return;
            }

            // If we drifted far from our anchor, teleport back
            if (Map != _dungeonAnchorMap || !InRange(_dungeonAnchor, 20))
                MoveToWorld(_dungeonAnchor, _dungeonAnchorMap);

            // Base AI handles combat with FightMode.Closest
        }

        private void BeginDungeonWithdraw()
        {
            string[] lines =
            {
                "That's enough — we're withdrawing.",
                "Time to head back. Good fight.",
                "Pull out! We return to the city.",
                "Withdrawal — move back to the roads.",
            };
            PublicOverheadMessage(MessageType.Regular, 0x3B2, false,
                lines[Utility.Random(lines.Length)]);

            _dungeonPhase = DungeonPhase.Withdrawing;
            FightMode     = FightMode.None;
            Combatant     = null;
            MoveToWorld(Home, Map.Felucca);
        }

        // ----------------------------------------------------------------
        // HQ idle hangout
        // ----------------------------------------------------------------

        private void ManageHQIdle()
        {
            if (Map == Map.Internal) return;

            // One-time stagger: first HQ visit 5-20 min after spawn
            if (!_hqInitialized)
            {
                _hqInitialized = true;
                _nextHQVisit   = DateTime.UtcNow
                    + TimeSpan.FromMinutes(Utility.RandomMinMax(5, 20));
            }

            switch (_hqPhase)
            {
                case HQPhase.None:
                    if (State == SimState.Idle && DateTime.UtcNow >= _nextHQVisit)
                    {
                        _hqPhase    = HQPhase.AtHQ;
                        _leaveHQAt  = DateTime.UtcNow
                            + TimeSpan.FromMinutes(Utility.RandomMinMax(20, 40));
                        MoveToWorld(IronCompanyHQ, IronCompanyHQMap);
                    }
                    break;

                case HQPhase.AtHQ:
                    // Gossip while hanging out
                    if (DateTime.UtcNow >= _nextHQSpeech)
                    {
                        bool anyoneNearby = false;
                        foreach (Mobile m in GetMobilesInRange(8))
                        {
                            if (m != this && !m.Deleted && m.Alive)
                            { anyoneNearby = true; break; }
                        }

                        if (anyoneNearby)
                            Say(HQGossip[Utility.Random(HQGossip.Length)]);

                        _nextHQSpeech = DateTime.UtcNow
                            + TimeSpan.FromSeconds(Utility.RandomMinMax(25, 70));
                    }

                    // Leave when time is up or a champ/dungeon run starts
                    if (DateTime.UtcNow >= _leaveHQAt)
                        ReturnFromHQ();
                    break;
            }
        }

        private void ReturnFromHQ()
        {
            _hqPhase     = HQPhase.None;
            _nextHQVisit = DateTime.UtcNow
                + TimeSpan.FromMinutes(Utility.RandomMinMax(30, 90));
            MoveToWorld(Home, Map.Felucca);
        }

        // ----------------------------------------------------------------

        private void StartChampRun()
        {
            _targetSpawn = FindBestFeluccaSpawn();

            if (_targetSpawn == null)
                return; // no surface Felucca spawn found — skip this run cycle

            _champPhase = ChampPhase.GatherAtBank;
            FightMode   = FightMode.None;

            // Pull every other Iron Company member to the same spawn
            PlayerSimulatorManager.BroadcastChampRun(_targetSpawn, this);

            // Sergeant Vale announces the destination to the world
            if (MemberName == "Sergeant Vale" && _targetSpawn != null)
            {
                string dest = string.IsNullOrEmpty(_targetSpawn.SpawnName)
                    ? $"({_targetSpawn.X}, {_targetSpawn.Y})"
                    : _targetSpawn.SpawnName;

                World.Broadcast(0x8C4, true,
                    $"[Iron Company] Sergeant Vale: \"Iron Company, move out! " +
                    $"We march on {dest}. All members rally at once!\"");
            }

            // Walk to nearest city bank — a short, reliable trip
            StartTravelTo(_bankLocation, TimeSpan.FromMinutes(5));
            Say(DepartureSpeech[Utility.Random(DepartureSpeech.Length)]);

            // 90 s: bank speech (wherever they are — even if stuck in a building)
            Timer.DelayCall(TimeSpan.FromSeconds(90), () =>
            {
                if (!Deleted && _champPhase == ChampPhase.GatherAtBank)
                    ArriveAtBank();
            });

            // 2 min: Sacred Journey fires unconditionally from wherever they stand.
            // This guarantees departure even if the state-machine WaitingAtBank
            // check never triggers (e.g. stuck inside a building, can't path to bank).
            Timer.DelayCall(TimeSpan.FromMinutes(2), () =>
            {
                if (!Deleted && (_champPhase == ChampPhase.GatherAtBank || _champPhase == ChampPhase.WaitingAtBank))
                    SacredJourneyToSpawn();
            });
        }

        private void ArriveAtBank()
        {
            if (_champPhase != ChampPhase.GatherAtBank) return; // already advanced

            _champPhase = ChampPhase.WaitingAtBank;
            _departAt   = DateTime.UtcNow + TimeSpan.FromMinutes(1); // 1-min wait starts NOW

            Say(GatherSpeech[Utility.Random(GatherSpeech.Length)]);

            // Open the bank after 1 second (flavour)
            Timer.DelayCall(TimeSpan.FromSeconds(1.0), () =>
            {
                if (!Deleted && _champPhase == ChampPhase.WaitingAtBank)
                    Say("bank");
            });
        }

        private void SacredJourneyToSpawn()
        {
            _champPhase     = ChampPhase.AtSpawn; // prevent re-entry on next tick
            _champAnnounced = false;              // reset so champKilled can't false-trigger before arrival
            _leaveSpawnAt   = DateTime.UtcNow + TimeSpan.FromHours(2); // set NOW — TickAtSpawn fires before ArriveAtSpawn

            if (_targetSpawn == null || _targetSpawn.Deleted)
            {
                // Spawn is gone — abort run
                _champPhase   = ChampPhase.None;
                _targetSpawn  = null;
                _leaveSpawnAt = DateTime.MinValue;
                _nextChampRun = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(10, 20));
                return;
            }

            Say(DepartureSpeech[Utility.Random(DepartureSpeech.Length)]);
            FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);
            PlaySound(0x1F5); // chivalry casting sound

            ChampionSpawn dest = _targetSpawn;
            Timer.DelayCall(TimeSpan.FromSeconds(2.0), () =>
            {
                if (Deleted) return;

                // Abort if BeginWithdraw already fired during the 2-second delay
                if (_champPhase != ChampPhase.AtSpawn || _targetSpawn == null || _targetSpawn.Deleted)
                    return;

                // Find a walkable tile near the altar
                Point3D landing = Point3D.Zero;
                for (int i = 0; i < 15; i++)
                {
                    int ox = dest.X + Utility.RandomMinMax(-8, 8);
                    int oy = dest.Y + Utility.RandomMinMax(-8, 8);
                    int oz = Map.Felucca.GetAverageZ(ox, oy);
                    if (Map.Felucca.CanSpawnMobile(ox, oy, oz))
                    { landing = new Point3D(ox, oy, oz); break; }
                }
                if (landing == Point3D.Zero)
                    landing = dest.Location;

                MoveToWorld(landing, Map.Felucca);
                _spawnEntryPoint = landing; // remember where we arrived
                ForceIdle(); // reset SimState after teleport
                ArriveAtSpawn();
            });
        }

        private void ArriveAtSpawn()
        {
            _champPhase       = ChampPhase.AtSpawn;
            FightMode         = FightMode.Closest;
            _leaveSpawnAt     = DateTime.UtcNow + TimeSpan.FromHours(2); // hard cap
            _champAnnounced   = false;

            if (_targetSpawn != null && !_targetSpawn.Deleted)
            {
                _lastKnownLevel = _targetSpawn.Level;

                // Activate the spawn if nobody has started it yet
                if (!_targetSpawn.Active)
                    _targetSpawn.Active = true;
            }
            else
            {
                _lastKnownLevel = 0;
            }

            // 50% faster movement at the champion spawn
            if (_baseActiveSpeed <= 0.0) _baseActiveSpeed = ActiveSpeed;
            ActiveSpeed  = Math.Max(0.05, _baseActiveSpeed * 0.5);
            CurrentSpeed = ActiveSpeed;

            // Non-zero team so spawn monsters recognise us as enemies and retaliate
            Team = 1;

            Say(ArrivalSpeech[Utility.Random(ArrivalSpeech.Length)]);
        }

        private void TickAtSpawn()
        {
            // Spawn reference gone — withdraw immediately, don't wait for the hard cap
            if (_targetSpawn == null || _targetSpawn.Deleted)
            {
                BeginWithdraw(false);
                return;
            }

            int level = _targetSpawn.Level;

            // Announce each tier advance — Sergeant Vale also fires a Battlecry buff
            if (level > _lastKnownLevel && level >= 1)
            {
                int idx = Math.Min(level - 1, TierSpeech.Length - 1);
                Say(TierSpeech[idx]);
                _lastKnownLevel = level;

                if (MemberName == "Sergeant Vale")
                    BattlecrySystem.ApplyBattlecry(this);
            }

            // Detect and directly engage champion when it spawns
            if (!_champAnnounced
                && _targetSpawn.Champion != null
                && !_targetSpawn.Champion.Deleted)
            {
                _champAnnounced = true;
                Combatant = _targetSpawn.Champion;

                // Start repeating battlecry every 2 minutes while champion is alive
                if (MemberName == "Sergeant Vale")
                    StartBossBattlecryTimer();
            }

            // Detect champion killed → victory
            bool champKilled = _champAnnounced
                && (_targetSpawn.Champion == null || _targetSpawn.Champion.Deleted);

            if (champKilled)
            {
                Say(WithdrawSpeech[0]); // "Champion down! Iron Company stands!"
                BeginWithdraw(true);
                return;
            }

            // Hard 2-hour timeout
            if (DateTime.UtcNow >= _leaveSpawnAt)
            {
                BeginWithdraw(false);
                return;
            }

            // Never fight friendly SimPlayers — clear if the AI auto-acquired one.
            // Hostile SimPlayers (AlwaysAttackable or AlwaysMurderer) are fair game.
            if (Combatant is SimPlayer sp && !sp.AlwaysAttackable && !sp.AlwaysMurderer)
                Combatant = null;

            // Never auto-attack innocent (blue) real players.
            // Only engage them if they damaged us first (retaliation window = 30s).
            if (Combatant is PlayerMobile bluePm && bluePm.Kills < 5 && !IsRetaliationTarget(bluePm))
                Combatant = null;

            // Never attack player pets unless they (or their owner) attacked us first.
            if (Combatant is BaseCreature pet && pet.Controlled && pet.ControlMaster is PlayerMobile petOwner
                && petOwner.Kills < 5 && !IsRetaliationTarget(pet) && !IsRetaliationTarget(petOwner))
                Combatant = null;

            // Track how long we've had this specific combatant
            if (Combatant == null)
            {
                if (_lastCombatantSerial != Serial.Zero)
                {
                    // Combat ended — reset stuck phase and re-equip melee if needed
                    if (_stuckPhase == StuckPhase.CrossbowOut) EquipMelee();
                    _stuckPhase          = StuckPhase.None;
                    _lastCombatantSerial = Serial.Zero;
                    _combatantSince      = DateTime.MinValue;
                }
            }
            else if (Combatant.Serial != _lastCombatantSerial)
            {
                // New target — reset stuck phase
                if (_stuckPhase == StuckPhase.CrossbowOut) EquipMelee();
                _stuckPhase          = StuckPhase.None;
                _lastCombatantSerial = Combatant.Serial;
                _combatantSince      = DateTime.UtcNow;
            }

            // No target — scan and optionally return to entry
            if (Combatant == null || Combatant.Deleted || !Combatant.Alive)
            {
                AcquireSpawnTarget();

                if (Combatant == null && _spawnEntryPoint != Point3D.Zero
                    && GetDistanceToSqrt(_spawnEntryPoint) > 20
                    && DateTime.UtcNow >= _nextReturnAt)
                {
                    TeleportToEntryPoint();
                }
            }
            else
            {
                double distFromEntry = _spawnEntryPoint != Point3D.Zero
                    ? GetDistanceToSqrt(_spawnEntryPoint)
                    : (_targetSpawn != null ? GetDistanceToSqrt(_targetSpawn.Location) : 0.0);

                double distToTarget = GetDistanceToSqrt(Combatant);

                // Chased too far outside spawn — abort and return
                if (distFromEntry > 80 && DateTime.UtcNow >= _nextReturnAt)
                {
                    if (_stuckPhase == StuckPhase.CrossbowOut) EquipMelee();
                    _stuckPhase = StuckPhase.None;
                    Combatant   = null;
                    TeleportToEntryPoint();
                }
                else if (distToTarget <= 4 && _stuckPhase == StuckPhase.CrossbowOut)
                {
                    // Managed to close the gap — swap back to melee
                    EquipMelee();
                    _stuckPhase = StuckPhase.None;
                }
                else
                {
                    // 3-phase stuck resolution
                    switch (_stuckPhase)
                    {
                        case StuckPhase.None:
                            // Stuck in melee range for 6s → teleport
                            if (distToTarget > 6
                                && _combatantSince != DateTime.MinValue
                                && DateTime.UtcNow >= _combatantSince + TimeSpan.FromSeconds(6))
                            {
                                TeleportToCombatant(Combatant as Mobile);
                                _stuckPhase  = StuckPhase.Teleported;
                                _stuckPhaseAt = DateTime.UtcNow;
                            }
                            break;

                        case StuckPhase.Teleported:
                            // 5s after teleport still out of range → pull out crossbow
                            if (distToTarget > 6
                                && DateTime.UtcNow >= _stuckPhaseAt + TimeSpan.FromSeconds(5))
                            {
                                EquipCrossbow();
                                _stuckPhase   = StuckPhase.CrossbowOut;
                                _stuckPhaseAt = DateTime.UtcNow;
                            }
                            break;

                        case StuckPhase.CrossbowOut:
                            // 15s with crossbow, still no progress → give up, return
                            if (DateTime.UtcNow >= _stuckPhaseAt + TimeSpan.FromSeconds(15))
                            {
                                // Blacklist this target so we don't immediately re-acquire it
                                if (Combatant != null)
                                    _blockedTargets[Combatant.Serial] = DateTime.UtcNow + BlockDuration;

                                EquipMelee();
                                _stuckPhase = StuckPhase.None;
                                Combatant   = null;
                                TeleportToEntryPoint();
                            }
                            break;
                    }
                }
            }
        }

        // Scan cooldown — don't scan every single tick when idle at spawn
        private DateTime _nextScanAt = DateTime.MinValue;

        /// <summary>
        /// Actively hunts for targets within the 80-tile spawn area.
        /// Prefers the champion, then the nearest creature.
        /// If the target is far away (>15 tiles), teleports toward it immediately
        /// rather than waiting for the AI to path there.
        /// </summary>
        private void AcquireSpawnTarget()
        {
            if (DateTime.UtcNow < _nextScanAt) return;
            _nextScanAt = DateTime.UtcNow + TimeSpan.FromSeconds(2);

            // Prune expired blacklist entries
            var expired = new List<Serial>();
            foreach (var kvp in _blockedTargets)
                if (DateTime.UtcNow >= kvp.Value) expired.Add(kvp.Key);
            foreach (var s in expired)
                _blockedTargets.Remove(s);

            // Champion is always top priority — never blacklisted
            if (_targetSpawn != null && !_targetSpawn.Deleted
                && _targetSpawn.Champion != null && !_targetSpawn.Champion.Deleted
                && _targetSpawn.Champion.Alive)
            {
                Mobile champ = _targetSpawn.Champion;
                Combatant = champ;
                if (GetDistanceToSqrt(champ) > 15)
                    TeleportToCombatant(champ);
                return;
            }

            // Scan up to 80 tiles — full spawn hunt radius
            Mobile nearest     = null;
            double nearestDist = double.MaxValue;

            foreach (Mobile m in GetMobilesInRange(80))
            {
                if (m == this || m.Deleted || !m.Alive) continue;
                if (m is SimPlayer sp2 && !sp2.AlwaysAttackable && !sp2.AlwaysMurderer) continue;
                if (!(m is BaseCreature bc) || bc.Controlled) continue;
                if (!CanBeHarmful(m, false)) continue;
                if (_blockedTargets.ContainsKey(m.Serial)) continue; // skip unreachable targets

                double dist = GetDistanceToSqrt(m);
                if (dist < nearestDist) { nearestDist = dist; nearest = m; }
            }

            if (nearest == null) return;

            Combatant = nearest;

            // Target is beyond normal AI range — teleport toward it to start the engagement
            if (nearestDist > 15)
                TeleportToCombatant(nearest);
        }

        /// <summary>
        /// Phase 1 stuck response: teleport next to the target.
        /// </summary>
        private void TeleportToCombatant(Mobile target)
        {
            if (target == null) return;

            FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);
            PlaySound(0x1F5);

            Timer.DelayCall(TimeSpan.FromSeconds(1.0), () =>
            {
                if (Deleted || _champPhase != ChampPhase.AtSpawn) return;
                if (target == null || target.Deleted || !target.Alive) return;

                Point3D landing = Point3D.Zero;
                for (int i = 0; i < 12; i++)
                {
                    int ox = target.X + Utility.RandomMinMax(-2, 2);
                    int oy = target.Y + Utility.RandomMinMax(-2, 2);
                    int oz = Map.GetAverageZ(ox, oy);
                    if (Map.CanSpawnMobile(ox, oy, oz)) { landing = new Point3D(ox, oy, oz); break; }
                }
                if (landing == Point3D.Zero) landing = target.Location;

                MoveToWorld(landing, Map);
                FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);

                if (!target.Deleted && target.Alive)
                    Combatant = target;
            });
        }

        /// <summary>Phase 2: swap to heavy crossbow for ranged engagement.</summary>
        private void EquipCrossbow()
        {
            if (Backpack == null) return;
            var xbow = Backpack.FindItemByType(typeof(HeavyCrossbow)) as HeavyCrossbow;
            if (xbow == null) return;

            var current = Weapon as BaseWeapon;
            if (current != null && !(current is HeavyCrossbow))
                PackItem(current);

            EquipItem(xbow);
        }

        /// <summary>Swap back to longsword after ranged phase ends.</summary>
        private void EquipMelee()
        {
            if (Backpack == null) return;
            var sword = Backpack.FindItemByType(typeof(Longsword)) as Longsword;
            if (sword == null) return;

            var current = Weapon as BaseWeapon;
            if (current != null && !(current is Longsword))
                PackItem(current);

            EquipItem(sword);
        }

        /// <summary>
        /// Sacred Journey back to the entry point of the champion spawn.
        /// Called when combat ends and the member has drifted far from their position.
        /// </summary>
        private void TeleportToEntryPoint()
        {
            _nextReturnAt = DateTime.UtcNow + TimeSpan.FromSeconds(10); // 10s cooldown

            FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);
            PlaySound(0x1F5);

            Timer.DelayCall(TimeSpan.FromSeconds(1.0), () =>
            {
                if (Deleted || _champPhase != ChampPhase.AtSpawn) return;
                if (_spawnEntryPoint == Point3D.Zero) return;

                MoveToWorld(_spawnEntryPoint, Map.Felucca);
                FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);

                // Immediately look for next target
                AcquireSpawnTarget();
            });
        }


        /// <summary>
        /// Called on victory. Scans the ground within 30 tiles for Gold items
        /// and removes 66% of the total — Iron Company takes their cut.
        /// Only one member should call this per victory (handled by the coordinator).
        /// </summary>
        private void CollectGroundGold(Point3D center, Map map)
        {
            if (Deleted || map == null || map == Map.Internal) return;

            var goldPiles = new List<Gold>();

            IPooledEnumerable<Item> eable = map.GetItemsInRange(center, 30);
            foreach (Item item in eable)
            {
                if (item is Gold g && !g.Deleted && g.Parent == null)
                    goldPiles.Add(g);
            }
            eable.Free();

            if (goldPiles.Count == 0) return;

            int totalGold = 0;
            foreach (Gold g in goldPiles)
                totalGold += g.Amount;

            int toTake    = (int)(totalGold * 0.80);
            int remaining = toTake;

            foreach (Gold g in goldPiles)
            {
                if (remaining <= 0) break;
                int take  = Math.Min(g.Amount, remaining);
                g.Amount -= take;
                if (g.Amount <= 0) g.Delete();
                remaining -= take;
            }

            Say($"Iron Company claims their due — {toTake:N0} gold.");
        }

        private void BeginWithdraw(bool victory)
        {
            // Stop boss battlecry timer if running
            StopBossBattlecryTimer();

            // Clean up phase immediately so no other code re-enters
            _champPhase   = ChampPhase.None;
            _targetSpawn  = null;
            FightMode     = FightMode.None;
            Combatant     = null;
            _blockedTargets.Clear(); // reset for next run
            _isDowned   = false;    // ensure clean state
            FightMode   = FightMode.None; // in case we were downed mid-fight
            _nextChampRun = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(60, 120));

            // Also abort any active dungeon run so movement is restored cleanly
            if (_dungeonPhase != DungeonPhase.None)
            {
                _dungeonPhase       = DungeonPhase.None;
                _dungeonAnchor      = Point3D.Zero;
                _dungeonAnchorMap   = Map.Felucca;
                FightMode           = FightMode.None;
                Combatant           = null;
            }

            // Abort HQ visit if active
            if (_hqPhase != HQPhase.None)
            {
                _hqPhase     = HQPhase.None;
                _nextHQVisit = DateTime.UtcNow
                    + TimeSpan.FromMinutes(Utility.RandomMinMax(30, 60));
            }

            // Restore normal movement speed and team
            if (_baseActiveSpeed > 0.0)
            {
                ActiveSpeed      = _baseActiveSpeed;
                CurrentSpeed     = ActiveSpeed;
                _baseActiveSpeed = 0.0;
            }
            Team = 0;

            if (victory)
            {
                Say(WithdrawSpeech[0]); // "Champion down! Iron Company stands!"

                // Only the first member to trigger victory collects gold —
                // guard with the spawn serial so the other 17 skip it.
                if (_targetSpawn != null && _goldCollectedForSpawn.Add(_targetSpawn.Serial))
                {
                    Serial  capturedSerial = _targetSpawn.Serial;
                    Point3D capturedLoc    = Location;
                    Map     capturedMap    = Map;

                    Timer.DelayCall(TimeSpan.FromSeconds(4.0), () =>
                    {
                        CollectGroundGold(capturedLoc, capturedMap);
                        // Remove after 10 min so the set doesn't grow forever
                        Timer.DelayCall(TimeSpan.FromMinutes(10), () =>
                            _goldCollectedForSpawn.Remove(capturedSerial));
                    });
                }
            }
            else
            {
                Say(WithdrawSpeech[Utility.Random(1, WithdrawSpeech.Length - 1)]);
            }

            // Sacred Journey back home after a short pause
            Timer.DelayCall(TimeSpan.FromSeconds(3.0), () =>
            {
                if (Deleted || Map == Map.Internal) return;
                FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);
                PlaySound(0x1F5);

                Timer.DelayCall(TimeSpan.FromSeconds(2.0), () =>
                {
                    if (!Deleted)
                    {
                        MoveToWorld(_homeLocation, Map.Felucca);
                        ForceIdle();
                    }
                });
            });
        }

        // -- Spawn selection ------------------------------------------

        /// <summary>
        /// Finds the best ChampionSpawn to run on Felucca.
        /// Preference order:
        ///   1. Active spawns — join a run already in progress (score = dist × 0.5)
        ///   2. Inactive spawns closest to home (we activate them ourselves)
        /// Underground spawns (Z &lt; -5) are skipped — Iron Company walks, not teleports.
        /// Returns null if the shard has no Felucca champion spawns configured.
        /// </summary>
        // ── Boss battlecry timer ──────────────────────────────────────────────

        private void StartBossBattlecryTimer()
        {
            StopBossBattlecryTimer();
            _bossBattlecryTimer = new BossBattlecryTimer(this);
            _bossBattlecryTimer.Start();
        }

        private void StopBossBattlecryTimer()
        {
            _bossBattlecryTimer?.Stop();
            _bossBattlecryTimer = null;
        }

        private class BossBattlecryTimer : Timer
        {
            private readonly IronCompanySimPlayer _vale;

            public BossBattlecryTimer(IronCompanySimPlayer vale)
                : base(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2))
            {
                _vale    = vale;
                Priority = TimerPriority.OneMinute;
            }

            protected override void OnTick()
            {
                if (_vale == null || _vale.Deleted || !_vale.Alive)
                {
                    Stop();
                    return;
                }
                // Stop if champion is gone (already handled by BeginWithdraw, but be safe)
                if (_vale._champPhase != ChampPhase.AtSpawn)
                {
                    Stop();
                    return;
                }
                BattlecrySystem.ApplyBattlecry(_vale);
            }
        }

        private ChampionSpawn FindBestFeluccaSpawn()
        {
            // Build weighted candidate list — all valid Felucca spawns are eligible.
            // Active spawns get 3 tickets (more likely to be joined), inactive get 1.
            // This ensures every spawn including distant ones (Abyss etc.) can be chosen.
            var candidates = new List<ChampionSpawn>();

            foreach (ChampionSpawn cs in ChampionSystem.AllSpawns)
            {
                if (cs == null || cs.Deleted)   continue;
                if (cs.Map != Map.Felucca)       continue;
                if (cs.Location.Z < -5)          continue; // skip underground altars
                if (IsAbyssSpawn(cs))            continue; // Iron Company never runs the Abyss

                int tickets = cs.Active ? 3 : 1;
                for (int i = 0; i < tickets; i++)
                    candidates.Add(cs);
            }

            if (candidates.Count == 0)
                return null;

            return candidates[Utility.Random(candidates.Count)];
        }

        /// <summary>Returns true if this spawn is the Abyss — Iron Company never runs it.</summary>
        public static bool IsAbyssSpawn(ChampionSpawn cs)
        {
            if (cs == null) return false;
            string typeName = cs.Type.ToString();
            return typeName.IndexOf("Abyss", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double DistanceTo(Point3D a, Point3D b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // -- Staff hooks -----------------------------------------------

        [CommandProperty(AccessLevel.GameMaster)]
        public string ChampPhaseInfo => GetStatusDetail();

        /// <summary>Returns the spawn the Iron Company is actively running toward or fighting,
        /// or null if they are idle/resting.</summary>
        public ChampionSpawn ActiveTargetSpawn =>
            (_champPhase != ChampPhase.None && _targetSpawn != null && !_targetSpawn.Deleted)
                ? _targetSpawn : null;

        public override string GetStatusDetail()
        {
            if (_isDowned)
                return "DOWNED";

            if (_champPhase != ChampPhase.None)
            {
                string spawnInfo = (_targetSpawn != null && !_targetSpawn.Deleted)
                    ? string.Format(" spawn={0} lvl={1}/4", _targetSpawn.SpawnName ?? "?", _targetSpawn.Level)
                    : " (no spawn ref)";
                return string.Format("Champ:{0}{1}", _champPhase, spawnInfo);
            }

            if (_dungeonPhase != DungeonPhase.None)
                return string.Format("Dungeon:{0}", _dungeonPhase);

            if (_hqPhase == HQPhase.AtHQ)
                return "AtHQ";

            TimeSpan remaining = _nextChampRun - DateTime.UtcNow;
            return remaining > TimeSpan.Zero
                ? string.Format("Idle — champ in {0}m {1}s", (int)remaining.TotalMinutes, remaining.Seconds)
                : "Idle — champ ready";
        }

        public override string TriggerNextEvent()
        {
            if (_champPhase != ChampPhase.None)
                return $"{MemberName} is already on a champ run (phase: {_champPhase}) — use [simreset to abort.";

            _champScheduleSet = true;
            _nextChampRun     = DateTime.UtcNow; // fire on the very next idle tick
            return $"{MemberName}: champ run queued — will depart on the next Idle tick.";
        }

        /// <summary>
        /// Force-starts a champ run to a specific spawn immediately.
        /// Used by the [simchamp GM command.
        /// Aborts any current run, activates the SimPlayer if not in world,
        /// and begins travelling to the spawn.
        /// </summary>
        public string ForceChampRunAt(ChampionSpawn spawn)
        {
            // Abort any current run
            if (_champPhase != ChampPhase.None)
            {
                _champPhase = ChampPhase.None;
                FightMode   = FightMode.None;
                Combatant   = null;
            }

            // Bring into world if inactive
            if (Map == Map.Internal)
                Activate();

            _targetSpawn      = spawn;
            _champPhase       = ChampPhase.GatherAtBank;
            _champScheduleSet = true;
            _champAnnounced   = false;
            _lastKnownLevel   = spawn.Level;
            FightMode         = FightMode.None;

            // Walk to local bank — ArriveAtBank() sets _departAt when they get there
            StartTravelTo(_bankLocation, TimeSpan.FromMinutes(5));
            Say(DepartureSpeech[Utility.Random(DepartureSpeech.Length)]);

            // 90 s: bank speech (wherever they are)
            Timer.DelayCall(TimeSpan.FromSeconds(90), () =>
            {
                if (!Deleted && _champPhase == ChampPhase.GatherAtBank)
                    ArriveAtBank();
            });

            // 2 min: unconditional Sacred Journey — fires from wherever they stand
            Timer.DelayCall(TimeSpan.FromMinutes(2), () =>
            {
                if (!Deleted && (_champPhase == ChampPhase.GatherAtBank || _champPhase == ChampPhase.WaitingAtBank))
                    SacredJourneyToSpawn();
            });

            string spawnName = spawn.SpawnName ?? $"({spawn.X},{spawn.Y})";
            return $"{MemberName}: rallying at bank, Sacred Journey to {spawnName} in ~2.5 min.";
        }

        // -- Template --------------------------------------------------
        protected override void ApplyTemplate()
        {
            SetStr(150, 150);
            SetDex(100, 100);
            SetInt(40,  40);
            SetHits(350, 350);
            SetSkill(SkillName.Swords,      115.0, 115.0);
            SetSkill(SkillName.Tactics,     115.0, 115.0);
            SetSkill(SkillName.Anatomy,     110.0, 110.0);
            SetSkill(SkillName.Healing,     110.0, 110.0);
            SetSkill(SkillName.Parry,       110.0, 110.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);
            SetSkill(SkillName.Chivalry,    100.0, 100.0);
            SetSkill(SkillName.Archery,     100.0, 100.0);
            VirtualArmor  = 65;
            Fame          = 2000;
            Karma         = 2000;
            Kills         = 0;
            ActiveSpeed   = 0.13; // ~50% faster than default 0.2

            // Full plate — max durability so RepairEquippedGear has room to work
            var chest  = new PlateChest();   chest.MaxHitPoints  = 255; chest.HitPoints  = 255; AddItem(chest);
            var legs   = new PlateLegs();    legs.MaxHitPoints   = 255; legs.HitPoints   = 255; AddItem(legs);
            var arms   = new PlateArms();    arms.MaxHitPoints   = 255; arms.HitPoints   = 255; AddItem(arms);
            var gloves = new PlateGloves();  gloves.MaxHitPoints = 255; gloves.HitPoints = 255; AddItem(gloves);
            var gorget = new PlateGorget();  gorget.MaxHitPoints = 255; gorget.HitPoints = 255; AddItem(gorget);
            var helm   = new PlateHelm();    helm.MaxHitPoints   = 255; helm.HitPoints   = 255; AddItem(helm);
            AddItem(new Boots());
            var shield = new HeaterShield(); shield.MaxHitPoints = 255; shield.HitPoints = 255; AddItem(shield);

            // Longsword with hit harm — single-target, no AOE splash on pets
            var sword = new Longsword();
            sword.MaxHitPoints                  = 255;
            sword.HitPoints                     = 255;
            sword.WeaponAttributes.HitHarm      = 70;
            sword.WeaponAttributes.HitLeechHits = 40; // 40% life leech
            sword.Attributes.WeaponDamage       = 50;
            sword.Attributes.AttackChance       = 15;
            AddItem(sword);

            PackItem(new BookOfChivalry()); // always PackItem

            // Ranged fallback: magic heavy crossbow stored in pack
            // EquipCrossbow() swaps to this when stuck on unreachable targets
            var xbow = new HeavyCrossbow();
            xbow.WeaponAttributes.HitLightning = 70; // single-target, no AOE splash on pets
            xbow.Attributes.WeaponDamage       = 50;
            xbow.Attributes.AttackChance       = 15;
            PackItem(xbow);
            PackItem(new Bolt(500)); // 500 bolts

            // Weapon specials — AI uses these automatically in combat
            SetWeaponAbility(WeaponAbility.ArmorIgnore);    // Longsword primary — bypasses armor
            SetWeaponAbility(WeaponAbility.ConcussionBlow); // Longsword secondary — reduces target Int
        }

    }

    // ============================================================
    // Arcane Brotherhood
    // War Mage template -- robes, spellbook
    // ============================================================
    public class ArcaneBrotherhoodSimPlayer : SimPlayer
    {
        // ---- Transient AI cooldowns (reset on restart, that's fine) ----
        private DateTime _nextBuffTime   = DateTime.MinValue;
        private DateTime _nextPortalTime = DateTime.MinValue;
        private DateTime _nextFlavorTime = DateTime.MinValue;
        private bool     _isOnExpedition = false;

        // Per-player buff tracking: Serial → last buff time
        private readonly Dictionary<int, DateTime> _buffedPlayers = new Dictionary<int, DateTime>();

        private static readonly TimeSpan BuffDuration = TimeSpan.FromMinutes(15);
        private static readonly int      BuffBonus    = 10;

        // Dungeon destinations the Brotherhood likes to research
        private static readonly (Point3D Loc, Map Map, string Name)[] _dungeons =
        {
            (new Point3D(5456, 1863,   0), Map.Felucca, "Covetous"),
            (new Point3D(5188,  638,   0), Map.Felucca, "Deceit"),
            (new Point3D(5501,  570,  59), Map.Felucca, "Despise"),
            (new Point3D(5243, 1006,   0), Map.Felucca, "Destard"),
            (new Point3D(5905,   20,  46), Map.Felucca, "Hythloth"),
            (new Point3D(5395,  126,   0), Map.Felucca, "Shame"),
            (new Point3D(5825,  630,   0), Map.Felucca, "Wrong"),
            (new Point3D(5571, 1302,   0), Map.Felucca, "Khaldun"),
            (new Point3D(5875,  150,  15), Map.Felucca, "the Ice Dungeon"),
            (new Point3D(5790, 1416,  40), Map.Felucca, "the Fire Dungeon"),
        };

        private static readonly string[] _flavorLines =
        {
            "The ley lines pulse with unusual energy today...",
            "I sense an elemental convergence forming nearby.",
            "The weave of magic is particularly strong in this place.",
            "*scribbles notes on a piece of parchment*",
            "Fascinating... the mana currents shift with the tides.",
            "The ancient texts speak of such arcane patterns.",
            "One must study the flow of magical energies constantly.",
            "The Brotherhood's research continues unabated.",
            "These reagent concentrations are worth documenting.",
            "*mutters an incantation under their breath*",
        };

        // ----------------------------------------------------------------

        public ArcaneBrotherhoodSimPlayer(string memberName, Point3D home,
                                          SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.ArcaneBrotherhood, memberName, home, zone, schedule) { }

        public ArcaneBrotherhoodSimPlayer(Serial serial) : base(serial) { }

        protected override void ApplyTemplate()
        {
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

        // -- Idle hook ---------------------------------------------------

        protected override void OnTickIdle()
        {
            if (_isOnExpedition) return; // hands-off while in a dungeon

            if (DateTime.UtcNow >= _nextFlavorTime)
                TryFlavorSpeech();

            if (DateTime.UtcNow >= _nextBuffTime)
                TryBuff();

            if (DateTime.UtcNow >= _nextPortalTime)
                TryOpenPortal();
        }

        // -- Flavor speech -----------------------------------------------

        private void TryFlavorSpeech()
        {
            Say(_flavorLines[Utility.Random(_flavorLines.Length)]);
            _nextFlavorTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(2, 5));
        }

        // -- Buff a nearby player ----------------------------------------

        private void TryBuff()
        {
            PlayerMobile target = null;

            foreach (Mobile m in GetMobilesInRange(8))
            {
                if (!(m is PlayerMobile pm)) continue;
                if (pm.Deleted || !pm.Alive)  continue;
                if (pm.AccessLevel != AccessLevel.Player) continue;
                if (pm.Map != Map) continue;

                // Per-player 15-minute cooldown
                if (_buffedPlayers.TryGetValue((int)pm.Serial, out DateTime last)
                    && DateTime.UtcNow - last < BuffDuration)
                    continue;

                target = pm;
                break;
            }

            if (target == null)
            {
                _nextBuffTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));
                return;
            }

            // Cast animation + speech
            Animate(203, 7, 1, true, false, 0);
            Say($"*extends an arcane blessing upon {target.Name}*");

            // Unique key per mage so multiple mages stack independently
            string keyBase = $"ArcBro_{(int)Serial}";
            target.AddStatMod(new StatMod(StatType.Str, keyBase + "_S", BuffBonus, BuffDuration));
            target.AddStatMod(new StatMod(StatType.Dex, keyBase + "_D", BuffBonus, BuffDuration));
            target.AddStatMod(new StatMod(StatType.Int, keyBase + "_I", BuffBonus, BuffDuration));

            target.FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);
            target.PlaySound(0x1EE);
            target.SendMessage(0x35,
                $"{Name} bestows an arcane blessing upon you. " +
                $"(+{BuffBonus} STR / DEX / INT for 15 minutes)");

            _buffedPlayers[(int)target.Serial] = DateTime.UtcNow;
            _nextBuffTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(60, 120));
        }

        // -- Open a dungeon portal ---------------------------------------

        private void TryOpenPortal()
        {
            var dungeon = _dungeons[Utility.Random(_dungeons.Length)];

            Animate(203, 7, 1, true, false, 0);
            Say($"*feels the ley lines drawing toward {dungeon.Name}...*");

            Point3D here  = Location;
            Map     myMap = Map;

            // Small delay so the speech appears before the portal pops
            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (Deleted) return;

                var portal = new BrotherhoodPortal(dungeon.Loc, dungeon.Map, dungeon.Name);
                portal.MoveToWorld(here, myMap);

                Effects.SendLocationParticles(
                    EffectItem.Create(here, myMap, EffectItem.DefaultDuration),
                    0x376A, 9, 32, 5023);
                Effects.PlaySound(here, myMap, 0x20E);

                // 50% chance: step through and investigate
                if (Utility.RandomBool())
                    EnterDungeon(dungeon.Loc, dungeon.Map, dungeon.Name);
            });

            _nextPortalTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(5, 10));
        }

        // -- Dungeon expedition logic ------------------------------------

        private void EnterDungeon(Point3D dest, Map destMap, string name)
        {
            if (Deleted || !Alive) return;

            _isOnExpedition = true;
            Point3D savedHome = _homeLocation;

            Timer.DelayCall(TimeSpan.FromSeconds(2.5), () =>
            {
                if (Deleted || !Alive) { _isOnExpedition = false; return; }

                Say("*steps through the portal*");
                MoveToWorld(dest, destMap);

                // Find a wild creature to engage after arriving
                Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
                {
                    if (Deleted || !Alive) { _isOnExpedition = false; return; }

                    BaseCreature combatTarget = null;
                    foreach (Mobile m in GetMobilesInRange(10))
                    {
                        if (m is BaseCreature bc
                            && !bc.Deleted && bc.Alive
                            && bc.ControlMaster == null  // wild, not a pet
                            && !bc.IsDeadBondedPet)
                        {
                            combatTarget = bc;
                            break;
                        }
                    }

                    if (combatTarget != null)
                    {
                        Say($"*engages a {combatTarget.Name}!*");
                        this.Combatant = combatTarget;
                    }
                    else
                    {
                        Say("*finds no prey nearby — recalls home*");
                    }

                    // Return home after 45-60 seconds regardless of outcome
                    int delay = Utility.RandomMinMax(45, 60);
                    Timer.DelayCall(TimeSpan.FromSeconds(delay), () => RecallHome(savedHome));
                });
            });
        }

        private void RecallHome(Point3D home)
        {
            _isOnExpedition = false;

            if (Deleted || !Alive) return; // death/respawn handles it

            this.Combatant = null;

            Animate(203, 7, 1, true, false, 0);
            Say("*recalls back to the city*");

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (!Deleted && Alive)
                    MoveToWorld(home, Map.Felucca);
            });
        }

        // ----------------------------------------------------------------

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

    // ============================================================
    // BrotherhoodPortal
    // A public one-way moongate opened by Arcane Brotherhood mages.
    // Anyone can step through — no player restriction.
    // Auto-closes after 3 minutes.
    // ============================================================
    public class BrotherhoodPortal : Item
    {
        private Point3D _destination;
        private Map     _destMap;
        private string  _dungeonName;

        public BrotherhoodPortal(Point3D destination, Map destMap, string dungeonName)
            : base(0xF6C)   // moongate graphic
        {
            _destination = destination;
            _destMap     = destMap;
            _dungeonName = dungeonName;

            Movable = false;
            Hue     = 1152;   // arcane blue
            Name    = $"a Brotherhood portal to {dungeonName}";
            Light   = LightType.Circle300;

            // Auto-close after 3 minutes
            Timer.DelayCall(TimeSpan.FromMinutes(3.0), ClosePortal);
        }

        public BrotherhoodPortal(Serial serial) : base(serial) { }

        public override bool OnMoveOver(Mobile m)
        {
            UsePortal(m);
            return true;
        }

        public override void OnDoubleClick(Mobile from)
        {
            UsePortal(from);
        }

        private void UsePortal(Mobile m)
        {
            if (Deleted || m == null || m.Deleted || !m.Alive) return;

            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            Effects.PlaySound(Location, Map, 0x1FE);

            m.MoveToWorld(_destination, _destMap);
            m.SendMessage(0x35, $"The Brotherhood portal carries you to {_dungeonName}...");

            Effects.SendLocationParticles(
                EffectItem.Create(_destination, _destMap, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            Effects.PlaySound(_destination, _destMap, 0x20E);
        }

        private void ClosePortal()
        {
            if (Deleted) return;
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                0x3728, 8, 20, 5042);
            Effects.PlaySound(Location, Map, 0x1FE);
            Delete();
        }

        // Transient — delete on server restart
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            Timer.DelayCall(TimeSpan.Zero, Delete);
        }
    }

    // ============================================================
    // Silver Wolves
    // Wolf Warrior template -- chain armour, patrol role
    // ============================================================
    public class SilverWolvesSimPlayer : SimPlayer
    {
        // ---- Transient AI state ----------------------------------------
        private bool     _isOnPatrol    = false;
        private DateTime _nextFlavorTime  = DateTime.MinValue;
        private DateTime _nextDetectTime  = DateTime.MinValue;
        private DateTime _nextJourneyTime = DateTime.MinValue;
        private DateTime _nextScoutTime   = DateTime.MinValue;

        // Dungeon entrance coords (overworld, outside dungeon mouths)
        // NOTE: these are approximate — verify/adjust in-game with [where
        private static readonly (Point3D Loc, Map Map, string Name)[] _dungeonEntrances =
        {
            (new Point3D(5290, 605,  0), Map.Felucca, "Deceit"),
            (new Point3D(5490, 534,  0), Map.Felucca, "Despise"),
            (new Point3D(5262, 977,  0), Map.Felucca, "Destard"),
            (new Point3D(5450, 1860, 0), Map.Felucca, "Covetous"),
            (new Point3D(5798, 636,  0), Map.Felucca, "Wrong"),
            (new Point3D(5381, 100,  0), Map.Felucca, "Shame"),
            (new Point3D(5910,  49,  0), Map.Felucca, "Hythloth"),
        };

        private static readonly string[] _flavorLines =
        {
            "*scans the area for threats*",
            "Stay vigilant. The Shadow Hand operates in these parts.",
            "These roads belong to the innocent.",
            "For the protection of all citizens.",
            "*checks patrol route*",
            "No murderer walks free while we stand watch.",
            "Criminal activity will not be tolerated here.",
            "The weak deserve protection. That is our purpose.",
        };

        // ----------------------------------------------------------------

        public SilverWolvesSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.SilverWolves, memberName, home, zone, schedule) { }

        public SilverWolvesSimPlayer(Serial serial) : base(serial) { }

        protected override void ApplyTemplate()
        {
            SetStr(80, 80);
            SetDex(65, 65);
            SetInt(35, 35);
            SetHits(110, 110);
            SetSkill(SkillName.Swords,        100.0, 100.0);
            SetSkill(SkillName.Tactics,       100.0, 100.0);
            SetSkill(SkillName.Anatomy,        90.0,  90.0);
            SetSkill(SkillName.Healing,        90.0,  90.0);
            SetSkill(SkillName.Parry,          90.0,  90.0);
            SetSkill(SkillName.MagicResist,    80.0,  80.0);
            SetSkill(SkillName.DetectHidden,   50.0,  50.0);
            VirtualArmor = 35;
            Fame  = 2000;
            Karma = 3000;
            Kills = 0;

            // --- Enchanted gear — drops on corpse naturally via AddItem ---

            // Longsword: Silver slayer (undead/vampires), +HCI, +DI
            var sword = new Longsword();
            sword.Slayer              = SlayerName.Silver;
            sword.Attributes.AttackChance  = 10;
            sword.Attributes.WeaponDamage  = 15;
            sword.MaxHitPoints = 200; sword.HitPoints = 200;
            AddItem(sword);

            // Chain armour: defensive bonuses on chest piece
            var chest = new ChainChest();
            chest.Attributes.DefendChance = 8;
            chest.Attributes.RegenHits    = 2;
            chest.MaxHitPoints = 200; chest.HitPoints = 200;
            AddItem(chest);

            // Heater shield: extra defence chance
            var shield = new HeaterShield();
            shield.Attributes.DefendChance = 7;
            shield.MaxHitPoints = 200; shield.HitPoints = 200;
            AddItem(shield);

            // Plain armour pieces
            AddItem(new ChainLegs());
            AddItem(new LeatherGorget());
            AddItem(new LeatherArms());
            AddItem(new LeatherGloves());
            AddItem(new Boots());

            // Jewelry
            var ring = new GoldRing();
            ring.Attributes.BonusHits  = 5;
            ring.Attributes.RegenHits  = 1;
            AddItem(ring);

            var bracelet = new GoldBracelet();
            bracelet.Attributes.BonusStam = 5;
            AddItem(bracelet);
        }

        // -- Idle hook ---------------------------------------------------

        protected override void OnTickIdle()
        {
            if (_isOnPatrol)
            {
                // While at a dungeon entrance — periodically scan for PKs
                if (DateTime.UtcNow >= _nextScoutTime)
                    TryScoutForPK();
                return;
            }

            if (DateTime.UtcNow >= _nextFlavorTime)
                TryFlavorSpeech();

            if (DateTime.UtcNow >= _nextDetectTime)
                TryDetectHidden();

            if (DateTime.UtcNow >= _nextJourneyTime)
                DoSacredJourney();
        }

        // -- Flavor speech -----------------------------------------------

        private void TryFlavorSpeech()
        {
            Say(_flavorLines[Utility.Random(_flavorLines.Length)]);
            _nextFlavorTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(3, 6));
        }

        // -- DetectHidden — counter the Shadow Hand in town --------------

        private void TryDetectHidden()
        {
            foreach (Mobile m in GetMobilesInRange(8))
            {
                if (!(m is ShadowHandSimPlayer sh) || !sh.Hidden || sh.Deleted || !sh.Alive)
                    continue;

                // Skill check: DetectHidden vs Stealth
                double detectChance = Skills[SkillName.DetectHidden].Value / 100.0 * 0.55;
                double stealthChance = sh.Skills[SkillName.Stealth].Value / 100.0 * 0.50;

                if (Utility.RandomDouble() < detectChance && Utility.RandomDouble() > stealthChance)
                {
                    sh.Hidden = false;
                    Say($"*calls out* Thief! {sh.Name} is unmasked!");
                    this.Combatant = sh;
                    break;
                }
            }

            _nextDetectTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));
        }

        // -- Sacred Journey to dungeon entrance --------------------------

        private void DoSacredJourney()
        {
            var entrance = _dungeonEntrances[Utility.Random(_dungeonEntrances.Length)];

            Animate(203, 7, 1, true, false, 0);
            Say($"*invokes Sacred Journey — scouting {entrance.Name}*");

            Point3D dest    = entrance.Loc;
            Map     destMap = entrance.Map;

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (Deleted || !Alive) return;

                // Depart effect
                Effects.SendLocationParticles(
                    EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                    0x376A, 9, 20, 2023);
                PlaySound(0x20E);

                MoveToWorld(dest, destMap);

                // Arrive effect
                Effects.SendLocationParticles(
                    EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                    0x376A, 9, 20, 2023);
                PlaySound(0x1FE);

                _isOnPatrol   = true;
                _nextScoutTime = DateTime.MinValue; // scan immediately on arrival

                // Return home after 60-90 seconds (extended if still in combat)
                int patrolTime = Utility.RandomMinMax(60, 90);
                Timer.DelayCall(TimeSpan.FromSeconds(patrolTime), ReturnHome);
            });

            _nextJourneyTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(8, 15));
        }

        // -- Scout dungeon entrance for PKs ------------------------------

        private void TryScoutForPK()
        {
            // Look for red (murderer) players
            foreach (Mobile m in GetMobilesInRange(15))
            {
                if (m is PlayerMobile pm && pm.Murderer && pm.Alive && !pm.Deleted && pm.Map == Map)
                {
                    Say($"*calls out* Murderer! {pm.Name} will face justice!");
                    this.Combatant = pm;
                    _nextScoutTime = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                    return;
                }
            }

            // Also engage PK NPCs if present
            foreach (Mobile m in GetMobilesInRange(15))
            {
                if (m is BasePKNPC pk && pk.Alive && !pk.Deleted && pk.Map == Map)
                {
                    Say("*draws sword* Hostile threat spotted!");
                    this.Combatant = pk;
                    _nextScoutTime = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                    return;
                }
            }

            Say("*area appears clear*");
            _nextScoutTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(15, 25));
        }

        // -- Return home via Sacred Journey ------------------------------

        private void ReturnHome()
        {
            // Stay if still actively fighting — check again in 30s
            if (Combatant != null && !Combatant.Deleted && Combatant.Alive)
            {
                Timer.DelayCall(TimeSpan.FromSeconds(30), ReturnHome);
                return;
            }

            _isOnPatrol = false;
            if (Deleted || !Alive) return;

            Animate(203, 7, 1, true, false, 0);
            Say("*invokes Sacred Journey — returning to the city*");

            Point3D home = _homeLocation;
            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (Deleted || !Alive) return;

                Effects.SendLocationParticles(
                    EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                    0x376A, 9, 20, 2023);
                PlaySound(0x20E);

                MoveToWorld(home, Map.Felucca);

                Effects.SendLocationParticles(
                    EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                    0x376A, 9, 20, 2023);
                PlaySound(0x1FE);
            });
        }

        // -- Death loot --------------------------------------------------

        public override void OnDeath(Container c)
        {
            base.OnDeath(c); // enchanted gear already drops via AddItem

            c.DropItem(new Gold(Utility.RandomMinMax(100, 300)));
            c.DropItem(new Bandage(Utility.RandomMinMax(10, 25)));

            // Healing potions (they're field medics)
            if (Utility.RandomDouble() < 0.50)
                c.DropItem(new HealPotion());
            if (Utility.RandomDouble() < 0.20)
                c.DropItem(new GreaterHealPotion());

            // Treasure map (2% L3, 13% L2)
            double mapRoll = Utility.RandomDouble();
            if (mapRoll < 0.02)
                c.DropItem(new TreasureMap(3, Map.Felucca));
            else if (mapRoll < 0.15)
                c.DropItem(new TreasureMap(2, Map.Felucca));

            // Random orb (1%)
            if (Utility.RandomDouble() < 0.01)
            {
                Type[] orbs = new Type[]
                {
                    typeof(OrbOfEnhancement), typeof(OrbOfMastery),   typeof(OrbOfExpansion),
                    typeof(OrbOfFortitude),   typeof(OrbOfAlacrity),   typeof(OrbOfInsight),
                    typeof(OrbOfBalance),     typeof(OrbOfCorruption), typeof(OrbOfResonance),
                    typeof(OrbOfCleansing),   typeof(OrbOfTempering),  typeof(OrbOfEnchantment),
                    typeof(OrbOfReforging)
                };
                c.DropItem((Item)Activator.CreateInstance(orbs[Utility.Random(orbs.Length)]));
            }
        }

        // ----------------------------------------------------------------

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

    // ============================================================
    // The Shadow Hand
    // Thief/Rogue template -- grey, hiding, town operations.
    // Pickpockets gold from nearby players while hidden.
    // Flees Silver Wolves. Never fights back.
    // ============================================================
    public class ShadowHandSimPlayer : SimPlayer
    {
        private DateTime _nextHideTime    = DateTime.MinValue;
        private DateTime _nextStealTime   = DateTime.MinValue; // transient — not serialized
        private DateTime _nextSnoopTime   = DateTime.MinValue; // transient — not serialized

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
            Hue   = 0;    // normal skin -- blend in

            // Dark, inconspicuous clothing
            var robe = new Robe();
            robe.Hue = 1109; // dark charcoal
            AddItem(robe);

            var boots = new Boots();
            boots.Hue = 1109;
            AddItem(boots);

            // Concealed weapon -- hidden under the robe
            AddItem(new Dagger());
        }

        // -- Idle hook -------------------------------------------------

        protected override void OnTickIdle()
        {
            // Periodically go Hidden
            if (DateTime.UtcNow >= _nextHideTime)
            {
                this.Hidden   = true;
                _nextHideTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(20, 60));
            }

            // Flee Silver Wolves on sight (priority — do before steal attempt)
            Mobile wolf = FindNearbyWolf();
            if (wolf != null)
            {
                FleeFrom(wolf);
                return; // don't steal while fleeing
            }

            // Snoop nearby packs (wider range, flavour + precursor to steal)
            if (Hidden && DateTime.UtcNow >= _nextSnoopTime)
                TrySnoop();

            // Attempt pickpocket if cooldown has passed
            if (Hidden && DateTime.UtcNow >= _nextStealTime)
                TryPickpocket();
        }

        // -- Pickpocket logic ------------------------------------------

        private void TryPickpocket()
        {
            // Find a real player within 2 tiles (pick-pocket range)
            PlayerMobile target = null;
            foreach (Mobile m in GetMobilesInRange(2))
            {
                if (m is PlayerMobile pm
                    && !pm.Deleted && pm.Alive
                    && pm.AccessLevel == AccessLevel.Player
                    && pm.Map == Map)
                {
                    target = pm;
                    break;
                }
            }

            if (target == null)
            {
                _nextStealTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(20, 40));
                return;
            }

            // Only steal gold -- avoids disrupting player inventories
            Container pack = target.Backpack;
            Gold goldItem  = null;

            if (pack != null)
            {
                foreach (Item item in pack.Items)
                {
                    if (item is Gold g && g.Amount >= 10)
                    { goldItem = g; break; }
                }
            }

            if (goldItem == null)
            {
                // Nothing worth taking -- try again soon
                _nextStealTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(2, 4));
                return;
            }

            // Skill check -- steal skill vs target's Detect Hidden
            double stealChance  = Skills[SkillName.Stealing].Value / 100.0 * 0.75; // max 75% at 80 skill
            double detectChance = target.Skills[SkillName.DetectHidden].Value / 100.0 * 0.45;

            bool caught  = Utility.RandomDouble() < detectChance;
            bool success = !caught && Utility.RandomDouble() < stealChance;

            if (success)
            {
                int amount = Math.Min(goldItem.Amount, Utility.RandomMinMax(50, 300));
                goldItem.Amount -= amount;
                if (goldItem.Amount <= 0)
                    goldItem.Delete();

                PackItem(new Gold(amount));
                // Silent success -- player doesn't notice
                _nextStealTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(5, 10));
            }
            else if (caught)
            {
                // Player notices the attempt
                target.SendMessage(0x22, $"You feel {Name} trying to pick your pocket!");
                this.Hidden = false; // revealed!
                FleeFrom(target);
                _nextStealTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(10, 15));
            }
            else
            {
                // Failed silently -- try again later
                _nextStealTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(3, 6));
            }
        }

        // -- Snoop logic -----------------------------------------------

        private void TrySnoop()
        {
            // Slightly wider range than steal — casing the area
            PlayerMobile target = null;
            foreach (Mobile m in GetMobilesInRange(4))
            {
                if (m is PlayerMobile pm
                    && !pm.Deleted && pm.Alive
                    && pm.AccessLevel == AccessLevel.Player
                    && pm.Map == Map)
                {
                    target = pm;
                    break;
                }
            }

            if (target == null)
            {
                _nextSnoopTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 90));
                return;
            }

            // Snooping skill check vs target's Detect Hidden
            double snoopChance  = Skills[SkillName.Snooping].Value / 100.0 * 0.80;
            double detectChance = target.Skills[SkillName.DetectHidden].Value / 100.0 * 0.35;

            bool caught  = Utility.RandomDouble() < detectChance;
            bool success = !caught && Utility.RandomDouble() < snoopChance;

            if (success)
            {
                // Player feels their pack being examined
                target.SendMessage(0x35, $"You feel someone's hands rifling through your belongings!");
                _nextSnoopTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(45, 120));
                // Reset steal cooldown so they may follow up quickly
                if (_nextStealTime > DateTime.UtcNow + TimeSpan.FromSeconds(10))
                    _nextStealTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(5, 10));
            }
            else if (caught)
            {
                target.SendMessage(0x22, $"You catch {Name} peeking into your pack!");
                this.Hidden = false;
                FleeFrom(target);
                _nextSnoopTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(8, 12));
            }
            else
            {
                // Failed silently
                _nextSnoopTime = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));
            }
        }

        // -- Death loot ------------------------------------------------

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);

            // Always: lockpicks, bandages, gold
            c.DropItem(new Lockpick(Utility.RandomMinMax(2, 8)));
            c.DropItem(new Bandage(Utility.RandomMinMax(5, 20)));
            c.DropItem(new Gold(Utility.RandomMinMax(25, 100)));

            // Random reagents — 2 types
            Type[] allRegs = new Type[]
            {
                typeof(Garlic), typeof(Ginseng), typeof(Bloodmoss),
                typeof(Nightshade), typeof(MandrakeRoot), typeof(BlackPearl),
                typeof(SulfurousAsh), typeof(SpidersSilk)
            };
            for (int i = 0; i < 2; i++)
            {
                Type regType = allRegs[Utility.Random(allRegs.Length)];
                Item reg = (Item)Activator.CreateInstance(regType);
                reg.Amount = Utility.RandomMinMax(3, 8);
                c.DropItem(reg);
            }

            // Treasure map: level 1 (35%) or level 2 (15%)
            double mapRoll = Utility.RandomDouble();
            if (mapRoll < 0.15)
                c.DropItem(new TreasureMap(2, Map.Felucca));
            else if (mapRoll < 0.50)
                c.DropItem(new TreasureMap(1, Map.Felucca));

            // Lesser Poison Potion (25%)
            if (Utility.RandomDouble() < 0.25)
                c.DropItem(new LesserPoisonPotion());

            // Stolen jewelry (20%)
            if (Utility.RandomDouble() < 0.20)
            {
                Item jewelry = Utility.RandomBool()
                    ? (Item)new GoldRing()
                    : (Item)new GoldBracelet();
                c.DropItem(jewelry);
            }

            // Tinker's Tools (10%)
            if (Utility.RandomDouble() < 0.10)
                c.DropItem(new TinkersTools());

            // Random orb (1%)
            if (Utility.RandomDouble() < 0.01)
                c.DropItem(RandomOrb());
        }

        private static Item RandomOrb()
        {
            Type[] orbs = new Type[]
            {
                typeof(OrbOfEnhancement), typeof(OrbOfMastery),   typeof(OrbOfExpansion),
                typeof(OrbOfFortitude),   typeof(OrbOfAlacrity),   typeof(OrbOfInsight),
                typeof(OrbOfBalance),     typeof(OrbOfCorruption), typeof(OrbOfResonance),
                typeof(OrbOfCleansing),   typeof(OrbOfTempering),  typeof(OrbOfEnchantment),
                typeof(OrbOfReforging)
            };
            return (Item)Activator.CreateInstance(orbs[Utility.Random(orbs.Length)]);
        }

        // -- Staff hooks -----------------------------------------------

        [CommandProperty(AccessLevel.GameMaster)]
        public string StealInfo => GetStatusDetail();

        public override string GetStatusDetail()
        {
            if (_nextStealTime <= DateTime.UtcNow)
                return $"Steal: ready (hidden={Hidden})";
            TimeSpan remaining = _nextStealTime - DateTime.UtcNow;
            return $"Steal cooldown: {(int)remaining.TotalMinutes}m {remaining.Seconds}s (hidden={Hidden})";
        }

        public override string TriggerNextEvent()
        {
            Hidden         = true;
            _nextStealTime = DateTime.UtcNow;
            return $"{MemberName}: steal cooldown cleared and hidden — will attempt pickpocket on next idle tick near a player.";
        }

        // -- Support methods -------------------------------------------

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
            // Pick a point roughly opposite the threat direction
            int dx = X - threat.X;
            int dy = Y - threat.Y;

            int fx = X + dx + Utility.RandomMinMax(-8, 8);
            int fy = Y + dy + Utility.RandomMinMax(-8, 8);
            int fz = Map.GetAverageZ(fx, fy);

            if (!Map.CanSpawnMobile(fx, fy, fz))
            {
                fx = _homeLocation.X + Utility.RandomMinMax(-15, 15);
                fy = _homeLocation.Y + Utility.RandomMinMax(-15, 15);
                fz = Map.GetAverageZ(fx, fy);
            }

            if (Map.CanSpawnMobile(fx, fy, fz))
            {
                this.Hidden = true;
                StartTravelTo(new Point3D(fx, fy, fz), TimeSpan.FromSeconds(20));
            }
        }

        // -- Serialization ---------------------------------------------
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
            // _nextStealTime is transient -- resets on load, that's fine
        }
    }

    // ============================================================
    // Paladin Order  (Tier 3 — Blue)
    // Heavy plate, chivalry, healing. Blue alignment.
    // StatCap 225, elite warrior.
    // ============================================================
    public class PaladinOrderSimPlayer : SimPlayer
    {
        public PaladinOrderSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.PaladinOrder, memberName, home, zone, schedule) { }

        public PaladinOrderSimPlayer(Serial serial) : base(serial) { }

        // Blue -- override BaseFBCombatNPC defaults
        public override bool AlwaysAttackable => false;
        public override bool AlwaysInnocent   => true;

        protected override void ApplyTemplate()
        {
            SetStr(90, 90);
            SetDex(80, 80);
            SetInt(55, 55);
            SetHits(200, 200);

            SetSkill(SkillName.Swords,      100.0, 100.0);
            SetSkill(SkillName.Tactics,     100.0, 100.0);
            SetSkill(SkillName.Anatomy,     100.0, 100.0);
            SetSkill(SkillName.Healing,     100.0, 100.0);
            SetSkill(SkillName.Parry,       100.0, 100.0);
            SetSkill(SkillName.Chivalry,    100.0, 100.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);

            VirtualArmor = 55;
            Fame  = 10000;
            Karma = 5000;
            Kills = 0;

            // White/silver plate
            var chest = new PlateChest();   chest.Hue = 1153; AddItem(chest);
            var legs  = new PlateLegs();    legs.Hue  = 1153; AddItem(legs);
            var arms  = new PlateArms();    arms.Hue  = 1153; AddItem(arms);
            var glove = new PlateGloves();  glove.Hue = 1153; AddItem(glove);
            var helm  = new PlateHelm();    helm.Hue  = 1153; AddItem(helm);
            var shield = new HeaterShield(); shield.Hue = 1153; AddItem(shield);
            AddItem(new Longsword());
            PackItem(new BookOfChivalry()); // always PackItem
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

    // ============================================================
    // Dead Watchers  (Tier 3 -- Perm Grey)
    // Two sub-types:
    //   Death Knight (default) — dark bone armour, necro warrior, AI_Melee
    //   Wraith Mage (_isWraithMage=true) — robes, dual spellbooks, AI_Mage
    // Both grey (AlwaysAttackable), Karma -100.
    //
    // AI: Dungeon hunters. Travel in groups of 1-4, never the same
    //     dungeon twice in a row. Death Knights melee, Wraith Mages cast.
    //     Hunt 20-30 min, return home, immediately form next group.
    // ============================================================
    public class DeadWatchersSimPlayer : SimPlayer
    {
        private bool     _isWraithMage;
        private bool     _isOnHunt;
        private DateTime _nextHuntTime   = DateTime.MinValue;
        private DateTime _huntReturnTime = DateTime.MinValue;
        private DateTime _nextFlavorTime = DateTime.MinValue;

        // ── Static group coordination ─────────────────────────────
        // One member picks the dungeon and sets group size; others join
        // within the 90-second window. Members beyond the cap wait for
        // the next group.
        private static int      _groupDungeonIdx  = -1;  // active group destination
        private static int      _lastGroupDungeonIdx = -1; // prevent repeats
        private static DateTime _groupWindowEnd   = DateTime.MinValue;
        private static int      _groupSlotsLeft   = 0;

        private static readonly (Point3D Loc, Map Map, string Name)[] _allDungeons =
        {
            // Felucca
            (new Point3D(5188,  638,   0), Map.Felucca,   "Deceit Level 1"),
            (new Point3D(5305,  533,   2), Map.Felucca,   "Deceit Level 2"),
            (new Point3D(5137,  650,   5), Map.Felucca,   "Deceit Level 3"),
            (new Point3D(5306,  652,   2), Map.Felucca,   "Deceit Level 4"),
            (new Point3D(5456, 1863,   0), Map.Felucca,   "Covetous Level 1"),
            (new Point3D(5614, 1997,   0), Map.Felucca,   "Covetous Level 2"),
            (new Point3D(5579, 1924,   0), Map.Felucca,   "Covetous Level 3"),
            (new Point3D(5501,  570,  59), Map.Felucca,   "Despise Level 1"),
            (new Point3D(5519,  673,  20), Map.Felucca,   "Despise Level 2"),
            (new Point3D(5407,  859,  45), Map.Felucca,   "Despise Level 3"),
            (new Point3D(5243, 1006,   0), Map.Felucca,   "Destard Level 1"),
            (new Point3D(5143,  801,   4), Map.Felucca,   "Destard Level 2"),
            (new Point3D(5137,  986,   5), Map.Felucca,   "Destard Level 3"),
            (new Point3D(5905,   20,  46), Map.Felucca,   "Hythloth Level 1"),
            (new Point3D(5976,  169,   0), Map.Felucca,   "Hythloth Level 2"),
            (new Point3D(6083,  145, -20), Map.Felucca,   "Hythloth Level 3"),
            (new Point3D(6059,   89,  24), Map.Felucca,   "Hythloth Level 4"),
            (new Point3D(5395,  126,   0), Map.Felucca,   "Shame Level 1"),
            (new Point3D(5515,   11,   5), Map.Felucca,   "Shame Level 2"),
            (new Point3D(5514,  148,  25), Map.Felucca,   "Shame Level 3"),
            (new Point3D(5875,   20,  -5), Map.Felucca,   "Shame Level 4"),
            (new Point3D(5825,  630,   0), Map.Felucca,   "Wrong Level 1"),
            (new Point3D(5690,  569,  25), Map.Felucca,   "Wrong Level 2"),
            (new Point3D(5703,  639,   0), Map.Felucca,   "Wrong Level 3"),
            (new Point3D(5571, 1302,   0), Map.Felucca,   "Khaldun"),
            (new Point3D(5790, 1416,  40), Map.Felucca,   "Fire Level 1"),
            (new Point3D(5702, 1316,   1), Map.Felucca,   "Fire Level 2"),
            (new Point3D(5875,  150,  15), Map.Felucca,   "Ice Level 1"),
            (new Point3D(5700,  305,   0), Map.Felucca,   "Ice Demon Lair"),
            // Malas
            (new Point3D(2367, 1268, -85), Map.Malas,     "Doom"),
            (new Point3D(2352, 1267,-110), Map.Malas,     "Doom Depths"),
            // Ilshenar
            (new Point3D( 155, 1482, -28), Map.Ilshenar,  "Ankh Dungeon"),
            (new Point3D(2114,  834, -28), Map.Ilshenar,  "Blood Dungeon"),
            (new Point3D(1974,  115, -28), Map.Ilshenar,  "Exodus Dungeon"),
            (new Point3D(2188,  318,  -7), Map.Ilshenar,  "Rock Dungeon"),
            (new Point3D( 428,  109, -28), Map.Ilshenar,  "Sorcerers Dungeon"),
            (new Point3D(1982, 1103, -28), Map.Ilshenar,  "Spectre Dungeon"),
        };

        private static readonly string[] _flavorLines =
        {
            "*surveys the dungeon with hollow eyes*",
            "Death comes for all who dwell here.",
            "*watches a creature fall with quiet satisfaction*",
            "The void is patient.",
            "*marks another soul in the tally*",
            "We see what the living choose to ignore.",
            "*moves silently through the darkness*",
            "Another restless soul joins the count.",
            "Nothing here escapes the eyes of the watchers.",
            "*tilts its head, listening to the silence*",
        };

        // Death Knight constructor
        public DeadWatchersSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.DeadWatchers, memberName, home, zone, schedule,
                   AIType.AI_Melee, FightMode.None)
        {
            _isWraithMage = false;
        }

        // Wraith Mage constructor
        public DeadWatchersSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule,
                                     bool isWraithMage)
            : base(FBGuilds.DeadWatchers, memberName, home, zone, schedule,
                   isWraithMage ? AIType.AI_Mage : AIType.AI_Melee, FightMode.None)
        {
            _isWraithMage = isWraithMage;
        }

        public DeadWatchersSimPlayer(Serial serial) : base(serial) { }

        // Grey -- attackable but not murderer
        public override bool AlwaysAttackable => true;

        protected override void ApplyTemplate()
        {
            Fame  = 8000;
            Karma = -100; // perm grey
            Kills = 0;

            if (_isWraithMage)
                ApplyWraithMage();
            else
                ApplyDeathKnight();
        }

        // ── Death Knight ─────────────────────────────────────────
        // STR 90 / DEX 80 / INT 55, Hits 200
        // Dark bone armour, enchanted longsword, necro spellbook
        private void ApplyDeathKnight()
        {
            SetStr(90, 90);
            SetDex(80, 80);
            SetInt(55, 55);
            SetHits(200, 200);

            SetSkill(SkillName.Swords,      100.0, 100.0);
            SetSkill(SkillName.Tactics,     100.0, 100.0);
            SetSkill(SkillName.Anatomy,     100.0, 100.0);
            SetSkill(SkillName.Healing,     100.0, 100.0);
            SetSkill(SkillName.Necromancy,  100.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 100.0, 100.0);
            SetSkill(SkillName.Parry,       100.0, 100.0);

            VirtualArmor = 45;

            // Dark bone armour — enchanted
            var chest = new BoneChest();
            chest.Hue = 0x455;
            chest.Attributes.DefendChance       = 8;
            chest.Attributes.RegenHits           = 2;
            chest.HitPoints = chest.MaxHitPoints = 200;
            AddItem(chest);

            var legs = new BoneLegs();
            legs.Hue = 0x455;
            legs.Attributes.DefendChance        = 6;
            legs.HitPoints = legs.MaxHitPoints = 200;
            AddItem(legs);

            var arms = new BoneArms();
            arms.Hue = 0x455;
            arms.Attributes.DefendChance        = 5;
            arms.HitPoints = arms.MaxHitPoints = 200;
            AddItem(arms);

            var glove = new BoneGloves();
            glove.Hue = 0x455;
            glove.Attributes.AttackChance       = 7;
            glove.HitPoints = glove.MaxHitPoints = 200;
            AddItem(glove);

            var helm = new BoneHelm();
            helm.Hue = 0x455;
            helm.Attributes.DefendChance        = 5;
            helm.HitPoints = helm.MaxHitPoints = 200;
            AddItem(helm);

            // Enchanted longsword
            var sword = new Longsword();
            sword.Hue                           = 0x455;
            sword.Attributes.AttackChance       = 10;
            sword.Attributes.WeaponDamage       = 15;
            sword.WeaponAttributes.HitHarm      = 30; // hits for extra cold damage
            sword.HitPoints = sword.MaxHitPoints = 200;
            AddItem(sword);

            // Jewellery
            var ring = new GoldRing();
            ring.Attributes.BonusHits           = 10;
            ring.Attributes.RegenHits            = 1;
            AddItem(ring);

            var bracelet = new GoldBracelet();
            bracelet.Attributes.BonusStam        = 5;
            bracelet.Attributes.DefendChance      = 5;
            AddItem(bracelet);

            // Spellbook (necro only — no regular spells)
            PackItem(new NecromancerSpellbook());
        }

        // ── Wraith Mage ──────────────────────────────────────────
        // STR 40 / DEX 55 / INT 130, Mana 300, VA 15
        // Black robes, enchanted staff, dual spellbooks, mana jewellery
        private void ApplyWraithMage()
        {
            SetStr(40, 40);
            SetDex(55, 55);
            SetInt(130, 130);
            SetHits(120, 120);
            SetMana(300);

            SetSkill(SkillName.Magery,      100.0, 100.0);
            SetSkill(SkillName.EvalInt,     100.0, 100.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);
            SetSkill(SkillName.Necromancy,  100.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 100.0, 100.0);
            SetSkill(SkillName.Meditation,  100.0, 100.0);
            SetSkill(SkillName.Wrestling,   100.0, 100.0);

            VirtualArmor = 15;

            // Spectral robes
            var robe = new Robe();
            robe.Hue = 0x455; // near-black
            AddItem(robe);

            var cloak = new Cloak();
            cloak.Hue = 0x455;
            AddItem(cloak);

            var sandals = new Sandals();
            sandals.Hue = 0x455;
            AddItem(sandals);

            // Enchanted BlackStaff — spell channeling so they can cast while holding it
            var staff = new BlackStaff();
            staff.Hue = 0x455;
            staff.Attributes.SpellChanneling    = 1;
            staff.Attributes.LowerManaCost      = 8;
            staff.WeaponAttributes.MageWeapon   = 30; // -30 weapon skill, use Magery instead
            staff.HitPoints = staff.MaxHitPoints = 200;
            AddItem(staff);

            // Jewellery — mana and cast bonuses
            var ring = new GoldRing();
            ring.Attributes.BonusMana           = 15;
            ring.Attributes.RegenMana            = 2;
            AddItem(ring);

            var bracelet = new GoldBracelet();
            bracelet.Attributes.BonusInt        = 10;
            bracelet.Attributes.SpellDamage     = 10;
            AddItem(bracelet);

            // Both spellbooks
            PackItem(new Spellbook());
            PackItem(new NecromancerSpellbook());
        }

        // ── AI: Dungeon Hunter ───────────────────────────────────────

        protected override void OnTickIdle()
        {
            if (_isOnHunt)
            {
                // Return home once time is up and we're not mid-fight
                if (DateTime.UtcNow >= _huntReturnTime && (Combatant == null || Combatant.Deleted || !Combatant.Alive))
                {
                    ReturnHome();
                    return;
                }

                TryHuntMonster();

                if (DateTime.UtcNow >= _nextFlavorTime)
                    TryFlavorSpeech();
            }
            else
            {
                if (DateTime.UtcNow >= _nextHuntTime)
                    TravelToDungeon();
            }
        }

        private void TravelToDungeon()
        {
            if (Deleted || !Alive) return;

            int idx;

            if (DateTime.UtcNow < _groupWindowEnd && _groupSlotsLeft > 0)
            {
                // Join the existing group
                _groupSlotsLeft--;
                idx = _groupDungeonIdx;
            }
            else if (DateTime.UtcNow >= _groupWindowEnd)
            {
                // Start a new group — pick dungeon different from last group
                do { idx = Utility.Random(_allDungeons.Length); }
                while (idx == _lastGroupDungeonIdx && _allDungeons.Length > 1);

                _groupDungeonIdx     = idx;
                _lastGroupDungeonIdx = idx;
                _groupWindowEnd      = DateTime.UtcNow + TimeSpan.FromSeconds(90);
                _groupSlotsLeft      = Utility.RandomMinMax(0, 3); // leader + 0-3 followers = 1-4 total
            }
            else
            {
                // Group full — wait for next cycle
                _nextHuntTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(2, 5));
                return;
            }

            var pick  = _allDungeons[idx];

            // Stagger departures within a group so they don't all gate at once
            int travelDelay = _groupSlotsLeft == 0
                ? 0
                : Utility.RandomMinMax(5, 25); // followers offset by 5-25s

            Animate(203, 7, 1, true, false, 0);
            Say(_isWraithMage
                ? $"*drifts toward {pick.Name} like smoke on still water*"
                : $"*steps through shadow toward {pick.Name}*");

            Point3D dest    = pick.Loc;
            Map     destMap = pick.Map;

            Timer.DelayCall(TimeSpan.FromSeconds(1.5 + travelDelay), () =>
            {
                if (Deleted || !Alive) return;

                Effects.SendLocationParticles(
                    EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                    0x376A, 9, 32, 5023);
                Effects.PlaySound(Location, Map, 0x20E);

                MoveToWorld(dest, destMap);

                Effects.SendLocationParticles(
                    EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                    0x376A, 9, 32, 5023);
                Effects.PlaySound(Location, Map, 0x20E);

                _isOnHunt       = true;
                _huntReturnTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(20, 30));
                _nextFlavorTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(1, 3));
            });
        }

        private void TryHuntMonster()
        {
            // Already in active combat — AI handles the rest
            if (Combatant != null && !Combatant.Deleted && Combatant.Alive) return;

            Combatant = null;

            Mobile best        = null;
            int    bestPriority = int.MaxValue;

            foreach (Mobile m in GetMobilesInRange(15))
            {
                if (m == this || m.Deleted || !m.Alive) continue;
                if (m.Map != Map) continue;

                int priority;

                if (m is BloodPactSimPlayer)
                    priority = 0; // highest — lore enemy
                else if (m is PlayerMobile pm && (pm.Murderer || pm.Criminal))
                    priority = 1; // reds / greys
                else if (m is BaseCreature bc
                         && bc.ControlMaster == null
                         && !bc.IsDeadBondedPet
                         && !(bc is SimPlayer))
                    priority = 2; // wild dungeon monsters
                else
                    continue;

                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    best         = m;
                }
            }

            if (best != null)
                Combatant = best;
        }

        private void TryFlavorSpeech()
        {
            Say(_flavorLines[Utility.Random(_flavorLines.Length)]);
            _nextFlavorTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(3, 7));
        }

        private void ReturnHome()
        {
            _isOnHunt = false;
            Combatant = null;

            if (Deleted || !Alive) return;

            Animate(203, 7, 1, true, false, 0);
            Say("*fades back into shadow*");

            Point3D home = _homeLocation;

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (Deleted || !Alive) return;

                Effects.SendLocationParticles(
                    EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                    0x376A, 9, 32, 5023);
                Effects.PlaySound(Location, Map, 0x20E);

                MoveToWorld(home, Map.Felucca);

                Effects.SendLocationParticles(
                    EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                    0x376A, 9, 32, 5023);
                Effects.PlaySound(Location, Map, 0x20E);

                // Brief pause for gate animation, then head straight to next dungeon
                _nextHuntTime = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            });
        }

        // ── Serialization ────────────────────────────────────────

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(2); // version

            // v1
            writer.Write(_isWraithMage);
            // v2: hunt state is transient — not saved
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            if (version >= 1)
                _isWraithMage = reader.ReadBool();

            // Hunt state resets on load — start fresh with a short delay
            _isOnHunt     = false;
            _nextHuntTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(2, 8));
        }
    }

    // ============================================================
    // Dread Hunters  (Tier 3 -- Blue/Grey)
    // Elite plate + chivalry, GM120 skills. Blue alignment.
    // StatCap 300, skill cap 880.
    // ============================================================
    public class DreadHuntersSimPlayer : SimPlayer
    {
        public DreadHuntersSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.DreadHunters, memberName, home, zone, schedule) { }

        public DreadHuntersSimPlayer(Serial serial) : base(serial) { }

        // Blue -- override BaseFBCombatNPC defaults
        public override bool AlwaysAttackable => false;
        public override bool AlwaysInnocent   => true;

        protected override void ApplyTemplate()
        {
            SetStr(125, 125);
            SetDex(125, 125);
            SetInt(50, 50);
            SetHits(300, 300);

            SetSkill(SkillName.Swords,      120.0, 120.0);
            SetSkill(SkillName.Tactics,     120.0, 120.0);
            SetSkill(SkillName.Anatomy,     120.0, 120.0);
            SetSkill(SkillName.Healing,     120.0, 120.0);
            SetSkill(SkillName.Parry,       120.0, 120.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);
            SetSkill(SkillName.Chivalry,    100.0, 100.0);
            SetSkill(SkillName.Focus,        80.0,  80.0);

            VirtualArmor = 60;
            Fame  = 15000;
            Karma = 1000;
            Kills = 0;

            // Dark grey plate
            var chest  = new PlateChest();   chest.Hue  = 0x497; AddItem(chest);
            var legs   = new PlateLegs();    legs.Hue   = 0x497; AddItem(legs);
            var arms   = new PlateArms();    arms.Hue   = 0x497; AddItem(arms);
            var glove  = new PlateGloves();  glove.Hue  = 0x497; AddItem(glove);
            var helm   = new PlateHelm();    helm.Hue   = 0x497; AddItem(helm);
            var shield = new HeaterShield(); shield.Hue = 0x497; AddItem(shield);
            AddItem(new Longsword());
            PackItem(new BookOfChivalry()); // always PackItem
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

    // ============================================================
    // Blood Pact  (Tier 3 -- Red/Murderer)
    // NecroMage template. Red alignment -- AlwaysMurderer.
    // StatCap 250, skill cap 800. Home: Destard outskirts.
    // ============================================================
    public class BloodPactSimPlayer : SimPlayer
    {
        // AI_Mage so they cast spells; FightMode.Closest so they aggro
        // the nearest mobile (players and monsters near Destard outskirts).
        public BloodPactSimPlayer(string memberName, Point3D home,
                                  SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.BloodPact, memberName, home, zone, schedule,
                   AIType.AI_Mage, FightMode.Closest) { }

        public BloodPactSimPlayer(Serial serial) : base(serial) { }

        // Red -- murderer flag
        public override bool AlwaysAttackable => true;
        public override bool AlwaysMurderer   => true;

        // Home is near Destard -- too far from Britain bank to make trips
        protected override bool CanBank => false;

        // Pause the SimState machine while actively fighting so combat
        // movement and state-machine movement don't fight each other.
        protected override bool SkipStateTick => Combatant != null;

        protected override void ApplyTemplate()
        {
            SetStr(50, 50);
            SetDex(75, 75);
            SetInt(125, 125);
            SetHits(150, 150);

            SetSkill(SkillName.Magery,      100.0, 100.0);
            SetSkill(SkillName.EvalInt,     100.0, 100.0);
            SetSkill(SkillName.MagicResist, 100.0, 100.0);
            SetSkill(SkillName.Necromancy,  100.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 100.0, 100.0);
            SetSkill(SkillName.Meditation,  100.0, 100.0);
            SetSkill(SkillName.Wrestling,   100.0, 100.0);
            SetSkill(SkillName.Hiding,      100.0, 100.0);

            VirtualArmor = 15;
            Fame  = 10000;
            Karma = -5000; // red
            Kills = 10;

            // Black robes
            var robe = new Robe(); robe.Hue = 0x455; AddItem(robe);
            AddItem(new Sandals());
            PackItem(new Spellbook());            // always PackItem
            PackItem(new NecromancerSpellbook()); // always PackItem
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

    // ============================================================
    // The Void  (Tier 3 -- Red/Murderer)
    // GM120 NecroMage. Red alignment -- AlwaysMurderer.
    // StatCap 270, skill cap 850. Home: Deceit outskirts.
    // ============================================================
    public class TheVoidSimPlayer : SimPlayer
    {
        // AI_Mage so they cast spells; FightMode.Closest so they aggro
        // the nearest mobile near Deceit outskirts.
        public TheVoidSimPlayer(string memberName, Point3D home,
                                SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.TheVoid, memberName, home, zone, schedule,
                   AIType.AI_Mage, FightMode.Closest) { }

        public TheVoidSimPlayer(Serial serial) : base(serial) { }

        // Red -- murderer flag
        public override bool AlwaysAttackable => true;
        public override bool AlwaysMurderer   => true;

        // Home is near Deceit -- too far from Britain bank to make trips
        protected override bool CanBank => false;

        // Pause state machine while actively fighting
        protected override bool SkipStateTick => Combatant != null;

        protected override void ApplyTemplate()
        {
            SetStr(60, 60);
            SetDex(85, 85);
            SetInt(125, 125);
            SetHits(170, 170);

            SetSkill(SkillName.Magery,      120.0, 120.0);
            SetSkill(SkillName.EvalInt,     120.0, 120.0);
            SetSkill(SkillName.MagicResist, 120.0, 120.0);
            SetSkill(SkillName.Necromancy,  120.0, 120.0);
            SetSkill(SkillName.SpiritSpeak, 120.0, 120.0);
            SetSkill(SkillName.Meditation,  100.0, 100.0);
            SetSkill(SkillName.Wrestling,   100.0, 100.0);
            SetSkill(SkillName.Hiding,       50.0,  50.0);

            VirtualArmor = 15;
            Fame  = 15000;
            Karma = -8000; // red
            Kills = 15;

            // Black robes
            var robe = new Robe(); robe.Hue = 0x455; AddItem(robe);
            AddItem(new Sandals());
            PackItem(new Spellbook());            // always PackItem
            PackItem(new NecromancerSpellbook()); // always PackItem
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

    // ============================================================
    // Shadowblade  (Tier 3 -- Grey)
    // Shadow Assassin template. Grey alignment -- attackable.
    // StatCap 250, skill cap 800. Home: Wrong outskirts.
    // ============================================================
    public class ShadowbladeSimPlayer : SimPlayer
    {
        // AI_Melee — high-Dex fencer; FightMode.Closest — ambush anything near Wrong.
        public ShadowbladeSimPlayer(string memberName, Point3D home,
                                    SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.Shadowblade, memberName, home, zone, schedule,
                   AIType.AI_Melee, FightMode.Closest) { }

        public ShadowbladeSimPlayer(Serial serial) : base(serial) { }

        // Grey -- attackable but not murderer
        public override bool AlwaysAttackable => true;

        // Home is near Wrong -- too far from Britain bank to make trips
        protected override bool CanBank => false;

        // Pause state machine while actively fighting
        protected override bool SkipStateTick => Combatant != null;

        // -- Hide / ambush ---------------------------------------------
        // Shadowblade hides near the dungeon entrance between fights.
        // FightMode.Closest auto-acquires a target; the act of attacking
        // reveals them naturally (standard UO mechanic).
        private DateTime _nextHideTime = DateTime.MinValue;

        protected override void OnTickIdle()
        {
            if (DateTime.UtcNow >= _nextHideTime)
            {
                this.Hidden   = true;
                _nextHideTime = DateTime.UtcNow
                    + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 90));
            }
        }

        protected override void ApplyTemplate()
        {
            SetStr(75, 75);
            SetDex(125, 125);
            SetInt(50, 50);
            SetHits(160, 160);

            SetSkill(SkillName.Ninjitsu,      100.0, 100.0);
            SetSkill(SkillName.Hiding,        100.0, 100.0);
            SetSkill(SkillName.Stealth,       100.0, 100.0);
            SetSkill(SkillName.Fencing,       100.0, 100.0);
            SetSkill(SkillName.Tactics,       100.0, 100.0);
            SetSkill(SkillName.Anatomy,       100.0, 100.0);
            SetSkill(SkillName.Healing,       100.0, 100.0);
            SetSkill(SkillName.DetectHidden,  100.0, 100.0);

            VirtualArmor = 20;
            Fame  = 5000;
            Karma = -500; // grey
            Kills = 0;

            // Black ninja gear
            var jacket = new LeatherNinjaJacket(); jacket.Hue = 0x455; AddItem(jacket);
            var hood   = new LeatherNinjaHood();   hood.Hue   = 0x455; AddItem(hood);
            var tabi   = new NinjaTabi();           tabi.Hue   = 0x455; AddItem(tabi);

            // Alternate between Kryss and AssassinSpike for variety
            if (Utility.RandomBool())
                AddItem(new Kryss());
            else
                AddItem(new AssassinSpike());
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

    // ============================================================
    // WandererSimPlayer
    // Guild: Wanderers of Britannia
    //
    // Loop:
    //   1. Britain Bank  — 10 min hang-out / banking
    //   2. Restock Area  — 10 min (provisioner near Britain bank)
    //   3. Hunt Destination — 40 min (random from 12 outdoor/dungeon L1 spots)
    //   4. Recall back to Britain bank — repeat
    //
    // Travel: Magery Recall spell visual + sound, 1.5 s cast delay.
    // Death:  Standard corpse left with equipped items (no OnBeforeDeath override).
    // ============================================================
    public class WandererSimPlayer : SimPlayer
    {
        // ── Hunt destinations ──────────────────────────────────
        private struct HuntSpot { public Point3D Loc; public Map HMap; public string Name; }

        private static readonly HuntSpot[] HuntSpots =
        {
            new HuntSpot { Loc = new Point3D(1382, 1483,  0), HMap = Map.Felucca, Name = "the Britain Graveyard"       },
            new HuntSpot { Loc = new Point3D(1533, 1412,  0), HMap = Map.Felucca, Name = "the Britain Farms"           },
            new HuntSpot { Loc = new Point3D(2230, 1148,  0), HMap = Map.Felucca, Name = "Cove outskirts"              },
            new HuntSpot { Loc = new Point3D( 596, 1132,  0), HMap = Map.Felucca, Name = "the Yew Cemetery"            },
            new HuntSpot { Loc = new Point3D(2728,  776,  0), HMap = Map.Felucca, Name = "the Vesper Cemetery"         },
            new HuntSpot { Loc = new Point3D( 596, 2161,  0), HMap = Map.Felucca, Name = "the Skara Brae Cemetery"     },
            new HuntSpot { Loc = new Point3D(2530,  530,  0), HMap = Map.Felucca, Name = "the Minoc outskirts"         },
            new HuntSpot { Loc = new Point3D(1828, 2820,  0), HMap = Map.Felucca, Name = "the Trinsic south gate"      },
            new HuntSpot { Loc = new Point3D(1315, 1280,  0), HMap = Map.Felucca, Name = "Despise (first level)"       },
            new HuntSpot { Loc = new Point3D(4111,  434,  5), HMap = Map.Felucca, Name = "Deceit (first level)"        },
            new HuntSpot { Loc = new Point3D(2523, 1001,  0), HMap = Map.Felucca, Name = "Covetous (first level)"      },
            new HuntSpot { Loc = new Point3D(1172, 1665,  0), HMap = Map.Felucca, Name = "the Orc Cave entrance"       },
        };

        // Britain bank anchor + restock spot (provisioner row near west bank)
        private static readonly Point3D BritBankLoc   = new Point3D(1430, 1695, 0);
        private static readonly Point3D BritRestockLoc = new Point3D(1457, 1706, 0);
        private static readonly Map     BritMap        = Map.Felucca;

        // ── Phase enum ─────────────────────────────────────────
        private enum WanderPhase { None, Banking, Restocking, Hunting, Recalling }

        // ── State fields ───────────────────────────────────────
        private WanderPhase _wanderPhase    = WanderPhase.None;
        private DateTime    _phaseEndsAt    = DateTime.MinValue;
        private DateTime    _nextSpeechAt   = DateTime.MinValue;
        private int         _currentHuntIdx = -1;
        private bool        _recalling      = false;

        // ── Ambient speech ─────────────────────────────────────
        private static readonly string[] BankLines =
        {
            "Anyone seen any good loot lately?",
            "Just got back from a long road. My feet are killing me.",
            "The roads are dangerous these days. Watch yourselves.",
            "I need to find work. Coin doesn't grow on trees.",
            "Heard there's trouble near Despise again.",
            "A traveller's life isn't glamorous, but it's honest.",
            "Have you got any spare regs? I'm running low.",
        };

        private static readonly string[] HuntLines =
        {
            "Stay alert — this place is no joke.",
            "I've seen worse... I think.",
            "Keep moving. Standing still gets you killed.",
            "Watch my back and I'll watch yours.",
            "*scans the area carefully*",
            "Good hunting grounds if you know where to look.",
        };

        // ── Constructor ────────────────────────────────────────
        public WandererSimPlayer(string memberName, Point3D home, SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.Wanderers, memberName, home, zone, schedule)
        {
        }

        public WandererSimPlayer(Serial serial) : base(serial) { }

        // ── Overrides ──────────────────────────────────────────
        // Suppress the base state machine (Idle/Travelling) whenever the wander
        // loop is running. WanderPhase.None is only a transient state that
        // immediately transitions to Hunting, so this covers all active phases.
        protected override bool SkipStateTick =>
            base.SkipStateTick || _wanderPhase != WanderPhase.None;

        // Wanderers manage their own banking inside the wander loop — never
        // let the base SimPlayer banking logic fire on top of it.
        protected override bool CanBank => false;

        // ── OnThink ────────────────────────────────────────────
        public override void OnThink()
        {
            base.OnThink();
            if (Deleted || Map == Map.Internal) return;

            // Snap back to hunt anchor if combat pushed us away
            if (_wanderPhase == WanderPhase.Hunting && _currentHuntIdx >= 0)
            {
                HuntSpot spot = HuntSpots[_currentHuntIdx];
                if (Map != spot.HMap || !InRange(spot.Loc, 20))
                    MoveToWorld(spot.Loc, spot.HMap);
            }

            ManageWanderLoop();
        }

        // ── Wander loop logic ──────────────────────────────────
        private void ManageWanderLoop()
        {
            switch (_wanderPhase)
            {
                case WanderPhase.None:
                    StartHuntPhase();
                    break;

                case WanderPhase.Hunting:
                    if (DateTime.UtcNow >= _phaseEndsAt)
                        RecallToBritain();
                    else
                        TrySpeakAmbient(HuntLines);
                    break;

                case WanderPhase.Banking:
                    if (DateTime.UtcNow >= _phaseEndsAt)
                        StartRestockPhase();
                    else
                        TrySpeakAmbient(BankLines);
                    break;

                case WanderPhase.Restocking:
                    if (DateTime.UtcNow >= _phaseEndsAt)
                        StartHuntPhase();
                    break;

                case WanderPhase.Recalling:
                    // DoRecall timer is running; nothing to do until callback fires
                    break;
            }
        }

        private void StartRestockPhase()
        {
            _wanderPhase = WanderPhase.Restocking;
            _phaseEndsAt = DateTime.UtcNow + TimeSpan.FromMinutes(10.0);
            MoveToWorld(BritRestockLoc, BritMap);
        }

        private void StartHuntPhase()
        {
            _currentHuntIdx = Utility.Random(HuntSpots.Length);
            HuntSpot spot   = HuntSpots[_currentHuntIdx];

            _phaseEndsAt = DateTime.UtcNow + TimeSpan.FromMinutes(40.0);

            if (Map == Map.Internal)
            {
                // First activation — place directly into the hunt spot
                MoveToWorld(spot.Loc, spot.HMap);
                _wanderPhase = WanderPhase.Hunting;
                SayIfVisible(string.Format("Heading out to {0}.", spot.Name));
            }
            else
            {
                // Subsequent loops — Recall from Britain to hunt spot
                _wanderPhase = WanderPhase.Recalling;  // guard during cast delay
                SayIfVisible(string.Format("*prepares a Recall scroll for {0}*", spot.Name));
                DoRecall(spot.Loc, spot.HMap, () =>
                {
                    _wanderPhase = WanderPhase.Hunting; // 40-min clock starts on arrival
                    SayIfVisible(string.Format("Made it to {0}.", spot.Name));
                });
            }
        }

        private void RecallToBritain()
        {
            // Recalling phase blocks ManageWanderLoop from re-entering until arrival.
            _wanderPhase = WanderPhase.Recalling;

            Say("That's enough hunting for now.");
            DoRecall(BritBankLoc, BritMap, () =>
            {
                // Arrive at bank — start the 10 min bank hang-out
                _wanderPhase = WanderPhase.Banking;
                _phaseEndsAt = DateTime.UtcNow + TimeSpan.FromMinutes(10.0);
                Say("Back to Britain.");
            });
        }

        // ── Recall helper — visual + sound + 1.5 s delay ──────
        private void DoRecall(Point3D dest, Map destMap, System.Action onArrival)
        {
            if (_recalling) return;
            _recalling = true;

            // Cast animation (spell cast effect)
            PlaySound(0x1FC);
            FixedParticles(0x3728, 8, 20, 5042, EffectLayer.Head);

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (Deleted) { _recalling = false; return; }

                // Arrival flash at source
                Effects.SendLocationEffect(Location, Map, 0x3728, 13, 1, 0, 0);

                MoveToWorld(dest, destMap);

                // Arrival flash at destination
                Effects.SendLocationEffect(Location, Map, 0x3728, 13, 1, 0, 0);
                PlaySound(0x1FC);

                _recalling = false;
                if (onArrival != null) onArrival();
            });
        }

        // ── Small ambient speech helper ────────────────────────
        private void TrySpeakAmbient(string[] lines)
        {
            if (DateTime.UtcNow < _nextSpeechAt) return;
            if (!Utility.RandomBool()) return;  // 50% chance each tick

            _nextSpeechAt = DateTime.UtcNow
                + TimeSpan.FromSeconds(Utility.RandomMinMax(60, 180));

            string line = lines[Utility.Random(lines.Length)];
            Say(line);
        }

        private void SayIfVisible(string text)
        {
            if (Map != null && Map != Map.Internal)
                Say(text);
        }

        // ── Status / health ────────────────────────────────────
        public override string GetStatusDetail()
        {
            switch (_wanderPhase)
            {
                case WanderPhase.Banking:
                {
                    int mins = (int)(_phaseEndsAt - DateTime.UtcNow).TotalMinutes;
                    return string.Format("Banking ({0}m left)", Math.Max(0, mins));
                }
                case WanderPhase.Restocking:
                {
                    int mins = (int)(_phaseEndsAt - DateTime.UtcNow).TotalMinutes;
                    return string.Format("Restocking ({0}m left)", Math.Max(0, mins));
                }
                case WanderPhase.Hunting:
                {
                    string spot = _currentHuntIdx >= 0 && _currentHuntIdx < HuntSpots.Length
                        ? HuntSpots[_currentHuntIdx].Name : "unknown";
                    int mins = (int)(_phaseEndsAt - DateTime.UtcNow).TotalMinutes;
                    return string.Format("Hunting: {0} ({1}m left)", spot, Math.Max(0, mins));
                }
                case WanderPhase.Recalling:
                    return "Recalling...";
                default:
                    return "Idle (wander)";
            }
        }

        public override SimHealthStatus GetHealth()
        {
            if (Deleted)                        return SimHealthStatus.Stuck;
            if (Map == Map.Internal)            return SimHealthStatus.Stuck;
            if (!Alive)                         return SimHealthStatus.Warning;
            if (Hits < HitsMax / 4 && Alive)   return SimHealthStatus.Warning;

            // Stuck hunting for more than 50 min (phase should last 40 max)
            if (_wanderPhase == WanderPhase.Hunting &&
                DateTime.UtcNow > _phaseEndsAt + TimeSpan.FromMinutes(10.0))
                return SimHealthStatus.Stuck;

            // Do NOT call base.GetHealth() — the base checks SimState.Travelling
            // which stays set permanently because SkipStateTick suppresses the
            // state machine. That would cause the watchdog to AutoFix every 15 min.
            return SimHealthStatus.Healthy;
        }

        public override void AutoFix()
        {
            _wanderPhase    = WanderPhase.None;
            _recalling      = false;
            _phaseEndsAt    = DateTime.MinValue;
            _currentHuntIdx = -1;
            base.AutoFix();
        }

        // ── Template ───────────────────────────────────────────
        protected override void ApplyTemplate()
        {
            SetStr(85,  95);
            SetDex(80,  90);
            SetInt(60,  70);
            SetHits(110, 130);

            SetSkill(SkillName.Swords,      65.0, 80.0);
            SetSkill(SkillName.Tactics,     65.0, 80.0);
            SetSkill(SkillName.Healing,     60.0, 75.0);
            SetSkill(SkillName.Anatomy,     55.0, 70.0);
            SetSkill(SkillName.Magery,      70.0, 85.0);  // needed for Recall
            SetSkill(SkillName.EvalInt,     50.0, 65.0);
            SetSkill(SkillName.MagicResist, 55.0, 70.0);

            VirtualArmor = 18;
            Fame  = 1500;
            Karma = 1500;
            Kills = 0;

            // Travelling gear — lootable on death
            var chest  = new StuddedChest();   chest.Hue  = 0x96D; AddItem(chest);
            var arms   = new StuddedArms();    arms.Hue   = 0x96D; AddItem(arms);
            var legs   = new StuddedLegs();    legs.Hue   = 0x96D; AddItem(legs);
            var gloves = new StuddedGloves();  gloves.Hue = 0x96D; AddItem(gloves);
            var gorget = new StuddedGorget();  gorget.Hue = 0x96D; AddItem(gorget);
            var cloak  = new Cloak();          cloak.Hue  = 0x55;  AddItem(cloak);

            AddItem(new Longsword());

            // Gold so the corpse is worth looting
            PackItem(new Gold(Utility.RandomMinMax(50, 150)));

            // Spellbook for Recall (rules: spellbooks always PackItem)
            var book = new Spellbook();
            book.Content = 0xFFFFFFFFFFFFFFFF; // all spells
            PackItem(book);
        }

        // ── Serialize / Deserialize ────────────────────────────
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1); // version

            writer.Write((int)_wanderPhase);
            writer.Write(_phaseEndsAt);
            writer.Write(_currentHuntIdx);
            writer.Write(_nextSpeechAt);  // v1
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            if (version >= 0)
            {
                _wanderPhase    = (WanderPhase)reader.ReadInt();
                _phaseEndsAt    = reader.ReadDateTime();
                _currentHuntIdx = reader.ReadInt();
            }
            if (version >= 1)
            {
                _nextSpeechAt = reader.ReadDateTime();
            }
        }
    }
}
