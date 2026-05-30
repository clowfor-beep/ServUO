# Claude 2 Work Instruction — Wandering Merchants
**Forsaken Britannia · Implementation Task**

## Your Job
Build two new NPC files from scratch based on the design doc at
`Design/WanderingMerchants_DesignDoc.md`. Read that file first — it is the source of truth
for all design decisions. This instruction tells you HOW to build; the design doc tells you WHAT to build.

Create two files:
- `Scripts/Custom/CurioCollectorNPC.cs`   ← Merchant 1
- `Scripts/Custom/GateVisitorMerchant.cs` ← Merchant 2

Both go in `Scripts/Custom/`. Both use `namespace Server.Custom`.

---

## Critical Rules (read before writing any code)

1. Every persistent class needs **two constructors** — `[Constructable]` and `(Serial serial)`.
2. Every persistent class must implement `Serialize` / `Deserialize` with a version int.
3. Never remove or reorder existing `reader.Read*()` calls. New fields go in `if (version >= N)` blocks.
4. Spellbooks → always `PackItem`, never `AddItem`.
5. `[Constructable]` constructor is what GMs use with `[add ClassName`.
6. Read `CLAUDE.md` in the root for the full pattern reference.

---

## Reference Files to Read Before Writing

| File | Why |
|---|---|
| `Scripts/Custom/TreasureHunterNPC.cs` | Best pattern for a double-click NPC with a gump. Follow its structure closely. |
| `Scripts/Custom/QuestFactory.cs` | How to build a multi-row gump with checkboxes and confirm buttons. |
| `Scripts/Custom/SimPlayer.cs` | Timer pattern, ForceIdle, DelayCall usage. |
| `Scripts/Items/Equipment/Instruments/BaseInstrument.cs` | Shows how to iterate AOS properties on items (GetDifficultyFor takes Mobile — shows what's available). |
| `Scripts/Services/LootGeneration/RunicReforging/RunicReforging.cs` | `GetIntensityForProperty` — use this to read property intensity for item scoring. |

---

## Part 1 — CurioCollectorNPC.cs

### Class Structure
```
CurioCollectorNPC : BaseCreature
  + MerchantCoin : Item              (new stackable currency — separate class in same file)
  + CurioCollectorGump : Gump        (appraisal gump — separate class in same file)
```

### MerchantCoin Item
Simple stackable item. Put it in the same file above `CurioCollectorNPC`.

```csharp
public class MerchantCoin : Item
{
    [Constructable] public MerchantCoin() : base(0xEED) // gold coin graphic
    {
        Name    = "merchant coin";
        Hue     = 1153;
        Weight  = 0.0;
        Stackable = true;
    }
    [Constructable] public MerchantCoin(int amount) : this() { Amount = amount; }
    public MerchantCoin(Serial serial) : base(serial) { }
    public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
    public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
}
```

### CurioCollectorNPC — NPC Class

**Constructor:**
```csharp
[Constructable]
public CurioCollectorNPC() : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
{
    Name  = "the Curio Collector";
    Title = "the appraiser";
    Body  = 0x190;  // human male
    Hue   = Utility.RandomSkinHue();
    // Set hair, clothing (dark travelling coat look — Robe hue 1109, Boots)
    // Stats: harmless NPC, no combat
    SetStr(50); SetDex(50); SetInt(100);
    SetHits(100);
    VirtualArmor = 10;
    Fame = 0; Karma = 1000;
    CantWalk = false;
}
```

**OnDoubleClick:**
```csharp
public override void OnDoubleClick(Mobile from)
{
    if (!from.InRange(Location, 4))
    { from.SendLocalizedMessage(500446); return; } // too far away

    Say("Let me have a look at what you're carrying...");
    var items = ScanPack(from);
    if (items.Count == 0)
    {
        Say("I see nothing worth my time today. Come back when you've found something rare.");
        return;
    }
    from.SendGump(new CurioCollectorGump(from, this, items));
}
```

### Item Scanning

**ScoredItem struct** (put at top of class or as a nested class):
```csharp
public class ScoredItem
{
    public Item   Item;
    public int    Score;
    public ItemTier Tier;
    public int    BasePrice;   // gold, or 0 if coin tier
    public int    CoinPrice;   // merchant coins, or 0 if gold tier
}

public enum ItemTier { Refuse, GoldMinor, GoldNotable, CoinsHighEnd, CoinsArtifact }
```

**ScanPack method** — recursive, returns only items that score above Refuse:
```csharp
private List<ScoredItem> ScanPack(Mobile from)
{
    var results = new List<ScoredItem>();
    if (from.Backpack != null)
        ScanContainer(from.Backpack, results);
    return results;
}

private void ScanContainer(Container c, List<ScoredItem> results)
{
    foreach (Item item in c.Items)
    {
        if (item is Container sub)
            ScanContainer(sub, results); // recurse into bags

        if (!IsEligibleItem(item)) continue;

        var scored = ScoreItem(item);
        if (scored.Tier != ItemTier.Refuse)
            results.Add(scored);
    }
}

private bool IsEligibleItem(Item item)
{
    if (item.Deleted || item.LootType == LootType.Blessed) return false;
    if (item is Container) return false;           // don't buy bags
    if (item is Gold || item is MerchantCoin) return false;
    if (item.Stackable && !(item is BaseWeapon) && !(item is BaseArmor)) return false;
    // Only weapons, armor, jewelry, clothing
    return item is BaseWeapon || item is BaseArmor || item is BaseJewel || item is BaseClothing;
}
```

### Item Scoring

This is the core logic. Implement `ScoreItem(Item item) -> ScoredItem`.

**Step 1 — Count properties and sum intensities:**

Use `RunicReforging.GetIntensityForProperty(item, prop)` where `prop` is a
`RunicReforging.ReforgingOption` value. Alternatively, iterate the known AOS attribute
groups directly:

```csharp
private static int GetPropertyScore(Item item)
{
    int propCount = 0;
    int intensitySum = 0;

    // Helper: add a property if its value is non-zero
    // intensity = actual value / max possible value * 100 (approximate)
    // For simplicity use the item's ArtifactRarity-aware scoring below.
    // A practical shortcut: use the count of non-zero props and rough intensity.

    if (item is BaseWeapon bw)
    {
        propCount   += CountAosAttributes(bw.Attributes, out int ai);    intensitySum += ai;
        propCount   += CountWeaponAttributes(bw.WeaponAttributes, out int wi); intensitySum += wi;
        propCount   += CountSkillBonuses(bw.SkillBonuses, out int si);   intensitySum += si;
    }
    else if (item is BaseArmor ba)
    {
        propCount   += CountAosAttributes(ba.Attributes, out int ai);    intensitySum += ai;
        propCount   += CountArmorAttributes(ba.ArmorAttributes, out int ri); intensitySum += ri;
        propCount   += CountSkillBonuses(ba.SkillBonuses, out int si);   intensitySum += si;
        propCount   += CountResistances(ba.Resistances, out int resi);   intensitySum += resi;
    }
    else if (item is BaseJewel bj)
    {
        propCount   += CountAosAttributes(bj.Attributes, out int ai);    intensitySum += ai;
        propCount   += CountSkillBonuses(bj.SkillBonuses, out int si);   intensitySum += si;
    }
    else if (item is BaseClothing bc)
    {
        propCount   += CountAosAttributes(bc.Attributes, out int ai);    intensitySum += ai;
        propCount   += CountSkillBonuses(bc.SkillBonuses, out int si);   intensitySum += si;
    }

    int score = (propCount * 15) + (int)(intensitySum * 0.4) + (item.ArtifactRarity * 60);
    return score;
}
```

**Helper methods** — count non-zero attributes and return a rough total intensity:

```csharp
private static int CountAosAttributes(AosAttributes attrs, out int intensity)
{
    intensity = 0; int count = 0;
    // Check each known AosAttribute property via reflection or explicit list.
    // Explicit is safer in ServUO:
    if (attrs.BonusStr > 0)     { count++; intensity += attrs.BonusStr; }
    if (attrs.BonusDex > 0)     { count++; intensity += attrs.BonusDex; }
    if (attrs.BonusInt > 0)     { count++; intensity += attrs.BonusInt; }
    if (attrs.BonusHits > 0)    { count++; intensity += attrs.BonusHits; }
    if (attrs.BonusStam > 0)    { count++; intensity += attrs.BonusStam; }
    if (attrs.BonusMana > 0)    { count++; intensity += attrs.BonusMana; }
    if (attrs.RegenHits > 0)    { count++; intensity += attrs.RegenHits * 5; }
    if (attrs.RegenStam > 0)    { count++; intensity += attrs.RegenStam * 5; }
    if (attrs.RegenMana > 0)    { count++; intensity += attrs.RegenMana * 5; }
    if (attrs.Luck > 0)         { count++; intensity += attrs.Luck / 5; }
    if (attrs.WeaponDamage > 0) { count++; intensity += attrs.WeaponDamage; }
    if (attrs.DefendChance > 0) { count++; intensity += attrs.DefendChance; }
    if (attrs.AttackChance > 0) { count++; intensity += attrs.AttackChance; }
    if (attrs.LowerManaCost > 0){ count++; intensity += attrs.LowerManaCost; }
    if (attrs.LowerRegCost > 0) { count++; intensity += attrs.LowerRegCost; }
    if (attrs.SpellChanneling > 0){ count++; intensity += 50; }
    if (attrs.CastSpeed > 0)    { count++; intensity += attrs.CastSpeed * 15; }
    if (attrs.CastRecovery > 0) { count++; intensity += attrs.CastRecovery * 15; }
    if (attrs.NightSight > 0)   { count++; intensity += 20; }
    if (attrs.ReflectPhysical > 0){ count++; intensity += attrs.ReflectPhysical; }
    if (attrs.EnhancePotions > 0){ count++; intensity += attrs.EnhancePotions; }
    if (attrs.Brittle != 0)     intensity -= 30; // penalty for brittle
    return count;
}

// Implement CountWeaponAttributes, CountArmorAttributes, CountSkillBonuses,
// CountResistances similarly — check each named field, add to count and intensity.
// Weapon attrs: HitLightning, HitFireball, HitColdArea, HitPoisonArea, HitFireArea,
//               HitPhysicalArea, HitMagicArrow, HitHarm, HitDispel, HitLeechHits,
//               HitLeechStam, HitLeechMana, SelfRepair, DurabilityBonus, etc.
// Armor attrs:  MageArmor, SelfRepair, DurabilityBonus, etc.
// Skill bonuses: iterate all 5 slots (bw.SkillBonuses.GetValues(i, out skill, out bonus))
// Resistances:  physical, fire, cold, poison, energy resist values on armor
```

**Step 2 — Assign tier and price:**

```csharp
private static ScoredItem ScoreItem(Item item)
{
    int score = GetPropertyScore(item);
    var si = new ScoredItem { Item = item, Score = score };

    if (score < 40)
    {
        si.Tier = ItemTier.Refuse;
    }
    else if (score < 100)
    {
        si.Tier      = ItemTier.GoldMinor;
        si.BasePrice = 2000 + (int)((score - 40) / 60.0 * 23000);
    }
    else if (score < 180)
    {
        si.Tier      = ItemTier.GoldNotable;
        si.BasePrice = 25000 + (int)((score - 100) / 80.0 * 75000);
    }
    else if (score < 280)
    {
        si.Tier      = ItemTier.CoinsHighEnd;
        si.CoinPrice = Math.Max(1, Math.Min(5, score / 50));
    }
    else
    {
        si.Tier      = ItemTier.CoinsArtifact;
        si.CoinPrice = Math.Max(5, Math.Min(25, score / 40));
    }

    return si;
}
```

### Diminishing Returns

Apply AFTER scoring, in the gump or just before payment. Given a sorted list of items
(highest score first) apply the multiplier by index:

```csharp
private static double GetMultiplier(int index)
{
    return Math.Max(0.30, 1.0 - (index * 0.15));
}
```

For coin-tier items, apply the multiplier to coins (round down, minimum 1).
For gold-tier items, apply to gold price (round to nearest 100).

### CurioCollectorGump

Builds on the pattern in `TreasureHunterNPC.cs` and `QuestFactory.cs`.

Layout per item row:
- Checkbox (checked by default for buyable items, disabled/greyed for Refuse)
- Item name (from `item.Name` or `item.GetType().Name` if no name)
- Tier label: "Minor", "Notable", "High-End", "Artifact"
- Full price (strikethrough style if discounted)
- Adjusted price (gold amount OR "X coins")

Footer row: total gold + total coins offered.

Buttons: `[Sell Selected]` (BTN_CONFIRM = 1) and `[No Thanks]` (BTN_CLOSE = 0).

On BTN_CONFIRM response:
1. Get the list of checked item indices from `info.Switches`.
2. Apply diminishing returns in order of index (sort highest score first before display,
   track that order in the response).
3. Delete each sold item from the world.
4. Add gold to player backpack (use `from.AddToBackpack(new Gold(amount))` — or bank if
   backpack is overweight).
5. Add coins to player backpack (`from.AddToBackpack(new MerchantCoin(coinCount))`).
6. Say a closing flavour line.

### Serialization
All champ state is transient. Serialize only what needs to survive a restart (nothing
special for this NPC — just call base):

```csharp
public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
```

---

## Part 2 — GateVisitorMerchant.cs

### Class Structure
```
GateVisitorMerchant : BaseCreature
  (all shopping logic inline — no separate gump needed)
```

### NPC Setup
```csharp
[Constructable]
public GateVisitorMerchant() : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
{
    Name  = "a travelling merchant";
    Title = "the buyer";
    Body  = 0x190;
    Hue   = Utility.RandomSkinHue();
    // ... appearance setup
    CantWalk = false;
}
```

### Shopping Timer

Use the `Initialize()` pattern — wait, this is an NPC not a static class. Use `OnThink`
or a private timer started from the constructor/Deserialize.

Start a repeating `ShoppingTimer` from `OnAfterSpawn()` (override) and from `Deserialize`:

```csharp
private ShoppingTimer _timer;

public override void OnAfterSpawn()
{
    base.OnAfterSpawn();
    StartTimer();
}

private void StartTimer()
{
    _timer?.Stop();
    double minutes = Utility.RandomMinMax(3, 8);
    _timer = new ShoppingTimer(this, TimeSpan.FromMinutes(minutes));
    _timer.Start();
}

private class ShoppingTimer : Timer
{
    private readonly GateVisitorMerchant _owner;
    public ShoppingTimer(GateVisitorMerchant owner, TimeSpan delay)
        : base(delay) { _owner = owner; Priority = TimerPriority.OneMinute; }

    protected override void OnTick()
    {
        if (_owner == null || _owner.Deleted) return;
        _owner.DoShoppingTrip();
        _owner.StartTimer(); // reschedule
    }
}
```

### DoShoppingTrip

```csharp
private void DoShoppingTrip()
{
    if (Map == Map.Internal) return;

    PlayerVendor vendor = FindNextVendor();
    if (vendor == null) return;

    // Gate effect at current location
    FixedParticles(0x6F9, 10, 30, 5052, EffectLayer.CenterFeet);
    PlaySound(0x20E);

    Point3D dest = vendor.Location;
    Map     map  = vendor.Map;

    Timer.DelayCall(TimeSpan.FromSeconds(2.0), () =>
    {
        if (Deleted) return;
        MoveToWorld(dest, map);
        FixedParticles(0x6F9, 10, 30, 5052, EffectLayer.CenterFeet);
        PlaySound(0x20E);

        // Mark vendor as visited
        _visitedVendors[vendor.Serial] = DateTime.UtcNow;

        // Shop after a short arrival pause
        Timer.DelayCall(TimeSpan.FromSeconds(3.0), () =>
        {
            if (!Deleted) BrowseVendor(vendor);
        });
    });
}
```

### FindNextVendor

```csharp
private PlayerVendor FindNextVendor()
{
    var candidates = new List<PlayerVendor>();
    DateTime cooldownCutoff = DateTime.UtcNow - TimeSpan.FromMinutes(30);

    foreach (Mobile m in World.Mobiles.Values)
    {
        if (!(m is PlayerVendor pv)) continue;
        if (pv.Deleted || pv.Map != Map.Felucca) continue;
        if (_visitedVendors.TryGetValue(pv.Serial, out DateTime last) && last > cooldownCutoff)
            continue;
        if (!HasQualifyingItems(pv)) continue;
        candidates.Add(pv);
    }

    return candidates.Count > 0 ? candidates[Utility.Random(candidates.Count)] : null;
}

private Dictionary<Serial, DateTime> _visitedVendors = new Dictionary<Serial, DateTime>();
```

### BrowseVendor

```csharp
private void BrowseVendor(PlayerVendor vendor)
{
    if (vendor == null || vendor.Deleted) return;

    int budget = Utility.RandomMinMax(20000, 60000);
    int spent  = 0;
    var toBuy  = new List<(Item item, int price)>();

    // Collect qualifying items from vendor's backpack / sell list
    foreach (Item item in vendor.GetItems()) // use vendor's container items
    {
        if (spent >= budget) break;
        int price = GetVendorPrice(vendor, item);
        if (price <= 0) continue;
        int cap = GetCategoryCapForItem(item);
        if (cap <= 0 || price > cap) continue;
        if (spent + price > budget) continue;
        toBuy.Add((item, price));
        spent += price;
    }

    if (toBuy.Count == 0)
    {
        Say("Nothing here catches my eye today.");
        return;
    }

    // Execute purchases
    foreach (var (item, price) in toBuy)
    {
        if (item.Deleted) continue;
        // Pay the vendor
        vendor.HoldGold += price;   // adds to the vendor's earned gold pile
        // Remove the item (delete — merchant "resells" elsewhere)
        item.Delete();
    }

    int itemCount = toBuy.Count;
    Say($"I'll take {itemCount} item{(itemCount == 1 ? "" : "s")}. Pleasure doing business.");
}
```

**Important:** `PlayerVendor.HoldGold` is the field that stores earned gold for the vendor
owner. Check the exact field name in `Scripts/Mobiles/PlayerVendor.cs` — it may be
`HoldGold` or `BankAccount` depending on the ServUO version. Use `[props` on a PlayerVendor
in-game to verify the field name.

**GetVendorPrice:** PlayerVendors store their item prices in a `VendorItem` list. Look at
how `PlayerVendor.GetVendorItem(Item item)` works — it returns a `VendorItem` with a
`Price` field. Use that to get the listed price.

```csharp
private static int GetVendorPrice(PlayerVendor vendor, Item item)
{
    VendorItem vi = vendor.GetVendorItem(item);
    return vi?.Price ?? 0;
}
```

### Category Cap Table

```csharp
private static int GetCategoryCapForItem(Item item)
{
    // Resources
    if (item is BaseIngot)         return 6;     // per unit
    if (item is Board || item is Log) return 4;
    if (item is Cloth || item is Leather || item is HideArmor) return 5;
    // Reagents
    if (item is BaseReagent)       return 7;
    // Potions
    if (item is BasePotion bp)
    {
        if (bp is HealPotion || bp is CurePotion || bp is StrengthPotion || bp is AgilityPotion)
            return 20;
        return 0; // don't buy other potions
    }
    // Food
    if (item is Food)              return 15;
    // Gems
    if (item is BaseGem)           return 80;
    // Scrolls
    if (item is SpellScroll ss)    return Math.Min(400, ss.Circle * 50);
    // Plain weapons/armor (no magic)
    if (item is BaseWeapon bw && !HasMagicProperties(bw)) return 400;
    if (item is BaseArmor ba && !HasMagicProperties(ba))  return 400;
    // Tools
    if (item is BaseTool)          return 150;
    return 0; // won't buy
}

private static bool HasMagicProperties(Item item)
{
    // Quick check: any non-zero AOS attributes = magic item
    if (item is BaseWeapon bw)
        return bw.Attributes.Count > 0 || bw.WeaponAttributes.Count > 0;
    if (item is BaseArmor ba)
        return ba.Attributes.Count > 0 || ba.ArmorAttributes.Count > 0;
    return false;
}
```

**Note:** Check whether `AosAttributes` has a `.Count` property or if you need to check
individual fields. If no `.Count`, use the same explicit field-checking approach as in the
CurioCollector scoring helpers.

### HasQualifyingItems

```csharp
private static bool HasQualifyingItems(PlayerVendor vendor)
{
    foreach (Item item in vendor.GetItems())
    {
        int price = GetVendorPrice(vendor, item);
        if (price > 0 && GetCategoryCapForItem(item) >= price)
            return true;
    }
    return false;
}
```

### Serialization

`_visitedVendors` is transient (cleared on restart = acceptable). Serialize nothing special:

```csharp
public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
public override void Deserialize(GenericReader r)
{
    base.Deserialize(r); r.ReadInt();
    StartTimer(); // restart shopping timer after world load
}
```

---

## Key Things to Verify in Existing Code Before Writing

Before you write the final code, grep/read these to confirm the exact API:

1. `grep -n "HoldGold\|BankAccount\|holdGold" Scripts/Mobiles/PlayerVendor.cs`
   → confirms the field name for adding earned gold to a vendor

2. `grep -n "GetVendorItem\|VendorItem" Scripts/Mobiles/PlayerVendor.cs`
   → confirms how to get the listed price for an item

3. `grep -n "GetItems\|VendorBackpack\|backpack" Scripts/Mobiles/PlayerVendor.cs`
   → confirms how to enumerate vendor stock (may be via `Backpack.Items` or a custom list)

4. `grep -n "AosAttributes\|class AosAttributes" Scripts/Items/Attributes.cs`
   → confirm field names for all AOS attribute groups

5. `grep -n "GetIntensityForProperty\|ReforgingOption" Scripts/Services/LootGeneration/RunicReforging/RunicReforging.cs`
   → confirm the intensity API if you want to use it instead of the explicit field approach

---

## Deployment
1. Create both `.cs` files in `Scripts/Custom/`
2. Restart the server (scripts auto-compile on startup)
3. Test with `[add CurioCollectorNPC` and `[add GateVisitorMerchant`
4. For Curio Collector: drop some magic items in your pack and double-click him
5. For Gate Visitor: wait 3–8 minutes and watch him gate to a player vendor

---

## What NOT to Do
- Do not use `BaseVendor` as the base class — these are not shop vendors
- Do not add `[CommandProperty]` props that expose internal transient state
- Do not serialize `_visitedVendors` — it's intentionally transient
- Do not use `Timer.DelayCall` for the repeating shopping loop — use a proper `Timer` subclass
- Do not buy from vendors on Trammel or non-Felucca maps (Gate Visitor)
