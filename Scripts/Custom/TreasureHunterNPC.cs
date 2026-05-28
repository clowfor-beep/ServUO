// ============================================================
// TreasureHunterNPC.cs
// Scripts/Custom/TreasureHunterNPC.cs
//
// A placeable NPC offering two services for decoded treasure maps:
//   Portal Only     — 30% of reference chest gold, opens a one-use gate
//   Full Assistance — 80% of reference chest gold, digs/disarms/unlocks
//
// Payment is withdrawn from the player's bank account upfront.
//
// Staff placement: [add TreasureHunterNPC
//
// Design doc: Design/TreasureHunterNPC_DesignDoc.txt
// ============================================================

using System;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Custom
{
    // ============================================================
    // TreasurePortalGate
    // A one-use gate placed at the player's location.
    // Only the paying player can step through.
    // Auto-deletes after 60 seconds if unused.
    // ============================================================
    public class TreasurePortalGate : Item
    {
        private Serial  _ownerSerial;
        private Point3D _dest;
        private Map     _destMap;

        [Constructable]
        public TreasurePortalGate() : base(0xF6C)
        {
            Movable = false;
            Hue     = 0x481;
        }

        public TreasurePortalGate(Serial ownerSerial, Point3D dest, Map destMap) : base(0xF6C)
        {
            Movable      = false;
            Hue          = 0x481;
            _ownerSerial = ownerSerial;
            _dest        = dest;
            _destMap     = destMap;
            Timer.DelayCall(TimeSpan.FromSeconds(60.0), Delete);
        }

        public TreasurePortalGate(Serial serial) : base(serial) { }

        public override bool OnMoveOver(Mobile from)
        {
            if (from.Serial != _ownerSerial)
            {
                from.SendMessage("This portal was opened for someone else.");
                return true; // let them step off without teleporting
            }

            from.PlaySound(0x1FE);
            from.MoveToWorld(_dest, _destMap);
            Delete();
            return false;
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
            writer.Write((int)_ownerSerial);
            writer.Write(_dest);
            writer.Write(_destMap);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            _ownerSerial = (Serial)reader.ReadInt();
            _dest        = reader.ReadPoint3D();
            _destMap     = reader.ReadMap();
            // Restart 60s expiry on load — don't keep stale gates
            Timer.DelayCall(TimeSpan.FromSeconds(60.0), Delete);
        }
    }

    // ============================================================
    // TreasureHunterNPC
    // ============================================================
    public class TreasureHunterNPC : BaseCreature
    {
        // ── Fee table ─────────────────────────────────────────────
        // Reference gold = midpoint of GetGoldCount() range per level.
        // Stash=0, Supply=1, Cache=2, Hoard=3, Trove=4
        private static readonly int[] s_RefGold = { 25000, 35000, 45000, 55000, 60000 };

        public static int GetPortalFee(int level)
        {
            level = Math.Max(0, Math.Min(level, 4));
            return (int)(s_RefGold[level] * 0.30);
        }

        public static int GetFullFee(int level)
        {
            level = Math.Max(0, Math.Min(level, 4));
            return (int)(s_RefGold[level] * 0.80);
        }

        public static int GetDecodeFullFee(int level)
        {
            level = Math.Max(0, Math.Min(level, 4));
            return (int)(s_RefGold[level] * 0.90);
        }

        private static readonly string[] s_LevelNames = { "Stash", "Supply", "Cache", "Hoard", "Trove" };

        public static string GetLevelName(int level)
        {
            level = Math.Max(0, Math.Min(level, 4));
            return s_LevelNames[level];
        }

        // ── Constructor ───────────────────────────────────────────
        [Constructable]
        public TreasureHunterNPC() : base(AIType.AI_Vendor, FightMode.None, 10, 1, 0.2, 0.4)
        {
            Name  = "Treasure Hunter";
            Title = "the Treasure Hunter";
            Body  = 0x190;
            Hue   = Utility.RandomSkinHue();

            SetStr(100); SetDex(100); SetInt(100);
            SetHits(200);

            // Weathered explorer / treasure hunter outfit
            int leather = Utility.RandomList(1109, 2117, 2213, 2306); // earthy leather browns
            int boot    = Utility.RandomList(1107, 2101, 2306);       // dark boot tones
            AddItem(new StuddedChest(leather));
            AddItem(new StuddedLegs(leather));
            AddItem(new LeatherGloves(leather));
            AddItem(new FeatheredHat(boot));
            AddItem(new ThighBoots(boot));
        }

        public TreasureHunterNPC(Serial serial) : base(serial) { }

        // ── Double-click opens service picker ─────────────────────
        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(this, 4))
            {
                from.SendMessage("You are too far away.");
                return;
            }

            from.CloseGump(typeof(TreasureHunterServiceGump));
            from.CloseGump(typeof(TreasureConfirmGump));
            from.SendGump(new TreasureHunterServiceGump(from, this));
        }

        // ── Service 1: Portal Only ────────────────────────────────
        public void BeginPortalService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap)) return;

            int fee = GetPortalFee(tMap.Level);

            if (Banker.GetBalance(from) < fee)
            {
                from.SendMessage(0x22, $"You need at least {fee:N0} gold in your bank for this service.");
                return;
            }

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero)
            {
                from.SendMessage(0x22, "I cannot locate a safe landing point for that map. No gold was taken.");
                return;
            }

            Banker.Withdraw(from, fee);
            from.SendMessage(0x44, $"{fee:N0} gold withdrawn from your bank.");

            Say("The portal awaits. Good hunting!");
            PlaySound(0x1FE);

            var gate = new TreasurePortalGate(from.Serial, dest, tMap.Facet);
            gate.MoveToWorld(from.Location, from.Map);
        }

        // ── Service 2: Full Assistance ────────────────────────────
        public void BeginFullService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap)) return;

            int fee = GetFullFee(tMap.Level);

            if (Banker.GetBalance(from) < fee)
            {
                from.SendMessage(0x22, $"You need at least {fee:N0} gold in your bank for this service.");
                return;
            }

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero)
            {
                from.SendMessage(0x22, "I cannot locate a safe landing point for that map. No gold was taken.");
                return;
            }

            Banker.Withdraw(from, fee);
            from.SendMessage(0x44, $"{fee:N0} gold withdrawn from your bank.");

            Say("Follow me through the portal!");
            RunFullServiceSequence(from, tMap, dest);
        }

        private void RunFullServiceSequence(Mobile from, TreasureMap tMap, Point3D dest)
        {
            Point3D origin       = Location;
            Map     originMap    = Map;
            Serial  playerSerial = from.Serial;

            PlaySound(0x1FE);

            // T+0.5s: teleport both to treasure location
            Timer.DelayCall(TimeSpan.FromSeconds(0.5), () =>
            {
                Mobile player = World.FindMobile(playerSerial);
                if (player == null || player.Deleted || Deleted) return;

                player.PlaySound(0x1FE);
                player.MoveToWorld(dest, tMap.Facet);
                MoveToWorld(dest, tMap.Facet);

                // T+2.0s: digging emote
                Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
                {
                    if (Deleted) return;
                    Say("Stand back — I'll dig this up!");
                    Animate(11, 5, 1, true, false, 0);

                    // T+4.0s: spawn and fill chest
                    Timer.DelayCall(TimeSpan.FromSeconds(2.5), () =>
                    {
                        if (Deleted) return;

                        Mobile pl = World.FindMobile(playerSerial);

                        // Spawn the chest
                        var chest = new TreasureMapChest(pl, tMap.Level, false);
                        chest.TreasureMap = tMap;
                        tMap.AssignChestQuality(this, chest);
                        TreasureMapInfo.Fill(pl ?? this, chest, tMap);

                        // Disarm and unlock
                        chest.Locked   = false;
                        chest.TrapType = TrapType.None;
                        chest.MoveToWorld(dest, tMap.Facet);

                        tMap.Completed   = true;
                        tMap.CompletedBy = pl ?? this;

                        // Spawn 4 guardians — same count as normal digging in the new system
                        for (int i = 0; i < 4; i++)
                        {
                            bool isGuardian = Utility.RandomDouble() >= 0.3;
                            BaseCreature gc = TreasureMap.Spawn(tMap.Level, dest, tMap.Facet, pl, isGuardian);
                            if (gc != null && isGuardian)
                            {
                                gc.Hue = 2725;
                                chest.Guardians.Add(gc);
                            }
                        }

                        Say("Guardians! Defend yourself — chest is yours!");

                        // T+6.0s: open return gate, NPC departs
                        Timer.DelayCall(TimeSpan.FromSeconds(2.0), () =>
                        {
                            Say("My work here is done. Safe travels!");
                            PlaySound(0x1FE);

                            // Return gate for the player at the chest location
                            if (pl != null && !pl.Deleted)
                            {
                                var returnGate = new TreasurePortalGate(pl.Serial, origin, originMap);
                                returnGate.MoveToWorld(dest, tMap.Facet);
                            }

                            // NPC returns to its spawn location
                            MoveToWorld(origin, originMap);
                        });
                    });
                });
            });
        }

        // ── Service 3: Decode + Full Assistance ──────────────────
        public void BeginDecodeFullService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap, requireDecoded: false)) return;

            int fee = GetDecodeFullFee(tMap.Level);

            if (Banker.GetBalance(from) < fee)
            {
                from.SendMessage(0x22, $"You need at least {fee:N0} gold in your bank for this service.");
                return;
            }

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero)
            {
                from.SendMessage(0x22, "I cannot locate a safe landing point for that map. No gold was taken.");
                return;
            }

            Banker.Withdraw(from, fee);
            from.SendMessage(0x44, $"{fee:N0} gold withdrawn from your bank.");

            // Decode the map now on the player's behalf
            tMap.Decoder = from;
            Say("I've decoded your map. Now follow me!");

            // Reuse the full service sequence from here
            RunFullServiceSequence(from, tMap, dest);
        }

        // ── Shared helpers ────────────────────────────────────────
        private bool ValidateMap(Mobile from, TreasureMap tMap, bool requireDecoded = true)
        {
            if (!tMap.IsChildOf(from.Backpack))
            {
                from.SendMessage(0x22, "The map must be in your pack.");
                return false;
            }
            if (requireDecoded && tMap.Decoder == null)
            {
                from.SendMessage(0x22, "The map must be decoded before I can use it.");
                return false;
            }
            if (tMap.Completed)
            {
                from.SendMessage(0x22, "That treasure has already been claimed.");
                return false;
            }
            return true;
        }

        private static Point3D GetDestination(TreasureMap tMap)
        {
            Map map = tMap.Facet;
            if (map == null || map == Map.Internal) return Point3D.Zero;
            int x = tMap.ChestLocation.X;
            int y = tMap.ChestLocation.Y;
            int z = map.GetAverageZ(x, y);
            if (z <= -20) return Point3D.Zero; // water or invalid terrain
            return new Point3D(x, y, z);
        }

        public override bool IsInvulnerable   => true;
        public override bool AlwaysInnocent  => true;
        public override bool CanBeRenamedBy(Mobile from) => false;
        public override bool HandlesOnSpeech(Mobile from) => false;

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt(); // version
        }
    }

    // ============================================================
    // ServiceType enum
    // ============================================================
    public enum THServiceType { Portal, Full, DecodeAndFull }

    // ============================================================
    // MapTarget — targeting cursor to pick a TreasureMap
    // ============================================================
    public class TreasureMapTarget : Target
    {
        private readonly TreasureHunterNPC _npc;
        private readonly THServiceType     _service;

        public TreasureMapTarget(TreasureHunterNPC npc, THServiceType service)
            : base(2, false, TargetFlags.None)
        {
            _npc     = npc;
            _service = service;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            var tMap = targeted as TreasureMap;

            if (tMap == null)
            {
                from.SendMessage("That is not a treasure map.");
                return;
            }
            if (!tMap.IsChildOf(from.Backpack))
            {
                from.SendMessage("The map must be in your pack.");
                return;
            }
            if (_service != THServiceType.DecodeAndFull && tMap.Decoder == null)
            {
                from.SendMessage("The map must be decoded first. Choose 'Decode + Full Assistance' if you need that done too.");
                return;
            }
            if (tMap.Completed)
            {
                from.SendMessage("That treasure has already been claimed.");
                return;
            }

            from.CloseGump(typeof(TreasureConfirmGump));
            from.SendGump(new TreasureConfirmGump(from, _npc, tMap, _service));
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            from.SendMessage("Cancelled.");
        }
    }

    // ============================================================
    // TreasureHunterServiceGump — step 1: pick service
    // ============================================================
    public class TreasureHunterServiceGump : Gump
    {
        private readonly Mobile            _from;
        private readonly TreasureHunterNPC _npc;

        private const int BTN_CLOSE       = 0;
        private const int BTN_PORTAL      = 1;
        private const int BTN_FULL        = 2;
        private const int BTN_DECODE_FULL = 3;

        public TreasureHunterServiceGump(Mobile from, TreasureHunterNPC npc) : base(100, 100)
        {
            _from = from;
            _npc  = npc;

            AddBackground(0, 0, 440, 290, 9200);
            AddAlphaRegion(10, 10, 420, 270);

            AddLabel(145, 16, 0x44, "Treasure Hunter Services");

            AddLabel(20, 50, 1153, "I offer three services for treasure maps.");
            AddLabel(20, 66, 1153, "Payment is taken from your bank account upfront.");

            // Portal Only (requires decoded map)
            AddButton(20, 100, 0xFA5, 0xFA7, BTN_PORTAL, GumpButtonType.Reply, 0);
            AddLabel(55, 101, 0x44,   "Portal Only  (30% fee)");
            AddLabel(55, 118, 1153,  "I open a gate to the treasure. Map must be decoded.");

            // Full Assistance (requires decoded map)
            AddButton(20, 148, 0xFA5, 0xFA7, BTN_FULL, GumpButtonType.Reply, 0);
            AddLabel(55, 149, 0x44,   "Full Assistance  (80% fee)");
            AddLabel(55, 166, 1153,  "I travel with you, dig, disarm and unlock. Map must be decoded.");

            // Decode + Full Assistance (works on any undecoded map)
            AddButton(20, 196, 0xFA5, 0xFA7, BTN_DECODE_FULL, GumpButtonType.Reply, 0);
            AddLabel(55, 197, 0x44,   "Decode + Full Assistance  (90% fee)");
            AddLabel(55, 214, 1153,  "I decode the map, travel with you, dig, disarm and unlock.");

            AddButton(400, 10, 0xFB1, 0xFB2, BTN_CLOSE, GumpButtonType.Reply, 0);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_npc == null || _npc.Deleted) return;

            if (info.ButtonID == BTN_PORTAL)
            {
                _from.SendMessage("Which decoded map should I use?");
                _from.Target = new TreasureMapTarget(_npc, THServiceType.Portal);
            }
            else if (info.ButtonID == BTN_FULL)
            {
                _from.SendMessage("Which decoded map should I use?");
                _from.Target = new TreasureMapTarget(_npc, THServiceType.Full);
            }
            else if (info.ButtonID == BTN_DECODE_FULL)
            {
                _from.SendMessage("Which map should I decode and assist with?");
                _from.Target = new TreasureMapTarget(_npc, THServiceType.DecodeAndFull);
            }
        }
    }

    // ============================================================
    // TreasureConfirmGump — step 2: show price + confirm
    // ============================================================
    public class TreasureConfirmGump : Gump
    {
        private readonly TreasureHunterNPC _npc;
        private readonly TreasureMap       _map;
        private readonly THServiceType     _service;

        private const int BTN_CLOSE   = 0;
        private const int BTN_CONFIRM = 1;
        private const int BTN_CANCEL  = 2;

        public TreasureConfirmGump(Mobile from, TreasureHunterNPC npc, TreasureMap map, THServiceType service)
            : base(100, 100)
        {
            _npc     = npc;
            _map     = map;
            _service = service;

            int    level    = map.Level;
            string lvlName  = TreasureHunterNPC.GetLevelName(level);
            int fee;
            string svcLabel;
            switch (service)
            {
                case THServiceType.Portal:
                    fee      = TreasureHunterNPC.GetPortalFee(level);
                    svcLabel = "Portal Only — I open a gate to the treasure";
                    break;
                case THServiceType.DecodeAndFull:
                    fee      = TreasureHunterNPC.GetDecodeFullFee(level);
                    svcLabel = "Decode + Full Assistance — I do everything";
                    break;
                default: // Full
                    fee      = TreasureHunterNPC.GetFullFee(level);
                    svcLabel = "Full Assistance — I dig, disarm and unlock";
                    break;
            }
            int  balance    = Banker.GetBalance(from);
            bool canAfford  = balance >= fee;

            AddBackground(0, 0, 440, 240, 9200);
            AddAlphaRegion(10, 10, 420, 220);

            AddLabel(165, 16, 0x44, "Confirm Service");

            AddLabel(20, 52, 1153, $"Map:     A {lvlName} treasure map");
            AddLabel(20, 72, 1153, $"Service: {svcLabel}");
            AddLabel(20, 92, 0x44,   $"Cost:    {fee:N0} gold (from bank)");

            if (canAfford)
                AddLabel(20, 112, 0x44, $"Bank:    {balance:N0} gold available");
            else
                AddLabel(20, 112, 33,   $"Bank:    {balance:N0} gold  (need {fee - balance:N0} more)");

            if (canAfford)
            {
                AddButton(80, 178, 0xFA5, 0xFA7, BTN_CONFIRM, GumpButtonType.Reply, 0);
                AddLabel(115, 179, 0x44, "Confirm");
            }

            AddButton(250, 178, 0xFA5, 0xFA7, BTN_CANCEL, GumpButtonType.Reply, 0);
            AddLabel(285, 179, 1153, "Cancel");

            AddButton(400, 10, 0xFB1, 0xFB2, BTN_CLOSE, GumpButtonType.Reply, 0);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            Mobile from = sender.Mobile;
            if (from == null || _npc == null || _npc.Deleted) return;

            if (info.ButtonID == BTN_CONFIRM)
            {
                switch (_service)
                {
                    case THServiceType.Portal:        _npc.BeginPortalService(from, _map);      break;
                    case THServiceType.Full:          _npc.BeginFullService(from, _map);         break;
                    case THServiceType.DecodeAndFull: _npc.BeginDecodeFullService(from, _map);   break;
                }
            }
            // Cancel / Close: do nothing
        }
    }
}
