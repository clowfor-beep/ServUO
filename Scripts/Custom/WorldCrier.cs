// ============================================================
// WorldCrier.cs
// Scripts/Custom/WorldCrier.cs
//
// WorldCrierSystem — static class, subscribes to FBEventBus
//   events and maintains a rolling queue of recent announcements.
//
// WorldCrierNPC — placeable town herald NPC. Players double-click
//   it to open WorldCrierGump with active hunts, wanted NPCs,
//   and recent news.
//
//   Staff: [add WorldCrierNPC
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // WorldCrierSystem
    // ============================================================

    public static class WorldCrierSystem
    {
        private static readonly Queue<string> _recent = new Queue<string>();
        private const int MaxRecent = 5;

        public static void Initialize()
        {
            FBEventBus.HunterTargetSpawned += OnHunterSpawned;
            FBEventBus.WantedNPCSpawned    += OnWantedSpawned;
            FBEventBus.HunterTargetKilled  += OnHunterKilled;
            FBEventBus.WantedNPCKilled     += OnWantedKilled;
        }

        private static void Enqueue(string msg)
        {
            _recent.Enqueue(msg);
            if (_recent.Count > MaxRecent) _recent.Dequeue();
        }

        private static void OnHunterSpawned(string name, string location)
            => Enqueue($"A {name} has been sighted near {location}! Hunters, take heed.");

        private static void OnWantedSpawned(string name, string location)
            => Enqueue($"The wanted criminal {name} has been spotted near {location}!");

        private static void OnHunterKilled(Mobile creature, Mobile killer)
        {
            string killerName  = killer   != null ? killer.Name   : "an unknown hunter";
            string targetName  = creature != null ? creature.Name : "a hunted beast";
            Enqueue($"{killerName} has slain {targetName}! The guild shall reward them.");
        }

        private static void OnWantedKilled(Mobile npc, Mobile killer)
        {
            string killerName = killer != null ? killer.Name : "an unknown hero";
            string npcName    = npc    != null ? npc.Name    : "a wanted foe";
            Enqueue($"The wanted criminal {npcName} has been brought to justice by {killerName}!");
        }

        public static IEnumerable<string> GetRecentAnnouncements() => _recent;
    }

    // ============================================================
    // WorldCrierNPC
    // ============================================================

    public class WorldCrierNPC : BaseCreature
    {
        [Constructable]
        public WorldCrierNPC() : base(AIType.AI_Vendor, FightMode.None, 10, 1, 0.2, 0.4)
        {
            Name  = "Town Crier";
            Body  = 0x190;
            Title = "the Herald";
            AddItem(new Robe(Utility.RandomNeutralHue()));
        }

        public override bool IsInvulnerable => true;
        public override bool AlwaysInnocent => true;

        public override void OnDoubleClick(Mobile from)
        {
            if (from.InRange(Location, 6))
                from.SendGump(new WorldCrierGump(from));
            else
                from.SendMessage("You are too far away to hear the crier.");
        }

        public WorldCrierNPC(Serial serial) : base(serial) { }

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
    // WorldCrierGump
    // ============================================================

    public class WorldCrierGump : Gump
    {
        public WorldCrierGump(Mobile from) : base(60, 60)
        {
            const int W    = 460;
            const int PadX = 14;

            var activeHunts  = HunterSystem.GetActiveHunts();
            var activeWanted = HunterSystem.GetActiveWanted();
            var recentNews   = new List<string>(WorldCrierSystem.GetRecentAnnouncements());

            // Dynamic height: header + three sections with content rows
            int huntRows   = Math.Max(activeHunts.Count,  1);
            int wantedRows = Math.Max(activeWanted.Count, 1);
            int newsRows   = Math.Max(recentNews.Count,   1);
            int H = 50
                + 22 + huntRows   * 18 + 12
                + 22 + wantedRows * 18 + 12
                + 22 + newsRows   * 18
                + 36; // close button

            AddBackground(0, 0, W, H, 9200);
            AddAlphaRegion(2, 2, W - 4, H - 4);

            AddLabel(PadX, 10, 0x53, "Town Crier — Recent Events");
            AddImageTiled(PadX, 28, W - PadX * 2, 1, 9264);

            int y = 36;

            // ── Active Hunts ──────────────────────────────────────────────────
            AddLabel(PadX, y, 0x4AA, "Active Hunts:");
            y += 22;

            if (activeHunts.Count == 0)
            {
                AddLabel(PadX + 8, y, 2119, "(none at this time)");
                y += 18;
            }
            else
            {
                foreach (var hunt in activeHunts)
                {
                    string line = $"• {hunt.name} — {hunt.location}";
                    AddLabel(PadX + 8, y, 1153, line);
                    y += 18;
                }
            }

            y += 12;

            // ── Active Wanted ─────────────────────────────────────────────────
            AddLabel(PadX, y, 0x22, "Active Wanted:");
            y += 22;

            if (activeWanted.Count == 0)
            {
                AddLabel(PadX + 8, y, 2119, "(none at this time)");
                y += 18;
            }
            else
            {
                foreach (var wanted in activeWanted)
                {
                    string line = $"• {wanted.name} — {wanted.location}";
                    AddLabel(PadX + 8, y, 1153, line);
                    y += 18;
                }
            }

            y += 12;

            // ── Recent News ───────────────────────────────────────────────────
            AddLabel(PadX, y, 0x35, "Recent News:");
            y += 22;

            if (recentNews.Count == 0)
            {
                AddLabel(PadX + 8, y, 2119, "(none at this time)");
                y += 18;
            }
            else
            {
                foreach (string news in recentNews)
                {
                    string display = news.Length > 68 ? news.Substring(0, 68) + "..." : news;
                    AddLabel(PadX + 8, y, 1153, display);
                    y += 18;
                }
            }

            y += 8;
            AddButton(W / 2 - 30, y, 4005, 4007, 0, GumpButtonType.Reply, 0);
            AddLabel(W / 2 + 6,   y, 2119, "Close");
        }
    }
}
