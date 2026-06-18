// ============================================================
// BankExpansionSystem.cs
// Scripts/Custom/BankExpansionSystem.cs
//
// Allows players to expand their bank box beyond the default
// 125-item limit by using Bank Expansion Deeds purchased from
// the Hunter Token Shop.
//
// Rules:
//   - 1 token per slot, sold in packs of 25 (25 tokens each)
//   - Maximum 1000 bonus slots (total bank = 1125 items)
//   - Expansion persists across restarts via Saves/Custom/BankExpansion.bin
//   - Applied on login via EventSink.Login
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Custom
{
    // ============================================================
    // BANK EXPANSION SYSTEM
    // ============================================================
    public static class BankExpansionSystem
    {
        public const  int BaseSlots  = 125;   // default GlobalMaxItems
        public const  int ExtraCap   = 1000;  // max purchasable bonus slots
        public const  int SlotsPerDeed = 25;

        private const string SavePath = "Saves/Custom/BankExpansion.bin";

        private static Dictionary<Serial, int> _expansions = new Dictionary<Serial, int>();

        public static void Initialize()
        {
            EventSink.Login     += OnLogin;
            EventSink.WorldSave += OnWorldSave;
            Load();
        }

        // ── Public API ────────────────────────────────────────────────────

        public static int GetExtraSlots(Mobile m)
        {
            return _expansions.TryGetValue(m.Serial, out int v) ? v : 0;
        }

        public static int GetTotalSlots(Mobile m)
        {
            return BaseSlots + GetExtraSlots(m);
        }

        public static bool AtCap(Mobile m)
        {
            return GetExtraSlots(m) >= ExtraCap;
        }

        /// <summary>
        /// Adds slots to the player's bank (capped at ExtraCap).
        /// Returns the actual number of slots added.
        /// </summary>
        public static int AddSlots(Mobile m, int slots)
        {
            int current  = GetExtraSlots(m);
            int newTotal = Math.Min(current + slots, ExtraCap);
            int added    = newTotal - current;

            if (added <= 0)
                return 0;

            _expansions[m.Serial] = newTotal;
            ApplyToBank(m);
            return added;
        }

        // ── Internal ──────────────────────────────────────────────────────

        private static void ApplyToBank(Mobile m)
        {
            if (m?.BankBox != null)
                m.BankBox.MaxItems = BaseSlots + GetExtraSlots(m);
        }

        private static void OnLogin(LoginEventArgs e)
        {
            if (GetExtraSlots(e.Mobile) > 0)
                ApplyToBank(e.Mobile);
        }

        // ── Persistence ───────────────────────────────────────────────────

        private static void OnWorldSave(WorldSaveEventArgs e)
        {
            try
            {
                string dir = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                BinaryFileWriter writer = new BinaryFileWriter(SavePath, true);
                writer.Write(0); // version
                writer.Write(_expansions.Count);
                foreach (var kvp in _expansions)
                {
                    writer.Write((int)kvp.Key); // Serial → int
                    writer.Write(kvp.Value);
                }
                writer.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BankExpansionSystem] Save error: {0}", ex.Message);
            }
        }

        private static void Load()
        {
            if (!File.Exists(SavePath))
                return;

            try
            {
                using (FileStream fs = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    BinaryFileReader reader = new BinaryFileReader(br);

                    int version = reader.ReadInt();
                    int count   = reader.ReadInt();

                    _expansions = new Dictionary<Serial, int>(count);
                    for (int i = 0; i < count; i++)
                    {
                        Serial serial = (Serial)reader.ReadInt();
                        int    slots  = reader.ReadInt();
                        _expansions[serial] = slots;
                    }

                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BankExpansionSystem] Load error: {0}", ex.Message);
            }
        }
    }

    // ============================================================
    // BANK EXPANSION DEED
    // ============================================================
    // Double-click to permanently add 25 slots to your bank.
    // Blessed — won't drop on death. Purchased from token shop.
    // ============================================================
    public class BankExpansionDeed : Item
    {
        [Constructable]
        public BankExpansionDeed() : base(0x14F0) // scroll/deed graphic
        {
            Name     = "Bank Expansion Deed";
            Hue      = 0x481;
            Weight   = 1.0;
            LootType = LootType.Blessed;
        }

        public BankExpansionDeed(Serial serial) : base(serial) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack.
                return;
            }

            if (BankExpansionSystem.AtCap(from))
            {
                from.SendMessage(0x22,
                    $"Your bank is already at maximum capacity ({BankExpansionSystem.ExtraCap} bonus slots).");
                return;
            }

            int added = BankExpansionSystem.AddSlots(from, BankExpansionSystem.SlotsPerDeed);
            int extra = BankExpansionSystem.GetExtraSlots(from);
            int total = BankExpansionSystem.GetTotalSlots(from);

            from.SendMessage(0x35,
                $"Your bank has been expanded by {added} slots! " +
                $"It now holds {total} items ({extra}/{BankExpansionSystem.ExtraCap} bonus slots used).");

            from.PlaySound(0x249); // receipt/paper sound

            Delete();
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Expands your bank by {BankExpansionSystem.SlotsPerDeed} item slots");
            list.Add($"Maximum: {BankExpansionSystem.ExtraCap} bonus slots total");
            list.Add("Blessed — will not drop on death");
            list.Add("Double-click to use");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();
        }
    }
}
