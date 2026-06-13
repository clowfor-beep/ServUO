# AIther (Forsaken Britannia) — Custom Systems Overview
**Shard:** AIther | ServUO + ClassicUO  
**Last Updated:** 2026-06-06

---

## 1. Reputation System

Players build standing with **12 guilds** across Forsaken Britannia.

**Guilds:**
The Wanderers, The Craftsmen's League, The Shadow Hand, Iron Company, The Arcane Brotherhood, The Silver Wolves, The Paladin Order, The Dead Watchers, The Dread Hunters, Blood Pact, The Void, Shadowblade

**Standing Tiers:**

| Points | Tier |
|--------|------|
| < 0 | Hostile |
| 0–99 | Neutral (default) |
| 100–299 | Known |
| 300–599 | Trusted |
| 600+ | Allied |

**Tier-up Rewards:**
- **Known** — Rare bounty quests unlock on the Bounty Board
- **Trusted** — Standard Guild Sash awarded (+5 to 3 guild skills), Legendary quests unlock
- **Allied** — Refined/Exalted sashes available via quests

Use `[myrep` to view your standings. Persisted to `Saves/Misc/ReputationSystem.bin`.

---

## 2. Guild Sash System

Wearable sashes (waist slot) that grant skill bonuses based on guild focus.

| Tier | Skill Bonus | Drop Source |
|------|-------------|-------------|
| Standard | +5 to 3 skills | Rare quest (30%) |
| Refined | +10 to 3 skills | Legendary quest (40%) |
| Exalted | +15 to 3 skills | Legendary quest (20%) |

Each guild has a unique hue and grants bonuses to its thematically relevant skills (e.g. Paladin Order → Chivalry/Healing/Tactics; Shadow Hand → Hiding/Stealth/Swords).

---

## 3. Quest System (Quest Factory)

Dynamic bounty board quests generated per-guild.

**Quest Types:**
- **Hunt** — Kill N of a specific creature type
- **Gather** — Deliver N of a specific resource to the Bounty Board

**Tiers:** Common, Uncommon, Rare (requires Known rep), Legendary (requires Trusted rep)

**Rewards:** Gold + Reputation with the quest-giving guild. Higher tiers = more of both.

Bounty Boards placed in Britain, Trinsic, and Minoc. One active quest per player at a time.

---

## 4. Hunter System

A rotating bounty hunt system targeting dangerous monsters across the world.

- Up to **3 active hunts** running simultaneously
- New hunts spawn every **90–180 minutes** (random interval)
- Special **Wanted NPCs** spawn every 4–6 hours (player-difficulty named enemies)
- Kills award **Hunter Points** tracked on the Hunter Board (`[hunterboard`)
- World announcements on spawn and kill via the WorldCrier
- Hunters can earn title deeds from the Hunter Points token shop

**Hunter Creatures:** Scaled, named enemies with GM+ skills — far tougher than standard mobs.

---

## 5. Hunter Board

Leaderboard accessible via `[hunterboard`. Displays top 10 hunters by total Hunter Points earned. Updates live.

---

## 6. World Crier NPC

Placeable town herald (`[add WorldCrierNPC`). Double-click to open a gump showing:
- Active monster hunts (name + location)
- Active wanted NPCs
- Recent 5 world events (kills, spawns)

Receives events automatically via the FBEventBus.

---

## 7. PK Encounter System

Automated player-killer NPC encounters in dungeons.

- Covers **all dungeons** across all facets (Felucca, Ilshenar, Malas, Tokuno, TerMur)
- Checks every **5 minutes** — targets the player who has been in the dungeon longest
- **30-minute cooldown** per player
- Max **1 active PK per player** at a time
- PK stays on its spawn floor — despawns if target changes floor (waits 2 min first)
- Three tiers: **Newbie, Advanced, Expert** — matched to dungeon difficulty
- Portal moongate VFX + sound on spawn
- GMs and staff are never targeted

---

## 8. SimPlayer System (AI Guild Members)

Simulated players from each guild roam the world, interact with towns and dungeons.

**Behaviour states:** OnCooldown → Idle → Travelling → Banking → (combat/flee) → Dead → OnCooldown

**Guilds with SimPlayers:**
- The Wanderers (pacifist, wanders towns)
- Craftsmen's League (blacksmith template, moves at working pace)
- Shadow Hand (stealth/rogue, flees to safety on threat)
- Iron Company (warrior, patrols aggressively)
- Arcane Brotherhood (mage, casts from range)
- The Silver Wolves (archer/hunter)
- Paladin Order (chivalry-based fighter)
- The Dead Watchers (necromancer/summoner)
- The Dread Hunters (tracking/ranged)
- Blood Pact (red/murderer template)
- The Void (powerful mage-killer)
- Shadowblade (elite assassin)

SimPlayers follow a **schedule profile** — active hours vary by guild. They visit banks, travel between zones, and engage with dungeon areas. Their death/respawn cycle is managed by `PlayerSimulatorManager`.

---

## 9. SimPlayer Guilds — Combat Archetypes (BaseFBCombatNPC)

Each guild has multiple templates (Tier 1–3) with scaled stats and gear:

| Archetype | Weapon Style |
|-----------|-------------|
| Pure Warrior | Swords + Tactics + Anatomy + Healing + Parry |
| Archer | Archery + Tactics + Anatomy |
| Mage | Magery + EvalInt + MedItation + Resist |
| Rogue | Fencing + Hiding + Stealth + Ninjitsu |
| Necromancer | Necromancy + Spirit Speak + Magery |
| Paladin | Chivalry + Swords + Tactics |
| Tamer | Animal Taming + Lore + Veterinary |

---

## 10. Orb & Currency System

A secondary economy layer using **Orbs** and **Essence Shards**.

### Character Orbs
| Orb | Effect |
|-----|--------|
| Orb of Enhancement | +skill points (3 tiers: +0.1/+0.2/+0.3) |
| Orb of Mastery | +stat cap points (2 tiers) |
| Orb of Expansion | +total skill cap (3 tiers: +5/+10/+20 pts) |
| Orb of Fortitude | +total stat cap (2 tiers) |
| Orb of Alacrity | Double all skill gains for 15/30/60 min |
| Orb of Insight | Triple skill gain frequency for 15/30/60 min |
| Orb of Balance | Skill vessel: absorb up to 80/100/120 points, grant to any player |

### Item Orbs
| Orb | Effect | Risk |
|-----|--------|------|
| Orb of Corruption | Random risky enhancement | 50% chance to destroy item |
| Orb of Resonance | Push a property beyond normal cap | 25% chance to destroy |
| Orb of Cleansing | Remove a property (random or chosen) | None |
| Orb of Tempering | Improve base item quality | None |
| Orb of Enchantment | Add random magical property | None |
| Orb of Reforging | Randomise all properties | None |

### Scrolls (Temporary Combat Buffs)
| Scroll | Effect |
|--------|--------|
| Scroll of Warding | Absorb damage shield (3 tiers) |
| Scroll of Deathtouch | 1% chance for instant kill on hit |
| Scroll of Execution | Bonus damage when target is low HP (3 tiers) |
| Scroll of Leeching | Drain a random stat on hit (3 tiers) |

---

## 11. Skill Synergy System

Passive bonuses that reward investing in traditionally "support" or "crafting" skills.

### Offensive Synergies (weapon damage bonuses)
| Skill | Bonus |
|-------|-------|
| Camping | +10% weapon damage (all weapons) |
| Forensic Eval | +10% weapon damage (all weapons) |
| Tracking | +10% weapon damage (all weapons) |
| Blacksmithy | +10% metal weapon damage |
| Carpentry | +10% staff/ranged damage |
| Mining | +10% bashing weapon damage |
| Lumberjacking | +10% axe damage (stacks with native bonus) |

### Backstab Synergy
- Attacking from hidden with **80+ Hiding AND 80+ Stealth** triggers a backstab multiplier
- **PvM only** — scales with Stealth: up to +200% bonus damage (3× total) at GM Stealth
- Works on both melee and ranged (bow) attacks
- +50% hit chance bonus on the backstab swing

### Defensive Synergies (scale from skill 80+)
| Skill | Physical | Fire | Cold | Poison | Energy | DCI | HP | Heal | Regen | Debuff |
|-------|----------|------|------|--------|--------|-----|----|------|-------|--------|
| Blacksmithy | +10% | +5% | — | — | — | — | — | — | — | — |
| Mining | +5% | +5% | — | — | — | — | — | — | — | — |
| Lumberjacking | +5% | — | +8% | — | — | — | — | — | — | — |
| Carpentry | +3% | — | +5% | — | — | — | — | — | — | — |
| Camping | +5% | — | +8% | — | — | — | — | — | ×2.0 | — |
| Animal Taming | +5% | — | — | +5% | — | — | +15 | — | — | — |
| Herding | — | — | +5% | +8% | — | — | +20 | — | — | — |
| Alchemy | — | +12% | — | +5% | — | — | — | — | — | — |
| Taste ID | — | +5% | — | +12% | — | — | — | — | — | — |
| Inscription | — | — | +5% | — | +12% | — | — | — | — | — |
| Fishing | — | — | +5% | — | +3% | — | +10 | — | — | — |
| Tracking | — | — | — | — | — | +8% | — | — | — | — |
| Detect Hidden | — | — | — | — | — | +10% | — | — | — | -25% |
| Wrestling | +8% | — | — | — | — | — | — | — | — | — |
| Forensic Eval | — | — | — | — | — | — | — | +25% | — | — |
| Veterinary | — | — | — | — | — | — | — | +20% | — | — |

### Resist Cap Raises
Several skills raise elemental resist caps above the default 70%:
- Primary raisers (Blacksmithy, Alchemy, Taste ID, Inscription, Camping) raise their resist cap by +5% at 100, +8% at 120
- Secondary raisers add +2% at 100, +3% at 120 to related resists

---

## 12. Herding + Shepherd's Crook System

Herding amplifies damage dealt and reduces damage taken by all controlled/summoned followers.

- Requires an **activated Shepherd's Crook** (double-click to activate, stays active in backpack)
- **effectiveHerding** = Herding skill + crook HCI + crook DI
- **Follower outgoing damage (PvM):** +22% × (effectiveHerding/100)
- **Follower incoming damage (PvM):** -11% × (effectiveHerding/100)

Passive skill gain fires on every follower hit (5% chance per hit).

---

## 13. Summoner Synergy System

Spirit Speak scales all summoned creature stats at cast time.

**At GM Spirit Speak (100):**
- HP: +150% (2.5× total)
- Damage: +50%
- Wrestling: +50 (capped at 120)
- Virtual Armor: +25
- Magic Resist: +50 (capped at 120)

**At 120 Spirit Speak:** HP ×2.8, Damage +60%, Wrestling +60, VA +30, MR +60

Hooked into: Animate Dead, Summon Familiar, Air/Fire/Earth/Water Elemental, Summon Daemon, Blade Spirits, Energy Vortex.

---

## 14. Necromancer Summoner Build

A fully designed and implemented archetype combining Herding + Spirit Speak + Necromancy.

**Core Template (700 skill cap):**
Necromancy 100 + Spirit Speak 100 + Magery 100 + Eval Int 100 + Herding 100 + Meditation 100 + Magic Resist 100

At full build: summons have 2.5× HP and their damage is multiplied by both Spirit Speak (~1.5×) and Herding (~1.22×) = ~1.83× combined multiplier.

---

## 15. Battlecry System

Triggered when a red skull candle is lit at a champion spawn near Sergeant Vale (Iron Company).

Randomly applies one of 8 buffs to all nearby Iron Company members, players, and pets within 100 tiles:

| Buff | Effect |
|------|--------|
| Heal and Cure | Instant full heal, cure poison, restore stamina/mana |
| Damage Bonus | +30 STR stat mod (≈10% melee damage) for 2 min |
| HP Regen | +20 HP/sec for 2 min |
| Skill Gain | Double all skill gains for 2 min |
| Luck | +500 Luck item in backpack for 2 min |
| Mana Regen | +20 Mana/sec for 2 min |
| All Resists | +75 to all resistances for 2 min |
| Speed Boost | +60 DEX + full stamina restore for 2 min |

---

## 16. Traveling Merchants

### Traveling Rare Merchant
Appears at a random town bank for **30 minutes** before moving on.
- Black robe with gold trim
- Accepts **Merchant Coins** only
- Carries **10 randomly selected top-tier items** per visit
- Location announced via world broadcast on arrival

### Bulk Resource Merchant
Appears at a random town bank for **30 minutes** before moving on.
- Sells **10 randomly selected crafting resource deeds** per visit
- Deeds redeemable for 500/2,500/5,000 resource units
- Accepts **gold from bank account** directly
- Location announced via world broadcast

---

## 17. Shady Figure

A mysterious hidden informant near the Britain Town Crier.

- Always hidden, stealths around a fixed anchor point
- Responds to **"I need information"** spoken within 8 tiles
- Charges **1,000 gold** — whispers current Rare Merchant location and Iron Company status
- When revealed by Detect Hidden: freezes visible for 60 seconds, then re-hides
- Mimics realistic stealth movement (15 sec moving, 8 sec pause to "re-hide")

---

## 18. Treasure Hunter NPC

A placeable NPC offering paid services for decoded treasure maps.

| Service | Cost | What it does |
|---------|------|-------------|
| Portal Only | 30% of chest reference value | Opens a one-use moongate to the chest |
| Full Assistance | 80% of chest reference value | Digs, disarms trap, unlocks, and escorts to chest |

Payment withdrawn from bank account upfront. Portal gates auto-delete after 60 seconds if unused.

---

## 19. Training Chests

Lockable chests placed by staff for Lockpicking skill training.

| Chest | Required Skill |
|-------|---------------|
| Locked Training Chest | 39.0 |
| Advanced Training Chest | ~55.0 |
| Expert Training Chest | ~75.0 |

Re-lock automatically 1 minute after being picked. Stationary, not lootable.

---

## 20. Cooldown HUD System

A transparent on-screen display tracking active skill/ability cooldowns.

- Shows ability name + countdown timer + progress bar
- Updates every 0.5 seconds
- Configurable position (default bottom-center of screen)
- Used by special abilities and custom skills that have cooldown timers

---

## 21. World Atlas

An in-game item (`WorldAtlas`) that opens a navigation gump.

- Lists all **Towns**, **Dungeons**, and **Moongates** with their exact coordinates
- Double-click to open; select a destination to receive map coordinates
- All coordinates sourced from Regions.xml for accuracy

---

## 22. Dungeon Chest System

Custom tiered treasure chests (`TreasureLevel1–5`) replacing vanilla dungeon chest loot.

| Level | Lock Skill | Gold | Scrolls | Magic Items |
|-------|-----------|------|---------|-------------|
| 1 | 52 | 108–180 | None | 1–2 |
| 2 | 72 | 225–375 | Circles 4–5 (50% chance) | 2–3 |
| 3 | 84 | 400–600 | Circles 5–6 (67% chance) | 3–4 |
| 4 | 92 | 600–900 | Circles 6–7 (75% chance) | 4–6 + 10% circle 8 |
| 5 | 100 | 800–1400 | Circles 6–8 (90% chance) | 6–10 |

- Loot is generated at spawn time
- Items rolled with player's **Luck** on first open (deferred luck system)
- Chest deletes itself 2–5 minutes after opening
- Rare drops: Treasure Maps, Power Scrolls (105/110), Currency Orbs (1% chance all tiers)

---

## 23. Currency: Essence Shard

A stackable third currency used in the Orb economy.

---

## 24. FBEventBus

A decoupled cross-system event bus. No system references another directly — all communication flows through typed events:

Key events: `PlayerKilledSimPlayer`, `SimPlayerKilledPlayer`, `HunterTargetSpawned`, `HunterTargetKilled`, `WantedNPCSpawned`, `WantedNPCKilled`, `ReputationChanged`, `PlayerEnteredZone`, `PlayerLeftZone`

---

## 25. FBZones

Single source of truth for all world coordinates. Maps all dungeon floors, guild home areas, and town zones to named `SpawnZone` enum values with associated `Rectangle2D` footprints and `Map` references.

---

## 26. Assassin's Strike Weapon Ability

A bow weapon special ability scaling with the Stealth skill.

- **Secondary skill requirement:** Stealth 50+
- **Mana cost:** 20
- On hit: applies stealth-scaled damage bonus identical to the backstab system
- Re-hides the attacker 1 second after the shot resolves
- Available on select bow types

---

## 27. Combat System Changes

See the dated combat update note for full details. Summary:

- **Backstab** — damage + hit chance bonus when attacking from stealth (melee and ranged)
- **Force Arrow / Shadow Strike** — Elven Composite Longbow primary hides attacker after hit at 80+ Stealth
- **Parrying** — reworked to 50% base chance, Bushido scales damage reduction to 80%
- **Anatomy** — 1% damage reduction per 10 skill (passive, all damage types)
- **Magic Resist** — full rework: spell damage reduction, debuff resist formula, Spell Absorption, Spell Siphon buff
