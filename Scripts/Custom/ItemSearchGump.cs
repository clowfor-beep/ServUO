// ============================================================
// ItemSearchGump.cs
// Scripts/Custom/ItemSearchGump.cs
//
// Searches a player's backpack, bank box, and all containers
// in houses where the player is owner or co-owner.
//
// Usage: [itemsearch  (player command)
// ============================================================

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Spells;

namespace Server.Gumps
{
    // ── Result record ──────────────────────────────────────────────
    public class ItemSearchResult
    {
        public string ItemName;
        public int    Amount;
        public string Location;
        public int    Serial;
        public int    ItemID;
        public int    Hue;

        public ItemSearchResult(string itemName, int amount, string location, int serial, int itemID, int hue)
        {
            ItemName = itemName;
            Amount   = amount;
            Location = location;
            Serial   = serial;
            ItemID   = itemID;
            Hue      = hue;
        }
    }

    // ── Gump ───────────────────────────────────────────────────────
    public class ItemSearchGump : Gump
    {
        // Layout
        private const int W       = 620;
        private const int H       = 560;
        private const int PadX    = 15;
        private const int RowH    = 40;
        private const int MaxRows = 10;
        private const int MinChars = 3;

        // Column X positions
        private const int ColIcon = PadX;
        private const int ColName = PadX + 46;
        private const int ColQty  = 330;
        private const int ColLoc  = 390;

        // HTML hex colours (6-digit, web-safe)
        private const string ColTitle   = "#C8A428";   // gold     – title
        private const string ColHeader  = "#6699CC";   // steel blue – column headers
        private const string ColRowEven = "#DDCCAA";   // tan      – row text (even)
        private const string ColRowOdd  = "#BBAA88";   // darker tan – row text (odd)
        private const string ColHint    = "#888888";   // grey     – idle hint
        private const string ColOk      = "#88CC88";   // green    – found results
        private const string ColWarn    = "#FF8844";   // orange   – warning/empty

        // Button IDs
        private const int BTN_SEARCH   = 1;
        private const int BTN_PREVPAGE = 2;
        private const int BTN_NEXTPAGE = 3;

        private readonly PlayerMobile           _player;
        private readonly string                 _query;
        private readonly List<ItemSearchResult> _results;
        private readonly int                    _page;

        // ── Command registration ───────────────────────────────────

        public static void Initialize()
        {
            CommandSystem.Register("itemsearch", AccessLevel.Player, OnCommand);
        }

        private static void OnCommand(CommandEventArgs e)
        {
            if (!(e.Mobile is PlayerMobile pm)) return;
            pm.CloseGump(typeof(ItemSearchGump));
            pm.SendGump(new ItemSearchGump(pm, "", null, 0));
        }

        // ── Constructor ────────────────────────────────────────────

        public ItemSearchGump(PlayerMobile player, string query, List<ItemSearchResult> results, int page)
            : base(100, 80)
        {
            _player  = player;
            _query   = query  ?? "";
            _results = results ?? new List<ItemSearchResult>();
            _page    = page;

            Closable   = true;
            Disposable = true;
            Dragable   = true;
            Resizable  = false;

            Build();
        }

        // ── Layout ─────────────────────────────────────────────────

        private void Build()
        {
            int startIdx      = _page * MaxRows;
            int resultsOnPage = Math.Max(0, Math.Min(MaxRows, _results.Count - startIdx));
            int totalPages    = _results.Count > 0 ? (_results.Count + MaxRows - 1) / MaxRows : 1;

            // ── Background ─────────────────────────────────────────
            AddBackground(0, 0, W, H, 30536);

            // ── Title ──────────────────────────────────────────────
            AddHtml(PadX, 14, W - PadX * 2, 22,
                $"<BASEFONT COLOR={ColTitle}><B>Item Search</B></BASEFONT>",
                false, false);

            // Divider under title
            AddImageTiled(PadX, 38, W - PadX * 2, 1, 9264);

            // ── Search row ─────────────────────────────────────────
            AddHtml(PadX, 46, 60, 22,
                $"<BASEFONT COLOR={ColHeader}>Search:</BASEFONT>", false, false);

            AddBackground(PadX + 62, 44, 340, 24, 9350);
            AddTextEntry(PadX + 65, 46, 334, 20, 0x9C2, 0, _query);

            AddButton(PadX + 416, 44, 30534, 30534, BTN_SEARCH, GumpButtonType.Reply, 0);
            AddHtml(PadX + 456, 46, 70, 20,
                $"<BASEFONT COLOR={ColHeader}>Search</BASEFONT>", false, false);

            // ── Hint / status ──────────────────────────────────────
            string hint;
            string hintCol;

            if (_query.Length == 0)
            {
                hint    = "Searches backpack, bank, and all house containers (owner or co-owner).";
                hintCol = ColHint;
            }
            else if (_query.Length < MinChars)
            {
                hint    = $"Enter at least {MinChars} characters to search.";
                hintCol = ColWarn;
            }
            else if (_results.Count == 0)
            {
                hint    = $"No items found matching \"{_query}\".";
                hintCol = ColWarn;
            }
            else
            {
                hint    = $"{_results.Count} result{(_results.Count == 1 ? "" : "s")} for \"{_query}\"   —   Page {_page + 1} of {totalPages}";
                hintCol = ColOk;
            }

            AddHtml(PadX, 74, W - PadX * 2, 20,
                $"<BASEFONT COLOR={hintCol}>{hint}</BASEFONT>", false, false);

            // ── Column headers ─────────────────────────────────────
            int y = 100;
            AddHtml(ColName, y, 180, 18,
                $"<BASEFONT COLOR={ColHeader}><B>Item</B></BASEFONT>", false, false);
            AddHtml(ColQty, y, 55, 18,
                $"<BASEFONT COLOR={ColHeader}><B>Qty</B></BASEFONT>", false, false);
            AddHtml(ColLoc, y, W - ColLoc - PadX, 18,
                $"<BASEFONT COLOR={ColHeader}><B>Location</B></BASEFONT>", false, false);

            y += 20;
            AddImageTiled(PadX, y, W - PadX * 2, 1, 9264);
            y += 4;

            // ── Result rows ────────────────────────────────────────
            if (resultsOnPage > 0)
            {
                for (int i = startIdx; i < startIdx + resultsOnPage; i++)
                {
                    var    r      = _results[i];
                    string rowCol = (i % 2 == 0) ? ColRowEven : ColRowOdd;

                    // Subtle alternating background
                    if (i % 2 == 0)
                        AddAlphaRegion(PadX, y, W - PadX * 2, RowH);

                    // Item icon — hovering shows property tooltip
                    AddImageTiledButton(ColIcon, y + 4, 0x918, 0x918, 0, GumpButtonType.Page, 0,
                        r.ItemID, r.Hue, 0, 0);
                    AddItemProperty(r.Serial);

                    int textY = y + (RowH - 18) / 2;

                    AddHtml(ColName, textY, 280, 18,
                        $"<BASEFONT COLOR={rowCol}>{r.ItemName}</BASEFONT>", false, false);
                    AddHtml(ColQty, textY, 55, 18,
                        $"<BASEFONT COLOR={rowCol}>{(r.Amount > 1 ? r.Amount.ToString() : "—")}</BASEFONT>",
                        false, false);
                    AddHtml(ColLoc, textY, W - ColLoc - PadX, 18,
                        $"<BASEFONT COLOR={rowCol}>{r.Location}</BASEFONT>", false, false);

                    y += RowH;
                }
            }

            // ── Pagination ─────────────────────────────────────────
            if (totalPages > 1)
            {
                int navY = H - 36;
                AddImageTiled(PadX, navY - 6, W - PadX * 2, 1, 9264);

                if (_page > 0)
                {
                    AddButton(PadX, navY, 30533, 30533, BTN_PREVPAGE, GumpButtonType.Reply, 0);
                    AddHtml(PadX + 40, navY + 2, 80, 18,
                        $"<BASEFONT COLOR={ColHeader}>Previous</BASEFONT>", false, false);
                }

                if (_page < totalPages - 1)
                {
                    AddButton(W - 76, navY, 30534, 30534, BTN_NEXTPAGE, GumpButtonType.Reply, 0);
                    AddHtml(W - 116, navY + 2, 35, 18,
                        $"<BASEFONT COLOR={ColHeader}>Next</BASEFONT>", false, false);
                }
            }
        }

        // ── Response ───────────────────────────────────────────────

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (!(sender.Mobile is PlayerMobile pm)) return;

            string query = info.GetTextEntry(0)?.Text?.Trim() ?? "";

            switch (info.ButtonID)
            {
                case 0: return; // closed

                case BTN_SEARCH:
                {
                    if (query.Length < MinChars)
                    {
                        pm.SendGump(new ItemSearchGump(pm, query, null, 0));
                        return;
                    }
                    var results = DoSearch(pm, query);
                    pm.SendGump(new ItemSearchGump(pm, query, results, 0));
                    break;
                }

                case BTN_PREVPAGE:
                    pm.SendGump(new ItemSearchGump(pm, _query, _results, Math.Max(0, _page - 1)));
                    break;

                case BTN_NEXTPAGE:
                {
                    int totalPages = (_results.Count + MaxRows - 1) / MaxRows;
                    pm.SendGump(new ItemSearchGump(pm, _query, _results, Math.Min(totalPages - 1, _page + 1)));
                    break;
                }
            }
        }

        // ── Search logic ───────────────────────────────────────────

        private static List<ItemSearchResult> DoSearch(PlayerMobile player, string query)
        {
            var results = new List<ItemSearchResult>();
            query = query.ToLowerInvariant();

            // 1. Backpack (recursive — containers within containers)
            if (player.Backpack != null)
                ScanContainer(player.Backpack, query, "Backpack", results, player);

            // 2. Bank box (recursive)
            if (player.BankBox != null)
                ScanContainer(player.BankBox, query, "Bank", results, player);

            // 3. All houses where owner or co-owner
            foreach (BaseHouse house in BaseHouse.AllHouses)
            {
                if (house == null || house.Deleted) continue;

                bool isOwner   = house.IsOwner(player);
                bool isCoOwner = !isOwner && house.IsCoOwner(player);

                if (!isOwner && !isCoOwner) continue;

                string houseLabel = isOwner ? "Your House" : "Co-owned House";

                // Secured containers
                if (house.Secures != null)
                {
                    foreach (SecureInfo si in house.Secures)
                    {
                        if (si?.Item == null || si.Item.Deleted) continue;
                        if (!(si.Item is Container cont)) continue;
                        ScanContainer(cont, query, $"{houseLabel} › {GetLabel(cont)}", results, player);
                    }
                }

                // Locked-down containers (skip any already covered by Secures)
                if (house.LockDowns != null)
                {
                    foreach (Item item in house.LockDowns.Keys)
                    {
                        if (item == null || item.Deleted) continue;
                        if (!(item is Container lc)) continue;
                        if (house.Secures != null && house.Secures.Exists(si => si?.Item == item)) continue;
                        ScanContainer(lc, query, $"{houseLabel} › {GetLabel(lc)}", results, player);
                    }
                }
            }

            return results;
        }

        /// <summary>Recursively scans a container and all sub-containers.</summary>
        private static void ScanContainer(Container cont, string query,
                                           string location, List<ItemSearchResult> results,
                                           PlayerMobile player)
        {
            if (cont == null) return;

            foreach (Item item in cont.Items)
            {
                if (item == null || item.Deleted) continue;

                string label      = GetLabel(item);
                string searchable = (label + " " + GetItemProperties(item)).ToLowerInvariant();

                if (searchable.Contains(query))
                {
                    item.SendPropertiesTo(player);
                    results.Add(new ItemSearchResult(
                        Capitalise(label), item.Amount, location,
                        item.Serial.Value, item.ItemID, item.Hue));
                }

                if (item is Container sub)
                    ScanContainer(sub, query, $"{location} › {GetLabel(sub)}", results, player);
            }
        }

        // ── Property text builder ──────────────────────────────────

        private static string GetItemProperties(Item item)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append(CamelToWords(item.GetType().Name)).Append(' ');

            if (item is SpellScroll scroll)
            {
                try
                {
                    Spell spell = SpellRegistry.NewSpell(scroll.SpellID, null, null);
                    if (spell?.Info != null)
                    {
                        sb.Append(spell.Info.Name.ToLower()).Append(' ');
                        if (spell.Info.Reagents != null)
                            foreach (Type t in spell.Info.Reagents)
                                sb.Append(CamelToWords(t.Name)).Append(' ');
                    }
                }
                catch { }
            }

            AosAttributes       attrs       = null;
            AosArmorAttributes  armorAttrs  = null;
            AosWeaponAttributes weaponAttrs = null;

            if (item is BaseWeapon bw)
            {
                attrs       = bw.Attributes;
                weaponAttrs = bw.WeaponAttributes;
                sb.Append(CamelToWords(bw.Skill.ToString())).Append(' ');
                sb.Append($"damage {bw.MinDamage}-{bw.MaxDamage} ");
                if (bw.Slayer  != SlayerName.None) sb.Append(CamelToWords(bw.Slayer.ToString())).Append(" slayer ");
                if (bw.Slayer2 != SlayerName.None) sb.Append(CamelToWords(bw.Slayer2.ToString())).Append(" slayer ");
                if (bw.Resource != CraftResource.None && bw.Resource != CraftResource.Iron)
                    sb.Append(CamelToWords(bw.Resource.ToString())).Append(' ');
                if (bw.Quality == ItemQuality.Exceptional) sb.Append("exceptional ");
            }
            else if (item is BaseArmor ba)
            {
                attrs      = ba.Attributes;
                armorAttrs = ba.ArmorAttributes;
                sb.Append($"physical {ba.BasePhysicalResistance} fire {ba.BaseFireResistance} ");
                sb.Append($"cold {ba.BaseColdResistance} poison {ba.BasePoisonResistance} energy {ba.BaseEnergyResistance} ");
                if (ba.Resource != CraftResource.None && ba.Resource != CraftResource.Iron
                    && ba.Resource != CraftResource.RegularLeather)
                    sb.Append(CamelToWords(ba.Resource.ToString())).Append(' ');
                if (ba.Quality == ItemQuality.Exceptional) sb.Append("exceptional ");
            }
            else if (item is BaseJewel bj)  attrs = bj.Attributes;
            else if (item is BaseClothing bc)
            {
                attrs = bc.Attributes;
                if (bc.Resource != CraftResource.None && bc.Resource != CraftResource.RegularLeather)
                    sb.Append(CamelToWords(bc.Resource.ToString())).Append(' ');
            }

            if (attrs       != null) AppendAosAttributes(sb, attrs);
            if (armorAttrs  != null) AppendArmorAttributes(sb, armorAttrs);
            if (weaponAttrs != null) AppendWeaponAttributes(sb, weaponAttrs);

            AosSkillBonuses skillBonuses = null;
            if      (item is BaseWeapon  sbw) skillBonuses = sbw.SkillBonuses;
            else if (item is BaseArmor   sba) skillBonuses = sba.SkillBonuses;
            else if (item is BaseJewel   sbj) skillBonuses = sbj.SkillBonuses;
            else if (item is BaseClothing sbc) skillBonuses = sbc.SkillBonuses;

            if (skillBonuses != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    SkillName sk;
                    double    val;
                    if (skillBonuses.GetValues(i, out sk, out val) && val != 0)
                        sb.Append(CamelToWords(sk.ToString())).Append(' ');
                }
            }

            return sb.ToString();
        }

        private static void AppendAosAttributes(System.Text.StringBuilder sb, AosAttributes a)
        {
            if (a[AosAttribute.LowerRegCost]    > 0) sb.Append("lower reagent cost lrc ");
            if (a[AosAttribute.LowerManaCost]   > 0) sb.Append("lower mana cost lmc ");
            if (a[AosAttribute.SpellDamage]     > 0) sb.Append("spell damage increase sdi ");
            if (a[AosAttribute.CastSpeed]       > 0) sb.Append("faster casting fc cast speed ");
            if (a[AosAttribute.CastRecovery]    > 0) sb.Append("faster cast recovery fcr ");
            if (a[AosAttribute.DefendChance]    > 0) sb.Append("defense chance increase dci ");
            if (a[AosAttribute.AttackChance]    > 0) sb.Append("hit chance increase hci ");
            if (a[AosAttribute.WeaponDamage]    > 0) sb.Append("damage increase di ");
            if (a[AosAttribute.WeaponSpeed]     > 0) sb.Append("swing speed increase ssi ");
            if (a[AosAttribute.BonusStr]        > 0) sb.Append("strength bonus str ");
            if (a[AosAttribute.BonusDex]        > 0) sb.Append("dexterity bonus dex ");
            if (a[AosAttribute.BonusInt]        > 0) sb.Append("intelligence bonus int ");
            if (a[AosAttribute.BonusHits]       > 0) sb.Append("hit point increase hp ");
            if (a[AosAttribute.BonusStam]       > 0) sb.Append("stamina increase stam ");
            if (a[AosAttribute.BonusMana]       > 0) sb.Append("mana increase ");
            if (a[AosAttribute.RegenHits]       > 0) sb.Append("hit point regeneration hpr ");
            if (a[AosAttribute.RegenStam]       > 0) sb.Append("stamina regeneration ");
            if (a[AosAttribute.RegenMana]       > 0) sb.Append("mana regeneration mr ");
            if (a[AosAttribute.Luck]            > 0) sb.Append("luck ");
            if (a[AosAttribute.EnhancePotions]  > 0) sb.Append("enhance potions ep ");
            if (a[AosAttribute.ReflectPhysical] > 0) sb.Append("reflect physical damage rpd ");
            if (a[AosAttribute.NightSight]      > 0) sb.Append("night sight ");
        }

        private static void AppendArmorAttributes(System.Text.StringBuilder sb, AosArmorAttributes a)
        {
            if (a.MageArmor       > 0) sb.Append("mage armor ");
            if (a.LowerStatReq    > 0) sb.Append("lower requirements lr ");
            if (a.DurabilityBonus > 0) sb.Append("durability ");
            if (a.SoulCharge      > 0) sb.Append("soul charge ");
        }

        private static void AppendWeaponAttributes(System.Text.StringBuilder sb, AosWeaponAttributes a)
        {
            if (a.HitLeechHits      > 0) sb.Append("hit life leech hll ");
            if (a.HitLeechMana      > 0) sb.Append("hit mana leech hml ");
            if (a.HitLeechStam      > 0) sb.Append("hit stamina leech hsl ");
            if (a.HitLowerAttack    > 0) sb.Append("hit lower attack hla ");
            if (a.HitLowerDefend    > 0) sb.Append("hit lower defense hld ");
            if (a.HitDispel         > 0) sb.Append("hit dispel ");
            if (a.HitFireball       > 0) sb.Append("hit fireball ");
            if (a.HitLightning      > 0) sb.Append("hit lightning ");
            if (a.HitMagicArrow     > 0) sb.Append("hit magic arrow ");
            if (a.HitHarm           > 0) sb.Append("hit harm ");
            if (a.SplinteringWeapon > 0) sb.Append("splintering ");
            if (a.BattleLust        > 0) sb.Append("battle lust ");
            if (a.BloodDrinker      > 0) sb.Append("blood drinker ");
        }

        // ── Helpers ────────────────────────────────────────────────

        private static string GetLabel(Item item)
        {
            if (item == null) return "Item";
            if (!string.IsNullOrEmpty(item.Name)) return item.Name;
            return CamelToWords(item.GetType().Name);
        }

        private static string CamelToWords(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]))
                    sb.Append(' ');
                sb.Append(char.ToLower(s[i]));
            }
            return sb.ToString();
        }

        private static string Capitalise(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
