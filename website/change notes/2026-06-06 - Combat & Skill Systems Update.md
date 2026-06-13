# Combat & Skill Systems Update
**Date:** 2026-06-06  
**Shard:** AIther (Forsaken Britannia)

---

## Backstab System

**Mechanic:** Attacking from stealth triggers a backstab damage bonus.

- Requires **80+ Hiding** AND **80+ Stealth** to activate
- Applies to both melee and **ranged weapons (bows)**
- **PvM only** — no effect vs players
- Bonus scales with Stealth skill: `(Stealth / 100) × 200%` = up to **+200% damage (3× total)** at GM Stealth
- At 80 Stealth (minimum): **+160% (2.6× total damage)**
- A missed attack from stealth **cancels** the pending bonus — you must actually land the hit
- **+50% hit chance bonus** applies on the same swing (outside item HCI cap)

---

## Shadow Strike (Force Arrow — Elven Composite Longbow)

**Mechanic:** Force Arrow gains Shadow Strike behaviour when the archer has 80+ Stealth.

- Activate via the **Primary Ability button** on the Elven Composite Longbow
- Requires **80+ Stealth** to trigger shadow mode
- On hit: hides the attacker, drops combat/warmode, grants stealth steps
- Without 80 Stealth: Force Arrow works normally (defense chance debuff on target)
- Designed to chain with backstab: **hide → Force Arrow shot hides you again → next shot gets backstab bonus**

---

## Parrying

**Reworked.**

### Block Chance
- `50% × (Parry / 100)` for both shield and two-handed weapons
- **No Bushido requirement** for the base chance
- At GM Parry: **50% block chance**

### Damage Reduction on Block
- **Base (no Bushido): 50% reduction**
- Each 20 Bushido adds **+5% reduction**

| Bushido | Damage Reduction |
|---------|-----------------|
| 0       | 50%             |
| 20      | 55%             |
| 40      | 60%             |
| 60      | 65%             |
| 80      | 70%             |
| 100     | 75%             |
| 120     | 80%             |

### Creature Spell Parry (Bushido)
- Requires Bushido > 0
- Chance: `40% × (Parry / 100)`
- Reduction scales with Bushido: **8% per 20 Bushido** (0% without Bushido, 40% at Bushido 100)

| Bushido | Spell Damage Reduction on Parry |
|---------|---------------------------------|
| 0       | 0% (no effect)                  |
| 20      | 8%                              |
| 60      | 24%                             |
| 100     | 40%                             |
| 120     | 48%                             |

---

## Anatomy — Passive Damage Reduction

- **1% damage reduction per 10 Anatomy skill**
- Applies to ALL incoming damage (melee, spell, ranged)
- Stacks with armour, parry, and resists

| Anatomy | Damage Reduction |
|---------|-----------------|
| 50      | 5%              |
| 100     | 10%             |
| 120     | 12%             |

*Anatomy also retains its existing +5% weapon damage bonus and +50% bandage heal bonus (combined with Healing).*

---

## Magic Resist (Resisting Spells) — Reworked

### 1. Spell Damage Reduction (all spells)
- **5% reduction per 20 Magic Resist**
- Applies to all incoming spell damage

| Magic Resist | Spell Damage Reduction |
|-------------|----------------------|
| 20          | 5%                   |
| 60          | 15%                  |
| 100         | 25%                  |
| 120         | 30%                  |

### 2. Non-Damaging Spell Resist
- Chance to fully resist curses, poison, paralysis, stat debuffs
- `(40% - Circle × 5%) × (MagicResist / 100)`

| Circle | Resist Chance at GM |
|--------|-------------------|
| 1      | 35%               |
| 2      | 30%               |
| 4      | 20%               |
| 6      | 10%               |
| 7      | 5%                |
| 8      | 0%                |

### 3. Spell Absorption (PvM)
- **25% × (MagicResist / 100)** chance to absorb a creature spell
- On absorption: **75% damage reduction** + parry visual effect
- At GM Magic Resist: **25% absorption chance**

### 4. Spell Siphon (PvM Buff)
- Triggered the **first time a spell hits you every 5 minutes**
- Buff lasts **60 minutes**
- Grants PvM bonuses scaled to `10% × (MagicResist / 100)`:
  - **Swing Speed increase**
  - **Mana refund chance** on spell cast
  - **Damage resistance**
- Shown as a buff icon; sends expiry notification when it ends
- At GM Magic Resist: +10% to each bonus

---

## Dungeon Chest Scrolls

Scroll circles now scale with chest difficulty — lower-level scrolls removed from higher-tier chests.

| Chest Level | Scroll Circles |
|-------------|---------------|
| Level 2     | Circles 4–5   |
| Level 3     | Circles 5–6   |
| Level 4     | Circles 6–7   |
| Level 5     | Circles 6–8   |

Paragon chests also updated: minimum circle 4 (was circle 1).

---

## Bug Fixes

- **Power scroll values corrected**: `CreateRandom(105, 110)` was generating 205/210 scrolls due to API misuse. Fixed to produce 105 and 110 scrolls as intended.
- **Weapon ability button feedback**: Activating a weapon special ability now correctly lights up the button (red) in ClassicUO.
