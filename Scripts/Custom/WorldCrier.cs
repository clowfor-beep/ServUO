// ============================================================
// WorldCrier.cs
// Scripts/Custom/WorldCrier.cs
// ============================================================
//
// WorldCrierSystem  — static class that subscribes to FBEventBus
//                     events and keeps a circular announcement queue.
//
// WorldCrierNPC     — placeable BaseCreature NPC. Players double-click
//                     it to hear current events via WorldCrierGump.
//
// WorldCrierGump    — shows active hunts, active wanted, and recent news.
//
// WorldCrierSystem is stateless (queue resets on server restart — acceptable).
// WorldCrierNPC is persistent and needs Serialize/Deserialize.
// ============================================================

using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // WorldCrierSystem — static event subscriber + announcement queue
    // ============================================================

    public static class WorldCrierSystem
    {
        // Circular queue of recent announcements — last 5
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
            string killerName = killer != null ? killer.Name : "an unknown hunter";
            Enqueue($"{killerName} has slain {(creature != null ? creature.Name : "a hunted beast")}! The guild shall reward them.");
        }

        private static void OnWantedKilled(Mobile npc, Mobile killer)
        {
            string killerName = killer != null ? killer.Name : "an unknown hero";
            Enqueue($"The wanted criminal {(npc != null ? npc.Name : "a wanted foe")} has been brought to justice by {killerName}!");
        }

        public static IEnumerable<string> GetRecentAnnouncements() => _recent;
    }

    // ============================================================
    // WorldCrierNPC — placeable NPC
    // ============================================================

    public class WorldCrierNPC : BaseCreature
    {
        [Constructable]
        public WorldCrierNPC()
            : base(AIType.AI_Animal, FightMode.None, 10, 1, 0.2, 0.4)
        {
            Name           = "Town Crier";
            Body           = 0x190;
            Title          = "the Herald";
            IsInvulnerable = true;

            AddItem(new Robe(Utility.RandomNeutralHue()));
        }

        public WorldCrierNPC(Serial serial) : base(serial) { }

        public override bool IsInvulnerable { get; set; }

        public override bool CanBeRenamedBy(Mobile from) => false;
        public override bool ClickTitle   => true;
        public override bool AlwaysRun    => false;

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), 4))
            {
                from.SendMessage("You are too far away to hear the crier.");
                return;
            }

            from.SendGump(new WorldCrierGump(from));
        }

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
    // WorldCrierGump — shows active hunts, wanted, recent news
    // ============================================================

    public class WorldCrierGump : Gump
    {
        private const int W    = 460;
        private const int PadX = 16;

        public WorldCrierGump(Mobile from) : base(60, 50)
        {
            var hunts  = HunterSystem.GetActiveHunts();
            var wanted = HunterSystem.GetActiveWanted();
            var news   = new List<string>(WorldCrierSystem.GetRecentAnnouncements());

            // Height: header(50) + hunts section + wanted section + news section + footer(20)
            int huntsH  = 22 + System.Math.Max(hunts.Count,  1) * 18 + 10;
            int wantedH = 22 + System.Math.Max(wanted.Count, 1) * 18 + 10;
            int newsH   = 22 + System.Math.Max(news.Count,   1) * 18 + 10;
            int h       = 50 + huntsH + wantedH + newsH + 20;
            h = System.Math.Max(h, 340); // minimum height

            AddBackground(0, 0, W, h, 9200);
            AddAlphaRegion(2, 2, W - 4, h - 4);

            // ── Title ────────────────────────────────────────────────────────────
            AddLabel(PadX, 12, 0x4AA, "Town Crier — Recent Events");
            AddButton(W - 40, 10, 4017, 4019, 0, GumpButtonType.Reply, 0);

            int y = 40;

            // ── Active Hunts ─────────────────────────────────────────────────────
            AddLabel(PadX, y, 0x53, "Active Hunts:");
            y += 20;

            if (hunts.Count == 0)
            {
                AddLabel(PadX + 8, y, 2119, "(none at this time)");
                y += 18;
            }
            else
            {
                foreach (var (name, location) in hunts)
                {
                    AddLabel(PadX + 8, y, 0x4AA, $"• {name} — {location}");
                    y += 18;
                }
            }
            y += 8;

            // ── Active Wanted ────────────────────────────────────────────────────
            AddLabel(PadX, y, 33, "Active Wanted:");
            y += 20;

            if (wanted.Count == 0)
            {
                AddLabel(PadX + 8, y, 2119, "(none at this time)");
                y += 18;
            }
            else
            {
                foreach (var (name, location) in wanted)
                {
                    AddLabel(PadX + 8, y, 0x22, $"• {name} — {location}");
                    y += 18;
                }
            }
            y += 8;

            // ── Recent News ──────────────────────────────────────────────────────
            AddLabel(PadX, y, 0x35, "Recent News:");
            y += 20;

            if (news.Count == 0)
            {
                AddLabel(PadX + 8, y, 2119, "(none at this time)");
            }
            else
            {
                // Show most recent first
                for (int i = news.Count - 1; i >= 0; i--)
                {
                    string line = news[i];
                    // Truncate long lines to fit within gump width
                    if (line.Length > 70) line = line.Substring(0, 70) + "...";
                    AddLabel(PadX + 8, y, 2119, line);
                    y += 18;
                }
            }
        }
    }
}
