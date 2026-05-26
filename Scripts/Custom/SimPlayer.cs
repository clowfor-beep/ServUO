// ============================================================
// SimPlayer.cs
// Scripts/Custom/SimPlayer.cs
//
// Base class for all AI guild members (SimPlayers).
// Phase 1: Idle <-> Travelling loop in Britain area.
//          FightMode.None - Wanderers do NOT fight.
// Phase 2: Banking state, OnTickIdle virtual hook,
//          StartTravelTo helper for subclasses (Shadow Hand flee).
//
// Lifecycle:
//   OnCooldown -> Activate() -> Idle -> Travelling -> Idle -> ...
//   Idle/Travelling/Banking -> OnDeath() -> Dead -> OnCooldown -> Activate()
//
// Managed by PlayerSimulatorManager.
// ============================================================

using System;
using Server;
using Server.Custom;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    public class SimPlayer : BaseFBCombatNPC
    {
        // -- Identity --------------------------------------------------
        private string _guildName;
        private string _memberName;

        [CommandProperty(AccessLevel.GameMaster)]
        public string GuildName
        {
            get => _guildName;
            set { _guildName = value; InvalidateProperties(); }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public string MemberName
        {
            get => _memberName;
            set { _memberName = value; InvalidateProperties(); }
        }

        // -- State -----------------------------------------------------
        private SimState _state = SimState.OnCooldown;
        private DateTime _cooldownUntil;
        protected Point3D _homeLocation;   // protected so ShadowHandSimPlayer can use it in FleeFrom()
        private SpawnZone _homeZone;

        [CommandProperty(AccessLevel.GameMaster)]
        public SimState State => _state;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IsOnCooldown => DateTime.UtcNow < _cooldownUntil;

        // -- AI sub-systems (transient - not serialized) ---------------
        private ScheduleProfile _schedule;
        private SimChatBrain    _chatBrain;

        // Travelling state
        private Point3D  _travelDest;
        private DateTime _travelTimeout;
        private DateTime _idleUntil;
        private bool     _travelIsBankTrip = false;

        // Banking state
        private bool     _inBankingCycle  = false;
        private DateTime _nextBankingTime = DateTime.MinValue; // immediately eligible on first activation
        private DateTime _bankUntil;

        // -- Cooldown duration -----------------------------------------
        private static readonly TimeSpan Tier1Cooldown = TimeSpan.FromMinutes(5);

        // -- Constructor (for PlayerSimulatorManager) ------------------
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

            ApplyTemplate(); // stats, skills, gear - overridden by guild subclasses

            // Start hidden in internal map - manager will activate via Activate()
            MoveToWorld(Point3D.Zero, Map.Internal);
            _state         = SimState.OnCooldown;
            _cooldownUntil = DateTime.UtcNow; // immediately eligible
        }

        public SimPlayer(Serial serial) : base(serial) { }

        // -- Template - overridden by guild subclasses -----------------
        /// <summary>
        /// Applies guild-specific stats, skills, and gear.
        /// Default is the Wanderer Warrior template.
        /// Subclasses override this - it is called from the main constructor.
        /// </summary>
        protected virtual void ApplyTemplate()
        {
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
            Kills = 0; // blue - Wanderers are not red

            AddItem(new LeatherChest());
            AddItem(new LeatherLegs());
            AddItem(new LeatherGorget());
            AddItem(new LeatherArms());
            AddItem(new LeatherGloves());
            AddItem(new Boots());
            AddItem(new Longsword());
        }

        // -- Manager integration ---------------------------------------

        /// <summary>Returns true if the schedule says this SimPlayer should be active now.</summary>
        public bool Schedule_ShouldBeActive() => _schedule.ShouldBeActive(DateTime.UtcNow);

        /// <summary>
        /// Called by PlayerSimulatorManager to bring the SimPlayer into the world.
        /// Moves to home location on Felucca and starts idle state.
        /// </summary>
        public void Activate()
        {
            if (Deleted) return;

            Resurrect();
            MoveToWorld(_homeLocation, Map.Felucca);
            _state     = SimState.Idle;
            _idleUntil = DateTime.UtcNow
                + TimeSpan.FromSeconds(Utility.RandomMinMax(5, 30));

            FBEventBus.Fire_SimPlayerActivated(this);
        }

        // -- OnThink - main AI tick ------------------------------------
        // ServUO calls this every ActiveSpeed seconds.
        public override void OnThink()
        {
            base.OnThink();

            if (Map == Map.Internal) return; // inactive - skip

            switch (_state)
            {
                case SimState.Idle:       TickIdle();       break;
                case SimState.Travelling: TickTravelling(); break;
                case SimState.Banking:    TickBanking();    break;
            }
        }

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

        private void StartTravelling()
        {
            Point3D dest = GetRandomNearbyPoint();
            if (dest == Point3D.Zero) return; // no valid point - stay idle briefly

            _travelDest       = dest;
            _travelTimeout    = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            _state            = SimState.Travelling;
            _travelIsBankTrip = false;
            CurrentSpeed      = ActiveSpeed;
        }

        private void StartBankingTrip()
        {
            // Pick a tile within +-4 of the Britain bank NPC coord
            Point3D bankBase = FBZones.BountyBoard_Britain_Bank;
            int bx = bankBase.X + Utility.RandomMinMax(-4, 4);
            int by = bankBase.Y + Utility.RandomMinMax(-4, 4);
            int bz = Map.Felucca.GetAverageZ(bx, by);

            _travelIsBankTrip = true;
            _travelDest       = new Point3D(bx, by, bz);
            _travelTimeout    = DateTime.UtcNow + TimeSpan.FromSeconds(120);
            _state            = SimState.Travelling;
            _inBankingCycle   = true;
            CurrentSpeed      = ActiveSpeed;
        }

        private void TickTravelling()
        {
            // Arrived?
            if (GetDistanceToSqrt(_travelDest) < 3.0)
            {
                ArriveAtDest();
                return;
            }

            // Timeout - stop trying, go idle
            if (DateTime.UtcNow > _travelTimeout)
            {
                ArriveAtDest();
                return;
            }

            // Step toward destination
            this.Move(this.GetDirectionTo(_travelDest));
        }

        private void ArriveAtDest()
        {
            if (_travelIsBankTrip)
            {
                _travelIsBankTrip = false;
                _state     = SimState.Banking;
                _bankUntil = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(3 * 60, 8 * 60));
                // Say "bank" after 1s delay - feels natural (walk up then speak)
                Timer.DelayCall(TimeSpan.FromSeconds(1.0), () =>
                {
                    if (!Deleted && _state == SimState.Banking)
                        this.Say("bank");
                });
            }
            else
            {
                _state     = SimState.Idle;
                _idleUntil = DateTime.UtcNow
                    + TimeSpan.FromSeconds(Utility.RandomMinMax(20, 60));
            }
        }

        private void TickBanking()
        {
            _chatBrain.TryBankSpeech(this);

            if (DateTime.UtcNow >= _bankUntil)
            {
                // Done banking - resume normal schedule
                _inBankingCycle  = false;
                _nextBankingTime = DateTime.UtcNow + TimeSpan.FromMinutes(Utility.RandomMinMax(20, 40));
                _state           = SimState.Idle;
                _idleUntil       = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(5, 20));
            }
        }

        /// <summary>Returns a random walkable point within ~20 tiles of home.</summary>
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

        /// <summary>
        /// Starts travelling toward the given destination.
        /// Used by guild subclasses that need to override travel behaviour (e.g. fleeing).
        /// </summary>
        protected void StartTravelTo(Point3D dest, TimeSpan timeout)
        {
            _travelDest       = dest;
            _travelTimeout    = DateTime.UtcNow + timeout;
            _state            = SimState.Travelling;
            _inBankingCycle   = false; // cancel any pending bank trip
            _travelIsBankTrip = false;
            CurrentSpeed      = ActiveSpeed;
        }

        // -- Death -----------------------------------------------------
        public override void OnDeath(Container c)
        {
            base.OnDeath(c);

            _state         = SimState.Dead;
            _cooldownUntil = DateTime.UtcNow + Tier1Cooldown;

            // Fire PlayerKilledSimPlayer so ReputationSystem can react
            Mobile killer = LastKiller;
            if (killer != null)
                FBEventBus.Fire_PlayerKilledSimPlayer(killer, this);

            FBEventBus.Fire_SimPlayerDeactivated(this);

            // Move to internal map after a short delay (let corpse linger)
            Timer.DelayCall(TimeSpan.FromSeconds(5.0), () =>
            {
                if (!Deleted)
                {
                    MoveToWorld(Point3D.Zero, Map.Internal);
                    _state = SimState.OnCooldown;
                }
            });

            // Schedule re-activation after cooldown
            TimeSpan delay = (_cooldownUntil - DateTime.UtcNow) + TimeSpan.FromSeconds(5);
            Timer.DelayCall(delay, Reactivate);
        }

        /// <summary>
        /// Called after cooldown expires. Returns to world if schedule allows;
        /// otherwise retries every 5 minutes.
        /// </summary>
        private void Reactivate()
        {
            if (Deleted) return;

            if (!_schedule.ShouldBeActive(DateTime.UtcNow))
            {
                Timer.DelayCall(TimeSpan.FromMinutes(5.0), Reactivate);
                return;
            }

            Activate();
        }

        // -- Properties ------------------------------------------------
        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            if (!string.IsNullOrEmpty(_guildName))
                list.Add($"[{_guildName}]");
        }

        // -- Helpers ---------------------------------------------------

        /// <summary>Returns the correct ScheduleProfile for a given guild name.</summary>
        internal static ScheduleProfile MakeSchedule(string guildName)
        {
            int drift = Utility.RandomMinMax(-30, 30);
            if (guildName == FBGuilds.CraftsmenLeague)   return ScheduleProfile.CraftsmensLeague(drift);
            if (guildName == FBGuilds.IronCompany)       return ScheduleProfile.IronCompany(drift);
            if (guildName == FBGuilds.ArcaneBrotherhood) return ScheduleProfile.ArcaneBrotherhood(drift);
            if (guildName == FBGuilds.SilverWolves)      return ScheduleProfile.SilverWolves(drift);
            if (guildName == FBGuilds.ShadowHand)        return ScheduleProfile.ShadowHand(drift);
            return ScheduleProfile.Wanderers(drift); // default / Wanderers
        }

        // -- Serialization ---------------------------------------------
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

            // Re-create transient sub-systems (not serialized)
            _schedule  = MakeSchedule(_guildName);
            _chatBrain = new SimChatBrain(_guildName);

            // Restore active state or schedule re-activation
            if (_state == SimState.Idle || _state == SimState.Travelling || _state == SimState.Banking)
            {
                // Was in world before save - return to idle shortly after load
                _state          = SimState.Idle;
                _idleUntil      = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                _inBankingCycle = false;
            }
            else // Dead or OnCooldown
            {
                if (!IsOnCooldown)
                    Timer.DelayCall(TimeSpan.FromSeconds(30), Reactivate);
                else
                    Timer.DelayCall(_cooldownUntil - DateTime.UtcNow, Reactivate);
            }
        }
    }
}
