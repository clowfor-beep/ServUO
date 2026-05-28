# Claude2 Work Instruction — Reputation Tier Benefits
Date: 2026-05-28
Branch: claude2/rep-tier-benefits

---

## Context files to read (in this order)

1. `Design/COWORK_HANDOVER.md`                          ← always first
2. `Design/QuestSystem_DesignDoc.txt`                   ← quest reward philosophy, tier definitions
3. `Scripts/Custom/ReputationSystem.cs`                 ← standing thresholds, public API, tier enum
4. `Scripts/Custom/FBEventBus.cs`                       ← ReputationChanged event signature
5. `Scripts/Custom/GuildSash.cs`                        ← GuildSash.For() factory, SashTier enum
6. `Scripts/Custom/QuestFactory.cs`                     ← BountyBoardGump, CompleteQuest, QuestTier enum

---

## Task

Create a new file `Scripts/Custom/RepTierBenefits.cs` that:

1. **Fires tier-up messages and sash awards** when a player's reputation with a guild crosses a threshold for the first time.
2. **Gates quest tiers on the Bounty Board** so Rare quests require Known standing and Legendary quests require Trusted standing — showing locked entries with a clear message rather than hiding them.
3. **Adds a `[myrep` player command** showing a gump with the player's standing toward every guild.

---

## Section A — Tier-up Rewards (`RepTierBenefits.cs`)

### Tier thresholds (from ReputationSystem.cs)

```
Hostile  < 0
Neutral   0 – 99
Known   100 – 299
Trusted 300 – 599
Allied  600+
```

### Initialize() hook

```csharp
public static void Initialize()
{
    FBEventBus.ReputationChanged += OnRepChanged;
    CommandSystem.Register("myrep", AccessLevel.Player, OnMyRepCommand);
}
```

### OnRepChanged logic

The event signature is `Action<Mobile, string, int>` — parameters are **(Mobile player, string guildName, int delta)**.
The event fires **after** standing has already been updated. To get the old tier:

```csharp
int newStanding = ReputationSystem.GetStanding(player, guildName);
int oldStanding = newStanding - delta;
ReputationTier oldTier = ReputationSystem.GetTier(oldStanding);
ReputationTier newTier = ReputationSystem.GetTier(newStanding);
```

Only act when `newTier > oldTier` (tier-up, not tier-down). Use a switch on `newTier`:

| newTier | Action |
|---------|--------|
| Known   | Send congratulatory message listing Known benefits (see below). No item. |
| Trusted | Send congratulatory message. Award `GuildSash.For(guildName, SashTier.Standard)` if player does NOT already have one for this guild in their backpack or equipment. |
| Allied  | Send congratulatory message. Award `GuildSash.For(guildName, SashTier.Refined)` if player does NOT already have a Refined or Exalted sash for this guild. |

**Messages** — use hue `0x53` (gold) for tier-up messages, sent to the player's client:

- Known: `"You are now Known to the {guildName}. Rare bounty quests are now available to you."`
- Trusted: `"You are now Trusted by the {guildName}. Legendary bounty quests are now available. You receive a guild sash as a mark of trust."` (send BEFORE or AFTER dropping item — doesn't matter, but include item drop)
- Allied: `"You are now Allied with the {guildName} — the highest honour. Your sash has been upgraded."` (include refined sash drop)

**Duplicate sash check** — scan `player.Backpack` (and `player.Items` for equipped layer) for any `GuildSash` whose `GuildAffiliation == guildName`. For Trusted, skip if any tier exists. For Allied, skip if tier is already Refined or Exalted.

Drop item with `player.Backpack.DropItem(sash)` — only if backpack is not null.

### No persistence needed

`RepTierBenefits` is stateless (no fields to save). It just hooks the event and reacts. No `Serialize`/`Deserialize` needed. No `Serial` constructor needed. It is a `static class`.

---

## Section B — Quest Gating in BountyBoardGump (`QuestFactory.cs`, EDIT)

### Rule

- **Rare** quests: require standing `>= Known` (i.e., `ReputationSystem.GetStanding(from, q.RepGuild) >= 100`). If below threshold, show the quest entry but replace the Accept button with a locked indicator.
- **Legendary** quests: require `>= Trusted` (standing `>= 300`). Same — show but lock.
- **Common** quests: always available, no change.

### How to render locked entries

In `BountyBoardGump`, inside the quest list drawing loop, after filtering by selected guild, for each quest `q`:

```csharp
bool locked = false;
string lockReason = null;

if (q.Tier == QuestTier.Rare && ReputationSystem.GetStanding(_from, q.RepGuild) < 100)
{
    locked    = true;
    lockReason = "[Known required]";
}
else if (q.Tier == QuestTier.Legendary && ReputationSystem.GetStanding(_from, q.RepGuild) < 300)
{
    locked    = true;
    lockReason = "[Trusted required]";
}
```

When `locked == true`:
- Draw the quest title and reward line as normal (grey hue 2119 instead of white 0xFFFFFF if you like).
- Where the Accept button normally goes, use `AddLabel` with the `lockReason` string in hue 33 (red). No button.

When `locked == false`: normal Accept button as today.

Do NOT hide locked quests — the player should see what's coming and be motivated to earn rep.

---

## Section C — `[myrep` Player Gump (`RepTierBenefits.cs`)

A simple read-only gump showing the player's standing with all 12 guilds.

### Layout (approximate)

```
┌─────────────────────────────────────────────────────┐
│  Your Guild Standing                          [Close]│
├─────────────────────────────────────────────────────┤
│  The Wanderers         Trusted   (320)               │
│  Craftsmen's League    Known     (150)               │
│  Shadow Hand           Neutral   (0)                 │
│  ...                                                  │
└─────────────────────────────────────────────────────┘
```

- Gump size: 420 × 350 (or however tall 12 rows need — use 22px per row + header)
- Title: "Your Guild Standing" in bold/highlight
- For each guild in `FBGuilds.All`: show guild name, tier name, and standing in parentheses
- Tier hues:
  - Allied  → 0x35 (green)
  - Trusted → 0x53 (gold)
  - Known   → 0x3B2 (light blue)
  - Neutral → 0xFFFFFF (white)
  - Hostile → 33 (red)

Do NOT use `AddImageTiledPart` — it does not exist in this build. Use `AddBackground`, `AddAlphaRegion`, `AddLabel`, and `AddButton` only.

### Command handler

```csharp
private static void OnMyRepCommand(CommandEventArgs e)
{
    e.Mobile.SendGump(new MyRepGump(e.Mobile));
}
```

`MyRepGump` is a `private class` inside `RepTierBenefits` (or a nested class — either fine).

---

## Files to create or edit

- `Scripts/Custom/RepTierBenefits.cs`   **(CREATE)**
- `Scripts/Custom/QuestFactory.cs`      **(EDIT — Section B only)**

---

## Must NOT touch

- `Scripts/Custom/ReputationSystem.cs`  ← read-only reference
- `Scripts/Custom/FBEventBus.cs`        ← read-only reference
- `Scripts/Custom/GuildSash.cs`         ← read-only reference
- `Scripts/Custom/HunterSystem.cs`
- Any file not listed above

---

## Interfaces to respect

```csharp
// ReputationSystem — public API
int   ReputationSystem.GetStanding(Mobile player, string guildName)
ReputationTier ReputationSystem.GetTier(int standing)   // or GetTier(Mobile, string)
void  ReputationSystem.AddStanding(Mobile player, string guildName, int delta)

// FBEventBus
public static event Action<Mobile, string, int> ReputationChanged;
// parameters: (Mobile player, string guildName, int delta)
// fires AFTER standing is already updated

// GuildSash
GuildSash GuildSash.For(string guildName, SashTier tier)  // returns null if unknown guild
public enum SashTier { Standard = 0, Refined = 1, Exalted = 2 }
string GuildSash.GuildAffiliation  // [CommandProperty] on the instance

// FBGuilds — all guild name constants
string[] FBGuilds.All  // iterate for gump rows

// QuestFactory — existing types
enum QuestTier { Common, Rare, Legendary }
class BoardQuest { public QuestTier Tier; public string RepGuild; ... }
```

Check the actual field/property names in QuestFactory.cs when you read it — use exactly what's there.

---

## Definition of done

- [ ] `RepTierBenefits.cs` compiles with no errors
- [ ] Namespace is `Server.Custom`
- [ ] `Initialize()` registers both the event hook and the `[myrep` command
- [ ] Tier-up messages fire only on tier-up (not tier-down, not same-tier delta)
- [ ] Known tier-up: message only, no sash
- [ ] Trusted tier-up: Standard sash awarded if no sash already held for that guild
- [ ] Allied tier-up: Refined sash awarded if player has no Refined/Exalted sash for that guild
- [ ] Sash drop uses `player.Backpack.DropItem()` with null guard
- [ ] `[myrep` gump opens for any player, shows all 12 guilds with correct tier hues
- [ ] `BountyBoardGump` in QuestFactory.cs: Rare quests locked below Known, Legendary locked below Trusted, locked entries still visible with red label instead of Accept button
- [ ] No new Serialize/Deserialize needed (stateless system)
- [ ] `AddImageTiledPart` NOT used anywhere

---

## Done signal

Commit this file as your **final commit** on the branch:

```
ToDo Cowork/DONE - Claude2 - RepTierBenefits - 2026-05-28.md
```

Contents of that file — one line:
```
Done: RepTierBenefits. Files created/edited: Scripts/Custom/RepTierBenefits.cs, Scripts/Custom/QuestFactory.cs.
```
