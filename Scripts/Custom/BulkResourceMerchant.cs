// ============================================================
// BulkResourceMerchant.cs
// Scripts/Custom/BulkResourceMerchant.cs
//
// A travelling bulk-goods merchant who appears at a random town
// bank for 30 minutes before moving on.
//
// - Sells 10 randomly selected crafting resource stacks per visit
// - Each stack is 1,000 / 5,000 / or 10,000 units (randomised)
// - Resource type and amount are re-randomised every visit
// - Accepts gold withdrawn directly from the player's bank account
// - Announces arrival via World.Broadcast
//
// Usage: [add BulkResourceMerchant  (or let system manage it)
//         [respawnbulkmerchant       (GM command to force respawn)
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ── Pool entry ────────────────────────────────────────────────────────────

    public class BulkResourceEntry
    {
        public readonly string          Name;
        public readonly int             GoldPerUnit;
        public readonly Func<int, Item> Create;

        public BulkResourceEntry(string name, int goldPerUnit, Func<int, Item> create)
        {
            Name        = name;
            GoldPerUnit = goldPerUnit;
            Create      = create;
        }
    }

    // ── Live stock slot ───────────────────────────────────────────────────────

    public class BulkResourceSlot
    {
        public readonly string          Name;
        public readonly int             Amount;
        public readonly int             GoldPrice;
        public readonly int             ItemID;
        public readonly int             ItemHue;
        public readonly Func<int, Item> Factory;

        public BulkResourceSlot(string name, int amount, int goldPerUnit,
                                int itemID, int itemHue, Func<int, Item> factory)
        {
            Name      = name;
            Amount    = amount;
            GoldPrice = amount * goldPerUnit;
            ItemID    = itemID;
            ItemHue   = itemHue;
            Factory   = factory;
        }
    }

    // ── Pool definition ───────────────────────────────────────────────────────

    public static class BulkResourcePool
    {
        private static readonly int[] Amounts = { 500, 2500, 5000 };

        public static readonly List<BulkResourceEntry> Pool = new List<BulkResourceEntry>
        {
            // ── Metals / Ingots ──────────────────────────────────────────
            new BulkResourceEntry("Iron Ingots",         3,  amt => new IronIngot(amt)),
            new BulkResourceEntry("Dull Copper Ingots",  6,  amt => new DullCopperIngot(amt)),
            new BulkResourceEntry("Shadow Iron Ingots",  8,  amt => new ShadowIronIngot(amt)),
            new BulkResourceEntry("Copper Ingots",       10, amt => new CopperIngot(amt)),
            new BulkResourceEntry("Bronze Ingots",       12, amt => new BronzeIngot(amt)),
            new BulkResourceEntry("Gold Ingots",         18, amt => new GoldIngot(amt)),
            new BulkResourceEntry("Agapite Ingots",      25, amt => new AgapiteIngot(amt)),
            new BulkResourceEntry("Verite Ingots",       35, amt => new VeriteIngot(amt)),
            new BulkResourceEntry("Valorite Ingots",     55, amt => new ValoriteIngot(amt)),

            // ── Wood / Boards ─────────────────────────────────────────────
            new BulkResourceEntry("Boards",              3,  amt => new Board(amt)),
            new BulkResourceEntry("Oak Boards",          8,  amt => new OakBoard(amt)),
            new BulkResourceEntry("Ash Boards",          10, amt => new AshBoard(amt)),
            new BulkResourceEntry("Yew Boards",          15, amt => new YewBoard(amt)),
            new BulkResourceEntry("Heartwood Boards",    22, amt => new HeartwoodBoard(amt)),
            new BulkResourceEntry("Bloodwood Boards",    28, amt => new BloodwoodBoard(amt)),
            new BulkResourceEntry("Frostwood Boards",    35, amt => new FrostwoodBoard(amt)),

            // ── Leather ───────────────────────────────────────────────────
            new BulkResourceEntry("Leather",             5,  amt => new Leather(amt)),
            new BulkResourceEntry("Spined Leather",      14, amt => new SpinedLeather(amt)),
            new BulkResourceEntry("Horned Leather",      22, amt => new HornedLeather(amt)),
            new BulkResourceEntry("Barbed Leather",      38, amt => new BarbedLeather(amt)),

            // ── Cloth / Fabric ────────────────────────────────────────────
            new BulkResourceEntry("Cloth",               4,  amt => new Cloth(amt)),
            new BulkResourceEntry("Wool",                5,  amt => new Wool(amt)),
            new BulkResourceEntry("Cotton",              5,  amt => new Cotton(amt)),

            // ── Reagents — Magery ─────────────────────────────────────────
            new BulkResourceEntry("Black Pearl",         4,  amt => new BlackPearl(amt)),
            new BulkResourceEntry("Bloodmoss",           3,  amt => new Bloodmoss(amt)),
            new BulkResourceEntry("Garlic",              2,  amt => new Garlic(amt)),
            new BulkResourceEntry("Ginseng",             2,  amt => new Ginseng(amt)),
            new BulkResourceEntry("Mandrake Root",       4,  amt => new MandrakeRoot(amt)),
            new BulkResourceEntry("Nightshade",          3,  amt => new Nightshade(amt)),
            new BulkResourceEntry("Spider's Silk",       3,  amt => new SpidersSilk(amt)),
            new BulkResourceEntry("Sulfurous Ash",       3,  amt => new SulfurousAsh(amt)),

            // ── Reagents — Necromancy ─────────────────────────────────────
            new BulkResourceEntry("Bat Wing",            4,  amt => new BatWing(amt)),
            new BulkResourceEntry("Grave Dust",          4,  amt => new GraveDust(amt)),
            new BulkResourceEntry("Nox Crystal",         5,  amt => new NoxCrystal(amt)),
            new BulkResourceEntry("Pig Iron",            4,  amt => new PigIron(amt)),

            // ── Gems ──────────────────────────────────────────────────────
            new BulkResourceEntry("Amber",               8,  amt => new Amber(amt)),
            new BulkResourceEntry("Amethyst",            8,  amt => new Amethyst(amt)),
            new BulkResourceEntry("Citrine",             8,  amt => new Citrine(amt)),
            new BulkResourceEntry("Diamond",             22, amt => new Diamond(amt)),
            new BulkResourceEntry("Emerald",             14, amt => new Emerald(amt)),
            new BulkResourceEntry("Ruby",                14, amt => new Ruby(amt)),
            new BulkResourceEntry("Sapphire",            12, amt => new Sapphire(amt)),
            new BulkResourceEntry("Star Sapphire",       18, amt => new StarSapphire(amt)),
            new BulkResourceEntry("Tourmaline",          10, amt => new Tourmaline(amt)),
        };

        public static List<BulkResourceSlot> GetRandomStock(int count = 10)
        {
            var pool   = new List<BulkResourceEntry>(Pool);
            var result = new List<BulkResourceSlot>();
            count = Math.Min(count, pool.Count);

            while (result.Count < count)
            {
                int idx   = Utility.Random(pool.Count);
                var entry = pool[idx];
                pool.RemoveAt(idx);

                int amount = Amounts[Utility.Random(Amounts.Length)];

                Item temp = entry.Create(1);
                int  iid  = temp.ItemID;
                int  hue  = temp.Hue;
                temp.Delete();

                result.Add(new BulkResourceSlot(
                    entry.Name, amount, (int)Math.Round(entry.GoldPerUnit * 1.5), iid, hue, entry.Create));
            }

            return result;
        }
    }

    // ── System manager ────────────────────────────────────────────────────────

    public static class BulkResourceMerchantSystem
    {
        private static readonly TimeSpan StayDuration = TimeSpan.FromMinutes(30);
        private static BulkResourceMerchant _current;

        private static readonly (Point3D loc, Map map, string town)[] BankLocations =
        {
            (new Point3D(1439, 1698, 20), Map.Felucca, "Britain"),
            (new Point3D(1859, 2754,  0), Map.Felucca, "Trinsic"),
            (new Point3D(2526,  582,  0), Map.Felucca, "Minoc"),
            (new Point3D(2893,  686,  0), Map.Felucca, "Vesper"),
            (new Point3D( 586, 2150,  0), Map.Felucca, "Skara Brae"),
            (new Point3D( 621, 1000,  0), Map.Felucca, "Yew"),
            (new Point3D(1355, 3834, 20), Map.Felucca, "Jhelom"),
            (new Point3D(4471, 1177,  0), Map.Felucca, "Moonglow"),
            (new Point3D(3714, 2190, 20), Map.Felucca, "Magincia"),
        };

        public static void Initialize()
        {
            CommandSystem.Register("respawnbulkmerchant", AccessLevel.GameMaster,
                e => { SpawnNext(); e.Mobile.SendMessage(0x35, "Bulk resource merchant respawned."); });

            Timer.DelayCall(TimeSpan.FromSeconds(30), SpawnNext);
        }

        public static string GetCurrentTown()
        {
            if (_current == null || _current.Deleted) return null;
            return _current.TownName;
        }

        public static void SpawnNext()
        {
            if (_current != null && !_current.Deleted)
            {
                _current.SayFarewell();
                BulkResourceMerchant captured = _current;
                Timer.DelayCall(TimeSpan.FromSeconds(3), () =>
                {
                    if (!captured.Deleted) captured.Delete();
                });
            }

            var entry = BankLocations[Utility.Random(BankLocations.Length)];
            _current  = new BulkResourceMerchant(entry.town);

            int x = entry.loc.X + Utility.RandomMinMax(-3, 3);
            int y = entry.loc.Y + Utility.RandomMinMax(-3, 3);
            int z = entry.map.GetAverageZ(x, y);

            _current.MoveToWorld(new Point3D(x, y, z), entry.map);
            _current.SayArrival();

            World.Broadcast(0xFFFF, true,
                $"[Trade] A bulk goods merchant has set up stall at the {entry.town} bank. " +
                "Crafting materials in bulk — gold withdrawn from your bank — 30 minutes only.");

            Timer.DelayCall(StayDuration, SpawnNext);
        }
    }

    // ── NPC ───────────────────────────────────────────────────────────────────

    public class BulkResourceMerchant : BaseCreature
    {
        private readonly string _townName;
        public string TownName => _townName;

        private readonly List<BulkResourceSlot> _stock = new List<BulkResourceSlot>();
        public IReadOnlyList<BulkResourceSlot> Stock => _stock;

        [Constructable]
        public BulkResourceMerchant() : this("Unknown Town") { }

        public BulkResourceMerchant(string townName)
            : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
        {
            _townName = townName;

            Name  = "the Bulk Goods Merchant";
            Title = "of the Supply Roads";
            Body  = Utility.RandomBool() ? 0x190 : 0x191;
            Hue   = Utility.RandomSkinHue();

            Utility.AssignRandomHair(this);

            AddItem(new Robe(Utility.RandomNeutralHue()));
            AddItem(new Boots());
            AddItem(new HalfApron(Utility.RandomNeutralHue()));

            SetStr(75); SetDex(75); SetInt(100);
            SetHits(150);
            VirtualArmor = 10;
            Fame  = 0;
            Karma = 1000;
            CantWalk = true;

            GenerateStock();
        }

        public BulkResourceMerchant(Serial serial) : base(serial) { }

        private void GenerateStock()
        {
            _stock.Clear();
            _stock.AddRange(BulkResourcePool.GetRandomStock(10));
        }

        public override bool IsInvulnerable => true;
        public override bool CanBeRenamedBy(Mobile from) => false;

        public void SayArrival()
        {
            Say($"*sets out crates at the {_townName} bank* " +
                "Bulk crafting supplies — gold drawn from your bank account!");
        }

        public void SayFarewell()
        {
            Say("*loads up the cart* That's all for today. Safe travels!");
            Effects.SendLocationParticles(
                EffectItem.Create(Location, Map, EffectItem.DefaultDuration),
                0x376A, 9, 32, 5023);
            PlaySound(0x1FE);
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!(from is PlayerMobile pm)) return;
            if (!pm.InRange(Location, 5))
            {
                pm.SendMessage("You are too far away.");
                return;
            }

            Say("*nods* Have a look at today's stock.");
            pm.CloseGump(typeof(BulkResourceGump));
            pm.SendGump(new BulkResourceGump(pm, this));
        }

        public Item PurchaseSlot(int index)
        {
            if (index < 0 || index >= _stock.Count) return null;
            var slot = _stock[index];
            _stock.RemoveAt(index);
            return slot.Factory(slot.Amount);
        }

        public override void OnDelete()
        {
            _stock.Clear();
            base.OnDelete();
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
            Timer.DelayCall(TimeSpan.Zero, Delete);
        }
    }

    // ── Shop gump ─────────────────────────────────────────────────────────────

    public class BulkResourceGump : Gump
    {
        private readonly PlayerMobile         _player;
        private readonly BulkResourceMerchant _npc;

        private const int GW        = 620;
        private const int ColW      = 295;
        private const int RowH      = 80;
        private const int GridLeft  = 8;
        private const int GridRight = 314;
        private const int GridTop   = 76;
        private const int HeaderH   = 66;

        private const int BtnClose   = 0;
        private const int BtnBuyBase = 100;

        public BulkResourceGump(PlayerMobile player, BulkResourceMerchant npc)
            : base(50, 30)
        {
            _player = player;
            _npc    = npc;

            var stock   = npc.Stock;
            int rows    = (int)Math.Ceiling(stock.Count / 2.0);
            int GH      = GridTop + rows * RowH + 44;
            int balance = Banker.GetBalance(player); // bank account balance

            AddBackground(0, 0, GW, GH, 9200);
            AddAlphaRegion(4, 4, GW - 8, GH - 8);

            // Header band
            AddImageTiled(4, 4, GW - 8, HeaderH, 9304);
            AddAlphaRegion(4, 4, GW - 8, HeaderH);

            AddLabel(GW / 2 - 130, 10, 0x9C2, "~ Bulk Goods Merchant ~");
            AddLabel(GW / 2 - 130, 28, 1152,  "Crafting supplies — gold drawn from bank");
            AddLabel(GW / 2 - 85,  46, 0x848, "Stock refreshes each visit");

            // Bank balance (top right)
            AddItem(GW - 135, 8,  0xEED, 0);
            AddLabel(GW - 115, 10, balance > 0 ? 0x35 : 33, $"Bank: {balance:N0}g");

            // Separator
            AddImageTiled(4, HeaderH + 2, GW - 8, 2, 9264);

            // Item grid — 2 columns
            for (int i = 0; i < stock.Count; i++)
            {
                var slot = stock[i];
                int col  = (i % 2 == 0) ? GridLeft : GridRight;
                int row  = GridTop + (i / 2) * RowH;

                bool canAfford = balance >= slot.GoldPrice;

                // Cell background
                int cellBg = (i / 2) % 2 == 0 ? 9274 : 9200;
                AddImageTiled(col, row, ColW, RowH - 3, cellBg);
                AddAlphaRegion(col, row, ColW, RowH - 3);

                // Left accent bar
                AddImageTiled(col, row, 3, RowH - 3, canAfford ? 0x9C2 : 33);

                // Item icon
                AddItem(col + 8, row + 8, slot.ItemID, slot.ItemHue);

                // Resource name
                string dispName = slot.Name.Length > 24
                    ? slot.Name.Substring(0, 23) + "…"
                    : slot.Name;
                AddLabel(col + 55, row + 8, canAfford ? 1152 : 0x848, dispName);

                // Amount — colour-coded by size
                int amtHue = slot.Amount == 10000 ? 0x21 :
                             slot.Amount == 5000  ? 0x4F : 0x9C2;
                AddLabel(col + 55, row + 26, amtHue, $"x {slot.Amount:N0}");

                // Gold price
                AddLabel(col + 55, row + 44, canAfford ? 0x35 : 33,
                    $"{slot.GoldPrice:N0} gold");

                // Buy button or can't afford label
                if (canAfford)
                {
                    AddButton(col + 190, row + 26, 4005, 4007,
                        BtnBuyBase + i, GumpButtonType.Reply, 0);
                    AddLabel(col + 225, row + 28, 0x35, "Buy");
                }
                else
                {
                    AddLabel(col + 185, row + 28, 33, "Can't afford");
                }

                // Column divider
                if (i % 2 == 0)
                    AddImageTiled(GridRight - 2, row, 2, RowH - 3, 9264);
            }

            // Footer
            int footY = GH - 34;
            AddImageTiled(4, footY - 6, GW - 8, 2, 9264);
            AddButton(GW / 2 - 40, footY, 4017, 4019, BtnClose, GumpButtonType.Reply, 0);
            AddLabel(GW / 2 - 5, footY + 2, 0x848, "Close");
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_player == null || _player.Deleted) return;
            if (_npc    == null || _npc.Deleted)    return;
            if (info.ButtonID == BtnClose)           return;

            if (!_player.InRange(_npc.Location, 6))
            {
                _player.SendMessage("You have moved too far away.");
                return;
            }

            int idx = info.ButtonID - BtnBuyBase;
            if (idx < 0 || idx >= _npc.Stock.Count) return;

            _player.CloseGump(typeof(BulkResourceConfirmGump));
            _player.SendGump(new BulkResourceConfirmGump(_player, _npc, idx));
        }
    }

    // ── Confirmation gump ─────────────────────────────────────────────────────

    public class BulkResourceConfirmGump : Gump
    {
        private readonly PlayerMobile         _player;
        private readonly BulkResourceMerchant _npc;
        private readonly int                  _slotIndex;

        private const int BtnConfirm = 1;
        private const int BtnCancel  = 2;

        public BulkResourceConfirmGump(PlayerMobile player, BulkResourceMerchant npc, int slotIndex)
            : base(200, 200)
        {
            _player    = player;
            _npc       = npc;
            _slotIndex = slotIndex;

            var slot    = npc.Stock[slotIndex];
            int balance = Banker.GetBalance(player);
            int remaining = balance - slot.GoldPrice;

            AddBackground(0, 0, 340, 210, 9200);
            AddAlphaRegion(5, 5, 330, 200);
            AddImageTiled(5, 5, 330, 44, 9304);

            AddLabel(170 - 70, 14, 0x9C2, "Confirm Purchase");

            AddItem(20, 58, slot.ItemID, slot.ItemHue);

            int amtHue = slot.Amount == 10000 ? 0x21 :
                         slot.Amount == 5000  ? 0x4F : 0x9C2;
            AddLabel(75, 56, 1152,   slot.Name);
            AddLabel(75, 74, amtHue, $"Amount:      {slot.Amount:N0} units");
            AddLabel(75, 92, 0x35,   $"Cost:        {slot.GoldPrice:N0} gold");
            AddLabel(75, 110, 0x848, $"Bank before: {balance:N0} gold");
            AddLabel(75, 128, remaining >= 0 ? 0x35 : 33,
                $"Bank after:  {remaining:N0} gold");

            AddImageTiled(10, 148, 320, 1, 9264);
            AddLabel(25, 158, 1152, "Gold will be withdrawn from your bank.");

            AddButton(60,  182, 4005, 4007, BtnConfirm, GumpButtonType.Reply, 0);
            AddLabel(95,  184, 0x35, "Confirm");
            AddButton(200, 182, 4017, 4019, BtnCancel, GumpButtonType.Reply, 0);
            AddLabel(235, 184, 33, "Cancel");
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_player == null || _player.Deleted) return;
            if (_npc    == null || _npc.Deleted)    return;

            if (info.ButtonID == BtnCancel)
            {
                _player.SendGump(new BulkResourceGump(_player, _npc));
                return;
            }

            if (info.ButtonID != BtnConfirm) return;

            if (!_player.InRange(_npc.Location, 6))
            {
                _player.SendMessage("You have moved too far away.");
                return;
            }

            if (_slotIndex >= _npc.Stock.Count)
            {
                _player.SendMessage("That stock is no longer available.");
                _player.SendGump(new BulkResourceGump(_player, _npc));
                return;
            }

            var slot    = _npc.Stock[_slotIndex];
            int balance = Banker.GetBalance(_player);

            if (balance < slot.GoldPrice)
            {
                _player.SendMessage(0x22, "You no longer have enough gold in your bank.");
                _player.SendGump(new BulkResourceGump(_player, _npc));
                return;
            }

            // Withdraw from bank and deliver items
            if (!Banker.Withdraw(_player, slot.GoldPrice))
            {
                _player.SendMessage(0x22, "The bank was unable to process the withdrawal.");
                _player.SendGump(new BulkResourceGump(_player, _npc));
                return;
            }

            string name   = slot.Name;
            int    amount = slot.Amount;

            Item purchased = _npc.PurchaseSlot(_slotIndex);
            if (purchased != null)
                _player.AddToBackpack(purchased);

            _npc.Say($"Pleasure doing business. Enjoy your {name.ToLower()}.");
            _player.SendMessage(0x35,
                $"You purchased {amount:N0} {name} for {slot.GoldPrice:N0} gold (withdrawn from bank).");

            Effects.SendLocationParticles(
                EffectItem.Create(_player.Location, _player.Map, EffectItem.DefaultDuration),
                0x376A, 9, 20, 5023);
            _player.PlaySound(0x2E6);

            _player.SendGump(new BulkResourceGump(_player, _npc));
        }
    }
}
