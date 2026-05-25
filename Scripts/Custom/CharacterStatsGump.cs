using System;
using System.Collections.Generic;
using System.Text;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps
{
    public class CharacterStatsGump : Gump
    {
        // ── Layout constants ─────────────────────────────────────────────
        private const int GW  = 540;   // gump width
        private const int GH  = 692;   // gump height (fixed)
        private const int TitleH = 28; // title bar height
        private const int RH  = 17;    // row height
        private const int SH  = 19;    // section-header bar height

        // Two-column x origins (each ~250 wide)
        private const int CL  = 10;    // left column x
        private const int CR  = 285;   // right column x
        private const int CW  = 250;   // column width

        // Full-width sections
        private const int FW  = GW - 20; // 520

        // Label column widths inside each section
        private const int LW  = 140;   // label text width

        // ── Hues ─────────────────────────────────────────────────────────
        private const int HTitle   = 1258;  // gold  – window title
        private const int HHead    = 53;    // yellow – section header text
        private const int HLabel   = 2100;  // silver – stat label
        private const int HValue   = 1153;  // white  – stat value
        private const int HAtCap   = 33;    // red    – at or over cap
        private const int HNearCap = 43;    // orange – within 10 % of cap
        private const int HGood    = 63;    // green  – informational positive

        // Tracks which players have auto-refresh enabled.
        // Cleared when the player manually closes the gump; NOT cleared by server-side refreshes.
        private static readonly HashSet<Serial> _refreshEnabled = new HashSet<Serial>();

        // ═════════════════════════════════════════════════════════════════
        public static void Initialize()
        {
            CommandSystem.Register("statwnd", AccessLevel.Player, OnCommand);
            EventSink.PaperdollRequest += OnPaperdollRequest;
            EventSink.Logout           += OnLogout;
        }

        private static void OnCommand(CommandEventArgs e)
        {
            OpenFor(e.Mobile);
        }

        private static void OnPaperdollRequest(PaperdollRequestEventArgs e)
        {
            // Auto-open stats window only when a player views their own paperdoll
            if (e.Beholder == e.Beheld && e.Beholder is PlayerMobile)
                OpenFor(e.Beholder);
        }

        private static void OnLogout(LogoutEventArgs e)
        {
            _refreshEnabled.Remove(e.Mobile.Serial);
        }

        /// Open (or re-open) the gump and enable auto-refresh for this player.
        private static void OpenFor(Mobile from)
        {
            _refreshEnabled.Add(from.Serial);
            from.CloseGump(typeof(CharacterStatsGump));
            from.SendGump(new CharacterStatsGump(from));
        }

        // ═════════════════════════════════════════════════════════════════
        private readonly Mobile _from;

        private const double RefreshSeconds = 3.0;

        public CharacterStatsGump(Mobile from) : base(300, 30)
        {
            _from      = from;
            Closable   = true;
            Dragable   = true;
            Disposable = true;
            Resizable  = false;

            BuildGump();

            // Schedule one auto-refresh tick — each new gump instance chains the next
            if (_refreshEnabled.Contains(from.Serial))
                Timer.DelayCall(TimeSpan.FromSeconds(RefreshSeconds), DoRefresh);
        }

        private void DoRefresh()
        {
            // Stop if player disconnected or deleted
            if (_from == null || _from.Deleted || _from.NetState == null)
            {
                _refreshEnabled.Remove(_from.Serial);
                return;
            }

            // Stop if player manually closed the gump (flag was cleared in OnResponse)
            if (!_refreshEnabled.Contains(_from.Serial))
                return;

            // Close this snapshot and open a fresh one (its constructor will schedule the next tick)
            _from.CloseGump(typeof(CharacterStatsGump));
            _from.SendGump(new CharacterStatsGump(_from));
        }

        // ── Top-level builder ─────────────────────────────────────────────
        private void BuildGump()
        {
            // Background + alpha overlay
            AddBackground(0, 0, GW, GH, 9270);
            AddAlphaRegion(4, 4, GW - 8, GH - 8);

            // Title bar
            AddBackground(4, 4, GW - 8, TitleH, 9270);
            AddLabel(12, 9, HTitle, $"Character Stats — {_from.Name}");

            // ── Left column ──────────────────────────────────────────────
            int lY = TitleH + 6;
            lY = DrawSection("BASICS",         CL, lY, CW);
            lY = DrawBasics(lY);
            lY += 4;
            lY = DrawSection("COMBAT DEFENSE", CL, lY, CW);
            lY = DrawCombatDefense(lY);

            // ── Right column (starts same y as left) ─────────────────────
            int rY = TitleH + 6;
            rY = DrawSection("COMBAT OFFENSE", CR, rY, CW);
            rY = DrawCombatOffense(rY);
            rY += 4;
            rY = DrawSection("RESISTANCES",    CR, rY, CW);
            rY = DrawResistances(rY);

            // ── Full-width sections below both columns ───────────────────
            int fY = Math.Max(lY, rY) + 6;
            fY = DrawSection("WIELDED WEAPON", CL, fY, FW);
            fY = DrawWeapon(fY);
            fY += 4;
            fY = DrawSection("LUCK / OTHER",   CL, fY, FW);
            fY = DrawLuckOther(fY);
            fY += 4;
            fY = DrawSection("ACTIVE EFFECTS", CL, fY, FW);
            DrawActiveEffects(fY, GH - fY - 42);

            // ── Bottom button row ─────────────────────────────────────────
            int btnY = GH - 30;
            AddButton(CL, btnY, 4005, 4007, 1, GumpButtonType.Reply, 0);
            AddHtml(CL + 22, btnY + 2, 120, 20,
                "<BASEFONT COLOR=#DDCCAA>Item Search</BASEFONT>", false, false);
        }

        // ── Section header bar ────────────────────────────────────────────
        // Returns y after the header (ready for first row)
        private int DrawSection(string title, int x, int y, int w)
        {
            AddBackground(x, y, w, SH, 9270);
            AddLabel(x + 5, y + 2, HHead, title);
            return y + SH + 2;
        }

        // ════════════════════════════════════════════════════════════════
        // BASICS  (left column)
        // ════════════════════════════════════════════════════════════════
        private int DrawBasics(int y)
        {
            Mobile m    = _from;
            var    pm   = m as PlayerMobile;
            int    x    = CL + 5;

            // Stats
            AddRow(x, ref y, "Str:", $"{m.Str}", HValue);
            AddRow(x, ref y, "Dex:", $"{m.Dex}", HValue);
            AddRow(x, ref y, "Int:", $"{m.Int}", HValue);
            int statTotal = m.Str + m.Dex + m.Int;
            int statCap   = m.StatCap;
            AddRow(x, ref y, "Stat Total:", $"{statTotal} / {statCap}", CapHue(statTotal, statCap));

            y += 3;

            // Vitals
            AddRow(x, ref y, "HP:",   $"{m.Hits} / {m.HitsMax}", HValue);
            AddRow(x, ref y, "Stam:", $"{m.Stam} / {m.StamMax}", HValue);
            AddRow(x, ref y, "Mana:", $"{m.Mana} / {m.ManaMax}", HValue);

            y += 3;

            // Skills
            int skillTotal = m.Skills.Total;       // fixed-point tenths
            int skillCap   = m.SkillsCap;           // fixed-point tenths
            AddRow(x, ref y, "Skills:",
                $"{skillTotal / 10.0:F1} / {skillCap / 10.0:F1}",
                CapHue(skillTotal, skillCap));

            return y;
        }

        // ════════════════════════════════════════════════════════════════
        // COMBAT DEFENSE  (left column)
        // ════════════════════════════════════════════════════════════════
        private int DrawCombatDefense(int y)
        {
            Mobile m = _from;
            int    x = CL + 5;

            int dci = Clamp(AosAttributes.GetValue(m, AosAttribute.DefendChance), 45);
            AddRowCap(x, ref y, "Def Chance Inc:", dci, 45, "%");

            int hpr = AosAttributes.GetValue(m, AosAttribute.RegenHits);
            int sr  = AosAttributes.GetValue(m, AosAttribute.RegenStam);
            int mr  = AosAttributes.GetValue(m, AosAttribute.RegenMana);
            AddRow(x, ref y, "HP Regen:",   $"{hpr}/s", HValue);
            AddRow(x, ref y, "Stam Regen:", $"{sr}/s",  HValue);
            AddRow(x, ref y, "Mana Regen:", $"{mr}/s",  HValue);

            int ns = AosAttributes.GetValue(m, AosAttribute.NightSight);
            AddRow(x, ref y, "Night Sight:", ns > 0 ? "Yes" : "No", ns > 0 ? HGood : HValue);

            int rp = AosAttributes.GetValue(m, AosAttribute.ReflectPhysical);
            if (rp > 0)
                AddRow(x, ref y, "Reflect Physical:", $"{rp}%", HValue);

            return y;
        }

        // ════════════════════════════════════════════════════════════════
        // COMBAT OFFENSE  (right column)
        // ════════════════════════════════════════════════════════════════
        private int DrawCombatOffense(int y)
        {
            Mobile m = _from;
            int    x = CR + 5;

            int hci = Clamp(AosAttributes.GetValue(m, AosAttribute.AttackChance), 45);
            AddRowCap(x, ref y, "Hit Chance Inc:", hci, 45, "%");

            int di = Clamp(AosAttributes.GetValue(m, AosAttribute.WeaponDamage), 100);
            AddRowCap(x, ref y, "Damage Increase:", di, 100, "%");

            int ssi = Clamp(AosAttributes.GetValue(m, AosAttribute.WeaponSpeed), 60);
            AddRowCap(x, ref y, "Swing Speed Inc:", ssi, 60, "%");

            int lmc = Clamp(AosAttributes.GetValue(m, AosAttribute.LowerManaCost), 40);
            AddRowCap(x, ref y, "Lower Mana Cost:", lmc, 40, "%");

            int lrc = Clamp(AosAttributes.GetValue(m, AosAttribute.LowerRegCost), 100);
            AddRowCap(x, ref y, "Lower Reg Cost:", lrc, 100, "%");

            int sdi = AosAttributes.GetValue(m, AosAttribute.SpellDamage);
            // SDI: 15% cap vs players, 100% vs monsters — show raw value, note cap
            AddRowCapNote(x, ref y, "Spell Dmg Inc:", sdi, 15, "%", "(PvP cap 15)");

            int fc  = Clamp(AosAttributes.GetValue(m, AosAttribute.CastSpeed),   4);
            AddRowCap(x, ref y, "Faster Casting:", fc, 4, "");

            int fcr = Clamp(AosAttributes.GetValue(m, AosAttribute.CastRecovery), 6);
            AddRowCap(x, ref y, "FC Recovery:", fcr, 6, "");

            int ep = AosAttributes.GetValue(m, AosAttribute.EnhancePotions);
            if (ep > 0)
                AddRow(x, ref y, "Enhance Potions:", $"{ep}%", HValue);

            int lac = AosAttributes.GetValue(m, AosAttribute.LowerAmmoCost);
            if (lac > 0)
                AddRow(x, ref y, "Lower Ammo Cost:", $"{lac}%", HValue);

            return y;
        }

        // ════════════════════════════════════════════════════════════════
        // RESISTANCES  (right column)
        // ════════════════════════════════════════════════════════════════
        private int DrawResistances(int y)
        {
            Mobile m = _from;
            int    x = CR + 5;

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
            if (cur >= cap)
                AddLabel(x + LW + 55, y, HAtCap, "[MAX]");
            y += RH;
        }

        // ════════════════════════════════════════════════════════════════
        // WIELDED WEAPON  (full width)
        // ════════════════════════════════════════════════════════════════
        private int DrawWeapon(int y)
        {
            int x  = CL + 5;
            var bw = _from.Weapon as BaseWeapon;

            if (bw == null)
            {
                AddLabel(x, y, HLabel, "No weapon equipped (unarmed)");
                y += RH;
                return y;
            }

            // Name
            string wname = bw.Name;
            if (string.IsNullOrEmpty(wname))
                wname = bw.ItemData.Name;
            AddRow(x, ref y, "Weapon:", wname, HValue);

            // Scaled damage (factors Str / Tactics / Anatomy / DI / buffs)
            bw.GetStatusDamage(_from, out int dmin, out int dmax);
            int avg = (dmin + dmax) / 2;
            AddRow(x, ref y, "Damage:", $"{dmin}–{dmax}  (avg {avg})", HValue);

            // Element split
            bw.GetDamageTypes(_from, out int phys, out int fire, out int cold,
                              out int pois, out int nrgy, out int chaos, out int direct);
            string elems = BuildElemString(phys, fire, cold, pois, nrgy, chaos, direct);
            AddRow(x, ref y, "Elements:", elems, HValue);

            // Speed
            float spd = bw.WeaponSpeed;
            AddRow(x, ref y, "Base Speed:", $"{spd:F1}", HValue);

            // On-hit effects — only show the line if something is present
            string onhit = BuildOnHitString(bw);
            if (!string.IsNullOrEmpty(onhit))
            {
                // Might wrap, so put on its own line under the label
                AddLabel(x, y, HLabel, "On-Hit:");
                AddLabel(x + LW, y, HValue, onhit);
                y += RH;
            }

            return y;
        }

        private string BuildElemString(int phys, int fire, int cold,
                                       int pois, int nrgy, int chaos, int direct)
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
            var a = bw.WeaponAttributes;
            var parts = new List<string>();
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
            if (a.HitLeechHits  > 0) parts.Add($"Life Leech {a.HitLeechHits}%");
            if (a.HitLeechMana  > 0) parts.Add($"Mana Leech {a.HitLeechMana}%");
            if (a.HitLeechStam  > 0) parts.Add($"Stam Leech {a.HitLeechStam}%");
            if (a.HitManaDrain  > 0) parts.Add($"Mana Drain {a.HitManaDrain}%");
            if (a.HitLowerAttack  > 0) parts.Add($"Lower Atk {a.HitLowerAttack}%");
            if (a.HitLowerDefend  > 0) parts.Add($"Lower Def {a.HitLowerDefend}%");
            return string.Join("  ", parts);
        }

        // ════════════════════════════════════════════════════════════════
        // LUCK / OTHER  (full width, two-column layout inside)
        // ════════════════════════════════════════════════════════════════
        private int DrawLuckOther(int y)
        {
            Mobile m  = _from;
            var    pm = m as PlayerMobile;

            int luck = AosAttributes.GetValue(m, AosAttribute.Luck);

            // Left sub-column
            int lx = CL + 5;
            AddLabel(lx, y, HLabel, "Luck:");
            AddLabel(lx + LW, y, HValue, $"{luck}");

            AddLabel(lx, y + RH, HLabel, "Fame:");
            AddLabel(lx + LW, y + RH, HValue, $"{m.Fame}");

            AddLabel(lx, y + RH * 2, HLabel, "Karma:");
            AddLabel(lx + LW, y + RH * 2, HValue, $"{m.Karma}");

            // Right sub-column
            int rx = CR + 5;
            if (pm != null)
            {
                AddLabel(rx, y, HLabel, "Tithing Points:");
                AddLabel(rx + LW, y, HValue, $"{pm.TithingPoints}");
            }

            // Spell defence flavour: Magic Resist skill
            double mrSkill     = m.Skills[SkillName.MagicResist].Value;
            int    resistChance = (int)Math.Min(100.0, mrSkill);   // rough: 100 skill ≈ 100% chance

            AddLabel(rx, y + RH, HLabel, "Magic Resist:");
            AddLabel(rx + LW, y + RH, HValue, $"{mrSkill:F1}");

            AddLabel(rx, y + RH * 2, HLabel, "Resist Chance:");
            AddLabel(rx + LW, y + RH * 2, HValue, $"~{resistChance}%");

            y += RH * 3 + 2;
            return y;
        }

        // ════════════════════════════════════════════════════════════════
        // ACTIVE EFFECTS  (scrollable HTML, full width)
        // ════════════════════════════════════════════════════════════════
        private void DrawActiveEffects(int y, int height)
        {
            if (height < 40) height = 40;

            var pm = _from as PlayerMobile;
            if (pm == null || pm.Buffs == null || pm.Buffs.Count == 0)
            {
                AddHtml(CL + 5, y, FW - 10, height,
                    "<BASEFONT COLOR=#808080>No active effects.</BASEFONT>",
                    false, true);
                return;
            }

            var    sb  = new StringBuilder();
            var    now = DateTime.UtcNow;

            sb.Append("<BASEFONT COLOR=#FFFF00>");

            foreach (var kvp in pm.Buffs)
            {
                BuffInfo info = kvp.Value;
                string   name = GetBuffName(info.ID);
                string   dur  = "";

                if (!info.NoTimer && info.TimeLength > TimeSpan.Zero)
                {
                    TimeSpan remaining = info.TimeStart + info.TimeLength - now;
                    dur = remaining > TimeSpan.Zero
                        ? $" <BASEFONT COLOR=#AAFFAA>({FormatDuration(remaining)})</BASEFONT>"
                        : " <BASEFONT COLOR=#888888>(expired)</BASEFONT>";
                }

                sb.Append($"<BASEFONT COLOR=#FFFF00>{name}</BASEFONT>{dur}<BR>");
            }

            AddHtml(CL + 5, y, FW - 10, height, sb.ToString(), false, true);
        }

        private static string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        private static string GetBuffName(BuffIcon icon)
        {
            switch (icon)
            {
                case BuffIcon.MagicReflection:            return "Magic Reflection";
                case BuffIcon.Protection:                 return "Protection";
                case BuffIcon.ArchProtection:             return "Arch Protection";
                case BuffIcon.Bless:                      return "Bless";
                case BuffIcon.Agility:                    return "Agility";
                case BuffIcon.Cunning:                    return "Cunning";
                case BuffIcon.Strength:                   return "Strength";
                case BuffIcon.Clumsy:                     return "Clumsy (curse)";
                case BuffIcon.FeebleMind:                 return "Feeble Mind (curse)";
                case BuffIcon.Weaken:                     return "Weaken (curse)";
                case BuffIcon.Curse:                      return "Curse";
                case BuffIcon.MassCurse:                  return "Mass Curse";
                case BuffIcon.Paralyze:                   return "Paralyzed";
                case BuffIcon.Poison:                     return "Poisoned";
                case BuffIcon.Bleed:                      return "Bleeding";
                case BuffIcon.Sleep:                      return "Sleep";
                case BuffIcon.HidingAndOrStealth:         return "Hidden / Stealth";
                case BuffIcon.ReactiveArmor:              return "Reactive Armor";
                case BuffIcon.EtherealVoyage:             return "Ethereal Voyage";
                case BuffIcon.CorpseSkin:                 return "Corpse Skin";
                case BuffIcon.HorrificBeast:              return "Horrific Beast";
                case BuffIcon.LichForm:                   return "Lich Form";
                case BuffIcon.VampiricEmbrace:            return "Vampiric Embrace";
                case BuffIcon.WraithForm:                 return "Wraith Form";
                case BuffIcon.Fly:                        return "Fly";
                case BuffIcon.Inspire:                    return "Inspire";
                case BuffIcon.Invigorate:                 return "Invigorate";
                case BuffIcon.Resilience:                 return "Resilience";
                case BuffIcon.StoneForm:                  return "Stone Form";
                case BuffIcon.AttuneWeapon:               return "Attune Weapon";
                case BuffIcon.GiftOfLife:                 return "Gift of Life";
                case BuffIcon.ArcaneEmpowerment:          return "Arcane Empowerment";
                case BuffIcon.MortalStrike:               return "Mortal Strike";
                case BuffIcon.WhiteTigerForm:             return "White Tiger Form";
                case BuffIcon.Veterinary:                 return "Veterinary";
                case BuffIcon.HeatOfBattleStatus:         return "Heat of Battle";
                case BuffIcon.CriminalStatus:             return "Criminal";
                case BuffIcon.ArmorPierce:                return "Armor Pierce";
                case BuffIcon.SplinteringEffect:          return "Splintering Weapon";
                case BuffIcon.Berserk:                    return "Berserk";
                case BuffIcon.MysticWeapon:               return "Mystic Weapon";
                case BuffIcon.ConsecrateWeapon:           return "Consecrate Weapon";
                case BuffIcon.EnemyOfOne:                 return "Enemy of One";
                case BuffIcon.HonorableExecution:         return "Honorable Execution";
                case BuffIcon.Swarm:                      return "Swarm";
                case BuffIcon.FistsOfFury:                return "Fists of Fury";
                case BuffIcon.DivineFury:                 return "Divine Fury";
                case BuffIcon.AnimalForm:                 return "Animal Form";
                case BuffIcon.Incognito:                  return "Incognito";
                case BuffIcon.Confidence:                 return "Confidence";
                case BuffIcon.Evasion:                    return "Evasion";
                case BuffIcon.LightningStrike:            return "Lightning Strike";
                case BuffIcon.MomentumStrike:             return "Momentum Strike";
                case BuffIcon.Rampage:                    return "Rampage";
                case BuffIcon.Toughness:                  return "Toughness";
                case BuffIcon.PlayingTheOdds:             return "Playing the Odds";
                case BuffIcon.FocusedEye:                 return "Focused Eye";
                case BuffIcon.ElementalFury:              return "Elemental Fury";
                case BuffIcon.CalledShot:                 return "Called Shot";
                case BuffIcon.Knockout:                   return "Knockout";
                case BuffIcon.SavingThrow:                return "Saving Throw";
                case BuffIcon.Warcry:                     return "Warcry";
                case BuffIcon.Shadow:                     return "Shadow";
                case BuffIcon.Intuition:                  return "Intuition";
                case BuffIcon.BarrabHemolymphConcentrate: return "Barrabs Blood";
                case BuffIcon.JukariBurnPoiltice:         return "Jukari Burn Poultice";
                case BuffIcon.KurakAmbushersEssence:      return "Kurak Ambusher";
                case BuffIcon.BarakoDraftOfMight:         return "Barako Draft of Might";
                case BuffIcon.UraliTranceTonic:           return "Urali Trance Tonic";
                case BuffIcon.SakkhraProphylaxis:         return "Sakkhra Prophylaxis";
                default:                                  return icon.ToString();
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Helpers
        // ════════════════════════════════════════════════════════════════

        /// Clamp val to cap (UO attributes don't auto-cap when read)
        private static int Clamp(int val, int cap) => val > cap ? cap : val;

        /// Hue based on proximity to cap (higher = toward red)
        private static int CapHue(int val, int cap)
        {
            if (cap <= 0) return HValue;
            if (val >= cap)             return HAtCap;
            if (val >= cap * 9 / 10)   return HNearCap;
            return HValue;
        }

        // Simple label + value row (left column)
        private void AddRow(int x, ref int y, string label, string value, int valueHue)
        {
            AddLabel(x, y, HLabel, label);
            AddLabel(x + LW, y, valueHue, value);
            y += RH;
        }

        // Label + value + cap indicator row (right column or left)
        private void AddRowCap(int x, ref int y, string label, int val, int cap, string suffix)
        {
            AddLabel(x, y, HLabel, label);
            int hue = CapHue(val, cap);
            AddLabel(x + LW, y, hue, $"{val}{suffix}");
            if (val >= cap)
                AddLabel(x + LW + 50, y, HAtCap, "[MAX]");
            y += RH;
        }

        // Label + value + cap + small note
        private void AddRowCapNote(int x, ref int y, string label, int val, int pvpCap, string suffix, string note)
        {
            AddLabel(x, y, HLabel, label);
            int hue = val >= pvpCap ? HNearCap : HValue;  // orange if over pvp cap
            AddLabel(x + LW, y, hue, $"{val}{suffix}");
            AddLabel(x + LW + 40, y, HLabel, note);
            y += RH;
        }

        // ── Gump response ─────────────────────────────────────────────────
        public override void OnResponse(NetState sender, RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                // Item Search — open without killing auto-refresh
                if (_from is PlayerMobile pm)
                {
                    pm.CloseGump(typeof(Server.Custom.ItemSearchGump));
                    pm.SendGump(new Server.Custom.ItemSearchGump(pm, "", null, 0));
                }
                return;
            }

            // Button 0 = closed — disable auto-refresh so the timer doesn't reopen it
            _refreshEnabled.Remove(_from.Serial);
        }
    }
}
