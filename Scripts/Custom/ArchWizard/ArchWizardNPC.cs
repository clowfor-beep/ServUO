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

            // Wizard cast animation + speech
            Animate(203, 7, 1, true, false, 0);
            Say("*weaves a rift in the fabric of space*");

            // Create the portal at the player's feet after a short cast delay
            PlayerMobile   capturedPm   = pm;
            Point3D        capturedDest = destination;
            Map            capturedMap  = map;

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (capturedPm == null || capturedPm.Deleted) return;

                var portal = new ArchWizardPortal(capturedPm, capturedDest, capturedMap);
                portal.MoveToWorld(capturedPm.Location, capturedPm.Map);

                capturedPm.SendMessage(0x35, "A shimmering portal opens at your feet — step through it!");
            });
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

    // ================================================================
    // ArchWizardPortal
    // A temporary personal moongate placed at the player's feet.
    // Only the target player can use it. Auto-deletes after 30 seconds.
    // ================================================================
    public class ArchWizardPortal : Item
    {
        private PlayerMobile _target;
        private Point3D      _destination;
        private Map          _destMap;
        private bool         _used;

        // Not persisted — portals vanish on server restart
        public ArchWizardPortal(PlayerMobile target, Point3D destination, Map destMap)
            : base(0xF6C)   // moongate graphic
        {
            _target      = target;
            _destination = destination;
            _destMap     = destMap;
            _used        = false;

            Movable  = false;
            Hue      = 1153;   // light blue — distinct from regular moongates
            Name     = "a wizard's portal";
            Light    = LightType.Circle300;

            // Opening effect
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            Effects.PlaySound(Location, Map, 0x20E);

            // Auto-delete after 30 seconds if unused
            Timer.DelayCall(TimeSpan.FromSeconds(30.0), () =>
            {
                if (!Deleted && !_used)
                {
                    Effects.SendLocationParticles(
                        EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                        0x3728, 8, 20, 5042);
                    Effects.PlaySound(Location, Map, 0x1FE);
                    Delete();
                }
            });
        }

        public ArchWizardPortal(Serial serial) : base(serial) { }

        // Triggered when any mobile moves onto the same tile
        public override bool OnMoveOver(Mobile m)
        {
            UsePortal(m);
            return true;
        }

        // Also triggered on double-click for accessibility
        public override void OnDoubleClick(Mobile from)
        {
            UsePortal(from);
        }

        private void UsePortal(Mobile m)
        {
            if (_used || Deleted) return;
            if (!(m is PlayerMobile pm)) return;
            if (pm != _target)
            {
                pm.SendMessage("This portal was not opened for you.");
                return;
            }

            _used = true;

            // Through-the-gate effects at origin
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            Effects.PlaySound(Location, Map, 0x1FE);

            // Move player
            pm.MoveToWorld(_destination, _destMap);
            pm.SendMessage(0x35, "The portal whisks you across the world...");

            // Arrival effect at destination
            Effects.SendLocationParticles(
                EffectItem.Create(_destination, _destMap, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            Effects.PlaySound(_destination, _destMap, 0x20E);

            Delete();
        }

        // Portals are transient — no fields to save
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
            // Portal expired — delete on load
            Timer.DelayCall(TimeSpan.Zero, Delete);
        }
    }
}
