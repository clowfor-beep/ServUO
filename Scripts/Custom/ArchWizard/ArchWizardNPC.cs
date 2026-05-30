using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Custom.ArchWizard;

namespace Server.Mobiles
{
    public class ArchWizardNPC : BaseCreature
    {
        // ============================================================
        // CONFIG — tune these to change prices
        // ============================================================

        public static readonly int CostDungeon   = 500;   // gold per dungeon level teleport
        public static readonly int CostChampion  = 2000;  // gold per active champion spawn teleport

        // ============================================================

        [Constructable]
        public ArchWizardNPC()
            : base(AIType.AI_Mage, FightMode.None, 10, 1, 0.2, 0.4)
        {
            Name  = "the Arch Wizard";
            Title = "of the Far Realm";
            Body  = 0x190;
            Hue   = Utility.RandomSkinHue();

            AddItem(new Robe(1150));
            AddItem(new WizardsHat(1150));
            AddItem(new Sandals(1150));
            AddItem(new Spellbook());

            SetStr(100);
            SetDex(75);
            SetInt(500);

            SetHits(200);
            SetMana(1000);

            SetSkill(SkillName.Magery,     120.0);
            SetSkill(SkillName.EvalInt,    120.0);
            SetSkill(SkillName.MagicResist, 80.0);
            SetSkill(SkillName.Meditation,  80.0);

            Fame  = 0;
            Karma = 0;

            VirtualArmor = 40;
            CantWalk     = true;   // stays in place
        }

        public ArchWizardNPC(Serial serial) : base(serial) { }

        public override bool IsInvulnerable => true;
        public override bool CanBeRenamedBy(Mobile from) => false;

        public override void OnDoubleClick(Mobile from)
        {
            if (!(from is PlayerMobile pm))
                return;

            if (!pm.InRange(Location, 5))
            {
                pm.SendMessage("You are too far away.");
                return;
            }

            pm.SendGump(new ArchWizardGump(pm, this));
        }

        // Called by the gump when the player confirms a destination
        public void TeleportPlayer(PlayerMobile pm, Point3D destination, Map map, int cost)
        {
            if (!pm.InRange(Location, 10))
            {
                pm.SendMessage("You have moved too far away.");
                return;
            }

            if (!DeductBankGold(pm, cost))
            {
                pm.SendMessage("You do not have enough gold in your bank. ({0} gold required.)", cost);
                return;
            }

            pm.MoveToWorld(destination, map);
            pm.SendMessage(0x35, "The Arch Wizard gestures and the world blurs around you...");
            Effects.SendLocationParticles(EffectItem.Create(pm.Location, map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);
            pm.PlaySound(0x1FE);
        }

        // ── Bank gold helper ──────────────────────────────────────

        public static bool HasBankGold(PlayerMobile pm, int amount)
        {
            BankBox bank = pm.FindBankNoCreate();
            if (bank == null) return false;
            return bank.GetAmount(typeof(Gold)) >= amount;
        }

        public static bool DeductBankGold(PlayerMobile pm, int amount)
        {
            BankBox bank = pm.FindBankNoCreate();
            if (bank == null) return false;
            if (bank.GetAmount(typeof(Gold)) < amount) return false;
            bank.ConsumeTotal(typeof(Gold), amount);
            pm.SendMessage("Your bank balance has been reduced by {0} gold.", amount);
            return true;
        }

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
