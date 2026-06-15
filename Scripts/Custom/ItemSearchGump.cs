// ============================================================
// ItemSearchGump.cs  —  Scripts/Custom/ItemSearchGump.cs
//
// Personal item search that mirrors the Vendor Search gump:
//   • Same filter categories (Equipment, Combat, Casting,
//     Resists, Stats, Slayers, Skill Groups, Misc…)
//   • Searches backpack, bank, and house containers
//     (owner or co-owner)
//   • Reuses SearchCriteria / VendorSearch.CheckMatch so every
//     attribute filter works identically
//
// Usage: [itemsearch
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Engines.VendorSearching;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Server.Gumps
{
    // ── Result record ──────────────────────────────────────────────
    public class ItemSearchResult
    {
        public Item   Item;
        public string Location;

        public ItemSearchResult(Item item, string location)
        {
            Item     = item;
            Location = location;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Query gump — mirrors VendorSearchGump (no price/sort/auction)
    // ══════════════════════════════════════════════════════════════

    public class ItemSearchGump : Gump
    {
        // UO integer colours — same as VendorSearchGump
        public static int LabelColor    = 0x4BBD;
        public static int CriteriaColor = 0x6B55;
        public static int TextColor     = 0x9C2;
        public static int AlertColor    = 0x7C00;

        // Per-player criteria (separate from vendor search contexts)
        private static readonly Dictionary<PlayerMobile, SearchCriteria> _contexts
            = new Dictionary<PlayerMobile, SearchCriteria>();

        // All categories except price / sort / auction
        private static readonly SearchCriteriaCategory[] FilteredCats =
            SearchCriteriaCategory.AllCategories
                .Where(x => x.Category != Category.PriceRange
                         && x.Category != Category.Sort
                         && x.Category != Category.Auction)
                .ToArray();

        private readonly PlayerMobile   _player;
        private readonly SearchCriteria _criteria;
        private readonly int            _feedback; // -1 = none

        // ── Command registration ───────────────────────────────────

        public static void Initialize()
        {
            CommandSystem.Register("itemsearch", AccessLevel.Player, OnCommand);
        }

        private static void OnCommand(CommandEventArgs e)
        {
            if (!(e.Mobile is PlayerMobile pm)) return;
            pm.CloseGump(typeof(ItemSearchGump));
            pm.SendGump(new ItemSearchGump(pm));
        }

        public static SearchCriteria GetCriteria(PlayerMobile pm)
        {
            if (!_contexts.TryGetValue(pm, out SearchCriteria c) || c == null)
                c = _contexts[pm] = new SearchCriteria();
            return c;
        }

        // ── Constructor ────────────────────────────────────────────

        public ItemSearchGump(PlayerMobile pm, int feedback = -1)
            : base(10, 10)
        {
            _player   = pm;
            _criteria = GetCriteria(pm);
            _feedback = feedback;

            Closable = Disposable = Dragable = true;
            Resizable = false;

            Build();
        }

        // ── Layout ─────────────────────────────────────────────────

        private void Build()
        {
            AddPage(0);
            AddBackground(0, 0, 780, 600, 30546);

            // Title (1114513 = "~1_val~", outputs the arg as-is)
            AddHtmlLocalized(10, 10, 760, 18, 1114513, "#1154508", LabelColor, false, false); // "Item Search"

            // ── Right panel header ─────────────────────────────────
            AddHtmlLocalized(522, 30, 246, 18, 1154546, LabelColor, false, false); // "Selected Search Criteria"

            // ── Right panel: active criteria list ──────────────────
            int yRight = 0;

            if (!string.IsNullOrEmpty(_criteria.SearchName))
            {
                AddButton(522, 50 + yRight * 22, 4017, 4019, 7, GumpButtonType.Reply, 0);
                AddTooltip(1154694); // "Remove Selected Search Criteria"
                AddHtmlLocalized(562, 50 + yRight * 22, 206, 20, 1154510, CriteriaColor, false, false); // Item Name
                yRight++;
            }

            for (int i = 0; i < _criteria.Details.Count; i++)
            {
                SearchDetail det   = _criteria.Details[i];
                int          cliloc = det.PropLabel;

                if (cliloc > 0)
                {
                    if (det.Attribute is SkillName)
                        AddHtmlLocalized(562, 50 + yRight * 22, 206, 20, 1060451,
                            string.Format("#{0}@{1}", cliloc, det.Value), CriteriaColor, false, false);
                    else
                        AddHtmlLocalized(562, 50 + yRight * 22, 206, 20, cliloc,
                            det.Value.ToString(), CriteriaColor, false, false);
                }
                else
                {
                    AddHtmlLocalized(562, 50 + yRight * 22, 206, 20, det.Label, CriteriaColor, false, false);
                }

                AddButton(522, 50 + yRight * 22, 4017, 4019, 1001 + i, GumpButtonType.Reply, 0);
                AddTooltip(1154694);
                yRight++;
            }

            // ── Left panel: Item Name ──────────────────────────────
            AddHtmlLocalized(10, 30, 246, 18, 1154510, LabelColor, false, false); // "Item Name"
            AddBackground(10, 50, 246, 22, 9350);
            AddTextEntry(12, 52, 242, 18, TextColor, 1, _criteria.SearchName, 25);

            // ── Left panel: category navigation buttons ────────────
            int yLeft = 0;

            foreach (SearchCriteriaCategory cat in FilteredCats)
            {
                AddButton(10, 74 + yLeft * 22, 30533, 30533, 0, GumpButtonType.Page, cat.PageID);
                AddHtmlLocalized(50, 75 + yLeft * 22, 215, 20, cat.Cliloc, LabelColor, false, false);
                yLeft++;
            }

            // ── Bottom bar ─────────────────────────────────────────
            AddButton(10, 570, 0x7747, 0x7747, 0, GumpButtonType.Reply, 0);
            AddHtmlLocalized(50, 570, 50, 20, 1150300, LabelColor, false, false); // "CANCEL"

            if (_feedback != -1)
                AddHtmlLocalized(110, 570, 660, 20, _feedback, AlertColor, false, false);

            AddButton(740, 570, 30534, 30534, 1, GumpButtonType.Reply, 0);
            AddHtmlLocalized(630, 570, 100, 20, 1114514, "#1154641", LabelColor, false, false); // "Search"

            AddButton(740, 550, 30533, 30533, 2, GumpButtonType.Reply, 0);
            AddHtmlLocalized(630, 550, 100, 20, 1114514, "#1154588", LabelColor, false, false); // "Clear Search Criteria"

            // ── Sub-pages: one per category ────────────────────────
            int buttonIdx = 50;

            foreach (SearchCriteriaCategory cat in FilteredCats)
            {
                AddPage(cat.PageID);
                AddHtmlLocalized(266, 30, 246, 18, cat.Cliloc, LabelColor, false, false);

                int yEntry = 0;
                foreach (SearchCriterionEntry entry in cat.Criteria)
                {
                    AddHtmlLocalized(306, 50 + yEntry * 22, 215, 20, entry.Cliloc, LabelColor, false, false);
                    AddButton(266, 50 + yEntry * 22, 30533, 30533, buttonIdx, GumpButtonType.Reply, 0);

                    if (entry.PropCliloc != 0)
                    {
                        int val = _criteria.GetValueForDetails(entry.Object);
                        AddBackground(482, 50 + yEntry * 22, 30, 20, 9350);
                        AddTextEntry(484, 50 + yEntry * 22, 26, 16, TextColor,
                            buttonIdx - 40, val > 0 ? val.ToString() : "", 3);
                    }

                    yEntry++;
                    buttonIdx++;
                }
            }
        }

        // ── Response ───────────────────────────────────────────────

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (!(sender.Mobile is PlayerMobile pm)) return;

            // Persist search name on every response
            if (info.ButtonID != 0)
            {
                TextRelay nameRelay = info.GetTextEntry(1);
                if (nameRelay != null && !string.IsNullOrEmpty(nameRelay.Text))
                {
                    string txt = nameRelay.Text.Trim();
                    if (_criteria.SearchName == null
                        || !txt.Equals(_criteria.SearchName, StringComparison.OrdinalIgnoreCase))
                        _criteria.SearchName = txt;
                }
            }

            switch (info.ButtonID)
            {
                case 0: return; // close / cancel

                case 1: // Search
                {
                    if (_criteria.IsEmpty)
                    {
                        pm.SendGump(new ItemSearchGump(pm, 1154586)); // "Please select some criteria"
                        return;
                    }

                    List<ItemSearchResult> results;

                    try
                    {
                        results = DoSearch(pm, _criteria);
                    }
                    catch (Exception ex)
                    {
                        pm.SendMessage(0x22, "Item Search error: " + ex.Message);
                        pm.SendGump(new ItemSearchGump(pm));
                        return;
                    }

                    if (results == null || results.Count == 0)
                    {
                        pm.SendGump(new ItemSearchGump(pm, 1154587)); // "No items matched"
                        return;
                    }

                    pm.SendGump(new ItemSearchGump(pm));
                    pm.SendGump(new ItemSearchResultsGump(pm, results));
                    break;
                }

                case 2: // Clear
                    _criteria.Reset();
                    pm.SendGump(new ItemSearchGump(pm));
                    break;

                case 7: // Remove search name
                    _criteria.SearchName = null;
                    pm.SendGump(new ItemSearchGump(pm));
                    break;

                default:
                    if (info.ButtonID > 1000) // Remove a detail
                    {
                        int idx = info.ButtonID - 1001;
                        if (idx >= 0 && idx < _criteria.Details.Count)
                        {
                            SearchDetail det = _criteria.Details[idx];
                            if (det.Category == Category.Equipment)
                                _criteria.SearchType = Layer.Invalid;
                            _criteria.Details.Remove(det);
                        }
                        pm.SendGump(new ItemSearchGump(pm));
                    }
                    else if (info.ButtonID >= 50) // Add a criterion
                    {
                        int flatIdx = info.ButtonID - 50;
                        var allEntries = FilteredCats
                            .SelectMany(x => x.Criteria,
                                (x, c) => new { x.Category, c.Object, c.Cliloc, c.PropCliloc })
                            .ToList();

                        if (flatIdx < allEntries.Count)
                        {
                            var entry = allEntries[flatIdx];
                            int value = 0;

                            TextRelay valueText = info.GetTextEntry(info.ButtonID - 40);
                            if (valueText != null)
                                value = Math.Max(
                                    entry.Object is AosAttribute &&
                                    (AosAttribute)entry.Object == AosAttribute.CastSpeed ? -1 : 0,
                                    Utility.ToInt32(valueText.Text));

                            _criteria.TryAddDetails(
                                entry.Object, entry.Cliloc, entry.PropCliloc, value, entry.Category);
                        }

                        pm.SendGump(new ItemSearchGump(pm));
                    }
                    break;
            }
        }

        // ── Search logic ───────────────────────────────────────────

        private static List<ItemSearchResult> DoSearch(PlayerMobile player, SearchCriteria criteria)
        {
            var results = new List<ItemSearchResult>();

            // 1. Backpack (recursive)
            if (player.Backpack != null)
                ScanContainer(player.Backpack, "Backpack", results, player, criteria);

            // 2. Bank (recursive)
            if (player.BankBox != null)
                ScanContainer(player.BankBox, "Bank", results, player, criteria);

            // 3. Houses — owner or co-owner
            foreach (BaseHouse house in BaseHouse.AllHouses)
            {
                if (house == null || house.Deleted) continue;

                bool isOwner   = house.IsOwner(player);
                bool isCoOwner = !isOwner && house.IsCoOwner(player);
                if (!isOwner && !isCoOwner) continue;

                string houseLabel = isOwner ? "Your House" : "Co-owned House";

                if (house.Secures != null)
                {
                    foreach (SecureInfo si in house.Secures)
                    {
                        if (si?.Item == null || si.Item.Deleted || !(si.Item is Container sc)) continue;
                        ScanContainer(sc, houseLabel + " › " + GetLabel(sc), results, player, criteria);
                    }
                }

                if (house.LockDowns != null)
                {
                    foreach (Item item in house.LockDowns.Keys)
                    {
                        if (item == null || item.Deleted || !(item is Container lc)) continue;
                        if (house.Secures != null && house.Secures.Exists(si => si?.Item == item)) continue;
                        ScanContainer(lc, houseLabel + " › " + GetLabel(lc), results, player, criteria);
                    }
                }
            }

            return results;
        }

        private static void ScanContainer(Container cont, string location,
            List<ItemSearchResult> results, PlayerMobile player, SearchCriteria criteria)
        {
            if (cont == null) return;

            foreach (Item item in cont.Items)
            {
                if (item == null || item.Deleted) continue;

                if (Matches(item, criteria))
                    results.Add(new ItemSearchResult(item, location));

                if (item is Container sub)
                    ScanContainer(sub, location + " › " + GetLabel(sub), results, player, criteria);
            }
        }

        // Matches item against criteria — separates name check from attribute check
        // to handle items whose name comes from a cliloc (where GetItemName can return null)
        private static bool Matches(Item item, SearchCriteria criteria)
        {
            // ── Name check ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(criteria.SearchName))
            {
                // Prefer item.Name (set in constructor); fall back to GetItemName (OPL parsing);
                // fall back further to the type name so cliloc-named items are still found
                string name = item.Name
                    ?? VendorSearch.GetItemName(item)
                    ?? GetLabel(item)
                    ?? string.Empty;

                if (name.IndexOf(criteria.SearchName, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            // ── Attribute / layer check ────────────────────────────
            // Use CheckMatch with SearchName suppressed (we already validated it above)
            bool hasAttrCriteria = criteria.Details.Count > 0 || criteria.SearchType != Layer.Invalid;
            if (!hasAttrCriteria)
                return true;

            string savedName = criteria.SearchName;
            criteria.SearchName = null;
            bool attrOk = false;
            try   { attrOk = VendorSearch.CheckMatch(item, 0, criteria); }
            finally { criteria.SearchName = savedName; }

            return attrOk;
        }

        internal static string GetLabel(Item item)
        {
            if (item == null) return "Item";
            if (!string.IsNullOrEmpty(item.Name)) return item.Name;

            string n = item.GetType().Name;
            var sb = new System.Text.StringBuilder(n.Length + 4);
            for (int i = 0; i < n.Length; i++)
            {
                if (i > 0 && char.IsUpper(n[i]) && !char.IsUpper(n[i - 1])) sb.Append(' ');
                sb.Append(i == 0 ? char.ToUpper(n[i]) : n[i]);
            }
            return sb.ToString();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Results gump — mirrors SearchResultsGump (no price/map/button)
    // ══════════════════════════════════════════════════════════════

    public class ItemSearchResultsGump : Gump
    {
        private const int PerPage   = 25;
        private const int RowHeight = 18;

        private static int LabelColor = 0x4BBD;
        private static int TextColor  = 0x6B55;

        private readonly PlayerMobile           _player;
        private readonly List<ItemSearchResult> _results;
        private          int                    _index;

        public ItemSearchResultsGump(PlayerMobile pm, List<ItemSearchResult> results, int index = 0)
            : base(30, 30)
        {
            _player  = pm;
            _results = results;
            _index   = Math.Max(0, Math.Min(index, results.Count - 1));

            Closable = Disposable = Dragable = true;
            Resizable = false;

            Build();
        }

        private void Build()
        {
            // 580 wide × 545 tall:
            //   header 47px | 25 rows × 18px = 450px | footer 48px = 545
            AddBackground(0, 0, 580, 545, 30536);

            // Title
            AddHtml(10, 8, 560, 18,
                "<CENTER><BASEFONT COLOR=#7ABDE8>Item Search Results</BASEFONT></CENTER>",
                false, false);

            // Column headers
            AddHtml(30, 28, 220, 16, "<BASEFONT COLOR=#7ABDE8>Item</BASEFONT>", false, false);
            AddHtml(256, 28, 40,  16, "<BASEFONT COLOR=#7ABDE8>Qty</BASEFONT>", false, false);
            AddHtml(300, 28, 274, 16, "<BASEFONT COLOR=#7ABDE8>Location</BASEFONT>", false, false);

            // ── Rows ───────────────────────────────────────────────
            int start = _index;
            int shown = 0;

            for (int i = start; i < start + PerPage && i < _results.Count; i++)
            {
                var  r    = _results[i];
                Item item = r.Item;
                if (item == null || item.Deleted) { shown++; continue; }

                int y = 47 + shown * RowHeight;

                // Small item icon — no background box
                AddItem(8, y, item.ItemID, item.Hue);
                AddItemProperty(item);

                // Item name
                string name = VendorSearch.GetItemName(item) ?? ItemSearchGump.GetLabel(item);
                AddHtmlLocalized(30, y, 220, RowHeight, 1114513, name, TextColor, false, false);

                // Qty
                string qty = item.Amount > 1 ? item.Amount.ToString() : "-";
                AddHtmlLocalized(256, y, 40, RowHeight, 1114513, qty, TextColor, false, false);

                // Location
                AddHtmlLocalized(300, y, 274, RowHeight, 1114513, r.Location, TextColor, false, false);

                shown++;
            }

            // ── Footer ─────────────────────────────────────────────
            int totalPages = (_results.Count + PerPage - 1) / PerPage;
            int curPage    = _index / PerPage + 1;

            AddHtml(10, 508, 200, 16,
                string.Format("<BASEFONT COLOR=#888888>{0} result{1}  —  page {2}/{3}</BASEFONT>",
                    _results.Count, _results.Count == 1 ? "" : "s", curPage, totalPages),
                false, false);

            if (_index + PerPage < _results.Count)
            {
                AddButton(540, 506, 30534, 30534, 2, GumpButtonType.Reply, 0);
                AddHtml(458, 508, 78, 16,
                    "<BASEFONT COLOR=#7ABDE8>NEXT PAGE</BASEFONT>", false, false);
            }

            if (_index >= PerPage)
            {
                AddButton(218, 506, 30533, 30533, 3, GumpButtonType.Reply, 0);
                AddHtml(238, 508, 90, 16,
                    "<BASEFONT COLOR=#7ABDE8>PREV PAGE</BASEFONT>", false, false);
            }

            // Refine button
            AddButton(335, 506, 30533, 30533, 4, GumpButtonType.Reply, 0);
            AddHtml(355, 508, 60, 16,
                "<BASEFONT COLOR=#7ABDE8>Refine</BASEFONT>", false, false);
        }

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (!(sender.Mobile is PlayerMobile pm)) return;

            switch (info.ButtonID)
            {
                case 0: break; // close
                case 2: pm.SendGump(new ItemSearchResultsGump(pm, _results, _index + PerPage)); break;
                case 3: pm.SendGump(new ItemSearchResultsGump(pm, _results, Math.Max(0, _index - PerPage))); break;
                case 4: pm.SendGump(new ItemSearchGump(pm)); break;
            }
        }
    }
}
