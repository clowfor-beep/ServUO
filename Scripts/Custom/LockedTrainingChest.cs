// ============================================================
// LockedTrainingChest.cs
// Scripts/Custom/LockedTrainingChest.cs
//
// A chest used for lockpicking training.
//   - Requires 39.0 Lockpicking skill to pick
//   - Re-locks automatically 1 minute after being picked
//   - Fixed spawn via [add LockedTrainingChest
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Items
{
    public class LockedTrainingChest : LockableContainer
    {
        private bool _relockScheduled;

        [Constructable]
        public LockedTrainingChest() : base(0xE43)
        {
            Name     = "a locked training chest";
            Hue      = 0;
            Movable  = false;
            Weight   = 25.0;

            Lock();
        }

        public LockedTrainingChest(Serial serial) : base(serial) { }

        // ── Reset lock to initial state ───────────────────────────────
        private void Lock()
        {
            Locked        = true;
            LockLevel     = 39;   // requires ~39.0 Lockpicking
            MaxLockLevel  = 100;  // no upper-skill restriction — anyone >= 39 can pick it
            MinLockLevel  = 0;
        }

        // ── Detect when the chest is opened after being picked ────────
        public override void OnDoubleClick(Mobile from)
        {
            base.OnDoubleClick(from);

            // If unlocked (picked) and not already counting down, start relock timer
            if (!Locked && !_relockScheduled)
            {
                _relockScheduled = true;

                from.SendMessage(0x59, "The lock will reset in 1 minute.");

                Timer.DelayCall(TimeSpan.FromMinutes(1.0), () =>
                {
                    if (Deleted) return;

                    Lock();
                    _relockScheduled = false;
                });
            }
        }

        // ── Serialization ─────────────────────────────────────────────
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();

            // Re-lock on server restart — any mid-cooldown state is lost, that's fine
            if (!Locked)
                Lock();

            _relockScheduled = false;
        }
    }
}
