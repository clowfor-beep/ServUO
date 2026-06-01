using Server.Items;
using System;
using System.Collections.Generic;

namespace Server.Engines.Quests
{
    public class Jusae : MondainQuester
    {
        [Constructable]
        public Jusae()
            : base("Jusae", "the bowcrafter")
        {
            SetSkill(SkillName.Meditation, 60.0, 83.0);
            SetSkill(SkillName.Focus, 60.0, 83.0);
        }

        public Jusae(Serial serial)
            : base(serial)
        {
        }

        // 35% LethalDarts, remaining 65% split equally across the other 5 (13% each).
        // Uniform random selection requires a weighted array: 35 + 5*13 = 100 entries.
        private static readonly Type[] m_WeightedQuests = BuildWeightedQuests();

        private static Type[] BuildWeightedQuests()
        {
            var list = new List<Type>(100);
            for (int i = 0; i < 35; i++) list.Add(typeof(LethalDartsQuest));
            for (int i = 0; i < 13; i++) list.Add(typeof(SimpleBowQuest));
            for (int i = 0; i < 13; i++) list.Add(typeof(IngeniousArcheryPartOneQuest));
            for (int i = 0; i < 13; i++) list.Add(typeof(IngeniousArcheryPartTwoQuest));
            for (int i = 0; i < 13; i++) list.Add(typeof(IngeniousArcheryPartThreeQuest));
            for (int i = 0; i < 13; i++) list.Add(typeof(StopHarpingOnMeQuest));
            return list.ToArray();
        }

        public override Type[] Quests => m_WeightedQuests;
        public override void InitBody()
        {
            InitStats(100, 100, 25);

            Female = false;
            Race = Race.Elf;

            Hue = 0x83E5;
            HairItemID = 0x2FD0;
            HairHue = 0x238;
        }

        public override void InitOutfit()
        {
            SetWearable(new Sandals(), 0x901, 1);
            SetWearable(new ShortPants(), 0x651, 1);
            SetWearable(new MagicalShortbow(), dropChance: 1);
			SetWearable(new HideChest(), 0x27B, 1);
			SetWearable(new HidePauldrons(), 0x27E, 1); 
        }

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
    }
}