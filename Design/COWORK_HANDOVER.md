# COWORK HANDOVER — Forsaken Britannia
**Last updated:** 2026-05-24
**Read this first, every time, before doing any work on this codebase.**

---

## 1. WHAT THIS PROJECT IS

A custom UO (Ultima Online) shard called **"Forsaken Britannia"** (server display name: **"Fun Stuff"**).
Built on **ServUO** (C# / .NET Framework 4.7+), played via the **ClassicUO** client.

The core promise: a server that never feels empty, with a living world of AI guild factions, rare hunt targets, PvP danger in dungeons, and a deep quest system. Think EverQuest's faction system + Path of Exile's economy + classic UO's danger.

**Server infrastructure:**
- Host: Ubuntu VPS at `178.105.173.80`
- Prod container: `servuo` — port 2593
- Test container: `servuo-test` — port 2594
- Git repo on server: `/home/servuo/`
- Deploy: run `bash /home/servuo/deploy-test.sh` (test) or `bash /home/servuo/deploy.sh` (prod)
- **Always deploy test before prod. Never touch prod without testing first.**

---

## 2. CODEBASE STRUCTURE

```
Scripts/Custom/     <- ALL custom code lives here. This is where you write.
Scripts/            <- Vanilla ServUO. Do NOT modify except at defined hook points.
Server/             <- Core engine. Do NOT modify.
Design/             <- Design documents (this folder). Read before implementing.
ToDo Cowork/        <- Work instructions for Claude 2. Follow format in README.md.
Config/             <- Key=Value server config files.
Data/Spawns/        <- XmlSpawner files. ForsakenBritannia.xml goes here.
```

**Namespace conventions:**
- Custom systems: `namespace Server.Custom`
- Custom items: `namespace Server.Items`
- Never use `namespace Server` directly from Scripts/Custom/

---

## 3. CORE SERVUO PATTERNS (must follow)

### Every persistent class needs two constructors
```csharp
[Constructable]
public MyClass() : base(...) { /* setup */ }

public MyClass(Serial serial) : base(serial) { }  // NEVER add logic here
```

### Serialize / Deserialize — always versioned
```csharp
public override void Serialize(GenericWriter writer)
{
    base.Serialize(writer);
    writer.Write(0); // version
    writer.Write(_myField);
}

public override void Deserialize(GenericReader reader)
{
    base.Deserialize(reader);
    int version = reader.ReadInt();
    if (version >= 1) _myField = reader.ReadString();
}
```
**Never change the order of existing Read calls. Only add new reads inside `if (version >= N)` blocks.**

### Static Initialize() — automatic startup hook
```csharp
public static void Initialize()
{
    EventSink.Login += OnLogin;
    // etc.
}
```
Any `public static void Initialize()` in Scripts/ is called automatically on server start. Use this for all event hooks.

### Equipment rule — CRITICAL
- Weapons: use explicit layer (`SetWearable(new Katana(), Layer.OneHanded)` or `AddItem`)
- Spellbooks, Books of Chivalry: **always `AddToBackpack()`**, NEVER `SetWearable`
- This fixes LayerConflict bugs. Applies to all PK NPCs, SimPlayers.

### Assembly boundary
- `Server/` compiles before `Scripts/Custom/`
- Never reference `Server.Custom` directly from `Server/`
- Use static delegates for cross-boundary calls (see CooldownSystem.cs for the pattern)

### ServUO gump limitation
- `AddImageTiledPart` does NOT exist in this build
- Use two `AddImageTiled` calls instead

---

## 4. SYSTEM IMPLEMENTATION STATUS

### DONE — Live in Scripts/Custom/

| File | What it does | Notes |
|------|-------------|-------|
| `SkillSynergies.cs` | Offensive weapon damage bonuses, backstab multiplier | Defensive synergies designed but NOT yet hooked — see Section 6 |
| `CooldownSystem.cs` | Player cooldown HUD gump with progress bars | Fully working. Uses `Skills.OnSkillUsed` delegate |
| `NovicePlayerKiller.cs` | Original simple Newbie-tier PK NPC | Predates PlayerKillerNPCs.cs. Still used by GraveyardPKEncounter |
| `PlayerKillerNPCs.cs` | All 21 PK classes — 7 archetypes × 3 tiers | Contains `BasePKNPC` shared base. All serialised. |
| `GraveyardPKEncounter.cs` | Britain graveyard PK trigger (single zone) | Old system — still active. Will be retired once PKEncounterSystem is validated |
| `PKEncounterSystem.cs` | Multi-zone PK encounter system — all facets | Replaces GraveyardPKEncounter. 40+ zones, floor-tier logic, 30-min cooldown per player |
| `PKTestCommand.cs` | `[pktest` staff command with tier override | Tests PKEncounterSystem. Usage: `[pktest`, `[pktest advanced`, `[pktest <name> expert` |
| `WorldAtlas.cs` | Item: dungeon/town atlas with gump | Note: Luna/Umbra/Zento Z coordinates unverified in-game |
| `HunterSystem.cs` | Hunter spawn timer, persistence, player points, rank | Core of Hunter System |
| `HunterCreatures.cs` | All Hunter target creature classes (Tiers 1-4) | Extends existing ServUO monsters |
| `HunterWanted.cs` | Wanted PK NPC targets (Cutthroat / Murderer / Dread Lord) | Extends BasePKNPC |
| `HunterGuildmaster.cs` | Guildmaster NPC + turn-in gump + token shop | Placed in Britain, Trinsic, Minoc |
| `HunterItems.cs` | HunterHead, HunterMedallion, HunterToken, named weapons | All serialised |
| `OrbAndCurrencySystem.cs` | All orbs (12 types), scrolls (4 types), EssenceShard | Category 1 (character), 2 (item), 3 (scrolls) all implemented |
| `PlayerCountExport.cs` | Utility — exports player count | No dependencies |

### NOT YET BUILT — Pending Implementation

These are listed in build order (see Section 5). Do not skip steps.

| File | What it does | Depends on |
|------|-------------|-----------|
| `FBZones.cs` | Single source of truth for all world coordinates | Nothing |
| `FBEventBus.cs` | Cross-system event bus (decouples all systems) | Nothing |
| `BaseFBCombatNPC.cs` | Shared combat base for all PK/SimPlayer types — 7 templates | FBZones |
| `FBPKSpawner.cs` | Pool-managed patrol PKs on roads and in dungeons | BaseFBCombatNPC, FBZones |
| `ForsakenBritannia.xml` | Static world objects via XmlSpawner (boards, NPCs) | Nothing |
| `ReputationSystem.cs` | Player standing with each of 12 guilds | FBEventBus |
| `SimPlayer.cs` | Base class for all 144 AI guild members | BaseFBCombatNPC, FBEventBus |
| `PlayerSimulatorManager.cs` | Creates/manages all SimPlayers, active pool | SimPlayer, FBZones |
| `SimStateMachine.cs` | State machine: Idle, Hunting, Combat, Banking, etc. | SimPlayer |
| `SimPersonality.cs` | Personality types: Warrior, Mage, Crafter, Rogue, PK | SimPlayer |
| `SimChatBrain.cs` | Ambient/reactive/combat speech per guild | SimPlayer, SimPersonality |
| `ScheduleProfile.cs` | Daily schedule per SimPlayer with ±30 min drift | SimPlayer |
| `QuestTrackerHUD.cs` | Always-visible quest objective gump (like CooldownSystem) | Nothing (standalone) |
| `QuestFactory.cs` | Generates Hunt/Clear quests from SimGuild activity | FBEventBus, ReputationSystem |
| `BountyBoardFB.cs` | Physical bounty board item in towns | QuestFactory |
| `CrossClassSkillSystem.cs` | Epic quest cross-class skill unlock at 80 outside 700 cap | Phase 3 only |

---

## 5. BUILD ORDER

This is the authoritative sequence. Do not build out of order.

```
STEP 0  Fix NovicePlayerKiller.cs OnDeath: protected → public  ✓ DONE — confirmed public in live file
STEP 1  FBZones.cs + FBEventBus.cs         (no dependencies — pure data/events)
STEP 2  BaseFBCombatNPC.cs                 (7 templates, equipment rule enforced)
        Refactor NovicePlayerKiller.cs     (extend BaseFBCombatNPC, no behaviour change)
STEP 3  FBPKSpawner.cs                     (model from BountyQuestSpawner.cs)
        ForsakenBritannia.xml              (Britain bank board only to start)
STEP 4  ReputationSystem.cs               (hooks FBEventBus events)
STEP 5  SimPlayer.cs (base class only)
        PlayerSimulatorManager.cs
        Wanderers guild first (4 members) — verify performance before expanding
STEP 6  QuestTrackerHUD.cs               (standalone, no FB dependencies)
STEP 7  QuestFactory.cs                  (Hunt quests from bounty targets)
STEP 8  Defensive skill synergies        (hook into PlayerMobile — see Section 6)
STEP 9  All 12 SimGuilds                 (only after Phase 1 is stable)
STEP 10 CrossClassSkillSystem.cs         (Phase 3 — last)
```

---

## 6. DEFENSIVE SKILL SYNERGIES — PENDING HOOKS

`SkillSynergies.cs` has offensive synergies working. The defensive synergies are fully designed (see `Design/SkillSynergies_DesignDoc.txt`) but need hooks added.

Methods to ADD to `SkillSynergies.cs`:
- `GetPhysicalResistBonus(Mobile)`, `GetFireResistBonus`, `GetColdResistBonus`, `GetPoisonResistBonus`, `GetEnergyResistBonus`
- `GetPhysicalResistCap(Mobile)` ... (one per type)
- `GetDCIBonus(Mobile)` → double
- `GetBonusHP(Mobile)` → int
- `GetBandageHealBonus(Mobile)` → double
- `GetHPRegenMultiplier(Mobile)` → double
- `GetDebuffDurationMultiplier(Mobile)` → double

Hook locations (in vanilla ServUO files):
- Resist bonuses → `PlayerMobile.ComputeResistances()`
- Resist caps → `PlayerMobile.GetMaxResistance()`
- DCI bonus → `BaseWeapon.CheckHit()`
- HP bonus → `PlayerMobile.HitsMax` getter
- Bandage heal → `Bandage.cs` heal amount
- HP regen → `PlayerMobile.GetHitsRegenRate()`
- Debuff duration → individual spell timer setup

**Applies to PlayerMobile only. NOT SimPlayers (they use fixed SetResistance values).**

---

## 7. THE 12 SIMGUILDS (reference)

Full templates in `Design/GuildSystem_FullDesignDoc.txt`.

| # | Guild | Tier | Alignment | Skill Cap | Stat Cap |
|---|-------|------|-----------|-----------|----------|
| 1 | The Wanderers | 1 | Blue | ~450 | 150 |
| 2 | Craftsmen's League | 1 | Blue | ~380 | 150 |
| 3 | Shadow Hand | 1 | Grey | ~500 | 150 |
| 4 | Iron Company | 2 | Blue | 700 | 180 |
| 5 | Arcane Brotherhood | 2 | Blue | 700 | 180 |
| 6 | Silver Wolves | 2 | Blue | 700 | 180 |
| 7 | Paladin Order | 3 | Blue | 700 | 225 |
| 8 | Dead Watchers | 3 | Perm Grey | 700 | 225 |
| 9 | Dread Hunters | 3 | Blue/Grey | 880 | 300 |
| 10 | Blood Pact | 3 | Red | 800 | 250 |
| 11 | The Void | 3 | Red | 850 | 270 |
| 12 | Shadowblade | 3 | Red/Grey | 800 | 250 |

Phase 1 implementation: Wanderers only (4 members). Verify performance before expanding.

---

## 8. KNOWN ISSUES AND GOTCHAS

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| 1 | `NovicePlayerKiller.cs` — `OnDeath` must be `public override` not `protected override` | **DONE — confirmed `public` in live file** (`.bak` = old broken version) | Not blocking |
| 2 | Equipment layer conflicts — books vs weapons | **FIXED by rule**: books always in backpack | Affected NecroMage, Sampire, Paladin, Mage templates |
| 3 | `WorldAtlas.cs` Z coords for Luna/Umbra/Zento unverified | Open | Verify in-game with `[where` before Atlas goes live |
| 4 | Bad spawn at TerMur 946, 3858, -40 | Non-blocking | Pre-existing. Clean up when convenient |
| 5 | `GraveyardPKEncounter.cs` runs alongside `PKEncounterSystem.cs` | Open | Both currently active. Graveyard is covered by PKEncounterSystem. Retire the old file when validated. |
| 6 | `Skills.cs` compiles before `Scripts/Custom/` | Permanent | Use static delegate pattern — already done in CooldownSystem.cs |
| 7 | PowerShell Danish locale | Permanent | No Unicode in `Write-Host`. Plain ASCII only in PS scripts. |
| 8 | `AddImageTiledPart` does not exist | Permanent | Use two `AddImageTiled` calls |
| 9 | Reds (Blood Pact, Void) in cities = instant death from guards | Design constraint | Never gate evil guilds into Felucca cities |

---

## 9. DESIGN DOCUMENTS INDEX

All in `Design/` folder. Read the relevant doc before implementing any system.

| File | Covers |
|------|--------|
| `SystemArchitecture_DesignDoc.txt` | **Read this first for any new module.** Architecture, build order, module specs, dependency graph |
| `GuildSystem_FullDesignDoc.txt` | All 12 SimGuilds — member templates, activities, rivalries, schedules |
| `PlayerSimulator_DesignDoc.txt` | SimPlayer architecture — 6 components, state machine, personalities |
| `PlayerSimulator_ActivitySpec.txt` | 20 player activities mapped to SimPlayer implementation |
| `PKNPCDesignDoc.txt` | Original PK NPC design (3-tier) — mostly superseded by Templates doc |
| `PKNPCTemplates_AllArchetypes.txt` | **7 archetypes × 3 tiers = 21 PK templates** — full stats, skills, gear, encounter zones |
| `HunterSystemDesignDoc.txt` | Hunter System — 4 creature tiers, Wanted track, ranks, loot, Guildmaster |
| `OrbAndCurrencyDesignDoc.txt` | Orbs, scrolls, Essence Shards — all tiers and effects |
| `QuestSystem_DesignDoc.txt` | 6 quest types, SimGuild integration, reward philosophy, implementation phases |
| `SkillSynergies_DesignDoc.txt` | Offensive (done) + defensive (pending) synergy tables and hook locations |
| `WorldSpawn_DesignDoc.txt` | **The four world layers** — FBPKSpawner zones, ForsakenBritannia.xml objects, encounter triggers, SimPlayer home locations, build order |

---

## 10. TWO-CLAUDE WORKFLOW

This project is worked on by two Claude instances:

**Master Claude (Claude 1)** — this session
- Integration code, bugfixes, hooks into existing ServUO systems
- Reviews and validates Claude 2's output
- Issues work instructions to Claude 2

**Claude 2** — separate Cowork session on another machine
- Implements new standalone modules that have no existing code yet
- Always works on a dedicated branch: `claude2/<topic>`
- Reads this file and relevant design docs before starting any task
- Signals completion by committing `ToDo Cowork/DONE - Claude2 - <topic> <date>`

**Work instruction files** live in `ToDo Cowork/`.
Naming: `Claude2 - <topic> - <YYYY-MM-DD>.md`
Format: defined in `ToDo Cowork/README.md`

**Workflow:**
1. Master Claude writes a work instruction and commits to the branch
2. User pulls on the Claude 2 machine
3. Claude 2 reads `COWORK_HANDOVER.md` + the work instruction + referenced design docs
4. Claude 2 implements on branch `claude2/<topic>`, commits
5. Claude 2 commits `ToDo Cowork/DONE - Claude2 - <topic> <date>` as final commit
6. User pulls on Master Claude machine
7. Master Claude reviews the diff, runs integration checks, deploys to test

---

## 11. DEPLOY AND TEST COMMANDS

Run these on the **server** (not your PC):
```bash
# Test deploy
cd /home/servuo && git pull --no-edit && bash /home/servuo/deploy-test.sh

# Prod deploy
cd /home/servuo && bash /home/servuo/deploy.sh

# Is test server up?
docker exec servuo-test bash -c "cat /proc/net/tcp | grep -q '00000000:0A22' && echo 'UP' || echo 'DOWN'"

# Wait for test server to finish loading
until docker exec servuo-test bash -c "cat /proc/net/tcp | grep -q '00000000:0A22'"; do echo "$(date +%H:%M:%S) loading..."; sleep 5; done && echo "UP"

# View server log
docker exec servuo bash -c "strings /home/servuo/servuo.log | tail -20"
```

**Screen is broken in the test container.** Test server is started via `docker exec -d`, never restart.sh.
**Check server up** with `/proc/net/tcp` — ss and netstat are not available in containers.

---

*End of handover document. If anything in this doc conflicts with the design docs, the design docs take precedence for feature decisions. This doc takes precedence for implementation status and workflow rules.*
