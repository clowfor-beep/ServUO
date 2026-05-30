// ============================================================
// GateVisitorMerchant.cs
// Scripts/Custom/GateVisitorMerchant.cs
//
// A travelling NPC merchant who periodically gates to player
// vendors on Felucca and buys plain (non-magic) goods.
//
// Usage: [add GateVisitorMerchant
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Custom
{
    public class GateVisitorMerchant : BaseCreature
    {
        // Tracks when we last visited each vendor (by serial) to enforce cooldown
        private readonly Dictionary<Serial, DateTime> _visitedVendors
            = new Dictionary<Serial, DateTime>();

        private ShoppingTimer _timer;

        // ── Constructor ───────────────────────────────────────────────────────

        [Constructable]
        public GateVisitorMerchant()
            : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
        {
            Name  = "a travelling merchant";
            Title = "the buyer";
            Body  = 0x190;
            Hue   = Utility.RandomSkinHue();

            SetStr(60); SetDex(60); SetInt(80);
            SetHits(80);
            VirtualArmor = 5;
            Fame  = 0;
            Karma = 500;
            CantWalk = false;

            Utility.AssignRandomHair(this);
            AddItem(new Robe(Utility.RandomNeutralHue()));
            AddItem(new Boots());
        }

        public GateVisitorMerchant(Serial serial) : base(serial) { }

        // ── Spawn / load ──────────────────────────────────────────────────────

        public override void OnAfterSpawn()
        {
            base.OnAfterSpawn();
            StartTimer();
        }

        private void StartTimer()
        {
            _timer?.Stop();
            double minutes = Utility.RandomMinMax(3, 8);
            _timer = new ShoppingTimer(this, TimeSpan.FromMinutes(minutes));
            _timer.Start();
        }

        // ── Shopping Timer ────────────────────────────────────────────────────

        private class ShoppingTimer : Timer
        {
            private readonly GateVisitorMerchant _owner;

            public ShoppingTimer(GateVisitorMerchant owner, TimeSpan delay)
                : base(delay)
            {
                _owner   = owner;
                Priority = TimerPriority.OneMinute;
            }

            protected override void OnTick()
            {
                if (_owner == null || _owner.Deleted) return;
                _owner.DoShoppingTrip();
                _owner.StartTimer(); // reschedule
            }
        }

        // ── Shopping logic ────────────────────────────────────────────────────

        private void DoShoppingTrip()
        {
            if (Map == Map.Internal) return;

            PlayerVendor vendor = FindNextVendor();
            if (vendor == null) return;

            // Gate effect at current location
            FixedParticles(0x6F9, 10, 30, 5052, EffectLayer.CenterFeet);
            PlaySound(0x20E);

            Point3D dest = vendor.Location;
            Map     map  = vendor.Map;

            Timer.DelayCall(TimeSpan.FromSeconds(2.0), () =>
            {
                if (Deleted) return;
                MoveToWorld(dest, map);
                FixedParticles(0x6F9, 10, 30, 5052, EffectLayer.CenterFeet);
                PlaySound(0x20E);

                // Record visit to enforce cooldown
                _visitedVendors[vendor.Serial] = DateTime.UtcNow;

                Timer.DelayCall(TimeSpan.FromSeconds(3.0), () =>
                {
                    if (!Deleted) BrowseVendor(vendor);
                });
            });
        }

        private PlayerVendor FindNextVendor()
        {
            var candidates = new List<PlayerVendor>();
            DateTime cooldownCutoff = DateTime.UtcNow - TimeSpan.FromMinutes(30);

            foreach (Mobile m in World.Mobiles.Values)
            {
                if (!(m is PlayerVendor pv)) continue;
                if (pv.Deleted || pv.Map != Map.Felucca) continue;

                DateTime last;
                if (_visitedVendors.TryGetValue(pv.Serial, out last) && last > cooldownCutoff)
                    continue;

                if (!HasQualifyingItems(pv)) continue;
                candidates.Add(pv);
            }

            return candidates.Count > 0
                ? candidates[Utility.Random(candidates.Count)]
                : null;
        }

        private void BrowseVendor(PlayerVendor vendor)
        {
            if (vendor == null || vendor.Deleted) return;

            int budget = Utility.RandomMinMax(20000, 60000);
            int spent  = 0;
            var toBuy  = new List<Tuple<Item, int>>();

            var stockItems = vendor.Backpack != null ? vendor.Backpack.Items : null;
            if (stockItems == null || stockItems.Count == 0)
            {
                Say("Nothing here catches my eye today.");
                return;
            }

            foreach (Item item in stockItems)
            {
                if (spent >= budget) break;
                if (item == null || item.Deleted) continue;

                int price = GetVendorPrice(vendor, item);
                if (price <= 0) continue;

                int cap = GetCategoryCapForItem(item);
                if (cap <= 0 || price > cap) continue;
                if (spent + price > budget) continue;

                toBuy.Add(Tuple.Create(item, price));
                spent += price;
            }

            if (toBuy.Count == 0)
            {
                Say("Nothing here catches my eye today.");
                return;
            }

            foreach (var pair in toBuy)
            {
                Item item  = pair.Item1;
                int  price = pair.Item2;

                if (item == null || item.Deleted) continue;

                vendor.HoldGold += price;
                item.Delete();
            }

            int itemCount = toBuy.Count;
            Say($"I'll take {itemCount} item{(itemCount == 1 ? "" : "s")}. Pleasure doing business.");
        }

        // ── Price and category helpers ─────────────────────────────────────────

        private static int GetVendorPrice(PlayerVendor vendor, Item item)
        {
            VendorItem vi = vendor.GetVendorItem(item);
            return vi != null ? vi.Price : 0;
        }

        private static int GetCategoryCapForItem(Item item)
        {
            // Resources
            if (item is BaseIngot)   return 6;
            if (item is Board || item is Log) return 4;
            if (item is Cloth || item is Leather) return 5;

            // Reagents
            if (item is BaseReagent) return 7;

            // Potions
            if (item is BasePotion bp)
            {
                if (bp is HealPotion || bp is CurePotion ||
                    bp is StrengthPotion || bp is AgilityPotion)
                    return 20;
                return 0;
            }

            // Food
            if (item is Food) return 15;

            // Scrolls — price cap scales with spell circle (approximated from SpellID)
            if (item is SpellScroll ss)
            {
                int circle = ss.SpellID / 8 + 1;
                return Math.Min(400, circle * 50);
            }

            // Plain weapons/armor (no magic properties)
            if (item is BaseWeapon bw && !HasMagicProperties(bw)) return 400;
            if (item is BaseArmor ba  && !HasMagicProperties(ba)) return 400;

            // Tools
            if (item is BaseTool) return 150;

            return 0; // won't buy
        }

        private static bool HasMagicProperties(Item item)
        {
            if (item is BaseWeapon bw)
                return !bw.Attributes.IsEmpty || !bw.WeaponAttributes.IsEmpty;
            if (item is BaseArmor ba)
                return !ba.Attributes.IsEmpty || !ba.ArmorAttributes.IsEmpty;
            return false;
        }

        private static bool HasQualifyingItems(PlayerVendor vendor)
        {
            if (vendor.Backpack == null) return false;

            foreach (Item item in vendor.Backpack.Items)
            {
                if (item == null || item.Deleted) continue;
                int price = GetVendorPrice(vendor, item);
                if (price > 0 && GetCategoryCapForItem(item) >= price)
                    return true;
            }

            return false;
        }

        // ── Serialization ─────────────────────────────────────────────────────

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
            StartTimer();    // restart shopping timer after world load
        }
    }
}
