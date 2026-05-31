using Server.Custom;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
    [Flipable(0xE81, 0xE82)]
    public class ShepherdsCrook : BaseStaff
    {
        [Constructable]
        public ShepherdsCrook()
            : base(0xE81)
        {
            Weight = 4.0;
        }

        public ShepherdsCrook(Serial serial)
            : base(serial)
        {
        }

        public override WeaponAbility PrimaryAbility => WeaponAbility.CrushingBlow;
        public override WeaponAbility SecondaryAbility => WeaponAbility.Disarm;
        public override int StrengthReq => 20;
        public override int MinDamage => 13;
        public override int MaxDamage => 16;
        public override float Speed => 2.75f;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 50;

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
        }

        // Equipping auto-activates the crook for this player
        public override bool OnEquip(Mobile from)
        {
            if (from is PlayerMobile pm)
                SkillSynergies.ActivateCrook(pm, this);

            return base.OnEquip(from);
        }

        // Double-click activates the crook (must be equipped or in backpack)
        public override void OnDoubleClick(Mobile from)
        {
            if (!(from is PlayerMobile pm))
                return;

            bool inPack     = IsChildOf(pm.Backpack);
            bool isEquipped = pm.FindItemOnLayer(Layer.TwoHanded) == this;

            if (!inPack && !isEquipped)
            {
                pm.SendMessage("The crook must be equipped or in your backpack to activate it.");
                return;
            }

            bool wasNew = SkillSynergies.ActivateCrook(pm, this);

            if (wasNew)
                pm.SendMessage(0x35, "You activate the shepherd's crook. Your followers will benefit from your Herding skill.");
            else
                pm.SendMessage(0x59, "This shepherd's crook is already your active crook.");
        }

        // Show activation status in item tooltip
        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);

            if (RootParent is PlayerMobile pm && SkillSynergies.IsActivated(pm, this))
                list.Add("Active Shepherd's Crook");
        }
    }
}
