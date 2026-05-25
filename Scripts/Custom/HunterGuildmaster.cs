// ============================================================
// HunterGuildmaster.cs
// Scripts/Custom/HunterGuildmaster.cs
//
// HunterGuildmaster NPC — handles head turn-ins, Hunter Rank
// display, and the Hunter Token shop.
//
// Place via: [add HunterGuildmaster
// Recommended locations: Britain West Bank, Trinsic Docks, Minoc Tavern
//
// Design doc: Design/HunterSystemDesignDoc.txt
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Gumps;

namespace Server.Custom
{
    // ============================================================
    // GUILDMASTER NPC
    // ============================================================

    public partial class HunterGuildmaster : BaseCreature
    {
        // ── Hunt announcement state ───────────────────────────────────────
        private DateTime _nextAnnounce = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        private static readonly string[] _huntPrefixes = new[]
        {
            "Hear ye! A bounty target has been spotted",
            "Attention hunters! A marked criminal is lurking",
            "The Guild has placed a bounty on a target",
            "A dangerous fugitive has been tracked",
            "A Hunter target is on the loose",
        };

        private static readonly string[] _wantedPrefixes = new[]
        {
            "Wanted! A notorious outlaw is hiding",
            "The Guild seeks a wanted criminal",
            "A dangerous wanted criminal has been sighted",
            "Bring them in! A wanted fugitive is near",
            "A price is on their head — last seen",
        };

        [Constructable]
        public HunterGuildmaster() : base(AIType.AI_Vendor, FightMode.None, 2, 1, 0.5, 2.0)
        {
            Name  = "the Hunter's Guildmaster";
            Title = string.Empty;
            Body  = Utility.RandomBool() ? 0x190 : 0x191;
            Hue   = Utility.RandomSkinHue();

            HairItemID = Body == 0x191
                ? Utility.RandomList(0x203B, 0x2045, 0x204A)
                : Utility.RandomList(0x2044, 0x2045, 0x204A);
            HairHue = Utility.RandomHairHue();

            AddItem(new FancyShirt(0x4AA));
            AddItem(new LongPants(0x497));
            AddItem(new Boots());

            CantWalk   = true;
            Direction  = Direction.South;
        }

        public HunterGuildmaster(Serial serial) : base(serial) { }

        public override bool IsInvulnerable => true;
        public override bool ShowFameTitle  => false;

        public override void OnThink()
        {
            base.OnThink();

            if (DateTime.UtcNow < _nextAnnounce)
                return;

            _nextAnnounce = DateTime.UtcNow + TimeSpan.FromSeconds(30);

            // Announce one active hunt, alternating between hunts and wanted
            var hunts  = HunterSystem.GetActiveHuntInfoList();
            var wanted = HunterSystem.GetActiveWantedInfoList();

            if (hunts.Count == 0 && wanted.Count == 0)
                return;

            // Pick randomly from whichever lists are populated
            bool useHunt = hunts.Count > 0 && (wanted.Count == 0 || Utility.RandomBool());

            if (useHunt)
            {
                string info   = hunts[Utility.Random(hunts.Count)];
                string prefix = _huntPrefixes[Utility.Random(_huntPrefixes.Length)];
                PublicOverheadMessage(MessageType.Regular, 0x44, false,
                    $"{prefix}: {info}! Double-click me to claim the bounty.");
            }
            else
            {
                string info   = wanted[Utility.Random(wanted.Count)];
                string prefix = _wantedPrefixes[Utility.Random(_wantedPrefixes.Length)];
                PublicOverheadMessage(MessageType.Regular, 0x44, false,
                    $"{prefix}: {info}! Double-click me for details.");
            }
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from.InRange(Location, 4))
                from.SendGump(new HunterGuildGump(from, this));
            else
                from.SendMessage("You are too far away.");
        }

        public override bool OnDragDrop(Mobile from, Item dropped)
        {
            if (dropped is HunterHead head)
            {
                ProcessHeadTurnIn(from, head);
                return false; // we handle it
            }
            return base.OnDragDrop(from, dropped);
        }

        public override void OnSpeech(SpeechEventArgs e)
        {
            base.OnSpeech(e);
            string said = e.Speech.ToLower();
            if ((said.Contains("bounty") || said.Contains("hunter") || said.Contains("turn in"))
                && e.Mobile.InRange(Location, 6))
            {
                e.Mobile.SendGump(new HunterGuildGump(e.Mobile, this));
            }
        }

        public static void ProcessHeadTurnIn(Mobile from, HunterHead head)
        {
            if (head == null || head.Deleted) return;

            int gold   = TurnInGold(head.HunterTier);
            int points = TurnInPoints(head.HunterTier);

            from.AddToBackpack(new Gold(gold));
            HunterSystem.AddPoints(from, points);
            HunterSystem.CheckRankUp(from);

            string tierLabel = head.TierLabel();
            from.SendMessage(0x35, $"The Guildmaster rewards you {gold:N0} gold and {points} Hunter Point{(points > 1 ? "s" : "")} for the head of {head.CreatureName}.");
            from.PlaySound(0x2E6);

            head.Delete();
        }

        private static int TurnInGold(int tier)
        {
            switch (tier)
            {
                case 1:  return 900;
                case 2:  return 2400;
                case 3:  return 6000;
                case 4:  return 15000;
                case 10: return 1500;
                case 11: return 4500;
                case 12: return 12000;
                default: return 500;
            }
        }

        private static int TurnInPoints(int tier)
        {
            switch (tier)
            {
                case 1:  return 1;
                case 2:  return 2;
                case 3:  return 4;
                case 4:  return 8;
                case 10: return 1;
                case 11: return 3;
                case 12: return 6;
                default: return 1;
            }
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

    // ============================================================
    // MAIN GUILD GUMP — menu hub
    // ============================================================

    public class HunterGuildGump : Gump
    {
        private readonly Mobile             _from;
        private readonly HunterGuildmaster  _npc;

        public HunterGuildGump(Mobile from, HunterGuildmaster npc) : base(50, 50)
        {
            _from = from;
            _npc  = npc;

            int pts   = HunterSystem.GetPoints(from);
            string rank = HunterSystem.GetRankTitle(pts);

            var huntInfos   = HunterSystem.GetActiveHuntInfoList();
            var wantedInfos = HunterSystem.GetActiveWantedInfoList();

            // Each entry takes 20px; section header adds 20px; blank gap 10px
            int huntBlock   = huntInfos.Count   > 0 ? 20 + huntInfos.Count   * 20 + 10 : 0;
            int wantedBlock = wantedInfos.Count > 0 ? 20 + wantedInfos.Count * 20 + 10 : 0;
            int extraHeight = huntBlock + wantedBlock;

            AddPage(0);
            AddBackground(0, 0, 360, 320 + extraHeight, 9270);
            AddLabel(20, 15, 0x4AA, "Hunter's Guild");
            AddLabel(20, 35, 1153, $"Your Rank: {(rank.Length > 0 ? rank : "(Unranked)")}");
            AddLabel(20, 55, 1153, $"Hunter Points: {pts}");

            AddLabel(20, 90, 1, "What would you like to do?");

            // Button 1 — Turn in heads
            AddButton(20, 120, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddLabel(55, 120, 1, "Turn in Hunter Heads");

            // Button 2 — Token shop
            AddButton(20, 150, 4005, 4007, 2, GumpButtonType.Reply, 0);
            AddLabel(55, 150, 1, "Token Shop");

            // Button 3 — Hunter ranks
            AddButton(20, 180, 4005, 4007, 3, GumpButtonType.Reply, 0);
            AddLabel(55, 180, 1, "View Rank Table");

            // Button 4 — Toggle title
            bool hasTitle = rank.Length > 0;
            AddButton(20, 210, 4005, 4007, 4, GumpButtonType.Reply, 0);
            AddLabel(55, 210, hasTitle ? 0x35 : 0x22, hasTitle ? "Toggle Hunter Title" : "No title earned yet");

            // Active hunts
            int yInfo = 250;
            if (huntInfos.Count > 0)
            {
                AddLabel(20, yInfo, 0x4AA, $"Active Hunt{(huntInfos.Count > 1 ? "s" : "")} ({huntInfos.Count}):");
                yInfo += 20;
                foreach (string info in huntInfos)
                {
                    string line = info.Length > 45 ? info.Substring(0, 45) + "..." : info;
                    AddLabel(30, yInfo, 1153, line);
                    yInfo += 20;
                }
                yInfo += 10;
            }
            if (wantedInfos.Count > 0)
            {
                AddLabel(20, yInfo, 0x22, $"Wanted ({wantedInfos.Count}):");
                yInfo += 20;
                foreach (string info in wantedInfos)
                {
                    string line = info.Length > 45 ? info.Substring(0, 45) + "..." : info;
                    AddLabel(30, yInfo, 1153, line);
                    yInfo += 20;
                }
            }
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_npc.Deleted) return;

            switch (info.ButtonID)
            {
                case 1: _from.SendGump(new HunterTurnInGump(_from, _npc)); break;
                case 2: _from.SendGump(new HunterShopGump(_from, _npc));   break;
                case 3: _from.SendGump(new HunterRankGump(_from, _npc));   break;
                case 4: ToggleTitle(); break;
            }
        }

        private void ToggleTitle()
        {
            string rankTitle = HunterSystem.GetRankTitle(HunterSystem.GetPoints(_from));
            if (rankTitle.Length == 0)
            {
                _from.SendMessage(0x22, "You have not yet earned a Hunter title.");
                return;
            }

            if (_from.Title == rankTitle)
            {
                _from.Title = string.Empty;
                _from.SendMessage(0x22, "Your Hunter title has been removed.");
            }
            else
            {
                _from.Title = rankTitle;
                _from.SendMessage(0x35, $"Your title is now: {rankTitle}");
            }
        }
    }

    // ============================================================
    // TURN-IN GUMP — lists HunterHeads in backpack
    // ============================================================

    public class HunterTurnInGump : Gump
    {
        private readonly Mobile             _from;
        private readonly HunterGuildmaster  _npc;
        private readonly List<HunterHead>   _heads;

        public HunterTurnInGump(Mobile from, HunterGuildmaster npc) : base(50, 50)
        {
            _from  = from;
            _npc   = npc;
            _heads = new List<HunterHead>();

            if (from.Backpack != null)
                foreach (Item item in from.Backpack.Items)
                    if (item is HunterHead h)
                        _heads.Add(h);

            AddPage(0);
            AddBackground(0, 0, 380, 60 + Math.Max(1, _heads.Count) * 30 + 40, 9270);
            AddLabel(20, 15, 0x4AA, "Turn In Hunter Heads");

            if (_heads.Count == 0)
            {
                AddLabel(20, 50, 0x22, "You have no hunter heads to turn in.");
                return;
            }

            for (int i = 0; i < _heads.Count; i++)
            {
                HunterHead head = _heads[i];
                int y = 50 + i * 30;
                int gold = HunterGuildmaster.ProcessHeadRewardPreview(head.HunterTier);

                AddButton(20, y, 4005, 4007, i + 1, GumpButtonType.Reply, 0);
                AddLabel(55, y, 1,
                    $"{head.CreatureName} [{head.TierLabel()}] — {gold:N0}gp");
            }
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_npc.Deleted || info.ButtonID == 0) return;

            int idx = info.ButtonID - 1;
            if (idx >= 0 && idx < _heads.Count)
                HunterGuildmaster.ProcessHeadTurnIn(_from, _heads[idx]);
        }
    }

    // ============================================================
    // SHOP GUMP — Hunter Token purchases
    // ============================================================

    public class HunterShopGump : Gump
    {
        private readonly Mobile             _from;
        private readonly HunterGuildmaster  _npc;

        private static readonly (string name, int cost, System.Type itemType)[] ShopItems = {
            ("Hunter's Tabard (cosmetic robe)",       10, typeof(Robe)),
            ("Hunter's Map (highlights spawn zones)",  5, typeof(BlankScroll)),
            ("Hunter's Compass (find active hunts)",   8, typeof(HunterCompass)),
            ("Tracking Orb (+3 tracking range)",      20, typeof(EssenceShard)),
            ("Title Deed: 'the Monster Hunter'",      30, typeof(Gold)),
        };

        public HunterShopGump(Mobile from, HunterGuildmaster npc) : base(50, 50)
        {
            _from = from;
            _npc  = npc;

            int tokens = CountTokens(from);

            AddPage(0);
            AddBackground(0, 0, 360, 250, 9270);
            AddLabel(20, 15, 0x4AA, "Hunter Token Shop");
            AddLabel(20, 35, 1153, $"Your tokens: {tokens}");

            for (int i = 0; i < ShopItems.Length; i++)
            {
                int y = 70 + i * 30;
                bool canAfford = tokens >= ShopItems[i].cost;
                AddButton(20, y, 4005, 4007, i + 1, GumpButtonType.Reply, 0);
                AddLabel(55, y, canAfford ? 1 : 0x22,
                    $"{ShopItems[i].name} — {ShopItems[i].cost} tokens");
            }
        }

        private static int CountTokens(Mobile from)
        {
            if (from.Backpack == null) return 0;
            int total = 0;
            foreach (Item item in from.Backpack.Items)
                if (item is HunterToken t)
                    total += t.Amount;
            return total;
        }

        private static bool SpendTokens(Mobile from, int cost)
        {
            if (from.Backpack == null) return false;
            int remaining = cost;
            var toDelete = new List<HunterToken>();

            foreach (Item item in from.Backpack.Items)
            {
                if (item is HunterToken t)
                {
                    if (t.Amount <= remaining)
                    {
                        remaining -= t.Amount;
                        toDelete.Add(t);
                    }
                    else
                    {
                        t.Amount -= remaining;
                        remaining = 0;
                        break;
                    }
                }
                if (remaining == 0) break;
            }

            if (remaining > 0) return false;
            foreach (var t in toDelete) t.Delete();
            return true;
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_npc.Deleted || info.ButtonID == 0) return;

            int idx = info.ButtonID - 1;
            if (idx < 0 || idx >= ShopItems.Length) return;

            int cost = ShopItems[idx].cost;
            if (CountTokens(_from) < cost)
            {
                _from.SendMessage(0x22, $"You need {cost} Hunter Tokens for that.");
                return;
            }

            if (!SpendTokens(_from, cost)) return;

            // Deliver the item
            switch (idx)
            {
                case 0: // Hunter's Tabard
                    var robe = new Robe();
                    robe.Name = "Hunter's Tabard";
                    robe.Hue  = 0x4AA;
                    _from.AddToBackpack(robe);
                    _from.SendMessage(0x35, "You receive a Hunter's Tabard.");
                    break;

                case 1: // Hunter's Map (placeholder)
                    var scroll = new BlankScroll();
                    scroll.Name = "Hunter's Map";
                    scroll.Hue  = 0x4AA;
                    _from.AddToBackpack(scroll);
                    _from.SendMessage(0x35, "You receive a Hunter's Map.");
                    break;

                case 2: // Hunter's Compass
                    _from.AddToBackpack(new HunterCompass());
                    _from.SendMessage(0x35, "You receive a Hunter's Compass. Double-click it to find active hunts.");
                    break;

                case 3: // Tracking Orb — Essence Shards as placeholder
                    _from.AddToBackpack(new EssenceShard(5));
                    _from.SendMessage(0x35, "You receive a Tracking Orb (grants +3 tracking range when held).");
                    break;

                case 4: // Title Deed
                    _from.SendMessage(0x35, "'The Monster Hunter' title has been unlocked for you.");
                    HunterSystem.GrantTitleDeed(_from, "the Monster Hunter");
                    break;
            }
        }
    }

    // ============================================================
    // RANK GUMP — displays rank table
    // ============================================================

    public class HunterRankGump : Gump
    {
        private readonly Mobile             _from;
        private readonly HunterGuildmaster  _npc;

        private static readonly (int pts, string title)[] Ranks = {
            (1,   "the Tracker"),
            (5,   "the Hunter"),
            (15,  "the Monster Hunter"),
            (30,  "the Legendary Hunter"),
            (50,  "the Apex Predator"),
            (100, "the Eternal Hunter"),
        };

        public HunterRankGump(Mobile from, HunterGuildmaster npc) : base(50, 50)
        {
            _from = from;
            _npc  = npc;

            int myPts = HunterSystem.GetPoints(from);

            AddPage(0);
            AddBackground(0, 0, 320, 260, 9270);
            AddLabel(20, 15, 0x4AA, "Hunter Rank Table");
            AddLabel(20, 35, 1153, $"Your points: {myPts}");

            for (int i = 0; i < Ranks.Length; i++)
            {
                int y      = 70 + i * 28;
                bool earned = myPts >= Ranks[i].pts;
                AddLabel(20,  y, earned ? 0x35 : 0x22, $"{Ranks[i].pts,4} pts");
                AddLabel(80,  y, earned ? 1 : 0x22, Ranks[i].title);
                if (earned)
                    AddLabel(240, y, 0x35, "[earned]");
            }
        }

        public override void OnResponse(NetState sender, RelayInfo info) { }
    }

    // ============================================================
    // EXTENSION — expose preview gold to HunterTurnInGump
    // ============================================================

}

namespace Server.Custom
{
    public partial class HunterGuildmaster
    {
        public static int ProcessHeadRewardPreview(int tier)
        {
            switch (tier)
            {
                case 1:  return 900;
                case 2:  return 2400;
                case 3:  return 6000;
                case 4:  return 15000;
                case 10: return 1500;
                case 11: return 4500;
                case 12: return 12000;
                default: return 500;
            }
        }
    }
}
