// ============================================================
// LockedTrainingChestAdv.cs
// Scripts/Custom/LockedTrainingChestAdv.cs
//
// Advanced lockpicking training chest.
//   - Requires 60 Lockpicking to attempt
//   - 100% success chance at 85 Lockpicking
//   - Re-locks automatically 1 minute after being picked
//   - Spawn with [add LockedTrainingChestAdv
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Items
{
    public class LockedTrainingChestAdv : LockableContainer
    {
        private bool _relockScheduled;

        [Constructable]
        public LockedTrainingChestAdv() : base(0xE43)
        {
            Name     = "an advanced training chest";
            Hue      = 0x8A5; // blue tint to distinguish it
            Movable  = false;
            Weight   = 25.0;

            Lock();
        }

        public LockedTrainingChestAdv(Serial serial) : base(serial) { }

        private void Lock()
        {
            Locked        = true;
            LockLevel     = 60;  // minimum skill for success chance
            MaxLockLevel  = 85;  // 100% success at 85
            RequiredSkill = 60;  // hard gate — below this can't attempt
        }

        public override bool Locked
        {
            get => base.Locked;
            set
            {
                base.Locked = value;

                if (!value && !_relockScheduled)
                {
                    _relockScheduled = true;

                    foreach (Mobile m in GetMobilesInRange(3))
                        m.SendMessage(0x59, "The lock will reset in 1 minute.");

                    Timer.DelayCall(TimeSpan.FromMinutes(1.0), () =>
                    {
                        if (Deleted) return;
                        Lock();
                        _relockScheduled = false;
                    });
                }
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();

            if (!Locked)
                Lock();

            _relockScheduled = false;
        }
    }
}
