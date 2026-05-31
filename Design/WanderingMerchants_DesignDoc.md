# Wandering Merchants — Design Document
**Forsaken Britannia · Custom Systems**

Two distinct merchant NPCs that interact with the player economy in different ways.

---

## Merchant 1 — The Curio Collector

### Concept
A mysterious travelling collector who sets up in a fixed location (placed by GM with `[add`).
He scans a player's entire inventory — including all nested bags — and makes cash offers on
anything worth his time. He pays in **gold** for lesser items and **Merchant Coins** for
high-end pieces. He will not touch average or low-end magic items.

### NPC Behaviour
- Fixed world position, placed by GM.
- Wanders a small radius (~10 tiles) around his spawn point between interactions.
- On double-click: opens the Appraisal Gump (see below).
- Speech flavour: talks like a seasoned appraiser — "Let me have a look at what you're carrying."

### Inventory Scan
- Recursively walks the player's entire pack (`from.Backpack`) including all `Container` children.
- Skips: blessed items, items the player has locked down, stackable commodities (ore, cloth, etc.),
  items with no AOS properties and no `ArtifactRarity`.
- Returns a flat list of `ScoredItem { Item item; int score; ItemTier tier; }`.

### Item Scoring

Every qualifying item is given a **property score**:

```
score = (property_count × 15)
      + (sum_of_all_property_intensities × 0.4)
      + (ArtifactRarity × 60)
```

**Property count** = number of non-zero AOS attributes across all attribute groups
(AosAttributes, AosWeaponAttributes / AosArmorAttributes, AosSkillBonuses, element resists).

**Property intensity** = per-property intensity percentage (0–100), obtained via
`RunicReforging.GetIntensityForProperty` or a simplified per-attribute lookup table.

**ArtifactRarity** = the `Item.ArtifactRarity` field (0 = not an artifact, 1–15 = named artifacts).

### Item Tiers & Payment

| Tier | Score range | Description | Payment |
|---|---|---|---|
| **Refuse** | 0 – 39 | 0–2 weak properties, plain crafted gear | Won't buy |
| **Gold — Minor** | 40 – 99 | 3–4 decent properties, or ArtifactRarity 1–3 | 2,000 – 25,000 gp |
| **Gold — Notable** | 100 – 179 | 4–5 solid properties, or strong minor artifacts | 25,000 – 100,000 gp |
| **Coins — High End** | 180 – 279 | Near-perfect looted items, ArtifactRarity 4–6 | 1 – 5 Merchant Coins |
| **Coins — Artifact** | 280 +    | Major named artifacts, ArtifactRarity 7+ | 5 – 25 Merchant Coins |

**Gold price formula** (within Gold tiers):

```
gold = BasePriceForTier + ((score - TierMin) / TierRange) × TierBonus
```

- Gold Minor base: 2,000 gp · bonus up to +23,000 gp
- Gold Notable base: 25,000 gp · bonus up to +75,000 gp

**Coin formula** (within Coin tiers):

```
coins = floor(score / 50)   clamped to tier min/max
```

- High End: 1–5 coins
- Artifact: 5–25 coins

### Practical Examples

| Item | Score | Outcome |
|---|---|---|
| Plain exceptional katana | 0 | Won't buy |
| Katana · HitLightning 40%, DamageInc 15% | 37 | Won't buy |
| Katana · 4 properties at 40–60% avg | 84 | ~4,500 gp |
| Ornate Axe · 5 properties at 65% avg | 153 | ~55,000 gp |
| Rune Beetle Carapace (named, AR 7) | 505 | ~20 coins |
| Crystalline Ring (named, AR 10) | 810 | 25 coins (capped) |

### Diminishing Returns (Multiple Items per Sale)

The Collector pays full price for the first item sold in a single transaction.
Each additional item sold in the **same session** receives a reduced multiplier,
discouraging bulk dumps and encouraging players to spread sales across visits:

| Item # in transaction | Price multiplier |
|---|---|
| 1st | 100% |
| 2nd | 85% |
| 3rd | 70% |
| 4th | 55% |
| 5th | 40% |
| 6th+ | 30% (floor) |

Formula: `multiplier = max(0.30, 1.0 - (index * 0.15))` where index is 0-based.

This is applied to the **final calculated price** for each item after tier scoring.
The gump shows both the full price and the adjusted price so the player can see
the discount clearly.

### Appraisal Gump

Opens on double-click. Layout:

```
╔══════════════════════════════════════════════════════╗
║  Curio Collector — Appraisal                         ║
║  "Here's what I'll give you for these pieces."       ║
╠══════════════════════════════════════════════════════╣
║  ☑  Rune Beetle Carapace        ............  20 🪙  ║
║  ☑  Ornate Axe [4 props]        ......  54,200 gp   ║
║  ☐  Iron Katana [2 props]       ..........  (skip)  ║
╠══════════════════════════════════════════════════════╣
║  Total offer:  54,200 gp  +  20 Merchant Coins       ║
║                    [Sell Selected]   [No Thanks]     ║
╚══════════════════════════════════════════════════════╝
```

- Skipped/Refuse items are shown greyed out as "too common for my tastes."
- Player can uncheck individual items before confirming.
- On confirm: checked items are deleted, gold dropped into player's backpack (or bank if overweight), coins added to backpack.

### Key Implementation Notes
- Class: `CurioCollectorNPC : BaseCreature` (not BaseVendor — no buy/sell menu needed)
- `[Constructable]` so GMs can place with `[add CurioCollectorNPC`
- Recursive scan: `ScanContainer(Container c, List<ScoredItem> results)`
- Score helper: `static int ScoreItem(Item item)` — checks all AOS attribute groups
- Gump: `CurioCollectorGump(Mobile from, List<ScoredItem> items)`
- `MerchantCoin` — new `Item` subclass, stackable, no weight; design TBD

---

## Merchant 2 — The Gate Visitor

### Concept
A roaming buyer who gates between player vendor stalls across the world and purchases
ordinary goods at reasonable prices. He creates a passive income stream for player shops
and acts as a small gold sink by removing items from the economy.

He does **not** buy magic items — that is Merchant 1's domain.

### NPC Behaviour
- Spawned once (or a small number) by GM. Lives permanently.
- Every **3–8 minutes** (random interval) he:
  1. Opens a shimmering gate at his current position.
  2. Steps through and appears at a randomly chosen `PlayerVendor` in the world.
  3. Browses the vendor's stock and buys qualifying items (see rules below).
  4. Lingers for 45–90 seconds (walks nearby, says flavour speech).
  5. Opens another gate to the next vendor.
- Speech examples: "Hmm, fair price on that…", "I'll take the lot.", "Not worth my time."

### Shopping Rules

**Per-visit budget:** 20,000 – 60,000 gp (random, resets each gate jump).

He buys from the vendor's listed price if it is **at or below his per-category cap**:

| Category | Max price per unit | What he buys |
|---|---|---|
| Ingots / Ore | 6 gp | All types (Iron → Valorite) |
| Boards / Logs | 4 gp | All wood types |
| Cloth / Leather | 5 gp | All grades |
| Reagents | 7 gp | All 8 standard reagents |
| Potions | 20 gp | Heal, Cure, Strength, Agility |
| Food | 15 gp | Any food item |
| Gems | 80 gp | All gem types |
| Scrolls | Circle × 50 gp | Circle 1=50 gp … Circle 8=400 gp |
| Plain weapons / armor | 400 gp | No magic properties |
| Crafted tools | 150 gp | Shovels, hammers, tinker tools, etc. |

**He will never buy:**
- Items with any AOS magic properties
- Items with `ArtifactRarity > 0`
- Items priced above the per-category cap
- Items from a vendor he has visited in the last 30 minutes (prevents looping)

**Quantity limit per line item:** up to 500 units of a stackable at once (so a stack of 2000 boards
gets split into 500 units × vendor price, provided that fits his budget).

### Purchase Mechanic
When he buys an item from a `PlayerVendor`:
1. Determine price from the vendor's `SellList`.
2. Deduct from his per-visit budget.
3. Add gold directly to the `PlayerVendor`'s held gold (same path as a player purchase).
4. Remove the item (delete or move to his temporary hold — deleted on next gate jump so he
   doesn't accumulate inventory).

This means the vendor owner earns gold exactly as if a player had bought the item.

### Vendor Selection
- Runs on **Felucca only** — he never visits Trammel or other facets.
- On each gate jump: `World.Mobiles.Values` filtered to `PlayerVendor` instances on
  `Map.Felucca` that have at least one qualifying item in stock.
- If no eligible vendors exist: he sits idle and retries after 10 minutes.
- Visited-vendor cooldown list is a `Dictionary<Serial, DateTime>` (cleared on server restart,
  which is acceptable).

### Key Implementation Notes
- Class: `GateVisitorMerchant : BaseCreature`
- `[Constructable]` for GM placement.
- `IsQualifyingItem(Item item) -> bool` — checks category + has no magic properties + price ≤ cap
- `FindNextVendor() -> PlayerVendor` — random eligible vendor with cooldown check
- Gate effect: `FixedParticles(0x6F9, ...)` + `PlaySound(0x20E)` on departure; same on arrival
- **Important:** `PlayerVendor.HoldGold` (or equivalent) must be incremented directly;
  do not use the full buy-gump flow as that requires a player NetState.

---

## Merchant Coins
*(System TBD — placeholder notes only)*

- New stackable item: `MerchantCoin`
- No weight, no decay
- Earned exclusively from Merchant 1 (the Curio Collector)
- Future uses: Merchant Coin vendor selling rare/exclusive items, cosmetics, unlock recipes, etc.
- **Not earnable through any other system** — keeps the economy signal clean

---

---

## Merchant 3 — The Traveling Rare Merchant

### Concept
A single mysterious merchant who drifts between town banks across Britannia, staying 30 minutes before vanishing and reappearing elsewhere. He only accepts Merchant Coins and carries high-value items no regular vendor would stock.

### NPC Behaviour
- One global instance managed by `TravelingRareMerchantSystem`.
- Every 30 minutes: despawns with farewell speech + effect, then reappears at a randomly chosen town bank (Felucca or Trammel).
- A server-wide gold broadcast announces which bank he arrived at.
- `CantWalk = true` — stays put while at the bank.
- On double-click: opens the shop gump.
- GM command `[respawnmerchant` forces an immediate move (for testing).

### Appearance
- Black robe (hue 1), black wizard's hat
- Gold bracelet + gold necklace (gold trim effect)
- Gold sandals (hue 0x8A5)
- Random human body + skin + hair

### Shop
- 10 items randomly drawn from the stock pool each visit — no duplicates within a single visit.
- Items refresh every time he moves to a new location.
- **Payment: Merchant Coins only** — deducted directly from backpack.
- Gump shows the player's current coin balance alongside each item's cost.

### Stock Pool
| Category | Examples | Cost (Merchant Coins) |
|---|---|---|
| Power Scrolls +115 (combat) | Swords, Archery, Magery, Healing | 60c |
| Power Scrolls +120 (combat) | Swords, Archery, Magery, Tactics | 100c |
| Power Scroll: Taming 110/115/120 | — | 40 / 80 / 150c |
| Power Scrolls +120 (crafting) | Blacksmithy, Tailoring, Fishing, etc. | 80c |
| Major Artifacts | Hat of the Magi, Crystalline Ring, Rune Beetle Carapace, etc. | 100–200c |
| Special Resources | Gold 50k/100k, Valorite Ingots 500, Frostwood/Heartwood Boards 500 | 20–35c |

### Key Implementation Notes
- `TravelingRareMerchantSystem.Initialize()` — auto-called at startup, starts 30-min cycle.
- `RareMerchantStock.GetRandomStock(10)` — picks 10 without replacement from pool.
- `TravelingRareMerchant : BaseCreature` — `IsInvulnerable = true`, `CantWalk = true`.
- `TravelingRareMerchantGump` — shows 10 items, buy button per row, updates coin count.
- Merchant deletes on server restart (transient) — system spawns fresh on next startup.
- File: `Scripts/Custom/TravelingRareMerchant.cs`

---

## Decisions Log

| Decision | Answer |
|---|---|
| Daily cap on Curio Collector | None — no cap, but diminishing returns apply per transaction |
| Diminishing returns | 100% / 85% / 70% / 55% / 40% / 30% floor per item in one sale |
| Gate Visitor facet | Felucca only |
| Gate Visitor starting rates | Use suggested table — tune after observation |
| Gate Visitor pays | Vendor's listed price if at or below per-category cap |

## Open Questions (to resolve later)
1. **Merchant Coin spending** — what vendor/shop accepts them and at what exchange rate?
2. **GateVisitor count** — one global instance, or one per city?
3. **Price floor** — is 2,000 gp the hard minimum the Curio Collector will ever pay?
