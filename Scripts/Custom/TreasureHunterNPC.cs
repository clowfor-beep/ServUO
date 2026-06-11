// ============================================================
// TreasureHunterNPC.cs
// Scripts/Custom/TreasureHunterNPC.cs
//
// Services offered for decoded (or any) treasure maps:
//
//   Solo Portal          — 30%  gate for you + pets
//   Party Portal         — 33%  gate for whole party + pets (60s)
//   Solo Full            — 50%  NPC digs/disarms/unlocks, you + pets teleported
//   Party Full           — 55%  same, whole party + pets teleported
//   Solo Decode+Full     — 80%  NPC decodes + full service, you + pets
//   Party Decode+Full    — 88%  same, whole party + pets
//
// Payment withdrawn from bank upfront.
// Staff placement: [add TreasureHunterNPC
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Engines.PartySystem;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Custom
{
    // ============================================================
    // TreasurePortalGate
    // Solo mode:  only the paying player + their pets pass through.
    //             Gate deletes itself after the owner steps through.
    // Party mode: owner + party members (snapshot at purchase) + their
    //             pets all pass through. Gate stays open for 60 seconds.
    // Auto-deletes after 60 seconds regardless.
    // ============================================================
    public class TreasurePortalGate : Item
    {
        private Serial      _ownerSerial;
        private Point3D     _dest;
        private Map         _destMap;
        private bool        _partyGate;
        private List<int>   _partySerials = new List<int>();

        [Constructable]
        public TreasurePortalGate() : base(0xF6C)
        {
            Movable = false;
            Hue     = 0x481;
        }

        // Solo portal constructor
        public TreasurePortalGate(Serial ownerSerial, Point3D dest, Map destMap)
            : this(ownerSerial, dest, destMap, false, null) { }

        // Party portal constructor
        public TreasurePortalGate(Serial ownerSerial, Point3D dest, Map destMap,
                                   bool partyGate, List<int> partySerials) : base(0xF6C)
        {
            Movable       = false;
            Hue           = partyGate ? 0x489 : 0x481;
            _ownerSerial  = ownerSerial;
            _dest         = dest;
            _destMap      = destMap;
            _partyGate    = partyGate;
            _partySerials = partySerials ?? new List<int>();
            Timer.DelayCall(TimeSpan.FromSeconds(60.0), Delete);
        }

        public TreasurePortalGate(Serial serial) : base(serial) { }

        public override bool OnMoveOver(Mobile from)
        {
            bool isOwner = from.Serial == _ownerSerial;
            bool isParty = _partyGate && _partySerials.Contains((int)from.Serial);

            if (!isOwner && !isParty)
            {
                from.SendMessage("This portal was not opened for you.");
                return true;
            }

            from.PlaySound(0x1FE);

            // Collect pets before moving player (they're still nearby)
            List<BaseCreature> pets = TreasureHunterNPC.CollectPets(from);
            from.MoveToWorld(_dest, _destMap);
            foreach (BaseCreature pet in pets)
                pet.MoveToWorld(_dest, _destMap);

            // Solo gate: delete immediately after owner steps through
            if (!_partyGate)
                Delete();

            return false;
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1); // version
            writer.Write((int)_ownerSerial);
            writer.Write(_dest);
            writer.Write(_destMap);
            writer.Write(_partyGate);
            writer.Write(_partySerials.Count);
            foreach (int s in _partySerials)
                writer.Write(s);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version      = reader.ReadInt();
            _ownerSerial     = (Serial)reader.ReadInt();
            _dest            = reader.ReadPoint3D();
            _destMap         = reader.ReadMap();

            if (version >= 1)
            {
                _partyGate = reader.ReadBool();
                int count  = reader.ReadInt();
                _partySerials = new List<int>(count);
                for (int i = 0; i < count; i++)
                    _partySerials.Add(reader.ReadInt());
            }

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

        public static int GetPartyPortalFee(int level)
        {
            return (int)(GetPortalFee(level) * 1.10);
        }

        public static int GetFullFee(int level)
        {
            level = Math.Max(0, Math.Min(level, 4));
            return (int)(s_RefGold[level] * 0.50);
        }

        public static int GetPartyFullFee(int level)
        {
            return (int)(GetFullFee(level) * 1.10);
        }

        public static int GetDecodeFullFee(int level)
        {
            level = Math.Max(0, Math.Min(level, 4));
            return (int)(s_RefGold[level] * 0.80);
        }

        public static int GetPartyDecodeFullFee(int level)
        {
            return (int)(GetDecodeFullFee(level) * 1.10);
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
            Name  = Utility.RandomList(
                "Aldric", "Brennan", "Corvin", "Davan", "Emric",
                "Farrow", "Gareth", "Hadwin", "Ivor", "Jasper",
                "Keld", "Lorcan", "Maren", "Nolan", "Oryn",
                "Petra", "Rowena", "Sable", "Tova", "Wren"
            );
            Title = "the Treasure Hunter";
            Body  = 0x190;
            Hue   = Utility.RandomSkinHue();

            SetStr(100); SetDex(100); SetInt(100);
            SetHits(200);
            SetSkill(SkillName.Cartography, 100.0, 100.0);

            int leather = Utility.RandomList(1109, 2117, 2213, 2306);
            int boot    = Utility.RandomList(1107, 2101, 2306);
            AddItem(new StuddedChest  { Hue = leather });
            AddItem(new StuddedLegs   { Hue = leather });
            AddItem(new LeatherGloves { Hue = leather });
            AddItem(new FeatheredHat  { Hue = boot    });
            AddItem(new ThighBoots    { Hue = boot    });
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

        // ── Service: Solo Portal ──────────────────────────────────
        public void BeginPortalService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap)) return;

            int fee = GetPortalFee(tMap.Level);
            if (!WithdrawFee(from, fee)) return;

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero) { RefundFee(from, fee); return; }

            Say("The portal awaits. Good hunting!");
            PlaySound(0x1FE);

            var gate = new TreasurePortalGate(from.Serial, dest, tMap.Facet);
            gate.MoveToWorld(from.Location, from.Map);
        }

        // ── Service: Party Portal ─────────────────────────────────
        public void BeginPartyPortalService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap)) return;

            int fee = GetPartyPortalFee(tMap.Level);
            if (!WithdrawFee(from, fee)) return;

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero) { RefundFee(from, fee); return; }

            List<int> partySerials = SnapshotParty(from);

            int memberCount = partySerials.Count;
            Say(memberCount > 1
                ? $"A party gate for {memberCount} — good hunting!"
                : "The portal awaits. Good hunting!");
            PlaySound(0x1FE);

            var gate = new TreasurePortalGate(from.Serial, dest, tMap.Facet, true, partySerials);
            gate.MoveToWorld(from.Location, from.Map);
        }

        // ── Service: Solo Full Assistance ─────────────────────────
        public void BeginFullService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap)) return;

            int fee = GetFullFee(tMap.Level);
            if (!WithdrawFee(from, fee)) return;

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero) { RefundFee(from, fee); return; }

            Say("Follow me through the portal!");
            RunFullServiceSequence(from, tMap, dest, false, null);
        }

        // ── Service: Party Full Assistance ────────────────────────
        public void BeginPartyFullService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap)) return;

            int fee = GetPartyFullFee(tMap.Level);
            if (!WithdrawFee(from, fee)) return;

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero) { RefundFee(from, fee); return; }

            List<int> partySerials = SnapshotParty(from);

            int memberCount = partySerials.Count;
            Say(memberCount > 1
                ? $"Bringing your party of {memberCount} — follow me!"
                : "Follow me through the portal!");
            RunFullServiceSequence(from, tMap, dest, true, partySerials);
        }

        // ── Service: Solo Decode + Full ───────────────────────────
        public void BeginDecodeFullService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap, requireDecoded: false)) return;

            int fee = GetDecodeFullFee(tMap.Level);
            if (!WithdrawFee(from, fee)) return;

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero) { RefundFee(from, fee); return; }

            tMap.Decoder = from;
            Say("I've decoded your map. Now follow me!");
            RunFullServiceSequence(from, tMap, dest, false, null);
        }

        // ── Service: Party Decode + Full ──────────────────────────
        public void BeginPartyDecodeFullService(Mobile from, TreasureMap tMap)
        {
            if (!ValidateMap(from, tMap, requireDecoded: false)) return;

            int fee = GetPartyDecodeFullFee(tMap.Level);
            if (!WithdrawFee(from, fee)) return;

            Point3D dest = GetDestination(tMap);
            if (dest == Point3D.Zero) { RefundFee(from, fee); return; }

            List<int> partySerials = SnapshotParty(from);

            tMap.Decoder = from;
            int memberCount = partySerials.Count;
            Say(memberCount > 1
                ? $"Map decoded. Bringing your party of {memberCount} — follow me!"
                : "I've decoded your map. Now follow me!");
            RunFullServiceSequence(from, tMap, dest, true, partySerials);
        }

        // ── Shared: full service sequence ─────────────────────────
        private void RunFullServiceSequence(Mobile from, TreasureMap tMap, Point3D dest,
                                             bool partyMode, List<int> partySerials)
        {
            Point3D origin       = Location;
            Map     originMap    = Map;
            Serial  playerSerial = from.Serial;

            PlaySound(0x1FE);

            // T+0.5s: teleport everyone to treasure location
            Timer.DelayCall(TimeSpan.FromSeconds(0.5), () =>
            {
                Mobile player = World.FindMobile(playerSerial);
                if (player == null || player.Deleted || Deleted) return;

                player.PlaySound(0x1FE);

                if (partyMode && partySerials != null)
                {
                    foreach (int serial in partySerials)
                    {
                        Mobile member = World.FindMobile((Serial)serial);
                        if (member != null && !member.Deleted)
                        {
                            List<BaseCreature> memberPets = CollectPets(member);
                            member.MoveToWorld(dest, tMap.Facet);
                            foreach (BaseCreature pet in memberPets)
                                pet.MoveToWorld(dest, tMap.Facet);
                        }
                    }
                }
                else
                {
                    List<BaseCreature> playerPets = CollectPets(player);
                    player.MoveToWorld(dest, tMap.Facet);
                    foreach (BaseCreature pet in playerPets)
                        pet.MoveToWorld(dest, tMap.Facet);
                }

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

                        var chest = new TreasureMapChest(pl, tMap.Level, false);
                        chest.TreasureMap = tMap;
                        tMap.AssignChestQuality(this, chest);
                        TreasureMapInfo.Fill(pl ?? this, chest, tMap);

                        chest.Locked   = false;
                        chest.TrapType = TrapType.None;
                        chest.MoveToWorld(dest, tMap.Facet);

                        tMap.Completed   = true;
                        tMap.CompletedBy = pl ?? this;

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

                            if (pl != null && !pl.Deleted)
                            {
                                TreasurePortalGate returnGate;
                                if (partyMode && partySerials != null)
                                    returnGate = new TreasurePortalGate(pl.Serial, origin, originMap, true, partySerials);
                                else
                                    returnGate = new TreasurePortalGate(pl.Serial, origin, originMap);

                                returnGate.MoveToWorld(dest, tMap.Facet);
                            }

                            MoveToWorld(origin, originMap);
                        });
                    });
                });
            });
        }

        // ── Shared helpers ────────────────────────────────────────

        // Snapshot all party serials (including the payer)
        private static List<int> SnapshotParty(Mobile from)
        {
            var serials = new List<int> { (int)from.Serial };
            Party party = Party.Get(from);
            if (party != null)
            {
                foreach (PartyMemberInfo info in party.Members)
                {
                    if (info.Mobile != null && info.Mobile != from)
                        serials.Add((int)info.Mobile.Serial);
                }
            }
            return serials;
        }

        // Collect all live controlled pets near a mobile (before moving them)
        public static List<BaseCreature> CollectPets(Mobile owner)
        {
            var pets = new List<BaseCreature>();
            if (owner.Map == null) return pets;

            IPooledEnumerable<Mobile> eable = owner.Map.GetMobilesInRange(owner.Location, 30);
            foreach (Mobile m in eable)
            {
                if (m is BaseCreature bc && bc.Controlled && bc.ControlMaster == owner && !bc.IsDeadPet)
                    pets.Add(bc);
            }
            eable.Free();
            return pets;
        }

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

        private bool WithdrawFee(Mobile from, int fee)
        {
            if (Banker.GetBalance(from) < fee)
            {
                from.SendMessage(0x22, $"You need at least {fee:N0} gold in your bank for this service.");
                return false;
            }
            Banker.Withdraw(from, fee);
            from.SendMessage(0x44, $"{fee:N0} gold withdrawn from your bank.");
            return true;
        }

        private void RefundFee(Mobile from, int fee)
        {
            Banker.Deposit(from, fee);
            from.SendMessage(0x22, "I cannot locate a safe landing point for that map. Your gold has been refunded.");
        }

        private static Point3D GetDestination(TreasureMap tMap)
        {
            Map map = tMap.Facet;
            if (map == null || map == Map.Internal) return Point3D.Zero;

            int x = tMap.ChestLocation.X;
            int y = tMap.ChestLocation.Y;
            int z = map.GetAverageZ(x, y);

            if (map.CanFit(x, y, z, 16, false, false))
                return new Point3D(x, y, z);

            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    int nz = map.GetAverageZ(nx, ny);
                    if (map.CanFit(nx, ny, nz, 16, false, false))
                        return new Point3D(nx, ny, nz);
                }
            }

            return Point3D.Zero;
        }

        public override bool IsInvulnerable  => true;
        public override bool AlwaysInnocent  => true;
        public override bool CanBeRenamedBy(Mobile from) => false;
        public override bool HandlesOnSpeech(Mobile from) => false;

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
    // ServiceType enum
    // ============================================================
    public enum THServiceType
    {
        Portal,
        PartyPortal,
        Full,
        PartyFull,
        DecodeAndFull,
        PartyDecodeAndFull
    }

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

            bool needsDecoded = (_service != THServiceType.DecodeAndFull &&
                                 _service != THServiceType.PartyDecodeAndFull);

            if (needsDecoded && tMap.Decoder == null)
            {
                from.SendMessage("The map must be decoded first. Choose a Decode + Full Assistance option if you need that done too.");
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
    // Six options: solo/party for each of portal, full, decode+full
    // ============================================================
    public class TreasureHunterServiceGump : Gump
    {
        private readonly Mobile            _from;
        private readonly TreasureHunterNPC _npc;

        private const int BTN_CLOSE             = 0;
        private const int BTN_PORTAL            = 1;
        private const int BTN_PARTY_PORTAL      = 2;
        private const int BTN_FULL              = 3;
        private const int BTN_PARTY_FULL        = 4;
        private const int BTN_DECODE_FULL       = 5;
        private const int BTN_PARTY_DECODE_FULL = 6;

        public TreasureHunterServiceGump(Mobile from, TreasureHunterNPC npc) : base(100, 80)
        {
            _from = from;
            _npc  = npc;

            AddBackground(0, 0, 460, 460, 9200);
            AddAlphaRegion(10, 10, 440, 440);

            AddLabel(150, 16, 0x44, "Treasure Hunter Services");
            AddLabel(20, 46, 1153, "Choose a service — payment withdrawn from your bank.");

            // ── Solo Portal ──────────────────────────────────────
            AddButton(20, 80, 0xFA5, 0xFA7, BTN_PORTAL, GumpButtonType.Reply, 0);
            AddLabel(55, 81,  0x44,  "Solo Portal");
            AddLabel(55, 98,  1153, "Gate for you and your pets. Requires decoded map.");

            // ── Party Portal ─────────────────────────────────────
            AddButton(20, 128, 0xFA5, 0xFA7, BTN_PARTY_PORTAL, GumpButtonType.Reply, 0);
            AddLabel(55, 129, 0x44,  "Party Portal  (+10%)");
            AddLabel(55, 146, 1153, "Gate for your whole party and all pets. Open 60 seconds.");

            // ── Solo Full Assistance ─────────────────────────────
            AddButton(20, 176, 0xFA5, 0xFA7, BTN_FULL, GumpButtonType.Reply, 0);
            AddLabel(55, 177, 0x44,  "Solo Full Assistance");
            AddLabel(55, 194, 1153, "I travel with you, dig, disarm and unlock. You and pets teleported.");

            // ── Party Full Assistance ────────────────────────────
            AddButton(20, 224, 0xFA5, 0xFA7, BTN_PARTY_FULL, GumpButtonType.Reply, 0);
            AddLabel(55, 225, 0x44,  "Party Full Assistance  (+10%)");
            AddLabel(55, 242, 1153, "Same — whole party and all pets teleported.");

            // ── Solo Decode + Full ───────────────────────────────
            AddButton(20, 272, 0xFA5, 0xFA7, BTN_DECODE_FULL, GumpButtonType.Reply, 0);
            AddLabel(55, 273, 0x44,  "Solo Decode + Full Assistance");
            AddLabel(55, 290, 1153, "I decode the map, travel with you, dig, disarm and unlock.");

            // ── Party Decode + Full ──────────────────────────────
            AddButton(20, 320, 0xFA5, 0xFA7, BTN_PARTY_DECODE_FULL, GumpButtonType.Reply, 0);
            AddLabel(55, 321, 0x44,  "Party Decode + Full Assistance  (+10%)");
            AddLabel(55, 338, 1153, "Same — decodes undecoded map, whole party and pets.");

            AddButton(420, 10, 0xFB1, 0xFB2, BTN_CLOSE, GumpButtonType.Reply, 0);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_npc == null || _npc.Deleted) return;

            switch (info.ButtonID)
            {
                case BTN_PORTAL:
                    _from.SendMessage("Which decoded map should I use?");
                    _from.Target = new TreasureMapTarget(_npc, THServiceType.Portal);
                    break;
                case BTN_PARTY_PORTAL:
                    _from.SendMessage("Which decoded map should I use?");
                    _from.Target = new TreasureMapTarget(_npc, THServiceType.PartyPortal);
                    break;
                case BTN_FULL:
                    _from.SendMessage("Which decoded map should I use?");
                    _from.Target = new TreasureMapTarget(_npc, THServiceType.Full);
                    break;
                case BTN_PARTY_FULL:
                    _from.SendMessage("Which decoded map should I use?");
                    _from.Target = new TreasureMapTarget(_npc, THServiceType.PartyFull);
                    break;
                case BTN_DECODE_FULL:
                    _from.SendMessage("Which map should I decode and assist with?");
                    _from.Target = new TreasureMapTarget(_npc, THServiceType.DecodeAndFull);
                    break;
                case BTN_PARTY_DECODE_FULL:
                    _from.SendMessage("Which map should I decode and assist with?");
                    _from.Target = new TreasureMapTarget(_npc, THServiceType.PartyDecodeAndFull);
                    break;
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

            int    level   = map.Level;
            string lvlName = TreasureHunterNPC.GetLevelName(level);
            int    fee;
            string svcLabel;

            switch (service)
            {
                case THServiceType.Portal:
                    fee      = TreasureHunterNPC.GetPortalFee(level);
                    svcLabel = "Solo Portal — gate for you and your pets";
                    break;
                case THServiceType.PartyPortal:
                    fee      = TreasureHunterNPC.GetPartyPortalFee(level);
                    svcLabel = "Party Portal — gate for whole party and pets (60s)";
                    break;
                case THServiceType.Full:
                    fee      = TreasureHunterNPC.GetFullFee(level);
                    svcLabel = "Solo Full — I dig, disarm and unlock";
                    break;
                case THServiceType.PartyFull:
                    fee      = TreasureHunterNPC.GetPartyFullFee(level);
                    svcLabel = "Party Full — I dig/disarm, whole party teleported";
                    break;
                case THServiceType.DecodeAndFull:
                    fee      = TreasureHunterNPC.GetDecodeFullFee(level);
                    svcLabel = "Solo Decode + Full — I decode, dig, disarm and unlock";
                    break;
                default: // PartyDecodeAndFull
                    fee      = TreasureHunterNPC.GetPartyDecodeFullFee(level);
                    svcLabel = "Party Decode + Full — I decode and do everything, whole party";
                    break;
            }

            int  balance   = Banker.GetBalance(from);
            bool canAfford = balance >= fee;

            AddBackground(0, 0, 460, 240, 9200);
            AddAlphaRegion(10, 10, 440, 220);

            AddLabel(175, 16, 0x44, "Confirm Service");

            AddLabel(20, 52, 1153, $"Map:     A {lvlName} treasure map");
            AddLabel(20, 72, 1153, $"Service: {svcLabel}");
            AddLabel(20, 92, 0x44,  $"Cost:    {fee:N0} gold (from bank)");

            if (canAfford)
                AddLabel(20, 112, 0x44, $"Bank:    {balance:N0} gold available");
            else
                AddLabel(20, 112, 33,   $"Bank:    {balance:N0} gold  (need {fee - balance:N0} more)");

            if (canAfford)
            {
                AddButton(80, 178, 0xFA5, 0xFA7, BTN_CONFIRM, GumpButtonType.Reply, 0);
                AddLabel(115, 179, 0x44, "Confirm");
            }

            AddButton(260, 178, 0xFA5, 0xFA7, BTN_CANCEL, GumpButtonType.Reply, 0);
            AddLabel(295, 179, 1153, "Cancel");

            AddButton(420, 10, 0xFB1, 0xFB2, BTN_CLOSE, GumpButtonType.Reply, 0);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            Mobile from = sender.Mobile;
            if (from == null || _npc == null || _npc.Deleted) return;

            if (info.ButtonID == BTN_CONFIRM)
            {
                switch (_service)
                {
                    case THServiceType.Portal:            _npc.BeginPortalService(from, _map);           break;
                    case THServiceType.PartyPortal:       _npc.BeginPartyPortalService(from, _map);      break;
                    case THServiceType.Full:              _npc.BeginFullService(from, _map);              break;
                    case THServiceType.PartyFull:         _npc.BeginPartyFullService(from, _map);         break;
                    case THServiceType.DecodeAndFull:     _npc.BeginDecodeFullService(from, _map);        break;
                    case THServiceType.PartyDecodeAndFull:_npc.BeginPartyDecodeFullService(from, _map);   break;
                }
            }
        }
    }
}
