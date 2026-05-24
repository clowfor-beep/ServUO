# Claude2 Work Instruction — FBZones and FBEventBus
Date: 2026-05-24
Branch: claude2/fbzones-fbeventbus

---

## Context files to read (in this order)

1. `Design/COWORK_HANDOVER.md`                        ← always first — full project context
2. `Design/WorldSpawn_DesignDoc.txt`                  ← authoritative world spawn architecture (read this second)
3. `Design/SystemArchitecture_DesignDoc.txt`          ← module specs and dependency graph
4. `Scripts/Custom/PKEncounterSystem.cs`              ← example of how zones/rects are used today (reference only — do NOT modify)
5. `Scripts/Custom/CooldownSystem.cs`                 ← example of a clean static system class with Initialize()

---

## Task

Create two new files from scratch:

### File 1 — FBZones.cs

Single source of truth for all world coordinates used by Forsaken Britannia systems.
This replaces all hardcoded `Point3D`, `Rectangle2D`, and `Point2D` values scattered across other files.

**What to build:**

A static class `FBZones` in namespace `Server.Custom` containing:

1. **`SpawnZone` enum** — named identifiers for every significant zone.
   Start with the zones already used by `PKEncounterSystem.cs` plus the guild home areas.
   Naming convention: `SpawnZone.Destard_L1`, `SpawnZone.Britain_Graveyard`, `SpawnZone.BloodPact_Despise`, etc.

2. **Zone rectangles** — `static readonly Rectangle2D[]` per named zone.
   Pull the actual Rectangle2D values from `PKEncounterSystem.cs` (the `AllZones` list has them — extract them into FBZones).
   Use descriptive field names: `public static readonly Rectangle2D[] Destard_L1 = { ... };`

3. **Patrol waypoints** — `static readonly Point2D[][]` per zone, defining patrol courses for PoolPKs.
   Each outer array is a zone, each inner array is one patrol route (a loop of waypoints).
   Start with Britain_Roads and Despise_Entrance only — other zones will be added as FBPKSpawner expands.
   Naming: `public static readonly Point2D[][] Britain_Roads_Waypoints = new Point2D[][] { new Point2D[] { new Point2D(x,y), ... } };`
   Use approximate waypoints for now — they will be tuned in-game later.

4. **Guild home locations** — `static readonly Point3D` for each guild's base / starting position.
   Use approximate Britain-area coordinates for now; we will pin them in-game later.
   Include all 12 guilds (see `COWORK_HANDOVER.md` Section 7 and `WorldSpawn_DesignDoc.txt` for territory mapping).
   Naming: `public static readonly Point3D Wanderers_Home = new Point3D(1442, 1693, 5);`
   Key rule from design doc: Wanderers home = Britain bank. Evil guilds home = near wilderness, never cities.

5. **Town board locations** — `static readonly Point3D` for bounty/guild bulletin boards.
   Britain bank area, Trinsic, Minoc (3 towns to start — Phase 1 only).
   Naming: `public static readonly Point3D BritainBoard = new Point3D(1439, 1695, 5);`

6. **Helper method** — `static bool IsInZone(Mobile m, SpawnZone zone)` that checks if a mobile is within any rectangle of the named zone on the correct map. Use the Map associated with each zone's rectangles.

**Important:**
- This is a pure data class. No timers, no event hooks, no `Initialize()` needed.
- Do NOT copy-paste the entire PKEncounterSystem zone list verbatim — restructure it into named fields.
- Z-range filtering is PKEncounterSystem's responsibility, not FBZones. FBZones only stores Rectangle2D footprints.
- The rectangle values from PKEncounterSystem.cs are the ground truth for now. Use them.

---

### File 2 — FBEventBus.cs

Cross-system event bus. Decouples all Forsaken Britannia systems so they never reference each other directly.

**What to build:**

A static class `FBEventBus` in namespace `Server.Custom` with C# events using `Action<>` delegates.

Implement exactly these events (signatures from the design doc):

```csharp
// Fired when a SimPlayer kills a real player
public static event Action<Mobile, Mobile> SimPlayerKilledPlayer;

// Fired when a real player kills a SimPlayer  
public static event Action<Mobile, Mobile> PlayerKilledSimPlayer;

// Fired when a SimPlayer enters a named zone
public static event Action<Mobile, SpawnZone> SimPlayerEnteredZone;

// Fired when a SimPlayer becomes active (returns from cooldown)
public static event Action<Mobile> SimPlayerActivated;

// Fired when a SimPlayer goes on cooldown (after death or logout)
public static event Action<Mobile> SimPlayerDeactivated;

// Fired when a champion spawn activates
public static event Action<object> ChampionSpawnActivated;

// Fired when a champion spawn is completed
public static event Action<object, Mobile> ChampionSpawnCompleted;

// Fired when a player's reputation with a guild changes
public static event Action<Mobile, string, int> ReputationChanged;

// Fired when a Pool PK spawns into a zone (used by BountyBoardFB)
public static event Action<Mobile, SpawnZone> PoolPKSpawned;

// Fired when a Pool PK is killed (used by BountyBoardFB + QuestFactory)
public static event Action<Mobile, Mobile, SpawnZone> PoolPKKilled;
```

Note: `SimPlayer` and `PoolPK` don't exist yet as classes, so use `Mobile` for those parameters now.
`ChampionSpawn` doesn't exist in custom namespace, use `object`. These will be tightened later.
`SpawnZone` comes from `FBZones.cs` — both files are in the same namespace so this is fine.

Also add a **safe-fire helper** for each event to avoid null checks everywhere:

```csharp
public static void Fire_SimPlayerKilledPlayer(Mobile killer, Mobile victim)
    => SimPlayerKilledPlayer?.Invoke(killer, victim);
```

Add one `Fire_*` method per event. Callers always use the Fire_ method, never invoke directly.

No `Initialize()` needed — pure event declarations + fire helpers, no startup hooks.

---

## Files to create

- `Scripts/Custom/FBZones.cs`    (CREATE)
- `Scripts/Custom/FBEventBus.cs` (CREATE)

## Must NOT touch

- `Scripts/Custom/PKEncounterSystem.cs`   ← reference only
- `Scripts/Custom/NovicePlayerKiller.cs`
- `Scripts/Custom/PlayerKillerNPCs.cs`
- `Scripts/Custom/CooldownSystem.cs`
- Any file in `Server/` or `Scripts/` outside of `Scripts/Custom/`

---

## Interfaces to respect

These are used by PKEncounterSystem today and FBZones must be compatible:
- `Map.Felucca`, `Map.Trammel`, `Map.Ilshenar`, `Map.Malas`
- `Rectangle2D` — standard ServUO struct, constructed as `new Rectangle2D(x, y, width, height)`
- `Point3D` — standard ServUO struct
- `Point2D` — standard ServUO struct
- `Mobile` — base class for all living entities

Namespace for both files: `Server.Custom`

---

## Definition of done

- [ ] `FBZones.cs` compiles with no errors (read through carefully — no missing using statements)
- [ ] `FBEventBus.cs` compiles with no errors
- [ ] `SpawnZone` enum covers all zones currently in `PKEncounterSystem.cs` (at minimum)
- [ ] Patrol waypoints defined for Britain_Roads and Despise_Entrance (approximate, tunable in-game)
- [ ] All 12 guild home locations defined in FBZones (approximate coords fine — will be tuned in-game)
- [ ] All 10 FBEventBus events present with correct signatures (8 original + PoolPKSpawned + PoolPKKilled)
- [ ] All 10 `Fire_*` helper methods present
- [ ] No hardcoded values in FBEventBus (pure event declarations)
- [ ] Follows `namespace Server.Custom` convention
- [ ] No `Initialize()` in either file (neither needs one)
- [ ] No references to SimPlayer, PoolPK, or ReputationSystem (those don't exist yet)
- [ ] `using` statements cover all referenced types

---

## Zone data reference

### Encounter zones (extract from `PKEncounterSystem.cs` `AllZones` list)

Map these to the SpawnZone enum — Rectangle2D values come directly from PKEncounterSystem.cs:

```
Britain_Graveyard
Destard_L1, Destard_L2, Destard_L3
Shame_L1, Shame_L2, Shame_L3
Deceit_L1, Deceit_L2, Deceit_L3
Despise_L1, Despise_L2, Despise_L3
Covetous_L1, Covetous_L2, Covetous_L3
Wrong_L1, Wrong_L2
Hythloth_L1, Hythloth_L2, Hythloth_L3
Ilshenar_Ankh, Ilshenar_Spectre
Malas_Labyrinth, Malas_Doom
TerMur_Underworld, TerMur_Stygian
```

Add any additional zones that are in PKEncounterSystem.cs but not listed above.

### FBPKSpawner patrol zones (add to enum — coordinates approximate, Phase 1 only)

These are road/entrance zones used by FBPKSpawner. Start with just the first two:
```
Britain_Roads          (Phase 1 — Tier 1 patrol)
Despise_Entrance       (Phase 1 — Tier 2 patrol)
Trinsic_Outskirts      (future)
Yew_Roads              (future)
Covetous_Entrance      (future)
Wrong_Entrance         (future)
Shame_Entrance         (future)
Despise_Level3         (future)
Hythloth_Deep          (future)
Deceit_Level3          (future)
```

Approximate coords for Phase 1 zones (Britain_Roads rectangle ~1300-1500, 1600-1800 Felucca; Despise entrance ~1270-1350, 1060-1120 Felucca). These will be verified and tuned in-game — use reasonable estimates now.

---

## Done signal

When complete, commit this file as your **final commit** on the branch:

```
ToDo Cowork/DONE - Claude2 - FBZones and FBEventBus - 2026-05-24.md
```

Contents of that file (one line):
```
Done: FBZones and FBEventBus. Files created: Scripts/Custom/FBZones.cs, Scripts/Custom/FBEventBus.cs.
```

Then push the branch. Master Claude will review, run integration checks, and merge.
