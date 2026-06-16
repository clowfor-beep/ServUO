using System;
using System.Collections.Generic;
using System.Text;
using Server.Commands;
using Server.Custom;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps
{
    public class CharacterStatsGump : Gump
    {
        // ── Layout ───────────────────────────────────────────────────────
        private const int GW     = 590;
        private const int GH     = 700;
        private const int TitleH = 28;
        private const int TabH   = 28;
        private const int RH     = 18;
        private const int SH     = 20;
        private const int CL     = 10;
        private const int CR     = 305;
        private const int CW     = 270;
        private const int FW     = GW - 20;
        private const int LW     = 140;

        // ── Hues ─────────────────────────────────────────────────────────
        private const int HTitle   = 1258;
        private const int HHead    = 53;
        private const int HLabel   = 2100;
        private const int HValue   = 1153;
        private const int HAtCap   = 33;
        private const int HNearCap = 43;
        private const int HGood    = 63;
        private const int HTabOn   = 1153;
        private const int HTabOff  = 2049;

        // ── Button IDs ───────────────────────────────────────────────────
        private const int BTN_CLOSE        = 0;
        private const int BTN_TAB_STATS    = 1;
        private const int BTN_TAB_REP      = 2;
        private const int BTN_TAB_SKILLS   = 3;
        private const int BTN_ITEM_SEARCH  = 10;
        private const int BTN_SKILL_UP     = 100;   // + sorted skill index (0-57)
        private const int BTN_SKILL_DOWN   = 200;
        private const int BTN_SKILL_LOCKED = 300;

        // ── State ────────────────────────────────────────────────────────
        private static readonly HashSet<Serial>        _refreshEnabled = new HashSet<Serial>();
        private static readonly Dictionary<Serial,int> _currentTab     = new Dictionary<Serial,int>();
        private readonly Mobile _from;
        private readonly int    _tab;   // 0=Stats  1=Reputation  2=Skills
        private const double RefreshSeconds = 3.0;

        // ═════════════════════════════════════════════════════════════════
        public static void Initialize()
        {
            CommandSystem.Register("statwnd", AccessLevel.Player, OnCommand);
            EventSink.PaperdollRequest += OnPaperdollRequest;
            EventSink.Logout           += OnLogout;
        }

        private static void OnCommand(CommandEventArgs e)   { OpenFor(e.Mobile, 0); }
        private static void OnPaperdollRequest(PaperdollRequestEventArgs e)
        {
            if (e.Beholder == e.Beheld && e.Beholder is PlayerMobile) OpenFor(e.Beholder, 0);
        }
        private static void OnLogout(LogoutEventArgs e)
        {
            _refreshEnabled.Remove(e.Mobile.Serial);
            _currentTab.Remove(e.Mobile.Serial);
        }

        private static void OpenFor(Mobile from, int tab)
        {
            _currentTab[from.Serial] = tab;   // always track the latest tab choice
            _refreshEnabled.Add(from.Serial);
            from.CloseGump(typeof(CharacterStatsGump));
            from.SendGump(new CharacterStatsGump(from, tab));
        }

        public CharacterStatsGump(Mobile from, int tab = 0) : base(280, 30)
        {
            _from    = from;
            _tab     = tab;
            Closable = true; Dragable = true; Disposable = true; Resizable = false;

            BuildGump();

            if (_refreshEnabled.Contains(from.Serial))
                Timer.DelayCall(TimeSpan.FromSeconds(RefreshSeconds), DoRefresh);
        }

        private void DoRefresh()
        {
            if (_from == null || _from.Deleted || _from.NetState == null)
            { _refreshEnabled.Remove(_from.Serial); _currentTab.Remove(_from.Serial); return; }
            if (!_refreshEnabled.Contains(_from.Serial)) return;

            // Use the most recently selected tab (another gump instance may have changed it)
            int tab = _currentTab.ContainsKey(_from.Serial) ? _currentTab[_from.Serial] : 0;

            // Only auto-refresh the Stats tab — Reputation and Skills don't change in real-time
            if (tab != 0)
            {
                Timer.DelayCall(TimeSpan.FromSeconds(RefreshSeconds), DoRefresh);
                return;
            }

            _from.CloseGump(typeof(CharacterStatsGump));
            _from.SendGump(new CharacterStatsGump(_from, tab));
        }

        // ── Top-level builder ─────────────────────────────────────────────
        private void BuildGump()
        {
            AddBackground(0, 0, GW, GH, 9270);
            AddAlphaRegion(4, 4, GW - 8, GH - 8);

            // Title bar
            AddBackground(4, 4, GW - 8, TitleH, 9270);
            AddLabel(12, 9, HTitle, $"Character Stats — {_from.Name}");

            // Tab bar
            int tabY = TitleH + 4;
            DrawTabs(tabY);

            int contentY = tabY + TabH + 4;

            switch (_tab)
            {
                case 1:  BuildRepTab(contentY);    break;
                case 2:  BuildSkillsTab(contentY); break;
                default: BuildStatsTab(contentY);  break;
            }

            // Bottom row
            int btnY = GH - 30;
            AddButton(CL, btnY, 4005, 4007, BTN_ITEM_SEARCH, GumpButtonType.Reply, 0);
            AddHtml(CL + 30, btnY + 2, 120, 20,
                "<BASEFONT COLOR=#DDCCAA>Item Search</BASEFONT>", false, false);
        }

        private void DrawTabs(int y)
        {
            string[] labels = { "Stats", "Reputation", "Skills" };
            int[]    ids    = { BTN_TAB_STATS, BTN_TAB_REP, BTN_TAB_SKILLS };
            int tw = (GW - CL * 2) / 3;
            int tx = CL;

            for (int i = 0; i < 3; i++)
            {
                bool active = (_tab == i);
                if (active)
                    AddBackground(tx, y, tw - 2, TabH - 2, 9270);
                else
                    AddImageTiled(tx, y, tw - 2, TabH - 2, 9274);

                AddButton(tx + 5, y + 5, active ? 4007 : 4005, 4007, ids[i], GumpButtonType.Reply, 0);
                AddLabel(tx + 26, y + 7, active ? HTabOn : HTabOff, labels[i]);
                tx += tw;
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // TAB 0 — STATS  (original content)
        // ═════════════════════════════════════════════════════════════════
        private void BuildStatsTab(int startY)
        {
            int lY = startY;
            lY = DrawSection("BASICS",         CL, lY, CW);
            lY = DrawBasics(lY);
            lY += 4;
            lY = DrawSection("COMBAT DEFENSE", CL, lY, CW);
            lY = DrawCombatDefense(lY);

            int rY = startY;
            rY = DrawSection("COMBAT OFFENSE", CR, rY, CW);
            rY = DrawCombatOffense(rY);
            rY += 4;
            rY = DrawSection("RESISTANCES",    CR, rY, CW);
            rY = DrawResistances(rY);

            int fY = Math.Max(lY, rY) + 6;
            fY = DrawSection("WIELDED WEAPON", CL, fY, FW);
            fY = DrawWeapon(fY);
            fY += 4;
            fY = DrawSection("LUCK / OTHER",   CL, fY, FW);
            fY = DrawLuckOther(fY);
            fY += 4;
            fY = DrawSection("ACTIVE EFFECTS", CL, fY, FW);
            DrawActiveEffects(fY, GH - fY - 42);
        }

        // ═════════════════════════════════════════════════════════════════
        // TAB 1 — REPUTATION + CURRENCY
        // ═════════════════════════════════════════════════════════════════
        private void BuildRepTab(int startY)
        {
            var pm = _from as PlayerMobile;
            if (pm == null) return;

            int y = startY;

            // ── Guild Reputation ─────────────────────────────────────────
            y = DrawSection("GUILD REPUTATION", CL, y, FW);

            // Column headers
            AddLabel(CL + 5,   y, HHead, "Guild");
            AddLabel(CL + 225, y, HHead, "Standing");
            AddLabel(CL + 330, y, HHead, "Progress");
            y += RH;

            foreach (string guild in FBGuilds.All)
            {
                StandingTier tier  = ReputationSystem.GetTier(pm, guild);
                int          tHue  = TierHue(tier);
                int          gHue  = RepGuildHue(guild);

                AddLabel(CL + 5,   y, gHue,  guild);
                AddLabel(CL + 225, y, tHue,  tier.ToString());

                // Progress dots (0–4 filled)
                int filled = (int)tier;
                for (int d = 0; d < 5; d++)
                    AddLabel(CL + 330 + d * 16, y, d <= filled ? tHue : 2049, "•");

                y += RH;
            }

            y += 8;

            // ── Special Currency ─────────────────────────────────────────
            y = DrawSection("SPECIAL CURRENCY", CL, y, FW);

            int packGold = CountGoldIn(pm.Backpack);
            int bankGold = pm.BankBox != null ? CountGoldIn(pm.BankBox) : 0;

            AddLabel(CL + 5,   y, HLabel, "Gold (pack):");
            AddLabel(CL + 160, y, HGood,  $"{packGold:N0}");
            AddLabel(CL + 285, y, HLabel, "Gold (bank):");
            AddLabel(CL + 445, y, HGood,  $"{bankGold:N0}");
            y += RH;

            // Merchant Coins — may not exist yet, handle gracefully
            int merchantCoins = CountItemByTypeName(pm, "Server.Custom.MerchantCoin");
            if (merchantCoins >= 0)
            {
                AddLabel(CL + 5,   y, HLabel, "Merchant Coins:");
                AddLabel(CL + 160, y, HValue, $"{merchantCoins}");
                y += RH;
            }

            // Hunter Tokens
            int hunterTokens = CountItemByTypeName(pm, "Server.Custom.HunterToken");
            if (hunterTokens >= 0)
            {
                AddLabel(CL + 5,   y, HLabel, "Hunter Tokens:");
                AddLabel(CL + 160, y, HValue, $"{hunterTokens}");
                y += RH;
            }

            if (merchantCoins < 0 && hunterTokens < 0)
            {
                AddLabel(CL + 5, y, HLabel, "No special currencies found.");
            }
        }

        private static int TierHue(StandingTier tier)
        {
            switch (tier)
            {
                case StandingTier.Hostile: return 0x22;
                case StandingTier.Neutral: return 2049;
                case StandingTier.Known:   return 1153;
                case StandingTier.Trusted: return 0x40;
                case StandingTier.Allied:  return 1258;
                default:                   return HValue;
            }
        }

        private static int RepGuildHue(string guild)
        {
            if (guild == FBGuilds.BloodPact  || guild == FBGuilds.TheVoid)    return 0x22;
            if (guild == FBGuilds.SilverWolves)                                return 0x40;
            if (guild == FBGuilds.IronCompany || guild == FBGuilds.PaladinOrder
                || guild == FBGuilds.DreadHunters)                             return 1153;
            if (guild == FBGuilds.ArcaneBrotherhood)                           return 1152;
            if (guild == FBGuilds.ShadowHand   || guild == FBGuilds.DeadWatchers
                || guild == FBGuilds.Shadowblade)                              return 1150;
            return 0x481;
        }

        private static int CountGoldIn(Container c)
        {
            if (c == null) return 0;
            int total = 0;
            foreach (Item item in c.Items)
            {
                if (item is Gold g)         total += g.Amount;
                else if (item is Container s) total += CountGoldIn(s);
            }
            return total;
        }

        /// Returns total stack count of the type, or -1 if the type doesn't exist.
        private static int CountItemByTypeName(PlayerMobile pm, string typeName)
        {
            Type t = ScriptCompiler.FindTypeByFullName(typeName);
            if (t == null) return -1;

            int count = 0;
            if (pm.Backpack != null) count += CountOfType(pm.Backpack, t);
            if (pm.BankBox  != null) count += CountOfType(pm.BankBox,  t);
            return count;
        }

        private static int CountOfType(Container c, Type t)
        {
            int count = 0;
            foreach (Item item in c.Items)
            {
                if (t.IsInstanceOfType(item))  count += Math.Max(1, item.Amount);
                else if (item is Container sub) count += CountOfType(sub, t);
            }
            return count;
        }

        // ═════════════════════════════════════════════════════════════════
        // TAB 2 — SKILLS
        // ═════════════════════════════════════════════════════════════════
        private void BuildSkillsTab(int startY)
        {
            List<Skill> sorted = GetSortedSkills();
            int total = sorted.Count;
            int half  = (total + 1) / 2;

            int y = startY;
            y = DrawSection("SKILL OVERVIEW  —  click Up / Dn / Lk to set training lock", CL, y, FW);

            // Column headers
            DrawSkillColHeader(CL + 5, y);
            DrawSkillColHeader(CR + 5, y);
            y += RH;
            AddImageTiled(CL, y - 3, FW, 1, 9304);

            for (int i = 0; i < half; i++)
            {
                int rowY = y + i * RH;
                DrawSkillRow(sorted[i], i, CL + 5, rowY);
                if (i + half < total)
                    DrawSkillRow(sorted[i + half], i + half, CR + 5, rowY);
            }
        }

        // Per-column offsets for the skills tab (relative to column x)
        private const int SkillNameX  = 0;   // skill name
        private const int SkillBaseX  = 110; // trained (base) value
        private const int SkillEffX   = 150; // effective value (with item/stat bonuses)
        private const int SkillBtn1X  = 196; // Up button
        private const int SkillBtn2X  = 224; // Down button  (+28)
        private const int SkillBtn3X  = 252; // Locked button (+28)

        private void DrawSkillColHeader(int x, int y)
        {
            AddLabel(x + SkillNameX,     y, HHead, "Skill");
            AddLabel(x + SkillBaseX,     y, HHead, "Base");
            AddLabel(x + SkillEffX,      y, HHead, "Eff");
            AddLabel(x + SkillBtn1X + 2, y, HHead, "Up");
            AddLabel(x + SkillBtn2X + 2, y, HHead, "Dn");
            AddLabel(x + SkillBtn3X + 2, y, HHead, "Lk");
        }

        private void DrawSkillRow(Skill skill, int sortedIndex, int x, int y)
        {
            double baseVal = skill.Base;             // trained skill (no bonuses), already in 0-120 range
            double effVal  = skill.Value;          // effective skill (with item/stat bonuses)
            bool   atCap   = skill.Cap > 0 && effVal >= skill.Cap / 10.0 - 0.05;
            bool   boosted = effVal > baseVal + 0.05; // item/buff bonus active

            int nameHue = atCap   ? HAtCap  : HValue;
            int baseHue = HLabel;                     // grey — trained value
            int effHue  = atCap   ? HAtCap  :
                          boosted ? HGood   : HValue; // green if boosted above base

            // Name — truncate if needed
            string name = skill.Name.Length > 14 ? skill.Name.Substring(0, 14) : skill.Name;
            AddLabel(x + SkillNameX, y, nameHue, name);

            // Base (trained) value — grey
            AddLabel(x + SkillBaseX, y, baseHue, $"{baseVal:F1}");

            // Effective value — white, or green if bonus active, red if at cap
            AddLabel(x + SkillEffX, y, effHue, $"{effVal:F1}");

            // Lock buttons
            bool isUp     = skill.Lock == SkillLock.Up;
            bool isDown   = skill.Lock == SkillLock.Down;
            bool isLocked = skill.Lock == SkillLock.Locked;

            AddButton(x + SkillBtn1X, y, 4005, 4007, BTN_SKILL_UP     + sortedIndex, GumpButtonType.Reply, 0);
            AddLabel( x + SkillBtn1X, y, isUp     ? HGood    : HLabel, isUp     ? "[Up]" : " Up ");

            AddButton(x + SkillBtn2X, y, 4005, 4007, BTN_SKILL_DOWN   + sortedIndex, GumpButtonType.Reply, 0);
            AddLabel( x + SkillBtn2X, y, isDown   ? HAtCap   : HLabel, isDown   ? "[Dn]" : " Dn ");

            AddButton(x + SkillBtn3X, y, 4005, 4007, BTN_SKILL_LOCKED + sortedIndex, GumpButtonType.Reply, 0);
            AddLabel( x + SkillBtn3X, y, isLocked ? HNearCap : HLabel, isLocked ? "[Lk]" : " Lk ");
        }

        private List<Skill> GetSortedSkills()
        {
            var list = new List<Skill>();
            for (int i = 0; i < _from.Skills.Length; i++)
                list.Add(_from.Skills[i]);
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        // ═════════════════════════════════════════════════════════════════
        // Shared section/column helpers (used by Stats tab)
        // ═════════════════════════════════════════════════════════════════
        private int DrawSection(string title, int x, int y, int w)
        {
            AddBackground(x, y, w, SH, 9270);
            AddLabel(x + 5, y + 2, HHead, title);
            return y + SH + 2;
        }

        private int DrawBasics(int y)
        {
            Mobile m  = _from;
            int    x  = CL + 5;

            AddRow(x, ref y, "Str:", $"{m.Str}", HValue);
            AddRow(x, ref y, "Dex:", $"{m.Dex}", HValue);
            AddRow(x, ref y, "Int:", $"{m.Int}", HValue);
            int statTotal = m.Str + m.Dex + m.Int;
            AddRow(x, ref y, "Stat Total:", $"{statTotal} / {m.StatCap}", CapHue(statTotal, m.StatCap));
            y += 3;
            AddRow(x, ref y, "HP:",   $"{m.Hits} / {m.HitsMax}", HValue);
            AddRow(x, ref y, "Stam:", $"{m.Stam} / {m.StamMax}", HValue);
            AddRow(x, ref y, "Mana:", $"{m.Mana} / {m.ManaMax}", HValue);
            y += 3;
            int skillTotal = m.Skills.Total;
            int skillCap   = m.SkillsCap;
            AddRow(x, ref y, "Skills:",
                $"{skillTotal / 10.0:F1} / {skillCap / 10.0:F1}",
                CapHue(skillTotal, skillCap));
            return y;
        }

        private int DrawCombatDefense(int y)
        {
            Mobile m = _from; int x = CL + 5;
            AddRowCap(x, ref y, "Def Chance Inc:", Clamp(AosAttributes.GetValue(m, AosAttribute.DefendChance), 45), 45, "%");
            AddRow(x, ref y, "HP Regen:",   $"{AosAttributes.GetValue(m, AosAttribute.RegenHits)}/s",  HValue);
            AddRow(x, ref y, "Stam Regen:", $"{AosAttributes.GetValue(m, AosAttribute.RegenStam)}/s",  HValue);
            AddRow(x, ref y, "Mana Regen:", $"{AosAttributes.GetValue(m, AosAttribute.RegenMana)}/s",  HValue);
            int ns = AosAttributes.GetValue(m, AosAttribute.NightSight);
            AddRow(x, ref y, "Night Sight:", ns > 0 ? "Yes" : "No", ns > 0 ? HGood : HValue);
            int rp = AosAttributes.GetValue(m, AosAttribute.ReflectPhysical);
            if (rp > 0) AddRow(x, ref y, "Reflect Physical:", $"{rp}%", HValue);
            return y;
        }

        private int DrawCombatOffense(int y)
        {
            Mobile m = _from; int x = CR + 5;
            AddRowCap(x, ref y, "Hit Chance Inc:",  Clamp(AosAttributes.GetValue(m, AosAttribute.AttackChance),   45),  45,  "%");
            AddRowCap(x, ref y, "Damage Increase:", Clamp(AosAttributes.GetValue(m, AosAttribute.WeaponDamage),  100), 100, "%");
            AddRowCap(x, ref y, "Swing Speed Inc:", Clamp(AosAttributes.GetValue(m, AosAttribute.WeaponSpeed),    60),  60,  "%");
            AddRowCap(x, ref y, "Lower Mana Cost:", Clamp(AosAttributes.GetValue(m, AosAttribute.LowerManaCost),  40),  40,  "%");
            AddRowCap(x, ref y, "Lower Reg Cost:",  Clamp(AosAttributes.GetValue(m, AosAttribute.LowerRegCost),  100), 100, "%");
            int sdi = AosAttributes.GetValue(m, AosAttribute.SpellDamage);
            AddRowCapNote(x, ref y, "Spell Dmg Inc:", sdi, 15, "%", "(PvP cap 15)");
            AddRowCap(x, ref y, "Faster Casting:", Clamp(AosAttributes.GetValue(m, AosAttribute.CastSpeed),      4),   4,  "");
            AddRowCap(x, ref y, "FC Recovery:",    Clamp(AosAttributes.GetValue(m, AosAttribute.CastRecovery),   6),   6,  "");
            int ep = AosAttributes.GetValue(m, AosAttribute.EnhancePotions);
            if (ep > 0) AddRow(x, ref y, "Enhance Potions:", $"{ep}%", HValue);
            int lac = AosAttributes.GetValue(m, AosAttribute.LowerAmmoCost);
            if (lac > 0) AddRow(x, ref y, "Lower Ammo Cost:", $"{lac}%", HValue);
            return y;
        }

        private int DrawResistances(int y)
        {
            Mobile m = _from; int x = CR + 5;
            DrawResistRow(x, ref y, "Physical:", m.PhysicalResistance, m.GetMaxResistance(ResistanceType.Physical));
            DrawResistRow(x, ref y, "Fire:",     m.FireResistance,     m.GetMaxResistance(ResistanceType.Fire));
            DrawResistRow(x, ref y, "Cold:",     m.ColdResistance,     m.GetMaxResistance(ResistanceType.Cold));
            DrawResistRow(x, ref y, "Poison:",   m.PoisonResistance,   m.GetMaxResistance(ResistanceType.Poison));
            DrawResistRow(x, ref y, "Energy:",   m.EnergyResistance,   m.GetMaxResistance(ResistanceType.Energy));
            return y;
        }

        private void DrawResistRow(int x, ref int y, string label, int cur, int cap)
        {
            AddLabel(x, y, HLabel, label);
            int hue = cur >= cap ? HAtCap : (cur >= cap - 5 ? HNearCap : HValue);
            AddLabel(x + LW, y, hue, $"{cur} / {cap}");
            if (cur >= cap) AddLabel(x + LW + 55, y, HAtCap, "[MAX]");
            y += RH;
        }

        private int DrawWeapon(int y)
        {
            int x  = CL + 5;
            var bw = _from.Weapon as BaseWeapon;
            if (bw == null)
            { AddLabel(x, y, HLabel, "No weapon equipped (unarmed)"); y += RH; return y; }

            string wname = string.IsNullOrEmpty(bw.Name) ? bw.ItemData.Name : bw.Name;
            AddRow(x, ref y, "Weapon:", wname, HValue);
            bw.GetStatusDamage(_from, out int dmin, out int dmax);
            AddRow(x, ref y, "Damage:", $"{dmin}–{dmax}  (avg {(dmin + dmax) / 2})", HValue);
            bw.GetDamageTypes(_from, out int phys, out int fire, out int cold, out int pois, out int nrgy, out int chaos, out int direct);
            AddRow(x, ref y, "Elements:", BuildElemString(phys, fire, cold, pois, nrgy, chaos, direct), HValue);
            AddRow(x, ref y, "Base Speed:", $"{bw.WeaponSpeed:F1}", HValue);
            string onhit = BuildOnHitString(bw);
            if (!string.IsNullOrEmpty(onhit))
            { AddLabel(x, y, HLabel, "On-Hit:"); AddLabel(x + LW, y, HValue, onhit); y += RH; }
            return y;
        }

        private string BuildElemString(int phys, int fire, int cold, int pois, int nrgy, int chaos, int direct)
        {
            var parts = new List<string>();
            if (phys   > 0) parts.Add($"{phys}% Phys");
            if (fire   > 0) parts.Add($"{fire}% Fire");
            if (cold   > 0) parts.Add($"{cold}% Cold");
            if (pois   > 0) parts.Add($"{pois}% Pois");
            if (nrgy   > 0) parts.Add($"{nrgy}% Nrgy");
            if (chaos  > 0) parts.Add($"{chaos}% Chaos");
            if (direct > 0) parts.Add($"{direct}% Direct");
            return parts.Count > 0 ? string.Join(", ", parts) : "100% Physical";
        }

        private string BuildOnHitString(BaseWeapon bw)
        {
            var a = bw.WeaponAttributes; var parts = new List<string>();
            if (a.HitPhysicalArea > 0) parts.Add($"Area Phys {a.HitPhysicalArea}%");
            if (a.HitFireArea     > 0) parts.Add($"Area Fire {a.HitFireArea}%");
            if (a.HitColdArea     > 0) parts.Add($"Area Cold {a.HitColdArea}%");
            if (a.HitPoisonArea   > 0) parts.Add($"Area Pois {a.HitPoisonArea}%");
            if (a.HitEnergyArea   > 0) parts.Add($"Area Nrgy {a.HitEnergyArea}%");
            if (a.HitFireball     > 0) parts.Add($"Fireball {a.HitFireball}%");
            if (a.HitLightning    > 0) parts.Add($"Lightning {a.HitLightning}%");
            if (a.HitHarm         > 0) parts.Add($"Harm {a.HitHarm}%");
            if (a.HitMagicArrow   > 0) parts.Add($"Magic Arrow {a.HitMagicArrow}%");
            if (a.HitDispel       > 0) parts.Add($"Dispel {a.HitDispel}%");
            if (a.HitLeechHits    > 0) parts.Add($"Life Leech {a.HitLeechHits}%");
            if (a.HitLeechMana    > 0) parts.Add($"Mana Leech {a.HitLeechMana}%");
            if (a.HitLeechStam    > 0) parts.Add($"Stam Leech {a.HitLeechStam}%");
            if (a.HitManaDrain    > 0) parts.Add($"Mana Drain {a.HitManaDrain}%");
            if (a.HitLowerAttack  > 0) parts.Add($"Lower Atk {a.HitLowerAttack}%");
            if (a.HitLowerDefend  > 0) parts.Add($"Lower Def {a.HitLowerDefend}%");
            return string.Join("  ", parts);
        }

        private int DrawLuckOther(int y)
        {
            Mobile m = _from; var pm = m as PlayerMobile;
            int lx = CL + 5; int rx = CR + 5;
            int luckDisplay = pm != null ? pm.RealLuck : m.Luck;
            AddLabel(lx, y, HLabel, "Luck:");   AddLabel(lx + LW, y, HValue, $"{luckDisplay}");
            AddLabel(lx, y + RH, HLabel, "Fame:");  AddLabel(lx + LW, y + RH, HValue, $"{m.Fame}");
            AddLabel(lx, y + RH * 2, HLabel, "Karma:"); AddLabel(lx + LW, y + RH * 2, HValue, $"{m.Karma}");
            if (pm != null) { AddLabel(rx, y, HLabel, "Tithing Points:"); AddLabel(rx + LW, y, HValue, $"{pm.TithingPoints}"); }
            double mrSkill = m.Skills[SkillName.MagicResist].Value;
            AddLabel(rx, y + RH,     HLabel, "Magic Resist:");  AddLabel(rx + LW, y + RH,     HValue, $"{mrSkill:F1}");
            AddLabel(rx, y + RH * 2, HLabel, "Resist Chance:"); AddLabel(rx + LW, y + RH * 2, HValue, $"~{(int)Math.Min(100.0, mrSkill)}%");
            return y + RH * 3 + 2;
        }

        private void DrawActiveEffects(int y, int height)
        {
            if (height < 40) height = 40;
            var pm = _from as PlayerMobile;
            if (pm == null || pm.Buffs == null || pm.Buffs.Count == 0)
            { AddHtml(CL + 5, y, FW - 10, height, "<BASEFONT COLOR=#808080>No active effects.</BASEFONT>", false, true); return; }

            var sb = new StringBuilder();
            var now = DateTime.UtcNow;
            foreach (var kvp in pm.Buffs)
            {
                BuffInfo info = kvp.Value;
                string   dur  = "";
                if (!info.NoTimer && info.TimeLength > TimeSpan.Zero)
                {
                    TimeSpan rem = info.TimeStart + info.TimeLength - now;
                    dur = rem > TimeSpan.Zero
                        ? $" <BASEFONT COLOR=#AAFFAA>({FormatDuration(rem)})</BASEFONT>"
                        : " <BASEFONT COLOR=#888888>(expired)</BASEFONT>";
                }
                sb.Append($"<BASEFONT COLOR=#FFFF00>{GetBuffName(info.ID)}</BASEFONT>{dur}<BR>");
            }
            AddHtml(CL + 5, y, FW - 10, height, sb.ToString(), false, true);
        }

        // ═════════════════════════════════════════════════════════════════
        // Response
        // ═════════════════════════════════════════════════════════════════
        public override void OnResponse(NetState sender, RelayInfo info)
        {
            int btn = info.ButtonID;

            if (btn == BTN_CLOSE)
            { _refreshEnabled.Remove(_from.Serial); return; }

            if (btn == BTN_TAB_STATS)  { OpenFor(_from, 0); return; }
            if (btn == BTN_TAB_REP)    { OpenFor(_from, 1); return; }
            if (btn == BTN_TAB_SKILLS) { OpenFor(_from, 2); return; }

            if (btn == BTN_ITEM_SEARCH)
            {
                if (_from is PlayerMobile pm2)
                { pm2.CloseGump(typeof(ItemSearchGump)); pm2.SendGump(new ItemSearchGump(pm2)); }
                return;
            }

            // Skill lock buttons (100–399)
            if (btn >= BTN_SKILL_UP && btn < BTN_SKILL_LOCKED + 100)
            {
                SkillLock newLock;
                int idx;

                if (btn >= BTN_SKILL_LOCKED)      { newLock = SkillLock.Locked; idx = btn - BTN_SKILL_LOCKED; }
                else if (btn >= BTN_SKILL_DOWN)   { newLock = SkillLock.Down;   idx = btn - BTN_SKILL_DOWN;   }
                else                              { newLock = SkillLock.Up;     idx = btn - BTN_SKILL_UP;     }

                List<Skill> sorted = GetSortedSkills();
                if (idx >= 0 && idx < sorted.Count)
                    sorted[idx].SetLockNoRelay(newLock);

                OpenFor(_from, 2);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Shared helpers
        // ═════════════════════════════════════════════════════════════════
        private static int Clamp(int val, int cap) => val > cap ? cap : val;

        private static int CapHue(int val, int cap)
        {
            if (cap <= 0)              return HValue;
            if (val >= cap)            return HAtCap;
            if (val >= cap * 9 / 10)  return HNearCap;
            return HValue;
        }

        private void AddRow(int x, ref int y, string label, string value, int valueHue)
        { AddLabel(x, y, HLabel, label); AddLabel(x + LW, y, valueHue, value); y += RH; }

        private void AddRowCap(int x, ref int y, string label, int val, int cap, string suffix)
        {
            AddLabel(x, y, HLabel, label);
            int hue = CapHue(val, cap);
            AddLabel(x + LW, y, hue, $"{val}{suffix}");
            if (val >= cap) AddLabel(x + LW + 50, y, HAtCap, "[MAX]");
            y += RH;
        }

        private void AddRowCapNote(int x, ref int y, string label, int val, int pvpCap, string suffix, string note)
        {
            AddLabel(x, y, HLabel, label);
            AddLabel(x + LW, y, val >= pvpCap ? HNearCap : HValue, $"{val}{suffix}");
            AddLabel(x + LW + 40, y, HLabel, note);
            y += RH;
        }

        private static string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)   return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        private static string GetBuffName(BuffIcon icon)
        {
            switch (icon)
            {
                case BuffIcon.MagicReflection:   return "Magic Reflection";
                case BuffIcon.Protection:        return "Protection";
                case BuffIcon.ArchProtection:    return "Arch Protection";
                case BuffIcon.Bless:             return "Bless";
                case BuffIcon.Agility:           return "Agility";
                case BuffIcon.Cunning:           return "Cunning";
                case BuffIcon.Strength:          return "Strength";
                case BuffIcon.Clumsy:            return "Clumsy (curse)";
                case BuffIcon.FeebleMind:        return "Feeble Mind (curse)";
                case BuffIcon.Weaken:            return "Weaken (curse)";
                case BuffIcon.Curse:             return "Curse";
                case BuffIcon.MassCurse:         return "Mass Curse";
                case BuffIcon.Paralyze:          return "Paralyzed";
                case BuffIcon.Poison:            return "Poisoned";
                case BuffIcon.Bleed:             return "Bleeding";
                case BuffIcon.Sleep:             return "Sleep";
                case BuffIcon.HidingAndOrStealth:return "Hidden / Stealth";
                case BuffIcon.ReactiveArmor:     return "Reactive Armor";
                case BuffIcon.EtherealVoyage:    return "Ethereal Voyage";
                case BuffIcon.CorpseSkin:        return "Corpse Skin";
                case BuffIcon.HorrificBeast:     return "Horrific Beast";
                case BuffIcon.LichForm:          return "Lich Form";
                case BuffIcon.VampiricEmbrace:   return "Vampiric Embrace";
                case BuffIcon.WraithForm:        return "Wraith Form";
                case BuffIcon.Fly:               return "Fly";
                case BuffIcon.Inspire:           return "Inspire";
                case BuffIcon.Invigorate:        return "Invigorate";
                case BuffIcon.Resilience:        return "Resilience";
                case BuffIcon.StoneForm:         return "Stone Form";
                case BuffIcon.AttuneWeapon:      return "Attune Weapon";
                case BuffIcon.GiftOfLife:        return "Gift of Life";
                case BuffIcon.ArcaneEmpowerment: return "Arcane Empowerment";
                case BuffIcon.MortalStrike:      return "Mortal Strike";
                case BuffIcon.WhiteTigerForm:    return "White Tiger Form";
                case BuffIcon.Veterinary:        return "Veterinary";
                case BuffIcon.HeatOfBattleStatus:return "Heat of Battle";
                case BuffIcon.CriminalStatus:    return "Criminal";
                case BuffIcon.ArmorPierce:       return "Armor Pierce";
                case BuffIcon.SplinteringEffect: return "Splintering Weapon";
                case BuffIcon.Berserk:           return "Berserk";
                case BuffIcon.MysticWeapon:      return "Mystic Weapon";
                case BuffIcon.ConsecrateWeapon:  return "Consecrate Weapon";
                case BuffIcon.EnemyOfOne:        return "Enemy of One";
                case BuffIcon.HonorableExecution:return "Honorable Execution";
                case BuffIcon.Swarm:             return "Swarm";
                case BuffIcon.FistsOfFury:       return "Fists of Fury";
                case BuffIcon.DivineFury:        return "Divine Fury";
                case BuffIcon.AnimalForm:        return "Animal Form";
                case BuffIcon.Incognito:         return "Incognito";
                case BuffIcon.Confidence:        return "Confidence";
                case BuffIcon.Evasion:           return "Evasion";
                case BuffIcon.LightningStrike:   return "Lightning Strike";
                case BuffIcon.MomentumStrike:    return "Momentum Strike";
                case BuffIcon.Rampage:           return "Rampage";
                case BuffIcon.Toughness:         return "Toughness";
                case BuffIcon.PlayingTheOdds:    return "Playing the Odds";
                case BuffIcon.FocusedEye:        return "Focused Eye";
                case BuffIcon.ElementalFury:     return "Elemental Fury";
                case BuffIcon.CalledShot:        return "Called Shot";
                case BuffIcon.Knockout:          return "Knockout";
                case BuffIcon.SavingThrow:       return "Saving Throw";
                case BuffIcon.Warcry:            return "Warcry";
                case BuffIcon.Shadow:            return "Shadow";
                case BuffIcon.Intuition:         return "Intuition";
                case BuffIcon.BarrabHemolymphConcentrate: return "Barrabs Blood";
                case BuffIcon.JukariBurnPoiltice:         return "Jukari Burn Poultice";
                case BuffIcon.KurakAmbushersEssence:      return "Kurak Ambusher";
                case BuffIcon.BarakoDraftOfMight:         return "Barako Draft of Might";
                case BuffIcon.UraliTranceTonic:           return "Urali Trance Tonic";
                case BuffIcon.SakkhraProphylaxis:         return "Sakkhra Prophylaxis";
                default:                                  return icon.ToString();
            }
        }
    }
}
