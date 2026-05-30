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

            if (!DeductGold(pm, cost))
            {
                pm.SendMessage("You do not have enough gold. ({0} gold required — pack and bank are both accepted.)", cost);
                return;
            }

            pm.MoveToWorld(destination, map);
            pm.SendMessage(0x35, "The Arch Wizard gestures and the world blurs around you...");
            Effects.SendLocationParticles(EffectItem.Create(pm.Location, map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);
            pm.PlaySound(0x1FE);
        }

        // ── Gold helpers — check/deduct from pack first, then bank ────────

        /// <summary>Returns true if the player can cover <paramref name="amount"/> gold
        /// between their backpack and bank combined.</summary>
        public static bool HasGold(PlayerMobile pm, int amount)
        {
            int packGold = pm.Backpack != null ? pm.Backpack.GetAmount(typeof(Gold)) : 0;
            if (packGold >= amount) return true;

            BankBox bank = pm.FindBankNoCreate();
            int bankGold = bank != null ? bank.GetAmount(typeof(Gold)) : 0;
            return packGold + bankGold >= amount;
        }

        /// <summary>Deducts <paramref name="amount"/> gold from the player, drawing
        /// from the backpack first and the bank for any remainder.
        /// Returns false (and takes nothing) if total funds are insufficient.</summary>
        public static bool DeductGold(PlayerMobile pm, int amount)
        {
            int packGold = pm.Backpack != null ? pm.Backpack.GetAmount(typeof(Gold)) : 0;
            BankBox bank = pm.FindBankNoCreate();
            int bankGold = bank != null ? bank.GetAmount(typeof(Gold)) : 0;

            if (packGold + bankGold < amount)
                return false;

            // Drain pack first
            int fromPack = Math.Min(packGold, amount);
            if (fromPack > 0)
            {
                pm.Backpack.ConsumeTotal(typeof(Gold), fromPack);
                pm.SendMessage("You pay {0} gold from your pack.", fromPack);
            }

            // Draw the rest from bank
            int fromBank = amount - fromPack;
            if (fromBank > 0)
            {
                bank.ConsumeTotal(typeof(Gold), fromBank);
                pm.SendMessage("Your bank balance has been reduced by {0} gold.", fromBank);
            }

            return true;
        }

        // Legacy wrapper — kept so any other callers still compile
        public static bool HasBankGold(PlayerMobile pm, int amount)  => HasGold(pm, amount);
        public static bool DeductBankGold(PlayerMobile pm, int amount) => DeductGold(pm, amount);

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
