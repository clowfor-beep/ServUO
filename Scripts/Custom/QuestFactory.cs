// ============================================================
// QuestFactory.cs
// Scripts/Custom/QuestFactory.cs
//
// Phase 1 — Foundation:
//   - Hunt quests (kill N of target type)
//   - Gather quests (deliver N items to BountyBoard)
//   - BountyBoard item placed in Britain / Trinsic / Minoc
//   - QuestTrackerHUD integration
//   - ReputationSystem reward on completion
//
// One quest active per player at a time.
// Active quest state is NOT persisted (resets on server restart).
//
// Note: ActiveQuest is already defined in QuestTrackerHUD.cs.
// The per-player tracking class here is named PlayerQuestState.
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Custom;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // Section 1 -- Data model
    // ============================================================

    public enum QuestType { Hunt, Gather }
    public enum QuestTier { Common = 0, Uncommon = 1, Rare = 2, Legendary = 3 }

    public class FBQuest
    {
        public string    Id;
        public QuestType Type;
        public QuestTier Tier;
        public string    Title;
        public string    GiverGuild;
        public string    Description;
        public string    RewardGold;    // "min-max" e.g. "500-1000"
        public string    RepGuild;      // FBGuilds constant
        public int       RepAmount;

        // Hunt quest fields
        public string    TargetMobileType; // exact GetType().Name match, e.g. "BloodPactSimPlayer"
        public string    TargetCategory;   // category match: "undead","orc","daemon","dragon","elemental","ratman","troll","terathan"
        public int       KillsRequired;

        // Gather quest fields
        public string    ItemType;         // GetType().Name to match, e.g. "IronIngot"
        public int       ItemAmount;
    }

    /// <summary>
    /// Per-player active quest tracking. Named PlayerQuestState to avoid conflict
    /// with the ActiveQuest class defined in QuestTrackerHUD.cs.
    /// </summary>
    public class PlayerQuestState
    {
        public FBQuest  Quest;
        public int      Progress;  // kills so far / items gathered
        public DateTime Accepted;

        public PlayerQuestState(FBQuest quest)
        {
            Quest    = quest;
            Progress = 0;
            Accepted = DateTime.UtcNow;
        }
    }

    // ============================================================
    // Section 2 -- Static quest table (8 quests for Phase 1)
    // ============================================================

    // ============================================================
    // Section 3 -- Static manager
    // ============================================================

    public static class QuestFactory
    {
        private static readonly Dictionary<Mobile, PlayerQuestState> _activeQuests =
            new Dictionary<Mobile, PlayerQuestState>();

        // ---- Live board — quests currently posted ---------------
        // Refreshes every 30 minutes; claimed quests are removed immediately.
        private static readonly List<FBQuest> _boardQuests = new List<FBQuest>();

        // Slots per tier on each refresh: Common=3, Uncommon=2, Rare=1, Legendary=0-1
        private static readonly int[] _tierSlots = { 3, 2, 1, 1 };

        public static List<FBQuest> BoardQuests => new List<FBQuest>(_boardQuests);

        public static void RefreshBoard(bool broadcast = false)
        {
            _boardQuests.Clear();

            for (int tier = 0; tier <= 3; tier++)
            {
                // Legendary has a 50% chance of appearing each cycle
                if (tier == (int)QuestTier.Legendary && Utility.RandomBool())
                    continue;

                int slots = _tierSlots[tier];
                var pool  = AllQuests.FindAll(q => (int)q.Tier == tier);

                for (int i = 0; i < slots && pool.Count > 0; i++)
                {
                    int idx = Utility.Random(pool.Count);
                    _boardQuests.Add(pool[idx]);
                    pool.RemoveAt(idx);
                }
            }

            if (broadcast)
                World.Broadcast(0x44, false,
                    "[Bounty Board] New contracts have been posted at the bounty boards across the realm!");
        }

        // -- Quest table ------------------------------------------
        // Full quest pool — quests are drawn from here onto the live board each refresh.
        public static readonly List<FBQuest> AllQuests = new List<FBQuest>
        {
            // ---- COMMON: SimPlayer / light hunt quests ----------
            new FBQuest {
                Id               = "hunt_shadowhand_001",
                Type             = QuestType.Hunt,  Tier = QuestTier.Common,
                Title            = "Shadow Hand Arrest",
                GiverGuild       = FBGuilds.SilverWolves,
                Description      = "Bring down 2 Shadow Hand operatives near the bank.",
                RewardGold       = "750-1500",
                RepGuild         = FBGuilds.SilverWolves,  RepAmount = 20,
                TargetMobileType = "ShadowHandSimPlayer",   KillsRequired = 2,
            },
            new FBQuest {
                Id             = "hunt_orc_001",
                Type           = QuestType.Hunt,  Tier = QuestTier.Common,
                Title          = "Reclaim the Roads",
                GiverGuild     = FBGuilds.SilverWolves,
                Description    = "Orc raiding parties are cutting off supply routes. Eliminate 8 of them.",
                RewardGold     = "1000-2000",
                RepGuild       = FBGuilds.SilverWolves,  RepAmount = 50,
                TargetCategory = "orc",                   KillsRequired = 8,
            },
            new FBQuest {
                Id             = "hunt_ratman_001",
                Type           = QuestType.Hunt,  Tier = QuestTier.Common,
                Title          = "Ratman Infestation",
                GiverGuild     = FBGuilds.Wanderers,
                Description    = "Ratman warrens are spreading near the roads. Thin their numbers — kill 8.",
                RewardGold     = "750-1500",
                RepGuild       = FBGuilds.Wanderers,  RepAmount = 45,
                TargetCategory = "ratman",             KillsRequired = 8,
            },
            new FBQuest {
                Id         = "gather_iron_001",
                Type       = QuestType.Gather,  Tier = QuestTier.Common,
                Title      = "Iron Stockpile",
                GiverGuild = FBGuilds.CraftsmenLeague,
                Description= "The forge is running low. Deliver 30 iron ingots to this board.",
                RewardGold = "300-600",
                RepGuild   = FBGuilds.CraftsmenLeague,  RepAmount = 25,
                ItemType   = "IronIngot",               ItemAmount = 30,
            },
            new FBQuest {
                Id         = "gather_bandage_001",
                Type       = QuestType.Gather,  Tier = QuestTier.Common,
                Title      = "Field Medicine",
                GiverGuild = FBGuilds.Wanderers,
                Description= "Travellers are wounded on the road. Deliver 20 bandages here.",
                RewardGold = "300-600",
                RepGuild   = FBGuilds.Wanderers,  RepAmount = 15,
                ItemType   = "Bandage",            ItemAmount = 20,
            },

            // ---- UNCOMMON: Multi-kill / medium threat -----------
            new FBQuest {
                Id               = "hunt_bloodpact_001",
                Type             = QuestType.Hunt,  Tier = QuestTier.Uncommon,
                Title            = "Blood Pact Extermination",
                GiverGuild       = FBGuilds.SilverWolves,
                Description      = "Eliminate 3 Blood Pact members threatening the roads.",
                RewardGold       = "1000-2000",
                RepGuild         = FBGuilds.SilverWolves,  RepAmount = 50,
                TargetMobileType = "BloodPactSimPlayer",    KillsRequired = 3,
            },
            new FBQuest {
                Id               = "hunt_deadwatchers_001",
                Type             = QuestType.Hunt,  Tier = QuestTier.Uncommon,
                Title            = "Silence the Dead Watchers",
                GiverGuild       = FBGuilds.PaladinOrder,
                Description      = "Drive back 2 Dead Watchers before they claim more souls.",
                RewardGold       = "1000-2000",
                RepGuild         = FBGuilds.PaladinOrder,    RepAmount = 30,
                TargetMobileType = "DeadWatchersSimPlayer",  KillsRequired = 2,
            },
            new FBQuest {
                Id               = "hunt_void_001",
                Type             = QuestType.Hunt,  Tier = QuestTier.Uncommon,
                Title            = "Void Incursion",
                GiverGuild       = FBGuilds.ArcaneBrotherhood,
                Description      = "Destroy 2 Void agents before they corrupt the ley lines.",
                RewardGold       = "1000-2000",
                RepGuild         = FBGuilds.ArcaneBrotherhood,  RepAmount = 40,
                TargetMobileType = "TheVoidSimPlayer",           KillsRequired = 2,
            },
            new FBQuest {
                Id             = "hunt_undead_001",
                Type           = QuestType.Hunt,  Tier = QuestTier.Uncommon,
                Title          = "Purge the Crypts",
                GiverGuild     = FBGuilds.PaladinOrder,
                Description    = "The undead stir in the crypts and dungeons. Slay 10 in the name of virtue.",
                RewardGold     = "1500-3000",
                RepGuild       = FBGuilds.PaladinOrder,  RepAmount = 60,
                TargetCategory = "undead",                KillsRequired = 10,
            },
            new FBQuest {
                Id             = "hunt_troll_001",
                Type           = QuestType.Hunt,  Tier = QuestTier.Uncommon,
                Title          = "Troll Bridge",
                GiverGuild     = FBGuilds.SilverWolves,
                Description    = "Trolls and ettins have seized the crossroads. Drive back 5 of them.",
                RewardGold     = "1000-2000",
                RepGuild       = FBGuilds.SilverWolves,  RepAmount = 50,
                TargetCategory = "troll",                 KillsRequired = 5,
            },
            new FBQuest {
                Id             = "hunt_elemental_001",
                Type           = QuestType.Hunt,  Tier = QuestTier.Uncommon,
                Title          = "Elemental Rift",
                GiverGuild     = FBGuilds.ArcaneBrotherhood,
                Description    = "Unstable elemental rifts are forming across the realm. Banish 6 elementals.",
                RewardGold     = "1250-2500",
                RepGuild       = FBGuilds.ArcaneBrotherhood,  RepAmount = 55,
                TargetCategory = "elemental",                  KillsRequired = 6,
            },
            new FBQuest {
                Id         = "gather_boards_001",
                Type       = QuestType.Gather,  Tier = QuestTier.Uncommon,
                Title      = "Craftsmen's Ironwood",
                GiverGuild = FBGuilds.CraftsmenLeague,
                Description= "We need lumber for repairs. Deliver 50 boards to this board.",
                RewardGold = "375-750",
                RepGuild   = FBGuilds.CraftsmenLeague,  RepAmount = 30,
                ItemType   = "Board",                    ItemAmount = 50,
            },

            // ---- RARE: Elite targets / high-end category kills --
            new FBQuest {
                Id               = "hunt_shadowblade_001",
                Type             = QuestType.Hunt,  Tier = QuestTier.Rare,
                Title            = "Shadowblade Contract",
                GiverGuild       = FBGuilds.DreadHunters,
                Description      = "Track and eliminate a Shadowblade assassin active in the region.",
                RewardGold       = "1500-3000",
                RepGuild         = FBGuilds.DreadHunters,  RepAmount = 35,
                TargetMobileType = "ShadowbladeSimPlayer",  KillsRequired = 1,
            },
            new FBQuest {
                Id             = "hunt_daemon_001",
                Type           = QuestType.Hunt,  Tier = QuestTier.Rare,
                Title          = "Daemon Purge",
                GiverGuild     = FBGuilds.ArcaneBrotherhood,
                Description    = "Daemonic rifts have opened across the land. Close them by destroying 5 daemons.",
                RewardGold     = "2000-4000",
                RepGuild       = FBGuilds.ArcaneBrotherhood,  RepAmount = 70,
                TargetCategory = "daemon",                     KillsRequired = 5,
            },

            // ---- LEGENDARY: Endgame challenge -------------------
            new FBQuest {
                Id             = "hunt_dragon_001",
                Type           = QuestType.Hunt,  Tier = QuestTier.Legendary,
                Title          = "Dragon Bounty",
                GiverGuild     = FBGuilds.DreadHunters,
                Description    = "Two dragons have been terrorising the eastern reaches. Bring them down.",
                RewardGold     = "3000-6000",
                RepGuild       = FBGuilds.DreadHunters,  RepAmount = 80,
                TargetCategory = "dragon",                KillsRequired = 2,
            },
        };

        // -- Initialize -- wired automatically at startup ---------
        public static void Initialize()
        {
            EventSink.CreatureDeath += OnMobDeath;

            // Populate board after world finishes loading, then start the refresh timer
            Timer.DelayCall(TimeSpan.FromSeconds(10), () =>
            {
                RefreshBoard(broadcast: false);
                new BoardRefreshTimer().Start();
            });
        }

        // -- Event handlers ---------------------------------------
        private static void OnMobDeath(CreatureDeathEventArgs e)
        {
            if (e == null || e.Creature == null) return;

            Mobile killer = e.Killer;
            if (killer == null) return;

            // Credit tamer if killed by controlled pet
            if (killer is BaseCreature petBC && petBC.ControlMaster is PlayerMobile pm)
                killer = pm;

            CheckHuntProgress(killer, e.Creature);
        }

        // -- Public API -------------------------------------------

        public static bool HasActiveQuest(Mobile m)
            => m != null && _activeQuests.ContainsKey(m);

        public static PlayerQuestState GetActiveQuestState(Mobile m)
        {
            if (m == null) return null;
            _activeQuests.TryGetValue(m, out var state);
            return state;
        }

        /// <summary>Called from BountyBoardGump when a player clicks Accept.</summary>
        public static bool AcceptQuest(Mobile m, string questId)
        {
            if (m == null) return false;

            if (HasActiveQuest(m))
            {
                m.SendMessage(0x22, "You already have an active quest. Abandon it first.");
                return false;
            }

            // Quest must still be on the live board — first come first served
            FBQuest quest = _boardQuests.Find(q => q.Id == questId);
            if (quest == null)
            {
                m.SendMessage(0x22, "That contract has already been claimed. Check the board for others.");
                return false;
            }

            // Remove from board — this player has claimed it
            _boardQuests.Remove(quest);

            _activeQuests[m] = new PlayerQuestState(quest);

            string objLabel = BuildObjectiveLabel(quest);
            int    objMax   = quest.Type == QuestType.Hunt ? quest.KillsRequired : quest.ItemAmount;

            QuestTrackerHUD.SetQuest(m, quest.Title, new List<QuestObjective>
            {
                new QuestObjective(objLabel, 0, objMax)
            });

            m.SendMessage(0x35, $"Quest accepted: {quest.Title}");
            return true;
        }

        /// <summary>Called from BountyBoardGump Abandon button.</summary>
        public static void AbandonQuest(Mobile m)
        {
            if (m == null) return;

            if (!HasActiveQuest(m))
            {
                m.SendMessage(0x22, "You have no active quest.");
                return;
            }

            _activeQuests.Remove(m);
            QuestTrackerHUD.ClearQuest(m);
            m.SendMessage(0x22, "Quest abandoned.");
        }

        /// <summary>Called from EventSink.Death handler for hunt quests.</summary>
        public static void CheckHuntProgress(Mobile killer, Mobile victim)
        {
            if (killer == null || victim == null) return;
            if (!_activeQuests.TryGetValue(killer, out var state)) return;

            FBQuest quest = state.Quest;
            if (quest.Type != QuestType.Hunt) return;

            // Category match takes priority; fall back to exact type name
            bool matched = !string.IsNullOrEmpty(quest.TargetCategory)
                ? MatchesCategory(victim, quest.TargetCategory)
                : victim.GetType().Name == quest.TargetMobileType;
            if (!matched) return;

            state.Progress++;

            string objLabel = BuildObjectiveLabel(quest);
            QuestTrackerHUD.UpdateObjective(killer, objLabel, state.Progress, quest.KillsRequired);

            killer.SendMessage(0x35, $"Quest progress: {state.Progress}/{quest.KillsRequired}");

            if (state.Progress >= quest.KillsRequired)
                CompleteQuest(killer);
        }

        /// <summary>Called from BountyBoardGump Turn In button for gather quests.</summary>
        public static void CheckGatherProgress(Mobile m)
        {
            if (!_activeQuests.TryGetValue(m, out var state)) return;

            FBQuest quest = state.Quest;
            if (quest.Type != QuestType.Gather) return;

            Container pack = m.Backpack;
            if (pack == null) { m.SendMessage(0x22, "You have no backpack."); return; }

            int count = CountItemsByTypeName(pack, quest.ItemType);
            if (count < quest.ItemAmount)
            {
                m.SendMessage(0x22, $"You need {quest.ItemAmount} {quest.ItemType} but only have {count}.");
                return;
            }

            ConsumeItemsByTypeName(pack, quest.ItemType, quest.ItemAmount);
            CompleteQuest(m);
        }

        /// <summary>Completes the active quest — gold + rep + HUD clear.</summary>
        public static void CompleteQuest(Mobile m)
        {
            if (!_activeQuests.TryGetValue(m, out var state)) return;

            FBQuest quest = state.Quest;
            _activeQuests.Remove(m);

            // Parse "min-max" gold reward
            int goldMin = 100, goldMax = 200;
            string[] parts = quest.RewardGold.Split('-');
            if (parts.Length == 2)
            {
                int.TryParse(parts[0].Trim(), out goldMin);
                int.TryParse(parts[1].Trim(), out goldMax);
            }

            int goldAmount = Utility.RandomMinMax(goldMin, goldMax);
            if (m.Backpack != null)
                m.Backpack.DropItem(new Gold(goldAmount));

            // Reputation reward
            ReputationSystem.AddStanding(m, quest.RepGuild, quest.RepAmount);

            // Clear HUD
            QuestTrackerHUD.ClearQuest(m);

            m.SendMessage(0x35, $"Quest complete: {quest.Title}!");
            m.SendMessage(0x35, $"Reward: {goldAmount} gold, +{quest.RepAmount} standing with {quest.RepGuild}.");
            m.PlaySound(0x3D);
        }

        // -- Internal helpers -------------------------------------

        private static string BuildObjectiveLabel(FBQuest quest)
        {
            if (quest.Type == QuestType.Hunt)
            {
                if (!string.IsNullOrEmpty(quest.TargetCategory))
                {
                    // Capitalise category name for display: "undead" -> "Undead"
                    string cat = char.ToUpper(quest.TargetCategory[0]) + quest.TargetCategory.Substring(1);
                    return $"Kill {cat}";
                }
                // Strip "SimPlayer" suffix for display, e.g. "BloodPactSimPlayer" -> "Blood Pact"
                string raw = quest.TargetMobileType.Replace("SimPlayer", "").Trim();
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (char c in raw)
                {
                    if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
                    sb.Append(c);
                }
                return $"Kill {sb}";
            }
            else
            {
                return $"Gather {quest.ItemType}";
            }
        }

        /// <summary>
        /// Returns true if the victim belongs to the named creature category.
        /// Uses type-name pattern matching — no engine dependency.
        /// </summary>
        private static bool MatchesCategory(Mobile victim, string category)
        {
            if (!(victim is BaseCreature)) return false;
            string t = victim.GetType().Name;

            switch (category.ToLower())
            {
                case "undead":
                    return ContainsAny(t, "Lich", "Zombie", "Skeleton", "Wraith", "Shade",
                        "Spectre", "Specter", "Revenant", "Mummy", "BoneKnight", "BoneMagi",
                        "Vampire", "Banshee", "Wight", "Undead", "Ghost", "Ghoul", "Rot");
                case "orc":
                    return ContainsAny(t, "Orc");
                case "daemon":
                    return ContainsAny(t, "Daemon", "Balron", "Demon", "Succubus", "Incubus", "Devil");
                case "dragon":
                    return ContainsAny(t, "Dragon", "Wyrm", "Drake", "Wyvern");
                case "elemental":
                    return ContainsAny(t, "Elemental");
                case "ratman":
                    return ContainsAny(t, "Ratman");
                case "troll":
                    return ContainsAny(t, "Troll", "Ettin", "Ogre");
                case "terathan":
                    return ContainsAny(t, "Terathan", "Ophidian");
                default:
                    return false;
            }
        }

        private static bool ContainsAny(string typeName, params string[] tokens)
        {
            foreach (string token in tokens)
                if (typeName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        private static int CountItemsByTypeName(Container pack, string typeName)
        {
            int total = 0;
            foreach (Item item in pack.Items)
            {
                if (item.GetType().Name == typeName)
                    total += item.Amount;
            }
            return total;
        }

        private static void ConsumeItemsByTypeName(Container pack, string typeName, int amount)
        {
            int remaining = amount;
            List<Item> snapshot = new List<Item>(pack.Items);
            foreach (Item item in snapshot)
            {
                if (remaining <= 0) break;
                if (item.GetType().Name != typeName) continue;

                if (item.Amount <= remaining)
                {
                    remaining -= item.Amount;
                    item.Delete();
                }
                else
                {
                    item.Amount -= remaining;
                    remaining   = 0;
                }
            }
        }
    }

    // ============================================================
    // Board refresh timer — fires every 30 minutes
    // ============================================================

    public class BoardRefreshTimer : Timer
    {
        public BoardRefreshTimer()
            : base(TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30))
        {
            Priority = TimerPriority.OneMinute;
        }

        protected override void OnTick()
        {
            QuestFactory.RefreshBoard(broadcast: true);
        }
    }

    // ============================================================
    // Section 4 -- BountyBoard item
    // ============================================================

    public class BountyBoard : Item
    {
        [Constructable]
        public BountyBoard() : base(0x1E5E) // bulletin board post
        {
            Name    = "a bounty board";
            Movable = false;
        }

        public BountyBoard(Serial serial) : base(serial) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), 3))
            {
                from.SendMessage("You are too far away to read the board.");
                return;
            }

            if (!(from is PlayerMobile))
                return;

            from.SendGump(new BountyBoardGump(from, this));
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
    // BountyBoardGump
    // Shows:
    //   - Active quest (if any): progress, Abandon, Turn In (gather only)
    //   - Available quests (if no active): list with Accept buttons
    // ============================================================

    public class BountyBoardGump : Gump
    {
        private readonly Mobile      _from;
        private readonly BountyBoard _board;

        private const int W    = 430;
        private const int PadX = 18;

        // Button IDs
        private const int BTN_CLOSE   = 0;
        private const int BTN_ABANDON = 100;
        private const int BTN_TURNIN  = 200;
        // 1-N: accept quest by index

        public BountyBoardGump(Mobile from, BountyBoard board) : base(50, 40)
        {
            _from  = from;
            _board = board;

            PlayerQuestState active = QuestFactory.GetActiveQuestState(from);
            List<FBQuest>    quests = QuestFactory.BoardQuests; // snapshot of live board

            // Calculate gump height
            int activeRows  = active != null ? (active.Quest.Type == QuestType.Gather ? 4 : 3) : 0;
            int questRows   = active == null ? quests.Count : 0;
            int h = 55                         // header
                  + activeRows * 24            // active quest section
                  + (active != null ? 12 : 0)  // divider gap
                  + questRows * 58             // quest list rows
                  + 20;                        // bottom padding

            AddBackground(0, 0, W, h, 9270);
            AddAlphaRegion(2, 2, W - 4, h - 4);

            // Header
            AddLabel(PadX, 12, 0x4AA, "Bounty Board");
            AddImageTiled(PadX, 32, W - PadX * 2, 1, 9264);

            int y = 40;

            if (active != null)
            {
                // Active quest display
                FBQuest q = active.Quest;
                AddLabel(PadX, y, 0x35, $"Active: {q.Title}");
                y += 22;

                string progressText = q.Type == QuestType.Hunt
                    ? $"Kills: {active.Progress} / {q.KillsRequired}"
                    : $"Items: {active.Progress} / {q.ItemAmount} {q.ItemType}";

                AddLabel(PadX + 6, y, 1153, progressText);
                y += 22;

                // Abandon button
                AddButton(PadX, y, 4005, 4007, BTN_ABANDON, GumpButtonType.Reply, 0);
                AddLabel(PadX + 35, y, 0x22, "Abandon Quest");
                y += 22;

                // Turn-in button (gather only)
                if (q.Type == QuestType.Gather)
                {
                    AddButton(PadX, y, 4005, 4007, BTN_TURNIN, GumpButtonType.Reply, 0);
                    AddLabel(PadX + 35, y, 0x35, $"Turn In ({q.ItemType} x{q.ItemAmount})");
                    y += 22;
                }

                y += 6;
                AddImageTiled(PadX, y, W - PadX * 2, 1, 9264);
                y += 8;
                AddLabel(PadX, y, 2119, "Complete your current quest before accepting another.");
            }
            else if (quests.Count == 0)
            {
                AddLabel(PadX, y, 2119, "The board is empty. Check back soon — contracts refresh every 30 minutes.");
            }
            else
            {
                // Quest list — grouped with tier badges
                for (int i = 0; i < quests.Count; i++)
                {
                    FBQuest q = quests[i];

                    // Tier badge colour and label
                    string tierLabel;
                    int    tierHue;
                    switch (q.Tier)
                    {
                        case QuestTier.Uncommon:  tierLabel = "[Uncommon]";  tierHue = 0x44;  break; // green
                        case QuestTier.Rare:      tierLabel = "[Rare]";      tierHue = 0x4AA; break; // blue
                        case QuestTier.Legendary: tierLabel = "[Legendary]"; tierHue = 0x22;  break; // orange
                        default:                  tierLabel = "[Common]";    tierHue = 0x9C2; break; // grey
                    }

                    int    typeHue = q.Type == QuestType.Hunt ? 0x4AA : 0x35;
                    string typeTag = q.Type == QuestType.Hunt ? "[Hunt]" : "[Gather]";
                    string goal    = q.Type == QuestType.Hunt
                        ? $"Kill {q.KillsRequired}"
                        : $"Deliver {q.ItemAmount} {q.ItemType}";
                    string desc    = q.Description.Length > 52
                        ? q.Description.Substring(0, 52) + "..."
                        : q.Description;
                    string reward  = $"{q.RewardGold}gp  +{q.RepAmount} {q.RepGuild} rep";

                    // Tier + type + title on one line
                    AddLabel(PadX,      y, tierHue, tierLabel);
                    AddLabel(PadX + 80, y, typeHue, typeTag);
                    AddLabel(PadX + 130, y, 1153,  q.Title);
                    y += 16;

                    AddLabel(PadX + 6, y, 2119, desc);
                    y += 14;
                    AddLabel(PadX + 6, y, 2119, $"{goal}  |  Reward: {reward}");
                    y += 14;

                    // Accept button — stores index into the snapshot; AcceptQuest validates by Id
                    AddButton(PadX + 6, y, 4005, 4007, i + 1, GumpButtonType.Reply, 0);
                    AddLabel(PadX + 42, y, 0x35, "Accept");
                    y += 20;
                }
            }
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (_board == null || _board.Deleted) return;
            if (!(_from is PlayerMobile)) return;

            int btn = info.ButtonID;
            if (btn == BTN_CLOSE) return;

            if (btn == BTN_ABANDON)
            {
                QuestFactory.AbandonQuest(_from);
                _from.SendGump(new BountyBoardGump(_from, _board));
                return;
            }

            if (btn == BTN_TURNIN)
            {
                QuestFactory.CheckGatherProgress(_from);
                // Reopen board to reflect new state
                _from.SendGump(new BountyBoardGump(_from, _board));
                return;
            }

            // Accept quest by index into the board snapshot (buttons 1..N)
            int idx = btn - 1;
            var board = QuestFactory.BoardQuests; // fresh snapshot for Id lookup
            if (idx >= 0 && idx < board.Count)
            {
                QuestFactory.AcceptQuest(_from, board[idx].Id);
                _from.SendGump(new BountyBoardGump(_from, _board));
            }
        }
    }
}
