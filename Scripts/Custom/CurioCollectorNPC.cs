// ============================================================
// CurioCollectorNPC.cs
// Scripts/Custom/CurioCollectorNPC.cs
//
// Wandering appraiser NPC who buys magic equipment from players
// for gold and merchant coins.
//
// Usage: [add CurioCollectorNPC
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ── MerchantCoin ─────────────────────────────────────────────────────────

    public class MerchantCoin : Item
    {
        [Constructable]
        public MerchantCoin() : base(0xEED)
        {
            Name      = "merchant coin";
            Hue       = 1153;
            Weight    = 0.0;
            Stackable = true;
        }

        [Constructable]
        public MerchantCoin(int amount) : this()
        {
            Amount = amount;
        }

        public MerchantCoin(Serial serial) : base(serial) { }

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
    }

    // ── Item scoring helpers ──────────────────────────────────────────────────

    public enum ItemTier { Refuse, GoldMinor, GoldNotable, CoinsHighEnd, CoinsArtifact }

    public class ScoredItem
    {
        public Item     Item;
        public int      Score;
        public ItemTier Tier;
        public int      BasePrice;  // gold, or 0 if coin tier
        public int      CoinPrice;  // coins, or 0 if gold tier
    }

    // ── CurioCollectorNPC ─────────────────────────────────────────────────────

    public class CurioCollectorNPC : BaseCreature
    {
        [Constructable]
        public CurioCollectorNPC()
            : base(AIType.AI_Melee, FightMode.None, 10, 1, 0.2, 0.4)
        {
            Name  = "the Curio Collector";
            Title = "the appraiser";
            Body  = 0x190;
            Hue   = Utility.RandomSkinHue();

            SetStr(50); SetDex(50); SetInt(100);
            SetHits(100);
            VirtualArmor = 10;
            Fame  = 0;
            Karma = 1000;
            CantWalk = false;

            // Appearance
            Utility.AssignRandomHair(this);
            AddItem(new Robe(1109));
            AddItem(new Boots());
        }

        public CurioCollectorNPC(Serial serial) : base(serial) { }

        // ── Double-click ──────────────────────────────────────────────────────

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(Location, 4))
            {
                from.SendLocalizedMessage(500446); // too far away
                return;
            }

            Say("Let me have a look at what you're carrying...");
            var items = ScanPack(from);

            if (items.Count == 0)
            {
                Say("I see nothing worth my time today. Come back when you've found something rare.");
                return;
            }

            from.SendGump(new CurioCollectorGump(from, this, items));
        }

        // ── Item scanning ─────────────────────────────────────────────────────

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
                    ScanContainer(sub, results);

                if (!IsEligibleItem(item)) continue;

                var scored = ScoreItem(item);
                if (scored.Tier != ItemTier.Refuse)
                    results.Add(scored);
            }
        }

        private static bool IsEligibleItem(Item item)
        {
            if (item.Deleted || item.LootType == LootType.Blessed) return false;
            if (item is Container) return false;
            if (item is Gold || item is MerchantCoin) return false;
            if (item.Stackable && !(item is BaseWeapon) && !(item is BaseArmor)) return false;
            return item is BaseWeapon || item is BaseArmor || item is BaseJewel || item is BaseClothing;
        }

        // ── Scoring ───────────────────────────────────────────────────────────

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

        private static int GetPropertyScore(Item item)
        {
            int propCount    = 0;
            int intensitySum = 0;

            if (item is BaseWeapon bw)
            {
                propCount   += CountAosAttributes(bw.Attributes, out int ai);       intensitySum += ai;
                propCount   += CountWeaponAttributes(bw.WeaponAttributes, out int wi); intensitySum += wi;
                propCount   += CountSkillBonuses(bw.SkillBonuses, out int si);     intensitySum += si;
            }
            else if (item is BaseArmor ba)
            {
                propCount   += CountAosAttributes(ba.Attributes, out int ai);       intensitySum += ai;
                propCount   += CountArmorAttributes(ba.ArmorAttributes, out int ri); intensitySum += ri;
                propCount   += CountSkillBonuses(ba.SkillBonuses, out int si);     intensitySum += si;
                propCount   += CountResistances(ba.PhysicalBonus, ba.FireBonus, ba.ColdBonus, ba.PoisonBonus, ba.EnergyBonus, out int resi); intensitySum += resi;
            }
            else if (item is BaseJewel bj)
            {
                propCount   += CountAosAttributes(bj.Attributes, out int ai);       intensitySum += ai;
                propCount   += CountSkillBonuses(bj.SkillBonuses, out int si);     intensitySum += si;
            }
            else if (item is BaseClothing bc)
            {
                propCount   += CountAosAttributes(bc.Attributes, out int ai);       intensitySum += ai;
                propCount   += CountSkillBonuses(bc.SkillBonuses, out int si);     intensitySum += si;
            }

            return (propCount * 15) + (int)(intensitySum * 0.4) + (GetArtifactRarity(item) * 60);
        }

        private static int CountAosAttributes(AosAttributes attrs, out int intensity)
        {
            intensity = 0; int count = 0;

            if (attrs.BonusStr > 0)         { count++; intensity += attrs.BonusStr; }
            if (attrs.BonusDex > 0)         { count++; intensity += attrs.BonusDex; }
            if (attrs.BonusInt > 0)         { count++; intensity += attrs.BonusInt; }
            if (attrs.BonusHits > 0)        { count++; intensity += attrs.BonusHits; }
            if (attrs.BonusStam > 0)        { count++; intensity += attrs.BonusStam; }
            if (attrs.BonusMana > 0)        { count++; intensity += attrs.BonusMana; }
            if (attrs.RegenHits > 0)        { count++; intensity += attrs.RegenHits * 5; }
            if (attrs.RegenStam > 0)        { count++; intensity += attrs.RegenStam * 5; }
            if (attrs.RegenMana > 0)        { count++; intensity += attrs.RegenMana * 5; }
            if (attrs.Luck > 0)             { count++; intensity += attrs.Luck / 5; }
            if (attrs.WeaponDamage > 0)     { count++; intensity += attrs.WeaponDamage; }
            if (attrs.DefendChance > 0)     { count++; intensity += attrs.DefendChance; }
            if (attrs.AttackChance > 0)     { count++; intensity += attrs.AttackChance; }
            if (attrs.LowerManaCost > 0)    { count++; intensity += attrs.LowerManaCost; }
            if (attrs.LowerRegCost > 0)     { count++; intensity += attrs.LowerRegCost; }
            if (attrs.SpellChanneling > 0)  { count++; intensity += 50; }
            if (attrs.CastSpeed > 0)        { count++; intensity += attrs.CastSpeed * 15; }
            if (attrs.CastRecovery > 0)     { count++; intensity += attrs.CastRecovery * 15; }
            if (attrs.NightSight > 0)       { count++; intensity += 20; }
            if (attrs.ReflectPhysical > 0)  { count++; intensity += attrs.ReflectPhysical; }
            if (attrs.EnhancePotions > 0)   { count++; intensity += attrs.EnhancePotions; }
            if (attrs.Brittle != 0)         intensity -= 30;

            return count;
        }

        private static int CountWeaponAttributes(AosWeaponAttributes attrs, out int intensity)
        {
            intensity = 0; int count = 0;

            if (attrs.HitLightning > 0)    { count++; intensity += attrs.HitLightning; }
            if (attrs.HitFireball > 0)     { count++; intensity += attrs.HitFireball; }
            if (attrs.HitColdArea > 0)     { count++; intensity += attrs.HitColdArea; }
            if (attrs.HitPoisonArea > 0)   { count++; intensity += attrs.HitPoisonArea; }
            if (attrs.HitFireArea > 0)     { count++; intensity += attrs.HitFireArea; }
            if (attrs.HitPhysicalArea > 0) { count++; intensity += attrs.HitPhysicalArea; }
            if (attrs.HitMagicArrow > 0)   { count++; intensity += attrs.HitMagicArrow; }
            if (attrs.HitHarm > 0)         { count++; intensity += attrs.HitHarm; }
            if (attrs.HitDispel > 0)       { count++; intensity += attrs.HitDispel; }
            if (attrs.HitLeechHits > 0)    { count++; intensity += attrs.HitLeechHits; }
            if (attrs.HitLeechStam > 0)    { count++; intensity += attrs.HitLeechStam; }
            if (attrs.HitLeechMana > 0)    { count++; intensity += attrs.HitLeechMana; }
            if (attrs.SelfRepair > 0)      { count++; intensity += attrs.SelfRepair * 10; }
            if (attrs.DurabilityBonus > 0) { count++; intensity += attrs.DurabilityBonus / 5; }
            if (attrs.ResistPhysicalBonus > 0) { count++; intensity += attrs.ResistPhysicalBonus; }

            return count;
        }

        private static int CountArmorAttributes(AosArmorAttributes attrs, out int intensity)
        {
            intensity = 0; int count = 0;

            if (attrs.MageArmor > 0)       { count++; intensity += 40; }
            if (attrs.SelfRepair > 0)      { count++; intensity += attrs.SelfRepair * 10; }
            if (attrs.DurabilityBonus > 0) { count++; intensity += attrs.DurabilityBonus / 5; }

            return count;
        }

        private static int CountSkillBonuses(AosSkillBonuses bonuses, out int intensity)
        {
            intensity = 0; int count = 0;

            for (int i = 0; i < 5; i++)
            {
                SkillName skill;
                double bonus;
                if (bonuses.GetValues(i, out skill, out bonus) && bonus > 0)
                {
                    count++;
                    intensity += (int)(bonus * 2);
                }
            }

            return count;
        }

        private static int GetArtifactRarity(Item item)
        {
            if (item is BaseWeapon bw) return bw.ArtifactRarity;
            if (item is BaseArmor  ba) return ba.ArtifactRarity;
            if (item is BaseJewel  bj) return bj.ArtifactRarity;
            return 0;
        }

        private static int CountResistances(int phys, int fire, int cold, int pois, int nrgy, out int intensity)
        {
            intensity = 0; int count = 0;

            if (phys > 0) { count++; intensity += phys; }
            if (fire > 0) { count++; intensity += fire; }
            if (cold > 0) { count++; intensity += cold; }
            if (pois > 0) { count++; intensity += pois; }
            if (nrgy > 0) { count++; intensity += nrgy; }

            return count;
        }

        // ── Diminishing returns ───────────────────────────────────────────────

        public static double GetMultiplier(int index)
        {
            return Math.Max(0.30, 1.0 - (index * 0.15));
        }

        // ── Serialization ─────────────────────────────────────────────────────

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

        // ── Gump ─────────────────────────────────────────────────────────────

        private class CurioCollectorGump : Gump
        {
            private readonly Mobile             _from;
            private readonly CurioCollectorNPC  _npc;
            private readonly List<ScoredItem>   _items; // sorted highest score first

            private const int BTN_CLOSE   = 0;
            private const int BTN_CONFIRM  = 1;

            public CurioCollectorGump(Mobile from, CurioCollectorNPC npc, List<ScoredItem> items)
                : base(50, 50)
            {
                _from = from;
                _npc  = npc;

                // Sort highest score first for diminishing returns
                items.Sort((a, b) => b.Score.CompareTo(a.Score));
                _items = items;

                int gumpWidth  = 560;
                int rowHeight  = 22;
                int headerY    = 60;
                int rowsStart  = 90;
                int rows       = _items.Count;
                int gumpHeight = rowsStart + rows * rowHeight + 80;

                AddBackground(0, 0, gumpWidth, gumpHeight, 9200);
                AddAlphaRegion(5, 5, gumpWidth - 10, gumpHeight - 10);

                // Title
                AddLabel(gumpWidth / 2 - 100, 12, 0x35, "Curio Collector — Appraisal");

                // Column headers
                AddLabel(38,  headerY, 0x3B2, "Sell");
                AddLabel(70,  headerY, 0x3B2, "Item");
                AddLabel(280, headerY, 0x3B2, "Tier");
                AddLabel(360, headerY, 0x3B2, "Full Price");
                AddLabel(450, headerY, 0x3B2, "Offer");

                int totalGold  = 0;
                int totalCoins = 0;

                for (int i = 0; i < _items.Count; i++)
                {
                    ScoredItem si = _items[i];
                    int y = rowsStart + i * rowHeight;
                    double mult = GetMultiplier(i);

                    // Checkbox (checked by default)
                    AddCheck(38, y + 2, 0xD2, 0xD3, true, i);

                    // Name
                    string name = !string.IsNullOrEmpty(si.Item.Name)
                        ? si.Item.Name
                        : si.Item.GetType().Name;
                    if (name.Length > 28) name = name.Substring(0, 28);
                    AddLabel(70, y, 0xFFFF, name);

                    // Tier label and colour
                    int tierHue;
                    string tierLabel;
                    switch (si.Tier)
                    {
                        case ItemTier.GoldMinor:    tierLabel = "Minor";    tierHue = 0x9C2; break;
                        case ItemTier.GoldNotable:  tierLabel = "Notable";  tierHue = 0x35;  break;
                        case ItemTier.CoinsHighEnd: tierLabel = "High-End"; tierHue = 0x4F;  break;
                        case ItemTier.CoinsArtifact:tierLabel = "Artifact"; tierHue = 0x21;  break;
                        default:                    tierLabel = "—";        tierHue = 0x3B2; break;
                    }
                    AddLabel(280, y, tierHue, tierLabel);

                    // Full price (un-adjusted)
                    if (si.BasePrice > 0)
                    {
                        AddLabel(360, y, 0x9C2, $"{si.BasePrice:N0}g");
                        int adjusted = (int)Math.Round(si.BasePrice * mult / 100.0) * 100;
                        adjusted = Math.Max(100, adjusted);
                        AddLabel(450, y, 0x35, $"{adjusted:N0}g");
                        totalGold += adjusted;
                    }
                    else
                    {
                        int adjCoins = Math.Max(1, (int)(si.CoinPrice * mult));
                        AddLabel(360, y, 0x4F, $"{si.CoinPrice}c");
                        AddLabel(450, y, 0x21, $"{adjCoins}c");
                        totalCoins += adjCoins;
                    }
                }

                // Footer totals
                int footerY = rowsStart + rows * rowHeight + 10;
                AddLabel(70,  footerY, 0x3B2, $"Total gold: {totalGold:N0}  |  Total coins: {totalCoins}");

                // Buttons
                int btnY = footerY + 28;
                AddButton(70,  btnY, 4005, 4007, BTN_CONFIRM, GumpButtonType.Reply, 0);
                AddLabel(106,  btnY + 2, 0x35, "Sell Selected");
                AddButton(260, btnY, 4017, 4019, BTN_CLOSE, GumpButtonType.Reply, 0);
                AddLabel(296,  btnY + 2, 33, "No Thanks");
            }

            public override void OnResponse(NetState sender, RelayInfo info)
            {
                if (info.ButtonID == BTN_CLOSE || _npc == null || _npc.Deleted)
                    return;

                if (!_from.InRange(_npc.Location, 6))
                {
                    _from.SendMessage("You have moved too far away.");
                    return;
                }

                // Build set of checked indices
                var checkedSet = new HashSet<int>(info.Switches);
                if (checkedSet.Count == 0)
                {
                    _npc.Say("Nothing selected — come back when you have something to sell.");
                    return;
                }

                int goldTotal  = 0;
                int coinTotal  = 0;
                int soldCount  = 0;

                for (int i = 0; i < _items.Count; i++)
                {
                    if (!checkedSet.Contains(i)) continue;

                    ScoredItem si = _items[i];
                    if (si.Item == null || si.Item.Deleted) continue;
                    if (!si.Item.IsChildOf(_from.Backpack)) continue;

                    double mult = GetMultiplier(i);

                    if (si.BasePrice > 0)
                    {
                        int adjusted = (int)Math.Round(si.BasePrice * mult / 100.0) * 100;
                        adjusted = Math.Max(100, adjusted);
                        goldTotal += adjusted;
                    }
                    else
                    {
                        int adjCoins = Math.Max(1, (int)(si.CoinPrice * mult));
                        coinTotal += adjCoins;
                    }

                    si.Item.Delete();
                    soldCount++;
                }

                if (soldCount == 0)
                {
                    _npc.Say("It seems those items have already left your possession.");
                    return;
                }

                // Pay gold
                if (goldTotal > 0)
                {
                    if (_from.Backpack != null && !_from.Backpack.Deleted)
                        _from.AddToBackpack(new Gold(goldTotal));
                    else
                        Banker.Deposit(_from, goldTotal);
                }

                // Pay coins
                if (coinTotal > 0)
                    _from.AddToBackpack(new MerchantCoin(coinTotal));

                _npc.Say($"A fine transaction. I've paid you {goldTotal:N0} gold" +
                         (coinTotal > 0 ? $" and {coinTotal} merchant coin{(coinTotal == 1 ? "" : "s")}" : "") +
                         ". Safe travels.");
            }
        }
    }
}
