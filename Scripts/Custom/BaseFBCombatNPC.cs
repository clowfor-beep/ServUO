// ============================================================
// BaseFBCombatNPC.cs
// Scripts/Custom/BaseFBCombatNPC.cs
//
// Shared combat foundation for all Forsaken Britannia AI
// fighter types: SimPlayers, Pool PKs, Encounter PKs.
//
// Inheritance chain:
//   BaseCreature
//     └─ BaseFBCombatNPC          ← this file
//          ├─ BasePKNPC           (PlayerKillerNPCs.cs)
//          │    └─ [21 concrete PK classes]
//          ├─ NovicePlayerKiller  (NovicePlayerKiller.cs)
//          └─ SimPlayer           (future)
//
// EQUIPMENT RULE (mandatory for every subclass):
//   Weapons / armour : AddItem(...)   — equips to correct layer
//   Spellbooks       : PackItem(...)  — NEVER AddItem (LayerConflict)
//   BookOfChivalry   : PackItem(...)  — NEVER AddItem (LayerConflict)
//
// NOTE: No Serialize/Deserialize override here — BaseFBCombatNPC
// has no persistent fields of its own. Adding them would shift
// the read order in every existing subclass save file.
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    public abstract class BaseFBCombatNPC : BaseCreature
    {
        // ── Speech (override in concrete classes) ─────────────────────────
        protected virtual string[] AggroLines => new string[0];
        protected virtual string[] KillLines  => new string[0];

        // ── Constructor ───────────────────────────────────────────────────
        protected BaseFBCombatNPC(AIType ai, FightMode mode, int range)
            : base(ai, mode, range, 1, 0.1, 0.2)
        {
            SetupAppearance();
            Kills = 5; // murderer flag — red name
        }

        public BaseFBCombatNPC(Serial serial) : base(serial) { }

        // ── Appearance ────────────────────────────────────────────────────
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

        // ── Encounter setup ───────────────────────────────────────────────
        // Called by PKEncounterSystem and GraveyardPKEncounter after MoveToWorld.
        // Sets combatant, fires aggro speech, and starts 5-min auto-delete.
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

        // ── AI ────────────────────────────────────────────────────────────
        // Keep combatant locked — some AI paths clear it on the base call.
        public override void OnThink()
        {
            Mobile target = Combatant as Mobile;
            base.OnThink();
            if (Deleted || !Alive) return;
            if (target != null && !target.Deleted && target.Alive && Combatant == null)
                Combatant = target;
        }

        // ── Combat speech hooks ───────────────────────────────────────────
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

        // ── Combat templates ──────────────────────────────────────────────
        // Call one of these from a concrete class constructor to apply a
        // full stat/skill/gear loadout for the given tier (1 = Newbie,
        // 2 = Advanced, 3 = Expert). Clamp unknown tiers to 1.
        //
        // EQUIPMENT RULE enforced here:
        //   Weapons/armour → AddItem   (equips correctly)
        //   Spellbooks     → PackItem  (avoids LayerConflict crash)

        // ── Template 1 — Dexxer ───────────────────────────────────────────
        // Pure warrior: Swords / Tactics / Anatomy / Healing / Parry
        protected void ApplyDexxerTemplate(int tier)
        {
            switch (tier)
            {
                case 2:
                    SetStr(82, 88);  SetDex(62, 68);  SetInt(27, 33);
                    SetHits(82, 88); SetStam(62, 68); SetMana(0);
                    SetSkill(SkillName.Swords,      100.0, 100.0);
                    SetSkill(SkillName.Tactics,     100.0, 100.0);
                    SetSkill(SkillName.Anatomy,      90.0,  90.0);
                    SetSkill(SkillName.Healing,      90.0,  90.0);
                    SetSkill(SkillName.Parry,        100.0, 100.0);
                    SetSkill(SkillName.Hiding,        60.0,  60.0);
                    SetSkill(SkillName.MagicResist,   60.0,  60.0);
                    AddItem(new Katana());
                    AddItem(new RingmailChest()); AddItem(new RingmailLegs());
                    AddItem(new LeatherArms());   AddItem(new LeatherGorget());
                    AddItem(new MetalShield());   AddItem(new Boots(Utility.RandomNeutralHue()));
                    Fame = 5000; Karma = -5000; VirtualArmor = 35;
                    break;

                case 3:
                    SetStr(97, 103);  SetDex(77, 83);  SetInt(42, 48);
                    SetHits(97, 103); SetStam(77, 83); SetMana(0);
                    SetSkill(SkillName.Swords,      100.0, 100.0);
                    SetSkill(SkillName.Tactics,     100.0, 100.0);
                    SetSkill(SkillName.Anatomy,     100.0, 100.0);
                    SetSkill(SkillName.Healing,     100.0, 100.0);
                    SetSkill(SkillName.Parry,       100.0, 100.0);
                    SetSkill(SkillName.Hiding,       70.0,  70.0);
                    SetSkill(SkillName.MagicResist,  70.0,  70.0);
                    SetSkill(SkillName.Chivalry,     60.0,  60.0);
                    AddItem(new Katana());
                    AddItem(new PlateChest());  AddItem(new PlateLegs());
                    AddItem(new PlateArms());   AddItem(new PlateGloves());
                    AddItem(new PlateHelm());   AddItem(new HeaterShield());
                    AddItem(new Boots(Utility.RandomNeutralHue()));
                    Fame = 12500; Karma = -12500; VirtualArmor = 50;
                    break;

                default: // Tier 1
                    SetStr(72, 78);  SetDex(47, 53);  SetInt(22, 28);
                    SetHits(72, 78); SetStam(47, 53); SetMana(0);
                    SetSkill(SkillName.Swords,      80.0, 80.0);
                    SetSkill(SkillName.Tactics,     75.0, 75.0);
                    SetSkill(SkillName.Anatomy,     70.0, 70.0);
                    SetSkill(SkillName.Healing,     75.0, 75.0);
                    SetSkill(SkillName.Parry,       70.0, 70.0);
                    SetSkill(SkillName.Hiding,      30.0, 30.0);
                    SetSkill(SkillName.MagicResist, 50.0, 50.0);
                    AddItem(new Katana());
                    AddItem(new LeatherChest()); AddItem(new LeatherLegs());
                    AddItem(new LeatherArms());  AddItem(new LeatherGorget());
                    AddItem(new WoodenShield()); AddItem(new Boots(Utility.RandomNeutralHue()));
                    Fame = 1500; Karma = -1500; VirtualArmor = 20;
                    break;
            }
        }

        // ── Template 2 — Mage ─────────────────────────────────────────────
        // Pure caster: Magery / EvalInt / MagicResist / Meditation / Wrestling
        protected void ApplyMageTemplate(int tier)
        {
            switch (tier)
            {
                case 2:
                    SetStr(32, 38);  SetDex(32, 38);  SetInt(107, 113);
                    SetHits(32, 38); SetStam(32, 38); SetMana(107, 113);
                    SetSkill(SkillName.Magery,       100.0, 100.0);
                    SetSkill(SkillName.EvalInt,      100.0, 100.0);
                    SetSkill(SkillName.MagicResist,  100.0, 100.0);
                    SetSkill(SkillName.Meditation,   100.0, 100.0);
                    SetSkill(SkillName.Wrestling,     80.0,  80.0);
                    SetSkill(SkillName.Focus,         60.0,  60.0);
                    SetSkill(SkillName.Hiding,        60.0,  60.0);
                    AddItem(new Robe(Utility.RandomNeutralHue()));
                    AddItem(new Sandals());
                    PackItem(new Spellbook());
                    Fame = 5000; Karma = -5000; VirtualArmor = 10;
                    break;

                case 3:
                    SetStr(37, 43);  SetDex(37, 43);  SetInt(142, 148);
                    SetHits(37, 43); SetStam(37, 43); SetMana(142, 148);
                    SetSkill(SkillName.Magery,       100.0, 100.0);
                    SetSkill(SkillName.EvalInt,      100.0, 100.0);
                    SetSkill(SkillName.MagicResist,  100.0, 100.0);
                    SetSkill(SkillName.Meditation,   100.0, 100.0);
                    SetSkill(SkillName.Wrestling,    100.0, 100.0);
                    SetSkill(SkillName.Focus,        100.0, 100.0);
                    SetSkill(SkillName.Inscription,  100.0, 100.0);
                    AddItem(new Robe(Utility.RandomNeutralHue()));
                    AddItem(new WizardsHat());
                    AddItem(new Sandals());
                    PackItem(new Spellbook());
                    Fame = 12500; Karma = -12500; VirtualArmor = 10;
                    break;

                default: // Tier 1
                    SetStr(27, 33);  SetDex(27, 33);  SetInt(87, 93);
                    SetHits(27, 33); SetStam(27, 33); SetMana(87, 93);
                    SetSkill(SkillName.Magery,      80.0, 80.0);
                    SetSkill(SkillName.EvalInt,     75.0, 75.0);
                    SetSkill(SkillName.MagicResist, 70.0, 70.0);
                    SetSkill(SkillName.Meditation,  75.0, 75.0);
                    SetSkill(SkillName.Wrestling,   60.0, 60.0);
                    SetSkill(SkillName.Hiding,      40.0, 40.0);
                    SetSkill(SkillName.Tactics,     50.0, 50.0);
                    AddItem(new Robe(Utility.RandomNeutralHue()));
                    AddItem(new Sandals());
                    PackItem(new Spellbook());
                    Fame = 2000; Karma = -2000; VirtualArmor = 10;
                    break;
            }
        }

        // ── Template 3 — NecroMage ────────────────────────────────────────
        // Magery + Necromancy. Wraith Form, Strangle, Wither.
        // Both spellbooks MUST go in pack.
        protected void ApplyNecroMageTemplate(int tier)
        {
            switch (tier)
            {
                case 2:
                    SetStr(32, 38);  SetDex(32, 38);  SetInt(107, 113);
                    SetHits(32, 38); SetStam(32, 38); SetMana(107, 113);
                    SetSkill(SkillName.Magery,       100.0, 100.0);
                    SetSkill(SkillName.EvalInt,       90.0,  90.0);
                    SetSkill(SkillName.MagicResist,   90.0,  90.0);
                    SetSkill(SkillName.Necromancy,   100.0, 100.0);
                    SetSkill(SkillName.SpiritSpeak,  100.0, 100.0);
                    SetSkill(SkillName.Meditation,    60.0,  60.0);
                    SetSkill(SkillName.Focus,         60.0,  60.0);
                    AddItem(new BoneChest()); AddItem(new BoneArms());
                    AddItem(new Sandals());
                    PackItem(new Spellbook());
                    PackItem(new NecromancerSpellbook());
                    Fame = 6000; Karma = -6000; VirtualArmor = 28;
                    break;

                case 3:
                    SetStr(47, 53);  SetDex(42, 48);  SetInt(127, 133);
                    SetHits(47, 53); SetStam(42, 48); SetMana(127, 133);
                    SetSkill(SkillName.Magery,       100.0, 100.0);
                    SetSkill(SkillName.EvalInt,      100.0, 100.0);
                    SetSkill(SkillName.MagicResist,  100.0, 100.0);
                    SetSkill(SkillName.Necromancy,   100.0, 100.0);
                    SetSkill(SkillName.SpiritSpeak,  100.0, 100.0);
                    SetSkill(SkillName.Swords,       100.0, 100.0);
                    SetSkill(SkillName.Tactics,      100.0, 100.0);
                    AddItem(new BoneChest());  AddItem(new BoneArms());
                    AddItem(new BoneLegs());   AddItem(new BoneGloves());
                    AddItem(new BoneHelm());   AddItem(new BoneHarvester());
                    AddItem(new Sandals());
                    PackItem(new Spellbook());
                    PackItem(new NecromancerSpellbook());
                    Fame = 15000; Karma = -15000; VirtualArmor = 35;
                    break;

                default: // Tier 1
                    SetStr(27, 33);  SetDex(27, 33);  SetInt(87, 93);
                    SetHits(27, 33); SetStam(27, 33); SetMana(87, 93);
                    SetSkill(SkillName.Magery,      70.0, 70.0);
                    SetSkill(SkillName.EvalInt,     60.0, 60.0);
                    SetSkill(SkillName.MagicResist, 60.0, 60.0);
                    SetSkill(SkillName.Necromancy,  80.0, 80.0);
                    SetSkill(SkillName.SpiritSpeak, 70.0, 70.0);
                    SetSkill(SkillName.Meditation,  60.0, 60.0);
                    SetSkill(SkillName.Hiding,      50.0, 50.0);
                    AddItem(new Robe(Utility.RandomNeutralHue()));
                    AddItem(new Sandals());
                    PackItem(new Spellbook());
                    PackItem(new NecromancerSpellbook());
                    Fame = 2000; Karma = -2000; VirtualArmor = 10;
                    break;
            }
        }

        // ── Template 4 — NinjaDexxer ──────────────────────────────────────
        // Fencing + Ninjitsu + Hiding/Stealth. Ambush and Death Strike.
        protected void ApplyNinjaDexxerTemplate(int tier)
        {
            switch (tier)
            {
                case 2:
                    SetStr(52, 58);  SetDex(82, 88);  SetInt(37, 43);
                    SetHits(52, 58); SetStam(82, 88); SetMana(0);
                    SetSkill(SkillName.Fencing,     100.0, 100.0);
                    SetSkill(SkillName.Tactics,     100.0, 100.0);
                    SetSkill(SkillName.Ninjitsu,    100.0, 100.0);
                    SetSkill(SkillName.Hiding,      100.0, 100.0);
                    SetSkill(SkillName.Stealth,     100.0, 100.0);
                    SetSkill(SkillName.Anatomy,      60.0,  60.0);
                    SetSkill(SkillName.Healing,      40.0,  40.0);
                    AddItem(new Kryss());
                    AddItem(new Kasa());
                    AddItem(new LeatherChest(0x1)); // dark hue
                    AddItem(new LeatherLegs());
                    AddItem(new Sandals());
                    Fame = 5000; Karma = -5000; VirtualArmor = 22;
                    break;

                case 3:
                    SetStr(57, 63);  SetDex(107, 113); SetInt(52, 58);
                    SetHits(57, 63); SetStam(107, 113); SetMana(0);
                    SetSkill(SkillName.Fencing,     100.0, 100.0);
                    SetSkill(SkillName.Tactics,     100.0, 100.0);
                    SetSkill(SkillName.Ninjitsu,    100.0, 100.0);
                    SetSkill(SkillName.Hiding,      100.0, 100.0);
                    SetSkill(SkillName.Stealth,     100.0, 100.0);
                    SetSkill(SkillName.Anatomy,     100.0, 100.0);
                    SetSkill(SkillName.Healing,      50.0,  50.0);
                    SetSkill(SkillName.Focus,        50.0,  50.0);
                    AddItem(new Kryss());
                    AddItem(new Kasa());
                    AddItem(new LeatherChest(0x1));
                    AddItem(new LeatherLegs());  AddItem(new LeatherArms());
                    AddItem(new LeatherGloves()); AddItem(new Sandals());
                    Fame = 12500; Karma = -12500; VirtualArmor = 28;
                    break;

                default: // Tier 1
                    SetStr(47, 53);  SetDex(67, 73);  SetInt(27, 33);
                    SetHits(47, 53); SetStam(67, 73); SetMana(0);
                    SetSkill(SkillName.Fencing,  75.0, 75.0);
                    SetSkill(SkillName.Tactics,  70.0, 70.0);
                    SetSkill(SkillName.Ninjitsu, 75.0, 75.0);
                    SetSkill(SkillName.Hiding,   75.0, 75.0);
                    SetSkill(SkillName.Stealth,  75.0, 75.0);
                    SetSkill(SkillName.Anatomy,  40.0, 40.0);
                    SetSkill(SkillName.Healing,  40.0, 40.0);
                    AddItem(new Kryss());
                    AddItem(new Kasa());
                    AddItem(new LeatherChest()); AddItem(new LeatherLegs());
                    AddItem(new Sandals());
                    Fame = 1500; Karma = -1500; VirtualArmor = 18;
                    break;
            }
        }

        // ── Template 5 — Paladin ──────────────────────────────────────────
        // Swords + Chivalry. Enemy of One, Close Wounds, Divine Fury.
        // BookOfChivalry MUST go in pack.
        protected void ApplyPaladinTemplate(int tier)
        {
            switch (tier)
            {
                case 2:
                    SetStr(77, 83);  SetDex(57, 63);  SetInt(37, 43);
                    SetHits(77, 83); SetStam(57, 63); SetMana(37, 43);
                    SetSkill(SkillName.Swords,    100.0, 100.0);
                    SetSkill(SkillName.Tactics,   100.0, 100.0);
                    SetSkill(SkillName.Chivalry,  100.0, 100.0);
                    SetSkill(SkillName.Healing,    90.0,  90.0);
                    SetSkill(SkillName.Anatomy,    90.0,  90.0);
                    SetSkill(SkillName.Parry,      70.0,  70.0);
                    SetSkill(SkillName.Focus,      50.0,  50.0);
                    AddItem(new Broadsword());
                    AddItem(new RingmailChest());  AddItem(new RingmailLegs());
                    AddItem(new RingmailArms());   AddItem(new RingmailGloves());
                    AddItem(new MetalShield());    AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new BookOfChivalry());
                    Fame = 5000; Karma = -5000; VirtualArmor = 38;
                    break;

                case 3:
                    SetStr(92, 98);  SetDex(72, 78);  SetInt(52, 58);
                    SetHits(92, 98); SetStam(72, 78); SetMana(52, 58);
                    SetSkill(SkillName.Swords,    100.0, 100.0);
                    SetSkill(SkillName.Tactics,   100.0, 100.0);
                    SetSkill(SkillName.Chivalry,  100.0, 100.0);
                    SetSkill(SkillName.Healing,   100.0, 100.0);
                    SetSkill(SkillName.Anatomy,   100.0, 100.0);
                    SetSkill(SkillName.Parry,     100.0, 100.0);
                    SetSkill(SkillName.Focus,     100.0, 100.0);
                    AddItem(new Broadsword());
                    AddItem(new PlateChest());  AddItem(new PlateLegs());
                    AddItem(new PlateArms());   AddItem(new PlateGloves());
                    AddItem(new PlateHelm());   AddItem(new HeaterShield());
                    AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new BookOfChivalry());
                    PackItem(new GreaterHealPotion());
                    PackItem(new GreaterHealPotion());
                    PackItem(new GreaterHealPotion());
                    Fame = 12500; Karma = -12500; VirtualArmor = 50;
                    break;

                default: // Tier 1
                    SetStr(67, 73);  SetDex(47, 53);  SetInt(27, 33);
                    SetHits(67, 73); SetStam(47, 53); SetMana(27, 33);
                    SetSkill(SkillName.Swords,    75.0, 75.0);
                    SetSkill(SkillName.Tactics,   70.0, 70.0);
                    SetSkill(SkillName.Chivalry,  80.0, 80.0);
                    SetSkill(SkillName.Healing,   70.0, 70.0);
                    SetSkill(SkillName.Anatomy,   70.0, 70.0);
                    SetSkill(SkillName.Parry,     55.0, 55.0);
                    SetSkill(SkillName.Hiding,    30.0, 30.0);
                    AddItem(new Broadsword());
                    AddItem(new StuddedChest());  AddItem(new LeatherLegs());
                    AddItem(new LeatherArms());   AddItem(new LeatherGorget());
                    AddItem(new WoodenShield());  AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new BookOfChivalry());
                    Fame = 1500; Karma = -1500; VirtualArmor = 22;
                    break;
            }
        }

        // ── Template 6 — Archer ───────────────────────────────────────────
        // Archery + Hiding/Stealth. Ranged opener, hides after kills.
        protected void ApplyArcherTemplate(int tier)
        {
            switch (tier)
            {
                case 2:
                    SetStr(57, 63);  SetDex(87, 93);  SetInt(27, 33);
                    SetHits(57, 63); SetStam(87, 93); SetMana(0);
                    SetSkill(SkillName.Archery,      100.0, 100.0);
                    SetSkill(SkillName.Tactics,      100.0, 100.0);
                    SetSkill(SkillName.Anatomy,       90.0,  90.0);
                    SetSkill(SkillName.Healing,       90.0,  90.0);
                    SetSkill(SkillName.Hiding,        100.0, 100.0);
                    SetSkill(SkillName.Stealth,       100.0, 100.0);
                    SetSkill(SkillName.MagicResist,    20.0,  20.0);
                    AddItem(new CompositeBow());
                    AddItem(new StuddedChest());  AddItem(new StuddedLegs());
                    AddItem(new StuddedArms());   AddItem(new StuddedGloves());
                    AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new Arrow(100));
                    Fame = 5000; Karma = -5000; VirtualArmor = 28;
                    break;

                case 3:
                    SetStr(67, 73);  SetDex(107, 113); SetInt(42, 48);
                    SetHits(67, 73); SetStam(107, 113); SetMana(0);
                    SetSkill(SkillName.Archery,   100.0, 100.0);
                    SetSkill(SkillName.Tactics,   100.0, 100.0);
                    SetSkill(SkillName.Anatomy,   100.0, 100.0);
                    SetSkill(SkillName.Healing,   100.0, 100.0);
                    SetSkill(SkillName.Hiding,    100.0, 100.0);
                    SetSkill(SkillName.Stealth,   100.0, 100.0);
                    SetSkill(SkillName.Ninjitsu,  100.0, 100.0);
                    AddItem(new ElvenCompositeLongbow());
                    AddItem(new Kasa());
                    AddItem(new LeatherChest()); AddItem(new LeatherLegs());
                    AddItem(new LeatherArms());
                    AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new Arrow(150));
                    Fame = 12500; Karma = -12500; VirtualArmor = 25;
                    break;

                default: // Tier 1
                    SetStr(47, 53);  SetDex(72, 78);  SetInt(22, 28);
                    SetHits(47, 53); SetStam(72, 78); SetMana(0);
                    SetSkill(SkillName.Archery,  80.0, 80.0);
                    SetSkill(SkillName.Tactics,  75.0, 75.0);
                    SetSkill(SkillName.Anatomy,  70.0, 70.0);
                    SetSkill(SkillName.Healing,  75.0, 75.0);
                    SetSkill(SkillName.Hiding,   75.0, 75.0);
                    SetSkill(SkillName.Stealth,  75.0, 75.0);
                    AddItem(new Bow());
                    AddItem(new LeatherChest()); AddItem(new LeatherArms());
                    AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new Arrow(50));
                    Fame = 1500; Karma = -1500; VirtualArmor = 15;
                    break;
            }
        }

        // ── Template 7 — Sampire ──────────────────────────────────────────
        // Swords + Bushido + Necromancy. Vampiric Embrace life leech.
        // NecromancerSpellbook and BookOfChivalry (Tier 3) MUST go in pack.
        protected void ApplySampireTemplate(int tier)
        {
            switch (tier)
            {
                case 2:
                    SetStr(72, 78);  SetDex(72, 78);  SetInt(27, 33);
                    SetHits(72, 78); SetStam(72, 78); SetMana(27, 33);
                    SetSkill(SkillName.Swords,      100.0, 100.0);
                    SetSkill(SkillName.Tactics,     100.0, 100.0);
                    SetSkill(SkillName.Bushido,     100.0, 100.0);
                    SetSkill(SkillName.Necromancy,  100.0, 100.0);
                    SetSkill(SkillName.SpiritSpeak, 100.0, 100.0);
                    SetSkill(SkillName.Parry,       100.0, 100.0);
                    AddItem(new Katana());
                    AddItem(new RingmailChest());  AddItem(new RingmailLegs());
                    AddItem(new LeatherArms());    AddItem(new LeatherGorget());
                    AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new NecromancerSpellbook());
                    Fame = 7000; Karma = -7000; VirtualArmor = 35;
                    break;

                case 3:
                    SetStr(87, 93);  SetDex(82, 88);  SetInt(47, 53);
                    SetHits(87, 93); SetStam(82, 88); SetMana(47, 53);
                    SetSkill(SkillName.Swords,      100.0, 100.0);
                    SetSkill(SkillName.Tactics,     100.0, 100.0);
                    SetSkill(SkillName.Bushido,     100.0, 100.0);
                    SetSkill(SkillName.Necromancy,  100.0, 100.0);
                    SetSkill(SkillName.SpiritSpeak, 100.0, 100.0);
                    SetSkill(SkillName.Parry,       100.0, 100.0);
                    SetSkill(SkillName.Chivalry,    100.0, 100.0);
                    AddItem(new Katana());
                    AddItem(new StuddedChest());  AddItem(new StuddedLegs());
                    AddItem(new StuddedArms());   AddItem(new StuddedGloves());
                    AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new NecromancerSpellbook());
                    PackItem(new BookOfChivalry());
                    PackItem(new GreaterHealPotion());
                    PackItem(new GreaterHealPotion());
                    PackItem(new GreaterHealPotion());
                    Fame = 15000; Karma = -15000; VirtualArmor = 38;
                    break;

                default: // Tier 1
                    SetStr(62, 68);  SetDex(57, 63);  SetInt(22, 28);
                    SetHits(62, 68); SetStam(57, 63); SetMana(22, 28);
                    SetSkill(SkillName.Swords,      80.0, 80.0);
                    SetSkill(SkillName.Tactics,     75.0, 75.0);
                    SetSkill(SkillName.Bushido,     80.0, 80.0);
                    SetSkill(SkillName.Necromancy,  80.0, 80.0);
                    SetSkill(SkillName.SpiritSpeak, 75.0, 75.0);
                    SetSkill(SkillName.Parry,       60.0, 60.0);
                    AddItem(new Katana());
                    AddItem(new LeatherChest());  AddItem(new LeatherLegs());
                    AddItem(new LeatherArms());   AddItem(new LeatherGorget());
                    AddItem(new Boots(Utility.RandomNeutralHue()));
                    PackItem(new NecromancerSpellbook());
                    Fame = 2000; Karma = -2000; VirtualArmor = 22;
                    break;
            }
        }
    }
}
