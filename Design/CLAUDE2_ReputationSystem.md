# Claude 2 Work Instruction — ReputationSystem

## Goal
Build `Scripts/Custom/ReputationSystem.cs` — a static system that tracks each
player's standing with each of the 12 Forsaken Britannia guilds, persists it to
disk, fires events when standing changes, and exposes GM commands to inspect and
edit standing. Also build a companion `Scripts/Custom/ReputationGump.cs` that
shows a player their full standing table.

---

## Files to create

| File | Purpose |
|------|---------|
| `Scripts/Custom/ReputationSystem.cs` | Core system — data, persistence, hooks, commands |
| `Scripts/Custom/ReputationGump.cs`   | Player-facing gump showing all 12 guild standings |

Do **not** modify any existing file except to verify compilation.

---

## Guild names — use these exact strings everywhere

```csharp
public static class Guilds
{
    public const string Wanderers        = "The Wanderers";
    public const string CraftsmenLeague  = "The Craftsmen's League";
    public const string ShadowHand       = "The Shadow Hand";
    public const string IronCompany      = "Iron Company";
    public const string ArcaneBrotherhood = "The Arcane Brotherhood";
    public const string SilverWolves     = "The Silver Wolves";
    public const string PaladinOrder     = "The Paladin Order";
    public const string DeadWatchers     = "The Dead Watchers";
    public const string DreadHunters     = "The Dread Hunters";
    public const string BloodPact        = "Blood Pact";
    public const string TheVoid          = "The Void";
    public const string Shadowblade      = "Shadowblade";

    public static readonly string[] All = {
        Wanderers, CraftsmenLeague, ShadowHand, IronCompany,
        ArcaneBrotherhood, SilverWolves, PaladinOrder, DeadWatchers,
        DreadHunters, BloodPact, TheVoid, Shadowblade
    };
}
```

---

## Standing tiers

```
< 0      Hostile  — guild is actively hostile toward you
0–99     Neutral  — no relationship (default for everyone)
100–299  Known    — guild acknowledges you
300–599  Trusted  — guild will assist you
600+     Allied   — guild actively defends you
```

```csharp
public enum StandingTier { Hostile, Neutral, Known, Trusted, Allied }

public static StandingTier GetTier(int standing)
{
    if (standing < 0)   return StandingTier.Hostile;
    if (standing < 100) return StandingTier.Neutral;
    if (standing < 300) return StandingTier.Known;
    if (standing < 600) return StandingTier.Trusted;
    return StandingTier.Allied;
}
```

---

## Storage

```
static Dictionary<Serial, Dictionary<string, int>> _standings
```

- Key = player Serial, value = dict of guild name → standing int.
- Missing entry = 0 (Neutral). Never store 0 explicitly — remove the key.
- Persist to `Saves/Misc/ReputationSystem.bin` using the same
  `Persistence.Serialize / Persistence.Deserialize` pattern as HunterSystem.cs.
  Look at `Scripts/Custom/HunterSystem.cs` for the exact pattern.
- Hook `EventSink.WorldSave` for saving. Call load inside `Initialize()` using
  `Persistence.Deserialize`.

Serialisation version starts at **0**.

---

## Public API

```csharp
// Get current standing (returns 0 if no record)
public static int GetStanding(Mobile from, string guild)

// Get tier for current standing
public static StandingTier GetTier(Mobile from, string guild)

// Add delta to standing (clamps to -500 min, no explicit max)
// Fires FBEventBus.Fire_ReputationChanged after change
// Sends a coloured message to the player describing the change
public static void AddStanding(Mobile from, string guild, int delta)

// Set standing directly (GM tool only)
public static void SetStanding(Mobile from, string guild, int value)
```

---

## Standing change rules — implement all of these

### 1. Real player kills a real player (EventSink.Killed)
Hook `EventSink.Killed`. Check that both killer and victim are `PlayerMobile`
and neither is staff.

```
-20  SilverWolves
-15  PaladinOrder
```

### 2. Player kills a PoolPK (FBEventBus.PoolPKKilled)
Hook `FBEventBus.PoolPKKilled`. Apply to the killer mobile (arg 2).
Only apply if killer is a PlayerMobile.

```
+5   SilverWolves
+5   PaladinOrder
```

### 3. Player kills a SimPlayer from Blood Pact (FBEventBus.PlayerKilledSimPlayer)
Hook `FBEventBus.PlayerKilledSimPlayer`. Check the victim's guild name
(SimPlayer isn't built yet — detect by checking if victim is a `BaseCreature`
with `victim.Title == "Blood Pact"` OR if it has a property/field `GuildName`
equal to `Guilds.BloodPact`; use a helper method `GetSimGuild(Mobile)` that
tries both and returns null if not a SimPlayer). Only apply if killer is PlayerMobile.

```
+10  SilverWolves
+15  PaladinOrder
```

### 4. Player kills a SimPlayer from The Void (FBEventBus.PlayerKilledSimPlayer)
Same hook, different guild check.

```
+10  ArcaneBrotherhood
+10  DreadHunters
```

### 5. Player kills a SimPlayer from Shadowblade (FBEventBus.PlayerKilledSimPlayer)
```
+10  PaladinOrder
+5   SilverWolves
```

### 6. SimPlayer kills a real player (FBEventBus.SimPlayerKilledPlayer)
Hook `FBEventBus.SimPlayerKilledPlayer`. Apply to the victim (they lost standing
by being killed, implying they were somewhere they shouldn't be).
Only apply if victim is PlayerMobile. Apply based on killer's guild.

Blood Pact kills player:
```
No reputation change — Blood Pact killing you is just part of the world
```
(Leave this handler as a stub, commented, for future use.)

### 7. Champion spawn assist — STUB only
Leave a commented-out stub:
```csharp
// FBEventBus.ChampionSpawnCompleted — +15 IronCompany for nearby players
// Not yet implemented — requires SimPlayer system
```

---

## Standing change message to player

When `AddStanding` is called and the player is online, send:

```csharp
if (delta > 0)
    from.SendMessage(0x3B2, $"Your standing with {guild} has improved. (+{delta})");
else
    from.SendMessage(0x22, $"Your standing with {guild} has decreased. ({delta})");
```

---

## GM commands

### `[repcheck`
Usage: `[repcheck` then target a player.  
Shows all 12 guilds and the targeted player's standing with each, plus tier name.

```
[repcheck — target a player to view their full standing table.
Silver Wolves: 55 (Neutral)
Paladin Order: 120 (Known)
...
```

### `[repset`
Usage: `[repset GuildName Value`  
Sets the **GM's own** standing with the named guild to the given value, for testing.
Example: `[repset "Silver Wolves" 300`

---

## Initialize() pattern

```csharp
public static void Initialize()
{
    // Load persisted data
    LoadData();

    // EventSink hooks
    EventSink.WorldSave += OnWorldSave;
    EventSink.Killed    += OnKilled;

    // FBEventBus hooks
    FBEventBus.PlayerKilledSimPlayer += OnPlayerKilledSimPlayer;
    FBEventBus.SimPlayerKilledPlayer += OnSimPlayerKilledPlayer;
    FBEventBus.PoolPKKilled          += OnPoolPKKilled;

    // GM commands
    CommandSystem.Register("repcheck", AccessLevel.GameMaster, OnRepCheck);
    CommandSystem.Register("repset",   AccessLevel.GameMaster, OnRepSet);
}
```

---

## ReputationGump.cs

**File:** `Scripts/Custom/ReputationGump.cs`  
**Namespace:** `Server.Gumps`  
**Command:** `[repgump` — AccessLevel.Player, opens for self only  
**Auto-opens:** Do NOT hook to paperdoll — standalone command only

Layout: single column, 300 × 370 px, dark background (9270).

Show all 12 guilds in Guilds.All order. For each row:
- Guild name (label, silver hue 2100)
- Standing number + tier name
- Hue for standing value:
  - Allied  (600+): green (63)
  - Trusted (300+): bright blue (1154)
  - Known   (100+): white (1153)
  - Neutral   (0+): grey (2100)
  - Hostile   (<0): red (33)

Title bar: "Guild Standings" in gold (1258).

No auto-refresh needed — static snapshot is fine. Include a [Refresh] button
(ButtonID=1) that reopens the gump.

---

## Key patterns to follow

### Persistence — copy this pattern from HunterSystem.cs exactly:

```csharp
private static readonly string SavePath = Path.Combine("Saves/Misc", "ReputationSystem.bin");

private static void OnWorldSave(WorldSaveEventArgs e)
{
    Persistence.Serialize(SavePath, writer =>
    {
        writer.Write(0); // version
        writer.Write(_standings.Count);
        foreach (var kvp in _standings)
        {
            writer.Write(kvp.Key); // Serial
            writer.Write(kvp.Value.Count);
            foreach (var rep in kvp.Value)
            {
                writer.Write(rep.Key);   // guild name string
                writer.Write(rep.Value); // standing int
            }
        }
    });
}

private static void LoadData()
{
    if (!File.Exists(SavePath)) return;
    Persistence.Deserialize(SavePath, reader =>
    {
        int version = reader.ReadInt();
        int count   = reader.ReadInt();
        for (int i = 0; i < count; i++)
        {
            Serial serial  = reader.ReadInt();
            int    repCount = reader.ReadInt();
            var    dict    = new Dictionary<string, int>();
            for (int j = 0; j < repCount; j++)
            {
                string guild   = reader.ReadString();
                int    standing = reader.ReadInt();
                if (standing != 0)
                    dict[guild] = standing;
            }
            if (dict.Count > 0)
                _standings[serial] = dict;
        }
    });
}
```

### Namespace
Use `Server.Custom` for ReputationSystem, `Server.Gumps` for ReputationGump.

### Using directives needed
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Commands;
using Server.Custom;   // for FBEventBus
using Server.Mobiles;
using Server.Network;
using Server.Targeting;
```

---

## Things NOT to do
- Do not modify PlayerMobile.cs
- Do not modify FBEventBus.cs
- Do not create any other files
- Do not implement SimPlayer logic — the hooks for SimPlayer events should be
  wired but guard with null/type checks since SimPlayer class doesn't exist yet
- Do not add reputation to the CharacterStatsGump — that comes later

---

## Verification
After writing both files, mentally trace through:
1. Player kills another real player → EventSink.Killed fires → SilverWolves -20, PaladinOrder -15
2. Player kills a PoolPK → PoolPKKilled fires → SilverWolves +5, PaladinOrder +5
3. `[worldsave` → bin file written
4. Server restart → bin file loaded, standings restored
5. `[repcheck` → target player → all 12 lines shown
6. `[repgump` → gump opens showing all tiers with correct colours
