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
using Server;
using Server.Custom;
using Server.Engines.CannedEvil;
using Server.Items;
using Server.Mobiles;

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

        // -- Combat healing + mobility (transient) ------------------------
        private DateTime _nextHealAt           = DateTime.MinValue;
        private Point3D  _spawnEntryPoint      = Point3D.Zero;   // where they landed at the spawn
        private Serial   _lastCombatantSerial  = Serial.Zero;    // for stuck detection
        private DateTime _combatantSince       = DateTime.MinValue; // when current combatant was acquired
        private DateTime _nextTeleportAt       = DateTime.MinValue; // teleport cooldown
        private DateTime _nextReturnAt         = DateTime.MinValue; // return-to-entry cooldown

        // Used when no Felucca ChampionSpawn is found in the world at all
        private static readonly Point3D[] FallbackDestinations =
        {
            new Point3D(1296, 1263, 0),   // Despise entrance (Felucca)
            new Point3D(533,  1566, 0),   // Shame entrance (Felucca)
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
        public IronCompanySimPlayer(string memberName, Point3D home,
                                    SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.IronCompany, memberName, home, zone, schedule) { }

        public IronCompanySimPlayer(Serial serial) : base(serial) { }

        // -- Overrides -------------------------------------------------

        /// <summary>Pause SimState movement ticks while fighting, rallying at bank, or fighting at spawn.</summary>
        protected override bool SkipStateTick => Combatant != null
            || _champPhase == ChampPhase.WaitingAtBank
            || _champPhase == ChampPhase.AtSpawn;

        /// <summary>No banking while on a champ run.</summary>
        protected override bool CanBank => _champPhase == ChampPhase.None;

        public override void OnThink()
        {
            // Champ state machine always runs — even during combat / SkipStateTick
            ManageChampPhase();

            // Self-heal when fighting at the spawn
            if (_champPhase == ChampPhase.AtSpawn && Alive)
                TrySelfHeal();

            base.OnThink();
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

        private void StartChampRun()
        {
            _targetSpawn = FindBestFeluccaSpawn();

            if (_targetSpawn == null)
                return; // no surface Felucca spawn found — skip this run cycle

            _champPhase = ChampPhase.GatherAtBank;
            FightMode   = FightMode.None;

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
            _champPhase     = ChampPhase.AtSpawn;
            FightMode       = FightMode.Closest;
            _leaveSpawnAt   = DateTime.UtcNow + TimeSpan.FromHours(2); // hard cap
            _champAnnounced = false;

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

            // Announce each tier advance
            if (level > _lastKnownLevel && level >= 1)
            {
                int idx = Math.Min(level - 1, TierSpeech.Length - 1);
                Say(TierSpeech[idx]);
                _lastKnownLevel = level;
            }

            // Detect and directly engage champion when it spawns
            if (!_champAnnounced
                && _targetSpawn.Champion != null
                && !_targetSpawn.Champion.Deleted)
            {
                _champAnnounced = true;
                Combatant = _targetSpawn.Champion;
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

            // Track how long we've had this specific combatant (for stuck detection)
            if (Combatant == null)
            {
                _lastCombatantSerial = Serial.Zero;
                _combatantSince      = DateTime.MinValue;
            }
            else if (Combatant.Serial != _lastCombatantSerial)
            {
                _lastCombatantSerial = Combatant.Serial;
                _combatantSince      = DateTime.UtcNow;
            }

            // Actively acquire a spawn target if not currently fighting
            if (Combatant == null || Combatant.Deleted || !Combatant.Alive)
            {
                AcquireSpawnTarget();

                // Return to entry point if we drifted far away and have no target
                if (Combatant == null && _spawnEntryPoint != Point3D.Zero
                    && GetDistanceToSqrt(_spawnEntryPoint) > 8
                    && DateTime.UtcNow >= _nextReturnAt)
                {
                    TeleportToEntryPoint();
                }
            }
            else
            {
                // Too far from spawn — drop the chase and return to fight inside the spawn area
                double distFromSpawn = _spawnEntryPoint != Point3D.Zero
                    ? GetDistanceToSqrt(_spawnEntryPoint)
                    : (_targetSpawn != null ? GetDistanceToSqrt(_targetSpawn.Location) : 0);

                if (distFromSpawn > 20 && DateTime.UtcNow >= _nextReturnAt)
                {
                    Combatant = null;
                    TeleportToEntryPoint();
                }
                // Stuck check: combatant alive but out of melee range for too long → teleport to them
                else if (GetDistanceToSqrt(Combatant) > 6
                    && _combatantSince != DateTime.MinValue
                    && DateTime.UtcNow >= _combatantSince + TimeSpan.FromSeconds(6)
                    && DateTime.UtcNow >= _nextTeleportAt)
                {
                    TeleportToCombatant(Combatant);
                }
            }
        }

        /// <summary>
        /// Scans nearby mobiles and sets Combatant to kick off fighting.
        /// Prefers the champion if it exists, otherwise the nearest creature.
        /// </summary>
        private void AcquireSpawnTarget()
        {
            // Champion first
            if (_targetSpawn != null && !_targetSpawn.Deleted
                && _targetSpawn.Champion != null && !_targetSpawn.Champion.Deleted
                && _targetSpawn.Champion.Alive)
            {
                Combatant = _targetSpawn.Champion;
                return;
            }

            // Nearest hostile creature within 15 tiles — never target other SimPlayers
            Mobile nearest     = null;
            double nearestDist = double.MaxValue;

            foreach (Mobile m in GetMobilesInRange(15))
            {
                if (m == this || m.Deleted || !m.Alive) continue;
                // Skip friendly SimPlayers; hostile ones (red/grey) are valid targets
                if (m is SimPlayer sp2 && !sp2.AlwaysAttackable && !sp2.AlwaysMurderer) continue;
                if (!(m is BaseCreature bc) || bc.Controlled) continue;
                if (!CanBeHarmful(m, false)) continue;

                double dist = GetDistanceToSqrt(m);
                if (dist < nearestDist) { nearestDist = dist; nearest = m; }
            }

            if (nearest != null)
                Combatant = nearest;
        }

        /// <summary>
        /// Teleports to a stuck combatant that is out of melee reach.
        /// Fires a chivalry visual, lands adjacent to the target, then re-engages.
        /// </summary>
        private void TeleportToCombatant(Mobile target)
        {
            _nextTeleportAt = DateTime.UtcNow + TimeSpan.FromSeconds(15); // 15s cooldown
            _combatantSince = DateTime.MinValue; // reset stuck timer

            FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);
            PlaySound(0x1F5);

            Timer.DelayCall(TimeSpan.FromSeconds(1.0), () =>
            {
                if (Deleted || _champPhase != ChampPhase.AtSpawn) return;
                if (target == null || target.Deleted || !target.Alive) return;

                // Land 1-2 tiles from the target
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

                // Re-engage immediately
                if (!target.Deleted && target.Alive)
                    Combatant = target;
            });
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


        private void BeginWithdraw(bool victory)
        {
            // Clean up phase immediately so no other code re-enters
            _champPhase   = ChampPhase.None;
            _targetSpawn  = null;
            FightMode     = FightMode.None;
            Combatant     = null;
            _nextChampRun = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(60, 120));

            if (!victory)
                Say(WithdrawSpeech[Utility.Random(1, WithdrawSpeech.Length - 1)]);

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
        private ChampionSpawn FindBestFeluccaSpawn()
        {
            ChampionSpawn best      = null;
            double        bestScore = double.MaxValue;

            foreach (ChampionSpawn cs in ChampionSystem.AllSpawns)
            {
                if (cs == null || cs.Deleted)      continue;
                if (cs.Map != Map.Felucca)          continue;
                if (cs.Location.Z < -5)             continue; // skip underground altars

                double dist  = DistanceTo(_homeLocation, cs.Location);
                double score = cs.Active ? dist * 0.5 : dist;

                if (score < bestScore)
                {
                    bestScore = score;
                    best      = cs;
                }
            }

            return best;
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

        public override string GetStatusDetail()
        {
            if (_champPhase == ChampPhase.None)
            {
                TimeSpan remaining = _nextChampRun - DateTime.UtcNow;
                return remaining > TimeSpan.Zero
                    ? $"ChampPhase: None — next run in {(int)remaining.TotalMinutes}m {remaining.Seconds}s"
                    : "ChampPhase: None — ready (waiting for Idle)";
            }

            string spawnInfo = (_targetSpawn != null && !_targetSpawn.Deleted)
                ? $" spawn={_targetSpawn.SpawnName ?? "?"} lvl={_targetSpawn.Level}/4"
                : " (no spawn ref)";

            return $"ChampPhase: {_champPhase}{spawnInfo}";
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
            VirtualArmor = 65;
            Fame  = 2000;
            Karma = 2000;
            Kills = 0;

            // Full plate
            AddItem(new PlateChest());
            AddItem(new PlateLegs());
            AddItem(new PlateArms());
            AddItem(new PlateGloves());
            AddItem(new PlateGorget());
            AddItem(new PlateHelm());
            AddItem(new Boots());
            AddItem(new HeaterShield());

            // Longsword with 25% hit fire area — bypasses physical resists on champion spawn creatures
            var sword = new Longsword();
            sword.WeaponAttributes.HitFireArea = 25;
            sword.Attributes.WeaponDamage      = 50; // +50% damage bonus
            sword.Attributes.AttackChance      = 15; // +15 HCI
            AddItem(sword);

            PackItem(new BookOfChivalry()); // always PackItem

            // Weapon specials — AI uses these automatically in combat
            SetWeaponAbility(WeaponAbility.ArmorIgnore);    // Longsword primary — bypasses armor
            SetWeaponAbility(WeaponAbility.ConcussionBlow); // Longsword secondary — reduces target Int
        }

        // -- Serialization (all champ state is transient) -------------
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
    // Arcane Brotherhood
    // War Mage template -- robes, spellbook
    // ============================================================
    public class ArcaneBrotherhoodSimPlayer : SimPlayer
    {
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
    // Silver Wolves
    // Wolf Warrior template -- chain armour, patrol role
    // ============================================================
    public class SilverWolvesSimPlayer : SimPlayer
    {
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

            AddItem(new ChainChest());
            AddItem(new ChainLegs());
            AddItem(new LeatherGorget());
            AddItem(new LeatherArms());
            AddItem(new LeatherGloves());
            AddItem(new Boots());
            AddItem(new Longsword());
            AddItem(new HeaterShield());
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
    // The Shadow Hand
    // Thief/Rogue template -- grey, hiding, town operations.
    // Pickpockets gold from nearby players while hidden.
    // Flees Silver Wolves. Never fights back.
    // ============================================================
    public class ShadowHandSimPlayer : SimPlayer
    {
        private DateTime _nextHideTime    = DateTime.MinValue;
        private DateTime _nextStealTime   = DateTime.MinValue; // transient — not serialized

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
    // Bone armour, necromancy. Grey alignment -- attackable.
    // StatCap 225.
    // ============================================================
    public class DeadWatchersSimPlayer : SimPlayer
    {
        public DeadWatchersSimPlayer(string memberName, Point3D home,
                                     SpawnZone zone, ScheduleProfile schedule)
            : base(FBGuilds.DeadWatchers, memberName, home, zone, schedule) { }

        public DeadWatchersSimPlayer(Serial serial) : base(serial) { }

        // Grey -- attackable but not murderer
        public override bool AlwaysAttackable => true;

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
            SetSkill(SkillName.Necromancy,  100.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 100.0, 100.0);
            SetSkill(SkillName.Parry,       100.0, 100.0);

            VirtualArmor = 45;
            Fame  = 8000;
            Karma = -100; // perm grey
            Kills = 0;

            // Dark bone armour
            var chest = new BoneChest();  chest.Hue = 0x455; AddItem(chest);
            var legs  = new BoneLegs();   legs.Hue  = 0x455; AddItem(legs);
            var arms  = new BoneArms();   arms.Hue  = 0x455; AddItem(arms);
            var glove = new BoneGloves(); glove.Hue = 0x455; AddItem(glove);
            var helm  = new BoneHelm();   helm.Hue  = 0x455; AddItem(helm);
            AddItem(new Longsword());
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
}
