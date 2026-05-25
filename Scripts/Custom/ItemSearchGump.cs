// ============================================================
// ItemSearchGump.cs
// Scripts/Custom/ItemSearchGump.cs
//
// Searches a player's backpack, bank box, and all secure
// containers in their house(s). Requires 4+ characters.
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

        public ItemSearchResult(string itemName, int amount, string location)
        {
            ItemName = itemName;
            Amount   = amount;
            Location = location;
        }
    }

    // ── Gump ───────────────────────────────────────────────────────
    public class ItemSearchGump : Gump
    {
        // Layout
        private const int W       = 540;
        private const int PadX    = 12;
        private const int RowH    = 22;
        private const int MaxRows = 14;
        private const int MinChars = 4;

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
            : base(60, 60)
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
            int startIdx     = _page * MaxRows;
            int resultsOnPage = Math.Max(0, Math.Min(MaxRows, _results.Count - startIdx));
            int totalPages   = _results.Count > 0 ? (_results.Count + MaxRows - 1) / MaxRows : 1;

            // Dynamic height
            int bodyH = 100; // header + search row + hint
            if (resultsOnPage > 0)
                bodyH += 26 + resultsOnPage * RowH; // column headers + rows
            if (totalPages > 1)
                bodyH += 26; // pagination row
            bodyH += 10; // bottom padding

            AddBackground(0, 0, W, bodyH, 9270);
            AddAlphaRegion(8, 8, W - 16, bodyH - 16);

            // ── Title ──────────────────────────────────────────────
            AddHtml(PadX, 10, W - PadX * 2, 20,
                "<BASEFONT COLOR=#C8A428><B>Item Search</B></BASEFONT>",
                false, false);
            AddImageTiled(PadX, 30, W - PadX * 2, 1, 9264);

            // ── Search row ─────────────────────────────────────────
            AddHtml(PadX, 38, 65, 22,
                "<BASEFONT COLOR=#AAAAAA>Search:</BASEFONT>", false, false);

            AddBackground(78, 35, 340, 24, 9350);
            AddTextEntry(81, 37, 334, 20, 0, 0, _query);

            AddButton(426, 36, 4005, 4007, BTN_SEARCH, GumpButtonType.Reply, 0);
            AddHtml(448, 38, 80, 20,
                "<BASEFONT COLOR=#DDCCAA>Search</BASEFONT>", false, false);

            // ── Status / hint line ─────────────────────────────────
            int hintY = 64;
            string hint;
            string hintColor;

            if (_query.Length == 0)
            {
                hint      = "Searches backpack, bank, and all house secure containers.";
                hintColor = "#888888";
            }
            else if (_query.Length < MinChars)
            {
                hint      = $"Enter at least {MinChars} characters to search.";
                hintColor = "#FF8844";
            }
            else if (_results.Count == 0)
            {
                hint      = $"No items found matching \"{_query}\".";
                hintColor = "#FF8844";
            }
            else
            {
                hint      = $"{_results.Count} item{(_results.Count == 1 ? "" : "s")} found matching \"{_query}\".";
                hintColor = "#88CC88";
            }

            AddHtml(PadX, hintY, W - PadX * 2, 20,
                $"<BASEFONT COLOR={hintColor}>{hint}</BASEFONT>", false, false);

            // ── Results ────────────────────────────────────────────
            if (resultsOnPage > 0)
            {
                int y = 88;

                // Column headers
                AddHtml(PadX,  y, 220, 20, "<BASEFONT COLOR=#666655>Item</BASEFONT>",      false, false);
                AddHtml(238,   y,  60, 20, "<BASEFONT COLOR=#666655>Qty</BASEFONT>",       false, false);
                AddHtml(302,   y, W - 302 - PadX, 20, "<BASEFONT COLOR=#666655>Location</BASEFONT>", false, false);
                y += 20;
                AddImageTiled(PadX, y, W - PadX * 2, 1, 9264);
                y += 5;

                for (int i = startIdx; i < startIdx + resultsOnPage; i++)
                {
                    var    r     = _results[i];
                    string color = (i % 2 == 0) ? "#DDCCAA" : "#BBAA88";

                    AddHtml(PadX, y, 220, RowH,
                        $"<BASEFONT COLOR={color}>{r.ItemName}</BASEFONT>", false, false);
                    AddHtml(238,  y,  60, RowH,
                        $"<BASEFONT COLOR={color}>{(r.Amount > 1 ? r.Amount.ToString() : "—")}</BASEFONT>",
                        false, false);
                    AddHtml(302,  y, W - 302 - PadX, RowH,
                        $"<BASEFONT COLOR={color}>{r.Location}</BASEFONT>", false, false);

                    y += RowH;
                }

                // Pagination
                if (totalPages > 1)
                {
                    y += 4;
                    AddHtml(PadX, y, 300, 20,
                        $"<BASEFONT COLOR=#888888>Page {_page + 1} of {totalPages}</BASEFONT>",
                        false, false);

                    if (_page > 0)
                    {
                        AddButton(W - 115, y, 4014, 4016, BTN_PREVPAGE, GumpButtonType.Reply, 0);
                        AddHtml(W - 93, y + 2, 40, 18,
                            "<BASEFONT COLOR=#AAAAAA>Prev</BASEFONT>", false, false);
                    }

                    if (_page < totalPages - 1)
                    {
                        AddButton(W - 60, y, 4005, 4007, BTN_NEXTPAGE, GumpButtonType.Reply, 0);
                        AddHtml(W - 38, y + 2, 40, 18,
                            "<BASEFONT COLOR=#AAAAAA>Next</BASEFONT>", false, false);
                    }
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

            // 1. Backpack
            if (player.Backpack != null)
                ScanContainer(player.Backpack, query, "Backpack", results);

            // 2. Bank box
            if (player.BankBox != null)
                ScanContainer(player.BankBox, query, "Bank", results);

            // 3. All house secure containers
            var houses = BaseHouse.GetHouses(player);
            foreach (BaseHouse house in houses)
            {
                if (house.Secures == null) continue;

                foreach (SecureInfo si in house.Secures)
                {
                    if (si?.Item == null || si.Item.Deleted) continue;

                    var cont = si.Item as Container;
                    if (cont == null) continue;

                    string label = GetLabel(si.Item);
                    ScanContainer(cont, query, $"House › {label}", results);
                }
            }

            return results;
        }

        /// <summary>
        /// Recursively scans a container and all sub-containers.
        /// Matches against item name AND searchable property text.
        /// </summary>
        private static void ScanContainer(Container cont, string query,
                                           string location, List<ItemSearchResult> results)
        {
            if (cont == null) return;

            foreach (Item item in cont.Items)
            {
                if (item == null || item.Deleted) continue;

                string label      = GetLabel(item);
                string searchable = (label + " " + GetItemProperties(item)).ToLowerInvariant();

                if (searchable.Contains(query))
                    results.Add(new ItemSearchResult(Capitalise(label), item.Amount, location));

                // Recurse into sub-containers
                if (item is Container sub)
                    ScanContainer(sub, query, $"{location} › {label}", results);
            }
        }

        // ── Property text builder ──────────────────────────────────

        /// <summary>
        /// Returns a space-separated lower-case string of searchable property text:
        /// spell name + reagents for scrolls, magic attributes for equipment.
        /// </summary>
        private static string GetItemProperties(Item item)
        {
            var sb = new System.Text.StringBuilder();

            // Spell scrolls — spell name + reagent names
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

            // AOS attributes on weapons, armor, jewelry, clothing
            AosAttributes       attrs       = null;
            AosArmorAttributes  armorAttrs  = null;
            AosWeaponAttributes weaponAttrs = null;

            if (item is BaseWeapon bw)
            {
                attrs       = bw.Attributes;
                weaponAttrs = bw.WeaponAttributes;
                sb.Append(CamelToWords(bw.Skill.ToString())).Append(' ');
                sb.Append($"damage {bw.MinDamage}-{bw.MaxDamage} ");
            }
            else if (item is BaseArmor ba)
            {
                attrs      = ba.Attributes;
                armorAttrs = ba.ArmorAttributes;
                sb.Append($"physical {ba.BasePhysicalResistance} fire {ba.BaseFireResistance} ");
                sb.Append($"cold {ba.BaseColdResistance} poison {ba.BasePoisonResistance} energy {ba.BaseEnergyResistance} ");
            }
            else if (item is BaseJewel bj)  attrs = bj.Attributes;
            else if (item is BaseClothing bc) attrs = bc.Attributes;

            if (attrs       != null) AppendAosAttributes(sb, attrs);
            if (armorAttrs  != null) AppendArmorAttributes(sb, armorAttrs);
            if (weaponAttrs != null) AppendWeaponAttributes(sb, weaponAttrs);

            return sb.ToString();
        }

        private static void AppendAosAttributes(System.Text.StringBuilder sb, AosAttributes a)
        {
            if (a[AosAttribute.LowerRegCost]        > 0) sb.Append("lower reagent cost lrc ");
            if (a[AosAttribute.LowerManaCost]       > 0) sb.Append("lower mana cost lmc ");
            if (a[AosAttribute.SpellDamage]         > 0) sb.Append("spell damage increase sdi ");
            if (a[AosAttribute.CastSpeed]           > 0) sb.Append("faster casting fc cast speed ");
            if (a[AosAttribute.CastRecovery]        > 0) sb.Append("faster cast recovery fcr ");
            if (a[AosAttribute.DefendChance]        > 0) sb.Append("defense chance increase dci ");
            if (a[AosAttribute.AttackChance]        > 0) sb.Append("hit chance increase hci ");
            if (a[AosAttribute.WeaponDamage]        > 0) sb.Append("damage increase di ");
            if (a[AosAttribute.WeaponSpeed]         > 0) sb.Append("swing speed increase ssi ");
            if (a[AosAttribute.BonusStr]            > 0) sb.Append("strength bonus str ");
            if (a[AosAttribute.BonusDex]            > 0) sb.Append("dexterity bonus dex ");
            if (a[AosAttribute.BonusInt]            > 0) sb.Append("intelligence bonus int ");
            if (a[AosAttribute.BonusHits]           > 0) sb.Append("hit point increase hp ");
            if (a[AosAttribute.BonusStam]           > 0) sb.Append("stamina increase stam ");
            if (a[AosAttribute.BonusMana]           > 0) sb.Append("mana increase ");
            if (a[AosAttribute.RegenHits]           > 0) sb.Append("hit point regeneration hpr ");
            if (a[AosAttribute.RegenStam]           > 0) sb.Append("stamina regeneration ");
            if (a[AosAttribute.RegenMana]           > 0) sb.Append("mana regeneration mr ");
            if (a[AosAttribute.Luck]                > 0) sb.Append("luck ");
            if (a[AosAttribute.EnhancePotions]      > 0) sb.Append("enhance potions ep ");
            if (a[AosAttribute.ReflectPhysical]     > 0) sb.Append("reflect physical damage rpd ");
            if (a[AosAttribute.NightSight]          > 0) sb.Append("night sight ");
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
            if (a.HitLeechHits    > 0) sb.Append("hit life leech hll ");
            if (a.HitLeechMana    > 0) sb.Append("hit mana leech hml ");
            if (a.HitLeechStam    > 0) sb.Append("hit stamina leech hsl ");
            if (a.HitLowerAttack  > 0) sb.Append("hit lower attack hla ");
            if (a.HitLowerDefend  > 0) sb.Append("hit lower defense hld ");
            if (a.HitDispel       > 0) sb.Append("hit dispel ");
            if (a.HitFireball     > 0) sb.Append("hit fireball ");
            if (a.HitLightning    > 0) sb.Append("hit lightning ");
            if (a.HitMagicArrow   > 0) sb.Append("hit magic arrow ");
            if (a.HitHarm         > 0) sb.Append("hit harm ");
            if (a.SplinteringWeapon > 0) sb.Append("splintering ");
            if (a.BattleLust      > 0) sb.Append("battle lust ");
            if (a.BloodDrinker    > 0) sb.Append("blood drinker ");
        }

        // ── Helpers ────────────────────────────────────────────────

        private static string GetLabel(Item item)
        {
            if (item == null) return "Item";
            if (!string.IsNullOrEmpty(item.Name)) return item.Name;
            return CamelToWords(item.GetType().Name);
        }

        /// <summary>Splits CamelCase into lower-case space-separated words.</summary>
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
