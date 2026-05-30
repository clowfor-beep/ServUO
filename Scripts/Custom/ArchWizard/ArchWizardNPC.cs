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
        public void TeleportPlayer(PlayerMobile pm, Point3D destination, Map map, int cost,
                                   PortalType portalType = PortalType.OneWay)
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

            PlayerMobile capturedPm   = pm;
            Point3D      capturedDest = destination;
            Map          capturedMap  = map;

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (capturedPm == null || capturedPm.Deleted) return;

                switch (portalType)
                {
                    case PortalType.TwoWayShort:
                        ArchWizardPortal.CreatePair(capturedPm, capturedDest, capturedMap,
                            TimeSpan.FromSeconds(30));
                        break;

                    case PortalType.TwoWayLong:
                        ArchWizardPortal.CreatePair(capturedPm, capturedDest, capturedMap,
                            TimeSpan.FromMinutes(10));
                        break;

                    default: // OneWay — instant teleport, no portal object
                        capturedPm.MoveToWorld(capturedDest, capturedMap);
                        capturedPm.SendMessage(0x35, "The Arch Wizard whisks you to your destination...");
                        Effects.SendLocationParticles(
                            EffectItem.Create(capturedPm.Location, capturedMap, EffectItem.DefaultDuration),
                            0x3728, 10, 10, 5023);
                        capturedPm.PlaySound(0x1FE);
                        break;
                }
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
    // A two-way personal moongate pair. One portal appears at the
    // player's starting location, a second at the destination.
    // Both ends stay open for 30 minutes and can be used repeatedly.
    // Only the paying player can step through either end.
    // When one portal closes it closes its partner too.
    //
    // Usage: call ArchWizardPortal.CreatePair(pm, dest, destMap)
    //        — do not use the constructor directly.
    // ================================================================
    public class ArchWizardPortal : Item
    {
        private PlayerMobile     _target;
        private Point3D          _destination;
        private Map              _destMap;
        private ArchWizardPortal _partner;   // the portal at the other end

        /// <summary>
        /// Creates a linked portal pair: one at <paramref name="pm"/>'s current
        /// location, one at <paramref name="destination"/>. Both share the same
        /// lifetime specified by <paramref name="duration"/>.
        /// </summary>
        public static void CreatePair(PlayerMobile pm, Point3D destination, Map destMap, TimeSpan duration)
        {
            // Portal A — at the player's current location, leads to destination
            var portalA = new ArchWizardPortal(pm, destination,    destMap);
            portalA.MoveToWorld(pm.Location, pm.Map);

            // Portal B — at the destination, leads back to the player's origin
            var portalB = new ArchWizardPortal(pm, pm.Location, pm.Map);
            portalB.MoveToWorld(destination, destMap);

            // Link them
            portalA._partner = portalB;
            portalB._partner = portalA;

            // Single shared expiry timer — closes both ends
            Timer.DelayCall(duration, () =>
            {
                portalA.ClosePortal();
                portalB.ClosePortal();
            });

            int seconds = (int)duration.TotalSeconds;
            string timeLabel = seconds >= 60
                ? (seconds / 60) + " minute" + (seconds / 60 != 1 ? "s" : "")
                : seconds + " second" + (seconds != 1 ? "s" : "");

            pm.SendMessage(0x35,
                "Two shimmering portals open — one here, one at your destination. " +
                "They will remain open for " + timeLabel + ".");
        }

        // Private — use CreatePair
        private ArchWizardPortal(PlayerMobile target, Point3D destination, Map destMap)
            : base(0xF6C)   // moongate graphic
        {
            _target      = target;
            _destination = destination;
            _destMap     = destMap;

            Movable  = false;
            Hue      = 1153;   // light blue
            Name     = "a wizard's portal";
            Light    = LightType.Circle300;
        }

        // Called after MoveToWorld so Location/Map are set
        public override void OnAfterMove(Point3D oldLoc)
        {
            // Opening effect when first placed
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            Effects.PlaySound(Location, Map, 0x20E);
        }

        public ArchWizardPortal(Serial serial) : base(serial) { }

        // Triggered when any mobile walks onto the tile
        public override bool OnMoveOver(Mobile m)
        {
            UsePortal(m);
            return true;
        }

        // Also triggered on double-click
        public override void OnDoubleClick(Mobile from)
        {
            UsePortal(from);
        }

        private void UsePortal(Mobile m)
        {
            if (Deleted) return;
            if (!(m is PlayerMobile pm)) return;

            if (pm != _target)
            {
                pm.SendMessage("This portal was not opened for you.");
                return;
            }

            // Departure effects
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            Effects.PlaySound(Location, Map, 0x1FE);

            // Move player
            pm.MoveToWorld(_destination, _destMap);
            pm.SendMessage(0x35, "The portal whisks you across the world...");

            // Arrival effects at the other end
            Effects.SendLocationParticles(
                EffectItem.Create(_destination, _destMap, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            Effects.PlaySound(_destination, _destMap, 0x20E);

            // Portals stay open — player can walk back through the partner portal
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

        // Portals are transient — delete on server restart
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
