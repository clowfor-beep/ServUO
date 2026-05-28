# Claude2 Work Instruction — Hunter Leaderboard + World Crier
Date: 2026-05-28
Branch: claude2/hunterboard-worldcrier

---

## Context files to read (in this order)

1. `Design/COWORK_HANDOVER.md`                   ← always first
2. `Design/HunterSystemDesignDoc.txt`            ← hunter ranks, point values, broadcast design
3. `Scripts/Custom/HunterSystem.cs`              ← existing points API, rank list, active hunt records
4. `Scripts/Custom/FBEventBus.cs`                ← existing events; you will add two new ones

---

## Task overview

Two small, self-contained systems:

| System | File | Type |
|--------|------|------|
| **Hunter Leaderboard** | `Scripts/Custom/HunterBoard.cs` (CREATE) | `[hunterboard` player command → read-only gump, top 10 |
| **World Crier** | `Scripts/Custom/WorldCrier.cs` (CREATE) | Placeable NPC + two new FBEventBus events |
| **HunterSystem additions** | `Scripts/Custom/HunterSystem.cs` (EDIT) | Two new public methods exposing private data |
| **FBEventBus additions** | `Scripts/Custom/FBEventBus.cs` (EDIT) | Two new events + Fire helpers |

---

## Section A — HunterSystem.cs additions (EDIT)

Add these two public static methods. Do NOT touch anything else in this file.

### A1 — Leaderboard data

```csharp
/// <summary>
/// Returns the top <paramref name="maxCount"/> players by hunter points,
/// sorted descending. Skips serials that no longer resolve to a mobile.
/// </summary>
public static List<(string name, int points, string rank)> GetLeaderboard(int maxCount = 10)
{
    var result = new List<(string, int, string)>();

    foreach (var kvp in _points)
    {
        if (kvp.Value <= 0) continue;
        Mobile m = World.FindMobile(kvp.Key);
        string name = m != null ? m.Name : $"<Unknown #{kvp.Key}>";
        result.Add((name, kvp.Value, GetRankTitle(kvp.Value)));
    }

    result.Sort((a, b) => b.Item2.CompareTo(a.Item2));

    if (result.Count > maxCount)
        result.RemoveRange(maxCount, result.Count - maxCount);

    return result;
}
```

### A2 — Active target lists for WorldCrier

```csharp
/// <summary>Returns a snapshot of active hunter creature targets.</summary>
public static List<(string name, string location)> GetActiveHunts()
{
    PruneHunts();
    var list = new List<(string, string)>();
    foreach (HuntRecord r in _activeHunts)
        list.Add((r.Name, r.Location));
    return list;
}

/// <summary>Returns a snapshot of active wanted NPC targets.</summary>
public static List<(string name, string location)> GetActiveWanted()
{
    PruneWanted();
    var list = new List<(string, string)>();
    foreach (HuntRecord r in _activeWanted)
        list.Add((r.Name, r.Location));
    return list;
}
```

---

## Section B — FBEventBus.cs additions (EDIT)

Add the two new events and their Fire helpers. Add them after the existing `WantedNPCKilled` event block. Do NOT touch anything else.

```csharp
/// <summary>Fired when a Hunter System creature target spawns in the world.</summary>
/// <param name="creatureName">Display name of the target (without [Hunted] prefix).</param>
/// <param name="location">Human-readable dungeon/area name.</param>
public static event Action<string, string> HunterTargetSpawned;

/// <summary>Fired when a Wanted NPC bounty target spawns in the world.</summary>
/// <param name="npcName">Display name of the wanted NPC.</param>
/// <param name="location">Human-readable location string.</param>
public static event Action<string, string> WantedNPCSpawned;
```

Fire helpers (add alongside the other Fire_ methods):

```csharp
public static void Fire_HunterTargetSpawned(string creatureName, string location)
    => HunterTargetSpawned?.Invoke(creatureName, location);

public static void Fire_WantedNPCSpawned(string npcName, string location)
    => WantedNPCSpawned?.Invoke(npcName, location);
```

### Also fire these events from HunterSystem.cs

In `SpawnHunterTarget()`, after `_activeHunts.Add(record)` (and after `BroadcastHuntSpawn`), add:

```csharp
FBEventBus.Fire_HunterTargetSpawned(record.Name, entry.DungeonName);
```

In `SpawnWantedTarget()`, after the wanted NPC is added to `_activeWanted`, add the equivalent:

```csharp
FBEventBus.Fire_WantedNPCSpawned(record.Name, record.Location);
```

Read `SpawnWantedTarget()` carefully to find the right insertion point — mirror exactly what `SpawnHunterTarget()` does when it calls `BroadcastHuntSpawn`.

---

## Section C — HunterBoard.cs (CREATE)

A `[hunterboard` command that opens a read-only gump showing the top 10 hunters.

### Layout

```
┌──────────────────────────────────────────────────────┐
│         Hunter's Guild — Hall of Legends      [Close] │
├──────────────────────────────────────────────────────┤
│  #   Name                     Points    Rank          │
│  1   Arthendal               142       the Eternal Hunter │
│  2   Kayleth                  67       the Apex Predator  │
│  3   Mortis                   31       the Legendary Hunter│
│  ...                                                  │
│  (No hunters yet — be the first!)                     │
└──────────────────────────────────────────────────────┘
```

- Gump size: 480 × 320
- Background: `AddBackground(0, 0, 480, 320, 9200)`
- Title row at y=10, column headers at y=45, rows start at y=70, 22px per row
- Column layout: rank# (30px), name (180px), points (80px), rank title (rest)
- If no entries: show "No hunters yet — be the first!" centred
- Available to all players (`AccessLevel.Player`)

### Command

```csharp
public static void Initialize()
{
    CommandSystem.Register("hunterboard", AccessLevel.Player, OnCommand);
}

private static void OnCommand(CommandEventArgs e)
{
    e.Mobile.SendGump(new HunterBoardGump(e.Mobile));
}
```

`HunterBoardGump` is a private nested class inside `HunterBoard`. It calls `HunterSystem.GetLeaderboard(10)` in its constructor to populate the rows.

Namespace: `Server.Custom`. No serialization needed (stateless).

---

## Section D — WorldCrier.cs (CREATE)

Two things in this file:
1. A `WorldCrierNPC` that players double-click to hear current events
2. A static `WorldCrierSystem` that subscribes to FBEventBus events and keeps an announcement queue

### WorldCrierSystem (static class)

```csharp
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
        Enqueue($"{killerName} has slain {creature?.Name ?? "a hunted beast"}! The guild shall reward them.");
    }

    private static void OnWantedKilled(Mobile npc, Mobile killer)
    {
        string killerName = killer != null ? killer.Name : "an unknown hero";
        Enqueue($"The wanted criminal {npc?.Name ?? "a wanted foe"} has been brought to justice by {killerName}!");
    }

    public static IEnumerable<string> GetRecentAnnouncements() => _recent;
}
```

### WorldCrierNPC

A standard NPC placed in towns. Players double-click it to hear recent events. It also announces live on spawn events.

```csharp
public class WorldCrierNPC : BaseVendor   // or BaseCreature with AI_Vendor — see note below
```

**NOTE:** Use `BaseCreature` with `AIType.AI_Animal, FightMode.None` — NOT `BaseVendor` (which requires a shop). Override `OnDoubleClick` to open the crier gump instead.

Fields:
- Name = "Town Crier"
- Body = 0x190 (or 0x191 for female — pick one)
- Title = "the Herald"
- Hue = 0 (default)
- `CanSwim = false`, `CantWalk = false`
- Not tameable, not lootable
- `IsInvulnerable = true`

`OnDoubleClick(Mobile from)`:
- Opens `WorldCrierGump(from)`

### WorldCrierGump

```
┌───────────────────────────────────────────────────┐
│  Town Crier — Recent Events                [Close] │
├───────────────────────────────────────────────────┤
│  Active Hunts:                                     │
│    • a Shadow Dragon — Destard Level 2             │
│    • a Wraith Warlord — Wrong Level 1              │
│                                                    │
│  Active Wanted:                                    │
│    • Bloodthorn — near Wrong                       │
│                                                    │
│  Recent news:                                      │
│    Arthendal has slain a Shadow Dragon!            │
│    The wanted criminal Bloodthorn has been seen... │
└───────────────────────────────────────────────────┘
```

- Size: 460 × 340
- Background: `AddBackground(0, 0, 460, 340, 9200)`
- Sections: "Active Hunts", "Active Wanted", "Recent News" (last 5 announcements)
- Data sources:
  - Active hunts: `HunterSystem.GetActiveHunts()`
  - Active wanted: `HunterSystem.GetActiveWanted()`
  - News: `WorldCrierSystem.GetRecentAnnouncements()`
- If a section is empty, show "(none at this time)" in grey
- Do NOT use `AddImageTiledPart` — use `AddBackground`, `AddAlphaRegion`, `AddLabel`, `AddButton` only

### [Constructable] constructor

```csharp
[Constructable]
public WorldCrierNPC() : base(AIType.AI_Animal, FightMode.None, 10, 1, 0.2, 0.4)
{
    Name  = "Town Crier";
    Body  = 0x190;
    Title = "the Herald";
    IsInvulnerable = true;
    // Give them a visual — plain robe
    AddItem(new Robe(Utility.RandomNeutralHue()));
}
```

Serial constructor and Serialize/Deserialize are required (persistent NPC):

```csharp
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
```

`WorldCrierSystem` itself is stateless (the queue is rebuilt from live events — it resets on server restart, which is fine).

---

## Files to create or edit

| File | Action |
|------|--------|
| `Scripts/Custom/HunterBoard.cs` | CREATE |
| `Scripts/Custom/WorldCrier.cs` | CREATE |
| `Scripts/Custom/HunterSystem.cs` | EDIT — add `GetLeaderboard`, `GetActiveHunts`, `GetActiveWanted`, fire two new FBEventBus events from spawn methods |
| `Scripts/Custom/FBEventBus.cs` | EDIT — add `HunterTargetSpawned`, `WantedNPCSpawned` events and Fire helpers |

---

## Must NOT touch

- `Scripts/Custom/ReputationSystem.cs`
- `Scripts/Custom/QuestFactory.cs`
- `Scripts/Custom/GuildSash.cs`
- `Scripts/Custom/PKEncounterSystem.cs`
- Any file not listed above

---

## Definition of done

- [ ] `HunterBoard.cs` compiles — `[hunterboard` opens gump for any player
- [ ] Leaderboard shows top 10 by points, with name, points, rank title
- [ ] Empty state: "No hunters yet — be the first!" shown when `_points` is empty
- [ ] `WorldCrier.cs` compiles — `WorldCrierNPC` and `WorldCrierSystem` both present
- [ ] `WorldCrierNPC` is `[Constructable]`, has Serial constructor + Serialize/Deserialize
- [ ] `WorldCrierNPC` double-click opens `WorldCrierGump`
- [ ] `WorldCrierGump` shows active hunts, active wanted, and recent news (all three sections)
- [ ] `WorldCrierSystem.Initialize()` hooks all four FBEventBus events
- [ ] `HunterSystem.cs` additions compile without breaking existing behaviour
- [ ] `FBEventBus.cs` additions compile; both new events fired from `SpawnHunterTarget` and `SpawnWantedTarget`
- [ ] Namespaces: `HunterBoard` → `Server.Custom`, `WorldCrier` → `Server.Custom`
- [ ] `AddImageTiledPart` NOT used anywhere
- [ ] No other files modified

---

## Done signal

Commit this file as your **final commit** on the branch:

```
ToDo Cowork/DONE - Claude2 - HunterBoard and WorldCrier - 2026-05-28.md
```

Contents — one line:
```
Done: HunterBoard and WorldCrier. Files created/edited: Scripts/Custom/HunterBoard.cs, Scripts/Custom/WorldCrier.cs, Scripts/Custom/HunterSystem.cs, Scripts/Custom/FBEventBus.cs.
```
