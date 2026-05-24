// ============================================================
// NovicePlayerKiller.cs
// Scripts/Custom/NovicePlayerKiller.cs
//
// Newbie-tier PK NPC.
// Two modes:
//   - Static spawn  : FightMode.Aggressor (only retaliates)
//   - Encounter spawn: FightMode.Closest  (hunts the player)
//
// The GraveyardPKEncounter system uses encounter mode.
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    public class NovicePlayerKiller : BaseFBCombatNPC
    {
        // -------------------------------------------------------
        // Speech
        // -------------------------------------------------------
        private static readonly string[] AggroSpeech = new[]
        {
            "You shouldn't have done that...",
            "Big mistake, friend.",
            "I was just passing through. Now you die.",
            "You picked the wrong grave to visit.",
            "Ha! Another one who thinks they can take me.",
        };

        private static readonly string[] KillSpeech = new[]
        {
            "Should've walked away.",
            "Rest here. The company's good.",
            "*spits* Easy gold.",
            "One more for the graveyard.",
            "Next!",
        };

        private static readonly string[] PortalSpeech = new[]
        {
            "Nowhere left to run!",
            "I've been watching you.",
            "This graveyard has a new tenant.",
            "Did you really think you were alone here?",
            "Your gold is mine.",
        };

        // Route base speech arrays through BaseFBCombatNPC virtual properties
        protected override string[] AggroLines => AggroSpeech;
        protected override string[] KillLines  => KillSpeech;

        // -------------------------------------------------------
        // Constructors
        // -------------------------------------------------------

        // Static spawn — passive until hit
        [Constructable]
        public NovicePlayerKiller()
            : this(FightMode.Aggressor, null)
        {
        }

        // Encounter spawn — immediately hunts a target
        public NovicePlayerKiller(Mobile target)
            : this(FightMode.Closest, target)
        {
        }

        private NovicePlayerKiller(FightMode mode, Mobile target)
            : base(AIType.AI_Melee, mode, 12)
        {
            // Appearance and Kills=5 handled by BaseFBCombatNPC.SetupAppearance()
            Title = "the ruffian";

            // Stats — Newbie tier: StatCap 150
            SetStr(72, 76);
            SetDex(46, 51);
            SetInt(20, 25);

            SetHits(72, 76);
            SetStam(46, 51);
            SetMana(0);

            // Skills — Newbie tier: SkillCap 450
            SetSkill(SkillName.Swords,      76.0, 80.0);
            SetSkill(SkillName.Tactics,     72.0, 76.0);
            SetSkill(SkillName.Anatomy,     67.0, 71.0);
            SetSkill(SkillName.Healing,     72.0, 76.0);
            SetSkill(SkillName.Parry,       67.0, 71.0);
            SetSkill(SkillName.Hiding,      27.0, 31.0);
            SetSkill(SkillName.MagicResist, 47.0, 51.0);

            Karma = -1500;
            Fame  = 1500;
            VirtualArmor = 20;

            // Equipment
            AddItem(new Katana());

            LeatherChest chest = new LeatherChest();
            chest.Hue = Utility.RandomNeutralHue();
            AddItem(chest);

            AddItem(new LeatherLegs());
            AddItem(new LeatherArms());
            AddItem(new LeatherGorget());
            AddItem(new WoodenShield());
            AddItem(new Boots(Utility.RandomNeutralHue()));

            if (Utility.RandomBool())
                AddItem(new Cloak(Utility.RandomNeutralHue()));

            // If encounter mode: say something and start hunting
            if (target != null && mode == FightMode.Closest)
            {
                Timer.DelayCall(TimeSpan.FromMilliseconds(500), () =>
                {
                    if (!Deleted && target != null && target.Alive)
                    {
                        Say(PortalSpeech[Utility.Random(PortalSpeech.Length)]);
                        Combatant = target;

                        // Auto-delete after 5 minutes if player escapes
                        Timer.DelayCall(TimeSpan.FromMinutes(5.0), () =>
                        {
                            if (!Deleted && Combatant == null && !Controlled)
                                Delete();
                        });
                    }
                });
            }
        }

        // OnGotMeleeAttack and OnDeath handled by BaseFBCombatNPC via
        // AggroLines / KillLines properties above.

        // -------------------------------------------------------
        // Loot
        // -------------------------------------------------------
        public override void GenerateLoot()
        {
            AddLoot(LootPack.Poor);
            PackGold(50, 150);
            PackItem(new Bandage(Utility.RandomMinMax(5, 15)));
        }

        // -------------------------------------------------------
        // Persistence
        // -------------------------------------------------------
        public NovicePlayerKiller(Serial serial) : base(serial) { } // → BaseFBCombatNPC → BaseCreature

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
        }
    }
}
