# Claude2 Work Instruction — QuestFactory Phase 1
Date: 2026-05-27
Branch: claude2/quest-factory-phase1

## Context files to read (in this order)
1. `Design/COWORK_HANDOVER.md`                              ← always first
2. `Design/QuestSystem_DesignDoc.txt`                       ← full quest system spec
3. `Design/GuildSystem_FullDesignDoc.txt`                   ← SimGuild activities that generate quests
4. `Scripts/Custom/QuestTrackerHUD.cs`                      ← HUD API you will drive
5. `Scripts/Custom/FBEventBus.cs`                           ← events you hook
6. `Scripts/Custom/ReputationSystem.cs`                     ← reputation API you call
7. `Scripts/Custom/HunterGuildmaster.cs`                    ← example of a gump + NPC pattern to follow

## Task

Build **QuestFactory.cs** — Phase 1 only (see QuestSystem_DesignDoc.txt "PHASE 1 — Foundation").

Phase 1 scope:
- **Hunt quests** — bounty board generates "kill this specific creature/wanted NPC" objectives
- **Gather quests** — Craftsmen's League NPC requests specific resources
- Basic **Reputation reward** on quest completion (call `ReputationSystem.AdjustStanding`)
- Wire to **QuestTrackerHUD** so active quest shows on the player's HUD
- Physical **BountyBoard item** placed in Britain, Trinsic, Minoc that players double-click to browse quests

Do NOT implement: Clear quests, Explore quests, Epic quests, dynamic SimGuild-driven generation.
Those are Phase 2+.

---

## Architecture

### QuestFactory.cs — one file, three logical sections

**Section 1 — Data model**

```csharp
public enum QuestType { Hunt, Gather }

public class FBQuest
{
    public string       Id;           // unique string, e.g. "hunt_bloodpact_001"
    public QuestType    Type;
    public string       Title;        // shown on HUD
    public string       GiverGuild;   // FBGuilds constant — who issued it
    public string       Description;  // one sentence shown on board
    public string       RewardGold;   // e.g. "500-1000"
    public int          RepGuild;     // reputation guild, uses FBGuilds constants
    public int          RepAmount;    // reputation adjustment on completion

    // Hunt quest fields
    public string       TargetMobileType;  // simple type name string, e.g. "BloodPactSimPlayer"
    public int          KillsRequired;

    // Gather quest fields
    public string       ItemType;          // simple type name string, e.g. "IronIngot"
    public int          ItemAmount;
}
```

**Section 2 — Static quest table**

Hardcode 8-10 quests in Phase 1 — do not use dynamic generation yet. Mix of Hunt and Gather.

Examples:
- Hunt: "Blood Pact Extermination" — kill 3 BloodPact members, rep reward: SilverWolves +50
- Hunt: "Void Incursion" — kill 2 Void members, rep reward: ArcaneBrotherhood +40
- Hunt: "Wanted: Dread Lord" — kill 1 WantedDreadLord (from HunterWanted.cs), reward: HunterSystem
- Gather: "Craftsmen's Ironwood" — deliver 50 Boards, rep reward: CraftsmensLeague +30
- Gather: "Iron Stockpile" — deliver 30 IronIngot, rep reward: CraftsmensLeague +25
- Hunt: "Shadow Hand Arrest" — kill 2 ShadowHand members, rep reward: SilverWolves +20
- Gather: "Field Medicine" — deliver 20 Bandage, rep reward: Wanderers +15

**Section 3 — Static manager**

```csharp
public static class QuestFactory
{
    // Per-player active quest (one quest active at a time, Phase 1)
    private static readonly Dictionary<Mobile, ActiveQuest> _activeQuests;

    public static void Initialize()  // hooked automatically at startup
    {
        EventSink.Login += OnLogin;
        EventSink.Death += OnMobDeath;
        // NOTE: item delivery checked via BountyBoard gump OnResponse
    }

    public static bool HasActiveQuest(Mobile m);
    public static ActiveQuest GetActiveQuest(Mobile m);
    public static bool AcceptQuest(Mobile m, string questId);  // called from board gump
    public static void AbandonQuest(Mobile m);                 // called from board gump
    public static void CheckHuntProgress(Mobile killer, Mobile victim);  // called from OnMobDeath
    public static void CheckGatherProgress(Mobile player, Item item, int amount); // called from board
    public static void CompleteQuest(Mobile m);                // called internally
}

public class ActiveQuest
{
    public FBQuest Quest;
    public int     Progress;  // kills so far / items gathered
    public DateTime Accepted;
}
```

**Section 4 — BountyBoard item**

```csharp
[Constructable]
public class BountyBoard : Item
```

- Place via `[add BountyBoard` — already has positions in FBZones (`FBZones.BountyBoard_Britain_Bank`, `FBZones.BountyBoard_Trinsic`, `FBZones.BountyBoard_Minoc`)
- Double-click opens `BountyBoardGump`
- Gump shows: list of available quests (title, description, reward), Accept button, current active quest if any with Abandon button
- Accept calls `QuestFactory.AcceptQuest(from, questId)` → calls `QuestTrackerHUD.SetQuest()`
- On accept: HUD shows quest title and one objective line, e.g. "Kill Blood Pact: 0 / 3"

---

## Quest completion flow

**Hunt quest:**
1. Player accepts quest — `ActiveQuest` stored, `QuestTrackerHUD.SetQuest()` called
2. On any mobile death (`EventSink.Death`) — `CheckHuntProgress(killer, victim)` runs
   - Checks if `victim.GetType().Name` matches `quest.TargetMobileType`
   - Increments progress
   - Calls `QuestTrackerHUD.UpdateObjective()`
   - If `progress >= KillsRequired` → `CompleteQuest()`
3. `CompleteQuest()`:
   - Adds gold to player's backpack (`PackItem(new Gold(Utility.RandomMinMax(min, max)))`)
   - Calls `ReputationSystem.AdjustStanding(player, guild, amount)`
   - Calls `QuestTrackerHUD.ClearQuest(player)`
   - Sends completion message (green text)

**Gather quest:**
1. Player accepts quest — same as hunt
2. Player returns to the BountyBoard and clicks "Turn in" in the gump
   - Gump checks player's pack for the item type and required amount
   - If found: removes items, calls `CompleteQuest()`
   - If not found: sends red message "You do not have the required items"

---

## HUD integration

```csharp
// On accept:
QuestTrackerHUD.SetQuest(m, quest.Title, new List<QuestObjective> {
    new QuestObjective(objectiveLabel, 0, quest.KillsRequired)  // or ItemAmount
});

// On progress update:
QuestTrackerHUD.UpdateObjective(m, objectiveLabel, current, max);

// On complete or abandon:
QuestTrackerHUD.ClearQuest(m);
```

---

## Reputation integration

```csharp
// On completion:
ReputationSystem.AdjustStanding(player, quest.RepGuild, quest.RepAmount);
```

Check `ReputationSystem.cs` for the exact method signature before calling.

---

## Serialization

`BountyBoard` is a persistent `Item` and needs `Serialize`/`Deserialize` (version 0, no custom fields — all state is in `QuestFactory` statics).

`ActiveQuest` state in `QuestFactory._activeQuests` is NOT persisted to disk in Phase 1 (quests reset on server restart — this is acceptable for Phase 1). Do not try to persist the dictionary.

---

## File(s) to create or edit

- `Scripts/Custom/QuestFactory.cs`   (CREATE — single file containing all sections above)

## Must NOT touch
- `Scripts/Custom/QuestTrackerHUD.cs`     (use its public API only — do not modify)
- `Scripts/Custom/ReputationSystem.cs`    (use its public API only)
- `Scripts/Custom/FBEventBus.cs`          (read events from it, do not add new events)
- `Scripts/Custom/HunterSystem.cs`        (do not touch)
- Any file in `Server/` or vanilla `Scripts/`

## Interfaces to respect

```csharp
// QuestTrackerHUD API (read QuestTrackerHUD.cs for exact signatures):
QuestTrackerHUD.SetQuest(Mobile m, string title, List<QuestObjective> objectives)
QuestTrackerHUD.UpdateObjective(Mobile m, string label, int current, int max)
QuestTrackerHUD.ClearQuest(Mobile m)

// ReputationSystem API (read ReputationSystem.cs for exact signature):
ReputationSystem.AdjustStanding(Mobile m, string guild, int delta)

// FBGuilds constants (from ReputationSystem.cs):
FBGuilds.Wanderers, FBGuilds.CraftsmenLeague, FBGuilds.SilverWolves, etc.

// FBZones board locations (from FBZones.cs):
FBZones.BountyBoard_Britain_Bank
FBZones.BountyBoard_Trinsic
FBZones.BountyBoard_Minoc
```

## Definition of done

- [ ] `QuestFactory.cs` compiles with no errors
- [ ] `BountyBoard` item exists, has `[Constructable]`, Serialize/Deserialize versioned at 0
- [ ] Double-clicking BountyBoard opens gump showing at least 5 quests
- [ ] Player can accept a Hunt quest — HUD shows title and kill counter
- [ ] Killing a matching mobile increments counter and updates HUD
- [ ] Quest completes correctly — gold to pack, reputation adjusted, HUD cleared
- [ ] Player can accept a Gather quest — HUD shows title and item counter
- [ ] Turn-in at board works — items consumed, quest completes
- [ ] Player cannot hold two quests at once (second accept blocked with message)
- [ ] Abandon button works — clears HUD, allows accepting new quest
- [ ] Follows namespace Server.Custom convention
- [ ] `Initialize()` static method present (wires EventSink.Death)
- [ ] No compile errors — check all SkillName, Item, Mobile references carefully

## Done signal
Commit this file as your final commit:
  `ToDo Cowork/DONE - Claude2 - QuestFactory Phase 1 - 2026-05-27.md`

Contents: "Done: QuestFactory Phase 1. Files created: QuestFactory.cs."
