// ============================================================
// BattlecrySystem.cs
// Scripts/Custom/BattlecrySystem.cs
//
// Sergeant Vale's Battlecry — triggered whenever a red skull
// candle is lit at a champion spawn.
//
// A random buff is applied to all Iron Company members, players,
// and player-controlled pets within 100 tiles of Vale.
// Only one Battlecry buff can be active at a time per mobile.
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    // ── Buff types ────────────────────────────────────────────────────────────

    public enum BattlecryType
    {
        HealAndCure  = 0,   // instant: full heal, cure poison, restore stam/mana
        DamageBonus  = 1,   // +30 STR stat mod  (≈10% melee damage increase)
        HpRegen      = 2,   // +20 HP per second for 2 minutes
        SkillGain    = 3,   // double all skill gain for 2 minutes
        Luck         = 4,   // +500 Luck item in backpack for 2 minutes
        ManaRegen    = 5,   // +20 Mana per second for 2 minutes
        AllResists   = 6,   // +75 all resistances for 2 minutes
        SpeedBoost   = 7,   // +60 DEX stat mod + full stam restore for 2 minutes
    }

    // ── Manager ───────────────────────────────────────────────────────────────

    public static class BattlecrySystem
    {
        private static readonly TimeSpan Duration = TimeSpan.FromMinutes(2);
        private const string ModName = "Battlecry";

        // Active buff entries keyed by mobile serial
        private static readonly Dictionary<Serial, BattlecryEntry> _active
            = new Dictionary<Serial, BattlecryEntry>();

        // Serials currently receiving doubled skill gain
        internal static readonly HashSet<Serial> SkillDoubled = new HashSet<Serial>();

        // Re-entrancy guard for the SkillGain event handler
        private static bool _inSkillHandler;

        // ── Startup hook ──────────────────────────────────────────────────────

        public static void Initialize()
        {
            EventSink.SkillGain += OnSkillGain;
        }

        // ── Skill gain doubling ───────────────────────────────────────────────

        private static void OnSkillGain(SkillGainEventArgs e)
        {
            if (_inSkillHandler) return;
            if (!SkillDoubled.Contains(e.From.Serial)) return;

            Skill s = e.From.Skills[e.Skill.SkillName];
            if (s == null) return;

            _inSkillHandler = true;
            // e.Gained is in internal fixed-point units — add the same again to double it
            s.BaseFixedPoint = Math.Min(s.CapFixedPoint, s.BaseFixedPoint + e.Gained);
            _inSkillHandler = false;
        }

        // ── Called from Sergeant Vale when a red candle lights ────────────────

        public static void ApplyBattlecry(Mobile vale)
        {
            if (vale == null || vale.Deleted || vale.Map == null) return;

            BattlecryType type = (BattlecryType)Utility.Random(8);

            // Announce with a world overhead + local say
            vale.PublicOverheadMessage(Network.MessageType.Regular, 0x4AA, false,
                $"*BATTLECRY: {GetBuffName(type)}*");
            Effects.PlaySound(vale.Location, vale.Map, 0x19C);

            // Apply to all eligible mobiles within 100 tiles
            foreach (Mobile m in vale.GetMobilesInRange(100))
            {
                if (m == null || m.Deleted || !m.Alive) continue;

                bool eligible = m is IronCompanySimPlayer
                             || m is PlayerMobile
                             || (m is BaseCreature bc && bc.Controlled
                                 && bc.ControlMaster is PlayerMobile);

                if (!eligible) continue;

                Apply(m, type);
            }
        }

        // ── Apply buff to a single mobile ─────────────────────────────────────

        public static void Apply(Mobile m, BattlecryType type)
        {
            // Cancel any existing Battlecry first
            Cancel(m);

            // Visual + sound on target
            Effects.SendLocationParticles(
                EffectItem.Create(m.Location, m.Map, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            m.PlaySound(0x1F2);

            // ── Instant buff — no timer needed ──────────────────────────────
            if (type == BattlecryType.HealAndCure)
            {
                m.Hits = m.HitsMax;
                m.Stam = m.StamMax;
                m.Mana = m.ManaMax;
                m.CurePoison();

                if (m is PlayerMobile)
                    m.SendMessage(0x35,
                        "Sergeant Vale's Battlecry: Full Restoration! You are completely healed and cured!");
                return;
            }

            // ── Timed buffs ─────────────────────────────────────────────────
            var entry = new BattlecryEntry(m, type, Duration);
            _active[m.Serial] = entry;
            entry.Start();

            if (m is PlayerMobile pm)
                pm.SendMessage(0x35,
                    $"Sergeant Vale's Battlecry: {GetBuffName(type)} — {GetBuffDescription(type)} for 2 minutes!");
        }

        // ── Cancel an active buff ─────────────────────────────────────────────

        public static void Cancel(Mobile m)
        {
            if (!_active.TryGetValue(m.Serial, out var entry)) return;
            entry.Stop();
            _active.Remove(m.Serial);
        }

        // ── Called by BattlecryEntry when the timer expires ───────────────────

        internal static void OnExpired(Mobile m)
        {
            _active.Remove(m.Serial);
            if (m is PlayerMobile pm && !pm.Deleted)
                pm.SendMessage(0x22, "Sergeant Vale's Battlecry has faded.");
        }

        // ── Buff text helpers ─────────────────────────────────────────────────

        public static string GetBuffName(BattlecryType type)
        {
            switch (type)
            {
                case BattlecryType.HealAndCure:  return "Full Restoration!";
                case BattlecryType.DamageBonus:  return "Strength of Iron!";
                case BattlecryType.HpRegen:      return "Iron Constitution!";
                case BattlecryType.SkillGain:    return "Warrior's Focus!";
                case BattlecryType.Luck:         return "Fortune's Favor!";
                case BattlecryType.ManaRegen:    return "Arcane Surge!";
                case BattlecryType.AllResists:   return "Iron Fortress!";
                case BattlecryType.SpeedBoost:   return "Swift as Iron!";
                default: return "Battlecry!";
            }
        }

        private static string GetBuffDescription(BattlecryType type)
        {
            switch (type)
            {
                case BattlecryType.DamageBonus:  return "+30 Strength (increased melee damage)";
                case BattlecryType.HpRegen:      return "+20 HP per second";
                case BattlecryType.SkillGain:    return "all skill gain doubled";
                case BattlecryType.Luck:         return "+500 Luck";
                case BattlecryType.ManaRegen:    return "+20 Mana per second";
                case BattlecryType.AllResists:   return "+75 to all resistances";
                case BattlecryType.SpeedBoost:   return "+60 Dexterity and full stamina restored";
                default: return "blessed by the Iron Company";
            }
        }
    }

    // ── Active buff entry — applies and removes a single timed buff ───────────

    public sealed class BattlecryEntry
    {
        private const string ModName = "Battlecry";

        private readonly Mobile        _mobile;
        public  readonly BattlecryType Type;
        private readonly TimeSpan      _duration;

        private Timer               _expireTimer;
        private RegenTimer          _regenTimer;
        private BattlecryLuckCharm  _luckCharm;

        public BattlecryEntry(Mobile m, BattlecryType type, TimeSpan duration)
        {
            _mobile   = m;
            Type      = type;
            _duration = duration;
        }

        public void Start()
        {
            switch (Type)
            {
                case BattlecryType.DamageBonus:
                    _mobile.AddStatMod(new StatMod(StatType.Str, ModName, 30, _duration));
                    break;

                case BattlecryType.HpRegen:
                    _regenTimer = new RegenTimer(_mobile, isHp: true);
                    _regenTimer.Start();
                    break;

                case BattlecryType.SkillGain:
                    BattlecrySystem.SkillDoubled.Add(_mobile.Serial);
                    break;

                case BattlecryType.Luck:
                    if (_mobile.Backpack != null)
                    {
                        _luckCharm = new BattlecryLuckCharm();
                        _mobile.Backpack.DropItem(_luckCharm);
                    }
                    break;

                case BattlecryType.ManaRegen:
                    _regenTimer = new RegenTimer(_mobile, isHp: false);
                    _regenTimer.Start();
                    break;

                case BattlecryType.AllResists:
                    _mobile.AddResistanceMod(new ResistanceMod(ResistanceType.Physical, ModName + "_Phys", 75));
                    _mobile.AddResistanceMod(new ResistanceMod(ResistanceType.Fire,     ModName + "_Fire", 75));
                    _mobile.AddResistanceMod(new ResistanceMod(ResistanceType.Cold,     ModName + "_Cold", 75));
                    _mobile.AddResistanceMod(new ResistanceMod(ResistanceType.Poison,   ModName + "_Pois", 75));
                    _mobile.AddResistanceMod(new ResistanceMod(ResistanceType.Energy,   ModName + "_Nrgy", 75));
                    break;

                case BattlecryType.SpeedBoost:
                    _mobile.AddStatMod(new StatMod(StatType.Dex, ModName, 60, _duration));
                    _mobile.Stam = _mobile.StamMax;
                    break;
            }

            _expireTimer = Timer.DelayCall(_duration, OnExpired);
        }

        public void Stop()
        {
            _expireTimer?.Stop();
            _regenTimer?.Stop();

            switch (Type)
            {
                case BattlecryType.DamageBonus:
                case BattlecryType.SpeedBoost:
                    _mobile.RemoveStatMod(ModName);
                    break;

                case BattlecryType.SkillGain:
                    BattlecrySystem.SkillDoubled.Remove(_mobile.Serial);
                    break;

                case BattlecryType.Luck:
                    if (_luckCharm != null && !_luckCharm.Deleted)
                        _luckCharm.Delete();
                    _luckCharm = null;
                    break;

                case BattlecryType.AllResists:
                    _mobile.RemoveResistanceMod(ModName + "_Phys");
                    _mobile.RemoveResistanceMod(ModName + "_Fire");
                    _mobile.RemoveResistanceMod(ModName + "_Cold");
                    _mobile.RemoveResistanceMod(ModName + "_Pois");
                    _mobile.RemoveResistanceMod(ModName + "_Nrgy");
                    break;
            }
        }

        private void OnExpired()
        {
            Stop();
            BattlecrySystem.OnExpired(_mobile);
        }
    }

    // ── Regen timer — ticks every second for 120 ticks (2 minutes) ───────────

    public sealed class RegenTimer : Timer
    {
        private readonly Mobile _mobile;
        private readonly bool   _isHp;
        private          int    _ticks;

        public RegenTimer(Mobile m, bool isHp)
            : base(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0))
        {
            _mobile   = m;
            _isHp     = isHp;
            _ticks    = 0;
            Priority  = TimerPriority.OneSecond;
        }

        protected override void OnTick()
        {
            if (++_ticks >= 120 || _mobile.Deleted || !_mobile.Alive)
            {
                Stop();
                return;
            }

            if (_isHp)
                _mobile.Hits = Math.Min(_mobile.HitsMax, _mobile.Hits + 20);
            else
                _mobile.Mana = Math.Min(_mobile.ManaMax, _mobile.Mana + 20);
        }
    }

    // ── Luck charm — BaseJewel with Attributes.Luck = 500 ────────────────────
    // Placed in the player's backpack. BaseJewel carries AosAttributes.Luck
    // which PlayerMobile.Luck sums from all equipped/owned items.

    public sealed class BattlecryLuckCharm : BaseJewel
    {
        public override string DefaultName => "Sergeant Vale's Fortune";

        [Constructable]
        public BattlecryLuckCharm() : base(0x1EBE, Layer.Ring)  // coin/token graphic
        {
            Blessed          = true;
            Movable          = false;
            LootType         = LootType.Blessed;
            Attributes.Luck  = 500;
        }

        public BattlecryLuckCharm(Serial serial) : base(serial) { }

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
}
