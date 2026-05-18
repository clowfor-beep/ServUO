// ============================================================
// PlayerKillerNPCs.cs
// Scripts/Custom/PlayerKillerNPCs.cs
//
// 21 PK NPC classes — 7 archetypes × 3 tiers
// Generated from PKNPCTemplates_AllArchetypes.txt
//
// Tier caps:
//   Newbie   — StatCap 150, SkillCap 450
//   Advanced — StatCap 180, SkillCap 600
//   Expert   — StatCap 225, SkillCap 700
//
// All classes use FightMode.Closest (encounter mode).
// Spawn via [add <ClassName>  or through GraveyardPKEncounter.
// ============================================================

using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Spells.Necromancy;

namespace Server.Custom
{
    // =========================================================
    // SHARED BASE — body setup, speech, persistence
    // =========================================================

    public abstract class BasePKNPC : BaseCreature
    {
        protected virtual string[] AggroLines => new string[0];
        protected virtual string[] KillLines  => new string[0];

        protected BasePKNPC(AIType ai, FightMode mode, int range)
            : base(ai, mode, range, 1, 0.1, 0.2)
        {
            SetupAppearance();
            Kills = 5; // murderer flag — red name
        }

        protected BasePKNPC(Serial serial) : base(serial) { }

        protected void SetupAppearance()
        {
            bool female = Utility.RandomBool();
            Body = female ? 0x191 : 0x190;
            Hue  = Utility.RandomSkinHue();
            Name = NameList.RandomName(female ? "female" : "male");
            HairItemID = female
                ? Utility.RandomList(0x203B, 0x203C, 0x2045, 0x204A)
                : Utility.RandomList(0x2044, 0x2045, 0x204A, 0x203C);
            HairHue = Utility.RandomHairHue();
        }

        // Called by GraveyardPKEncounter after MoveToWorld to set up hunt behaviour
        public void InitEncounter(Mobile target)
        {
            Timer.DelayCall(TimeSpan.FromMilliseconds(500), () =>
            {
                if (Deleted || target == null || !target.Alive)
                    return;

                if (AggroLines.Length > 0)
                    Say(AggroLines[Utility.Random(AggroLines.Length)]);

                Combatant = target;

                // Auto-delete after 5 min if the player escapes
                Timer.DelayCall(TimeSpan.FromMinutes(5.0), () =>
                {
                    if (!Deleted && Combatant == null && !Controlled)
                        Delete();
                });
            });
        }

        public override void OnThink()
        {
            // Cache combatant before base call — some AI paths can clear it
            Mobile target = Combatant;

            base.OnThink();

            if (Deleted || !Alive)
                return;

            // Restore combatant if base cleared it while we still have a valid target
            if (target != null && !target.Deleted && target.Alive && Combatant == null)
                Combatant = target;

            if (Combatant == null)
                return;

            // Out of melee range: drop warmode so the client shows running animation
            // Back into warmode the moment we're adjacent so attacks land normally
            if (!InRange(Combatant.Location, 1))
            {
                if (Warmode) Warmode = false;
            }
            else
            {
                if (!Warmode) Warmode = true;
            }
        }

        public override void OnGotMeleeAttack(Mobile attacker)
        {
            base.OnGotMeleeAttack(attacker);
            if (AggroLines.Length > 0 && Utility.RandomDouble() < 0.20)
                Say(AggroLines[Utility.Random(AggroLines.Length)]);
        }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);
            if (KillLines.Length > 0)
                Say(KillLines[Utility.Random(KillLines.Length)]);
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
        }
    }


    // =========================================================
    // ARCHETYPE 1 — CLASSIC DEXXER
    // =========================================================

    public class ClassicDexxerNewbie : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Taste steel!", "Nowhere to run!", "You shouldn't have come here.",
            "This'll be quick.", "Draw your weapon!"
        };
        protected override string[] KillLines => new[]
        {
            "Should've walked away.", "Too easy.", "Next!",
            "Stay down.", "*wipes blade* Pathetic."
        };

        [Constructable]
        public ClassicDexxerNewbie() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the ruffian";

            SetStr(72, 76);
            SetDex(46, 51);
            SetInt(20, 25);
            SetHits(72, 76);
            SetStam(46, 51);
            SetMana(0);

            SetSkill(SkillName.Swords,      76.0, 80.0);
            SetSkill(SkillName.Tactics,     72.0, 76.0);
            SetSkill(SkillName.Anatomy,     67.0, 71.0);
            SetSkill(SkillName.Healing,     72.0, 76.0);
            SetSkill(SkillName.Parry,       67.0, 71.0);
            SetSkill(SkillName.Hiding,      27.0, 31.0);
            SetSkill(SkillName.MagicResist, 47.0, 51.0);

            Fame         = 1500;
            Karma        = -1500;
            VirtualArmor = 20;

            AddItem(new Katana());

            var chest = new LeatherChest { Hue = Utility.RandomNeutralHue() };
            AddItem(chest);
            AddItem(new LeatherLegs());
            AddItem(new LeatherArms());
            AddItem(new LeatherGorget());
            AddItem(new WoodenShield());
            AddItem(new Boots(Utility.RandomNeutralHue()));
            if (Utility.RandomBool())
                AddItem(new Cloak(Utility.RandomNeutralHue()));
        }

        public ClassicDexxerNewbie(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Poor);
            PackGold(50, 200);
            PackItem(new Bandage(10));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class ClassicDexxerAdvanced : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Taste steel!", "You're mine now.", "I've killed better than you.",
            "Nowhere to run!", "Draw!"
        };
        protected override string[] KillLines => new[]
        {
            "Too easy.", "Another one down.", "Collect your gold — I dare you.",
            "Stay down.", "Barely worth the effort."
        };

        [Constructable]
        public ClassicDexxerAdvanced() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the brigand";

            SetStr(82, 88);
            SetDex(62, 68);
            SetInt(27, 33);
            SetHits(82, 88);
            SetStam(62, 68);
            SetMana(0);

            SetSkill(SkillName.Swords,      97.0, 100.0);
            SetSkill(SkillName.Tactics,     97.0, 100.0);
            SetSkill(SkillName.Anatomy,     87.0, 92.0);
            SetSkill(SkillName.Healing,     87.0, 92.0);
            SetSkill(SkillName.Parry,       97.0, 100.0);
            SetSkill(SkillName.Hiding,      57.0, 62.0);
            SetSkill(SkillName.MagicResist, 57.0, 62.0);

            Fame         = 5000;
            Karma        = -5000;
            VirtualArmor = 35;

            AddItem(new Katana());
            AddItem(new RingmailChest());
            AddItem(new RingmailLegs());
            AddItem(new LeatherArms());
            AddItem(new LeatherGorget());
            AddItem(new HeaterShield());
            AddItem(new Boots(Utility.RandomNeutralHue()));
            if (Utility.RandomBool())
                AddItem(new Cloak(Utility.RandomNeutralHue()));
        }

        public ClassicDexxerAdvanced(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Meager);
            PackGold(200, 600);
            PackItem(new Bandage(25));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class ClassicDexxerExpert : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Taste steel, cur!", "I am your death.", "You picked the wrong fight.",
            "On your knees!", "I haven't lost a fight in years."
        };
        protected override string[] KillLines => new[]
        {
            "As expected.", "I am unmatched.", "Your corpse is an insult to my blade.",
            "Pray I don't come back.", "Worthless."
        };

        [Constructable]
        public ClassicDexxerExpert() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the warlord";

            SetStr(97, 103);
            SetDex(77, 83);
            SetInt(42, 48);
            SetHits(97, 103);
            SetStam(77, 83);
            SetMana(42, 48);

            SetSkill(SkillName.Swords,      97.0, 100.0);
            SetSkill(SkillName.Tactics,     97.0, 100.0);
            SetSkill(SkillName.Anatomy,     97.0, 100.0);
            SetSkill(SkillName.Healing,     97.0, 100.0);
            SetSkill(SkillName.Parry,       97.0, 100.0);
            SetSkill(SkillName.Hiding,      67.0, 72.0);
            SetSkill(SkillName.MagicResist, 67.0, 72.0);
            SetSkill(SkillName.Chivalry,    57.0, 62.0);

            Fame         = 12500;
            Karma        = -12500;
            VirtualArmor = 50;

            AddItem(new Katana());

            var chest = new PlateChest { Hue = Utility.RandomNeutralHue() };
            AddItem(chest);
            AddItem(new PlateLegs());
            AddItem(new PlateArms());
            AddItem(new PlateGloves());
            AddItem(new PlateGorget());
            AddItem(new HeaterShield());
            AddItem(new Boots(Utility.RandomNeutralHue()));
            if (Utility.RandomBool())
                AddItem(new Cloak(Utility.RandomNeutralHue()));
        }

        public ClassicDexxerExpert(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
            PackGold(600, 2000);
            PackItem(new Bandage(Utility.RandomMinMax(10, 30)));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }


    // =========================================================
    // ARCHETYPE 2 — PURE MAGE
    // =========================================================

    public class PureMageNewbie : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "In Vas Flam!", "You cannot resist!", "Burn!",
            "Magic shall be your end.", "Feel my power!"
        };
        protected override string[] KillLines => new[]
        {
            "Your resistance was futile.", "Ash and cinders.", "Inadequate.",
            "Next victim.", "Magic always wins."
        };

        [Constructable]
        public PureMageNewbie() : base(AIType.AI_Mage, FightMode.Closest, 10)
        {
            Title = "the dark mage";

            SetStr(27, 33);
            SetDex(27, 33);
            SetInt(87, 93);
            SetHits(27, 33);
            SetStam(27, 33);
            SetMana(87, 93);

            SetSkill(SkillName.Magery,      77.0, 82.0);
            SetSkill(SkillName.EvalInt,     72.0, 77.0);
            SetSkill(SkillName.MagicResist, 67.0, 72.0);
            SetSkill(SkillName.Meditation,  72.0, 77.0);
            SetSkill(SkillName.Wrestling,   57.0, 62.0);
            SetSkill(SkillName.Hiding,      37.0, 42.0);
            SetSkill(SkillName.Tactics,     47.0, 52.0);

            Fame         = 2000;
            Karma        = -2000;
            VirtualArmor = 10;

            var robe = new Robe { Hue = Utility.RandomList(1175, 1109, 1102, 1153, 0x01) };
            AddItem(robe);
            AddItem(new Sandals());
            AddItem(new Spellbook());
        }

        public PureMageNewbie(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Meager);
            PackGold(50, 200);
            PackItem(new MandrakeRoot(15));
            PackItem(new BlackPearl(15));
            PackItem(new SulfurousAsh(15));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class PureMageAdvanced : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "In Vas Flam!", "Explosion incoming!", "You dare challenge a war mage?",
            "You cannot resist my power!", "I will reduce you to cinders!"
        };
        protected override string[] KillLines => new[]
        {
            "Predictable.", "Your spells are children's tricks.", "Dust to dust.",
            "The arcane favours the prepared.", "No contest."
        };

        [Constructable]
        public PureMageAdvanced() : base(AIType.AI_Mage, FightMode.Closest, 10)
        {
            Title = "the war mage";

            SetStr(32, 38);
            SetDex(32, 38);
            SetInt(107, 113);
            SetHits(32, 38);
            SetStam(32, 38);
            SetMana(107, 113);

            SetSkill(SkillName.Magery,      97.0, 100.0);
            SetSkill(SkillName.EvalInt,     97.0, 100.0);
            SetSkill(SkillName.MagicResist, 97.0, 100.0);
            SetSkill(SkillName.Meditation,  97.0, 100.0);
            SetSkill(SkillName.Wrestling,   77.0, 82.0);
            SetSkill(SkillName.Focus,       57.0, 62.0);
            SetSkill(SkillName.Hiding,      57.0, 62.0);

            Fame         = 5000;
            Karma        = -5000;
            VirtualArmor = 15;

            var robe = new Robe { Hue = Utility.RandomList(0x01, 1175, 1102) };
            AddItem(robe);
            AddItem(new Sandals());
            AddItem(new Spellbook());
        }

        public PureMageAdvanced(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Average);
            PackGold(200, 700);
            PackItem(new Spellbook());
            PackItem(new MandrakeRoot(25));
            PackItem(new BlackPearl(25));
            PackItem(new SulfurousAsh(25));
            PackItem(new Nightshade(25));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class PureMageExpert : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Vas Ort Flam!", "I have mastered every circle!", "You are nothing but a variable.",
            "Kneel before the arcane.", "Your death is already calculated."
        };
        protected override string[] KillLines => new[]
        {
            "Calculated.", "My formulae are never wrong.", "A footnote in my research.",
            "The void claims another.", "Expected outcome."
        };

        [Constructable]
        public PureMageExpert() : base(AIType.AI_Mage, FightMode.Closest, 12)
        {
            Title = "the archmage";

            SetStr(37, 43);
            SetDex(37, 43);
            SetInt(142, 148);
            SetHits(37, 43);
            SetStam(37, 43);
            SetMana(142, 148);

            SetSkill(SkillName.Magery,      97.0, 100.0);
            SetSkill(SkillName.EvalInt,     97.0, 100.0);
            SetSkill(SkillName.MagicResist, 97.0, 100.0);
            SetSkill(SkillName.Meditation,  97.0, 100.0);
            SetSkill(SkillName.Wrestling,   97.0, 100.0);
            SetSkill(SkillName.Focus,       97.0, 100.0);
            SetSkill(SkillName.Inscribe,    97.0, 100.0);

            Fame         = 12500;
            Karma        = -12500;
            VirtualArmor = 20;

            var robe = new Robe { Hue = 0x01 };
            AddItem(robe);
            AddItem(new WizardsHat());
            AddItem(new Sandals());
            AddItem(new Spellbook());
        }

        public PureMageExpert(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
            PackGold(700, 2000);
            PackItem(new Spellbook());
            PackItem(new MandrakeRoot(40));
            PackItem(new BlackPearl(40));
            PackItem(new SulfurousAsh(40));
            PackItem(new Nightshade(40));
            PackItem(new Garlic(40));
            PackItem(new Bloodmoss(40));
            PackItem(new Ginseng(40));
            PackItem(new SpidersSilk(40));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }


    // =========================================================
    // ARCHETYPE 3 — NECROMAGE
    // =========================================================

    public class NecroMageNewbie : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Your soul is mine.", "Kal Vas Xen Corp Ort!", "The dead hunger for you.",
            "Darkness claims all.", "I smell your fear."
        };
        protected override string[] KillLines => new[]
        {
            "Another spirit joins my collection.", "Rest — you'll be useful soon.",
            "Your suffering feeds me.", "Death is only the beginning.", "Delicious."
        };

        [Constructable]
        public NecroMageNewbie() : base(AIType.AI_Mage, FightMode.Closest, 10)
        {
            Title = "the blood mage";

            SetStr(27, 33);
            SetDex(27, 33);
            SetInt(87, 93);
            SetHits(27, 33);
            SetStam(27, 33);
            SetMana(87, 93);

            SetSkill(SkillName.Magery,      67.0, 72.0);
            SetSkill(SkillName.EvalInt,     57.0, 62.0);
            SetSkill(SkillName.MagicResist, 57.0, 62.0);
            SetSkill(SkillName.Necromancy,  77.0, 82.0);
            SetSkill(SkillName.SpiritSpeak, 67.0, 72.0);
            SetSkill(SkillName.Meditation,  57.0, 62.0);
            SetSkill(SkillName.Hiding,      47.0, 52.0);

            Fame         = 2000;
            Karma        = -2000;
            VirtualArmor = 10;

            var robe = new Robe { Hue = 0x01 };
            AddItem(robe);
            AddItem(new Sandals());
            AddItem(new Spellbook());
            AddItem(new NecromancerSpellbook());
        }

        public NecroMageNewbie(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Poor);
            PackGold(50, 200);
            PackItem(new BatWing(15));
            PackItem(new NoxCrystal(15));
            PackItem(new PigIron(15));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class NecroMageAdvanced : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Your soul is mine.", "Strangle!", "The dead walk for me!",
            "Wither before my power!", "Embrace the darkness."
        };
        protected override string[] KillLines => new[]
        {
            "Rise and serve me.", "Another pet for my collection.",
            "Your death is my gain.", "Perfect.", "Excellent material."
        };

        [Constructable]
        public NecroMageAdvanced() : base(AIType.AI_Mage, FightMode.Closest, 10)
        {
            Title = "the soul reaper";

            SetStr(32, 38);
            SetDex(32, 38);
            SetInt(107, 113);
            SetHits(32, 38);
            SetStam(32, 38);
            SetMana(107, 113);

            SetSkill(SkillName.Magery,      97.0, 100.0);
            SetSkill(SkillName.EvalInt,     87.0, 92.0);
            SetSkill(SkillName.MagicResist, 87.0, 92.0);
            SetSkill(SkillName.Necromancy,  97.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 97.0, 100.0);
            SetSkill(SkillName.Meditation,  57.0, 62.0);
            SetSkill(SkillName.Focus,       57.0, 62.0);

            Fame         = 6000;
            Karma        = -6000;
            VirtualArmor = 15;

            var chest = new BoneChest { Hue = 0x01 };
            AddItem(chest);
            AddItem(new BoneArms());
            AddItem(new Sandals());
            AddItem(new Spellbook());
            AddItem(new NecromancerSpellbook());
        }

        public NecroMageAdvanced(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Average);
            PackGold(300, 800);
            PackItem(new NecromancerSpellbook());
            PackItem(new BatWing(25));
            PackItem(new NoxCrystal(25));
            PackItem(new PigIron(25));
            PackItem(new GraveDust(25));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class NecroMageExpert : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Your soul is mine!", "I am the dread lord of this realm.",
            "Wither and die!", "The dead surround you.", "Kal Vas Xen Corp Ort!"
        };
        protected override string[] KillLines => new[]
        {
            "Rise and serve.", "Your agony is beautiful.",
            "Another corpse for my army.", "Inevitable.",
            "Death is merely your beginning."
        };

        [Constructable]
        public NecroMageExpert() : base(AIType.AI_Mage, FightMode.Closest, 12)
        {
            Title = "the dread lord";

            SetStr(47, 53);
            SetDex(42, 48);
            SetInt(127, 133);
            SetHits(47, 53);
            SetStam(42, 48);
            SetMana(127, 133);

            SetSkill(SkillName.Magery,      97.0, 100.0);
            SetSkill(SkillName.EvalInt,     97.0, 100.0);
            SetSkill(SkillName.MagicResist, 97.0, 100.0);
            SetSkill(SkillName.Necromancy,  97.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 97.0, 100.0);
            SetSkill(SkillName.Swords,      97.0, 100.0);
            SetSkill(SkillName.Tactics,     97.0, 100.0);

            Fame         = 15000;
            Karma        = -15000;
            VirtualArmor = 25;

            var chest = new BoneChest { Hue = 0x01 };
            AddItem(chest);
            AddItem(new BoneArms());
            AddItem(new BoneLegs());
            AddItem(new BoneGloves());
            AddItem(new BoneHelm());
            AddItem(new BoneHarvester());
            AddItem(new Sandals());
            AddItem(new Spellbook());
            AddItem(new NecromancerSpellbook());

            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterCurePotion());
            PackItem(new GreaterCurePotion());
        }

        public NecroMageExpert(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
            PackGold(800, 2500);
            PackItem(new NecromancerSpellbook());
            PackItem(new Spellbook());
            PackItem(new BatWing(40));
            PackItem(new NoxCrystal(40));
            PackItem(new PigIron(40));
            PackItem(new GraveDust(40));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }


    // =========================================================
    // ARCHETYPE 4 — NINJA DEXXER
    // =========================================================

    public class NinjaDexxerNewbie : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "You never saw me.", "Death Strike!", "From the shadows!",
            "You'll never land a hit.", "Silent as death."
        };
        protected override string[] KillLines => new[]
        {
            "You never had a chance.", "Shadows claim you.", "Too slow.",
            "I was never here.", "Ghost."
        };

        [Constructable]
        public NinjaDexxerNewbie() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the cutthroat";

            SetStr(47, 53);
            SetDex(67, 73);
            SetInt(27, 33);
            SetHits(47, 53);
            SetStam(67, 73);
            SetMana(27, 33);

            SetSkill(SkillName.Fencing,  72.0, 77.0);
            SetSkill(SkillName.Tactics,  67.0, 72.0);
            SetSkill(SkillName.Ninjitsu, 72.0, 77.0);
            SetSkill(SkillName.Hiding,   72.0, 77.0);
            SetSkill(SkillName.Stealth,  72.0, 77.0);
            SetSkill(SkillName.Anatomy,  37.0, 42.0);
            SetSkill(SkillName.Healing,  37.0, 42.0);

            Fame         = 1500;
            Karma        = -1500;
            VirtualArmor = 15;

            AddItem(new Kryss());
            AddItem(new Kasa());
            AddItem(new LeatherChest());
            AddItem(new LeatherLegs());
            AddItem(new NinjaTabi());
            AddItem(new Bandage(10));
        }

        public NinjaDexxerNewbie(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Poor);
            PackGold(50, 150);
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class NinjaDexxerAdvanced : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "You never saw me.", "Death Strike incoming!", "Mirrors deceive you.",
            "Count your heartbeats — they are numbered.", "From the shadow realm!"
        };
        protected override string[] KillLines => new[]
        {
            "Mirror Image wins again.", "Ghost.", "You fought a shadow.",
            "No honour. No mercy.", "Vanishing now."
        };

        [Constructable]
        public NinjaDexxerAdvanced() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the shadow blade";

            SetStr(52, 58);
            SetDex(82, 88);
            SetInt(37, 43);
            SetHits(52, 58);
            SetStam(82, 88);
            SetMana(37, 43);

            SetSkill(SkillName.Fencing,  97.0, 100.0);
            SetSkill(SkillName.Tactics,  97.0, 100.0);
            SetSkill(SkillName.Ninjitsu, 97.0, 100.0);
            SetSkill(SkillName.Hiding,   97.0, 100.0);
            SetSkill(SkillName.Stealth,  97.0, 100.0);
            SetSkill(SkillName.Anatomy,  57.0, 62.0);
            SetSkill(SkillName.Healing,  37.0, 42.0);

            Fame         = 5000;
            Karma        = -5000;
            VirtualArmor = 25;

            AddItem(new Kryss());

            var kasa = new Kasa { Hue = 0x01 };
            AddItem(kasa);
            var chest = new LeatherChest { Hue = 0x01 };
            AddItem(chest);
            var legs = new LeatherLegs { Hue = 0x01 };
            AddItem(legs);
            AddItem(new NinjaTabi());
            AddItem(new Bandage(20));
        }

        public NinjaDexxerAdvanced(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Meager);
            PackGold(200, 600);
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class NinjaDexxerExpert : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Phantom Lord has found you.", "You never stood a chance.",
            "Death Strike — the last thing you feel.", "I am the shadow itself.",
            "From the abyss I strike!"
        };
        protected override string[] KillLines => new[]
        {
            "Phantom victory.", "I was never here.", "The shadow always wins.",
            "Effortless.", "Exceptional."
        };

        [Constructable]
        public NinjaDexxerExpert() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the phantom lord";

            SetStr(57, 63);
            SetDex(107, 113);
            SetInt(52, 58);
            SetHits(57, 63);
            SetStam(107, 113);
            SetMana(52, 58);

            SetSkill(SkillName.Fencing,  97.0, 100.0);
            SetSkill(SkillName.Tactics,  97.0, 100.0);
            SetSkill(SkillName.Ninjitsu, 97.0, 100.0);
            SetSkill(SkillName.Hiding,   97.0, 100.0);
            SetSkill(SkillName.Stealth,  97.0, 100.0);
            SetSkill(SkillName.Anatomy,  97.0, 100.0);
            SetSkill(SkillName.Healing,  47.0, 52.0);
            SetSkill(SkillName.Focus,    47.0, 52.0);

            Fame         = 12500;
            Karma        = -12500;
            VirtualArmor = 35;

            AddItem(new Kryss());

            var kasa = new Kasa { Hue = 0x01 };
            AddItem(kasa);
            var chest = new LeatherChest { Hue = 0x01 };
            AddItem(chest);
            var legs = new LeatherLegs { Hue = 0x01 };
            AddItem(legs);
            AddItem(new NinjaTabi());
            AddItem(new Shuriken(20));
            AddItem(new Bandage(30));
        }

        public NinjaDexxerExpert(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
            PackGold(600, 1800);
            PackItem(new Shuriken(Utility.RandomMinMax(5, 15)));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }


    // =========================================================
    // ARCHETYPE 5 — PALADIN (Chivalry Dexxer)
    // =========================================================

    public class PaladinNewbie : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "For the darkness!", "Your light fades here.",
            "Divine Fury!", "Consecrate — then die.",
            "Fallen knights fear nothing."
        };
        protected override string[] KillLines => new[]
        {
            "The darkness prevails.", "Your faith was weak.",
            "Light snuffed out.", "Kneel.", "Justice? There is none here."
        };

        [Constructable]
        public PaladinNewbie() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the fallen knight";

            SetStr(67, 73);
            SetDex(47, 53);
            SetInt(27, 33);
            SetHits(67, 73);
            SetStam(47, 53);
            SetMana(27, 33);

            SetSkill(SkillName.Swords,   72.0, 77.0);
            SetSkill(SkillName.Tactics,  67.0, 72.0);
            SetSkill(SkillName.Chivalry, 77.0, 82.0);
            SetSkill(SkillName.Healing,  67.0, 72.0);
            SetSkill(SkillName.Anatomy,  67.0, 72.0);
            SetSkill(SkillName.Parry,    52.0, 57.0);
            SetSkill(SkillName.Hiding,   27.0, 32.0);

            Fame         = 1500;
            Karma        = -1500;
            VirtualArmor = 30;

            AddItem(new Broadsword());
            AddItem(new StuddedChest());
            AddItem(new LeatherArms());
            AddItem(new LeatherLegs());
            AddItem(new WoodenShield());
            AddItem(new Boots(Utility.RandomNeutralHue()));
            AddItem(new BookOfChivalry());
        }

        public PaladinNewbie(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Poor);
            PackGold(75, 250);
            PackItem(new BookOfChivalry());
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class PaladinAdvanced : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Enemy of One — that's you!", "For the darkness!",
            "Holy Light shall blind you!", "I consecrate your doom.",
            "Your light fades here!"
        };
        protected override string[] KillLines => new[]
        {
            "Light extinguished.", "Black knight prevails.",
            "Kneel before the fallen.", "Your faith failed you.",
            "The darkness endures."
        };

        [Constructable]
        public PaladinAdvanced() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the black knight";

            SetStr(77, 83);
            SetDex(57, 63);
            SetInt(37, 43);
            SetHits(77, 83);
            SetStam(57, 63);
            SetMana(37, 43);

            SetSkill(SkillName.Swords,   97.0, 100.0);
            SetSkill(SkillName.Tactics,  97.0, 100.0);
            SetSkill(SkillName.Chivalry, 97.0, 100.0);
            SetSkill(SkillName.Healing,  87.0, 92.0);
            SetSkill(SkillName.Anatomy,  87.0, 92.0);
            SetSkill(SkillName.Parry,    67.0, 72.0);
            SetSkill(SkillName.Focus,    47.0, 52.0);

            Fame         = 5000;
            Karma        = -5000;
            VirtualArmor = 40;

            AddItem(new Broadsword());
            AddItem(new RingmailChest());
            AddItem(new RingmailLegs());
            AddItem(new RingmailArms());
            AddItem(new RingmailGloves());
            AddItem(new HeaterShield());
            AddItem(new Boots(Utility.RandomNeutralHue()));
            AddItem(new BookOfChivalry());
            PackItem(new Bandage(30));
        }

        public PaladinAdvanced(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Meager);
            PackGold(250, 700);
            PackItem(new BookOfChivalry());
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class PaladinExpert : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Enemy of One — you are marked!", "For darkness eternal!",
            "I crusade against the living!", "Your light is an insult.",
            "Dark crusader never falls!"
        };
        protected override string[] KillLines => new[]
        {
            "Crusade complete.", "The darkness prevails always.",
            "Blood paladin claims another.", "Inevitable.",
            "Your god abandoned you."
        };

        [Constructable]
        public PaladinExpert() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the dark crusader";

            SetStr(92, 98);
            SetDex(72, 78);
            SetInt(52, 58);
            SetHits(92, 98);
            SetStam(72, 78);
            SetMana(52, 58);

            SetSkill(SkillName.Swords,   97.0, 100.0);
            SetSkill(SkillName.Tactics,  97.0, 100.0);
            SetSkill(SkillName.Chivalry, 97.0, 100.0);
            SetSkill(SkillName.Healing,  97.0, 100.0);
            SetSkill(SkillName.Anatomy,  97.0, 100.0);
            SetSkill(SkillName.Parry,    97.0, 100.0);
            SetSkill(SkillName.Focus,    97.0, 100.0);

            Fame         = 12500;
            Karma        = -12500;
            VirtualArmor = 55;

            AddItem(new Broadsword());

            var chest = new PlateChest { Hue = 0x01 };
            AddItem(chest);
            var legs = new PlateLegs { Hue = 0x01 };
            AddItem(legs);
            var arms = new PlateArms { Hue = 0x01 };
            AddItem(arms);
            var gloves = new PlateGloves { Hue = 0x01 };
            AddItem(gloves);
            var gorget = new PlateGorget { Hue = 0x01 };
            AddItem(gorget);
            AddItem(new HeaterShield());
            AddItem(new Boots(0x01));
            AddItem(new BookOfChivalry());

            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new Bandage(50));
        }

        public PaladinExpert(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
            PackGold(700, 2000);
            PackItem(new BookOfChivalry());
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }


    // =========================================================
    // ARCHETYPE 6 — ARCHER
    // =========================================================

    public class ArcherNewbie : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Bull's-eye.", "You should have stayed home.", "Draw!",
            "Nowhere to hide.", "My arrows find their mark."
        };
        protected override string[] KillLines => new[]
        {
            "Clean shot.", "Stay down.", "Too easy.",
            "Should have stayed home.", "One arrow, one kill."
        };

        [Constructable]
        public ArcherNewbie() : base(AIType.AI_Archer, FightMode.Closest, 10)
        {
            Title = "the road archer";

            SetStr(47, 53);
            SetDex(72, 78);
            SetInt(22, 28);
            SetHits(47, 53);
            SetStam(72, 78);
            SetMana(22, 28);

            SetSkill(SkillName.Archery,  77.0, 82.0);
            SetSkill(SkillName.Tactics,  72.0, 77.0);
            SetSkill(SkillName.Anatomy,  67.0, 72.0);
            SetSkill(SkillName.Healing,  72.0, 77.0);
            SetSkill(SkillName.Hiding,   72.0, 77.0);
            SetSkill(SkillName.Stealth,  72.0, 77.0);

            Fame         = 1500;
            Karma        = -1500;
            VirtualArmor = 15;

            AddItem(new Bow());
            AddItem(new Arrow(50));
            AddItem(new LeatherChest());
            AddItem(new LeatherArms());
            AddItem(new Boots(Utility.RandomNeutralHue()));
        }

        public ArcherNewbie(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Poor);
            PackGold(50, 150);
            PackItem(new Arrow(30));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class ArcherAdvanced : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Bull's-eye!", "You should've stayed home.", "Nowhere to hide from my arrows!",
            "Sniper's mark on you.", "You can't outrun this."
        };
        protected override string[] KillLines => new[]
        {
            "Textbook shot.", "Stay down.", "Didn't even break a sweat.",
            "Mark: confirmed.", "Clean."
        };

        [Constructable]
        public ArcherAdvanced() : base(AIType.AI_Archer, FightMode.Closest, 12)
        {
            Title = "the sniper";

            SetStr(57, 63);
            SetDex(87, 93);
            SetInt(27, 33);
            SetHits(57, 63);
            SetStam(87, 93);
            SetMana(27, 33);

            SetSkill(SkillName.Archery,     97.0, 100.0);
            SetSkill(SkillName.Tactics,     97.0, 100.0);
            SetSkill(SkillName.Anatomy,     87.0, 92.0);
            SetSkill(SkillName.Healing,     87.0, 92.0);
            SetSkill(SkillName.Hiding,      97.0, 100.0);
            SetSkill(SkillName.Stealth,     97.0, 100.0);
            SetSkill(SkillName.MagicResist, 17.0, 22.0);

            Fame         = 5000;
            Karma        = -5000;
            VirtualArmor = 25;

            AddItem(new CompositeBow());
            AddItem(new Arrow(100));
            AddItem(new StuddedChest());
            AddItem(new StuddedLegs());
            AddItem(new LeatherArms());
            AddItem(new Boots(Utility.RandomNeutralHue()));
            AddItem(new Cloak(0x01));
        }

        public ArcherAdvanced(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Meager);
            PackGold(200, 600);
            PackItem(new Arrow(50));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class ArcherExpert : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "Deadeye never misses.", "Bull's-eye — in the dark.",
            "You should have stayed home!", "Shadow archer found you.",
            "No escape from this range."
        };
        protected override string[] KillLines => new[]
        {
            "Deadeye confirmed.", "Shadows claim the slow.", "One. Two. Down.",
            "Too easy for a master.", "Mark eliminated."
        };

        [Constructable]
        public ArcherExpert() : base(AIType.AI_Archer, FightMode.Closest, 14)
        {
            Title = "the deadeye";

            SetStr(67, 73);
            SetDex(107, 113);
            SetInt(42, 48);
            SetHits(67, 73);
            SetStam(107, 113);
            SetMana(42, 48);

            SetSkill(SkillName.Archery,  97.0, 100.0);
            SetSkill(SkillName.Tactics,  97.0, 100.0);
            SetSkill(SkillName.Anatomy,  97.0, 100.0);
            SetSkill(SkillName.Healing,  97.0, 100.0);
            SetSkill(SkillName.Hiding,   97.0, 100.0);
            SetSkill(SkillName.Stealth,  97.0, 100.0);
            SetSkill(SkillName.Ninjitsu, 97.0, 100.0);

            Fame         = 12500;
            Karma        = -12500;
            VirtualArmor = 30;

            var kasa = new Kasa { Hue = 0x01 };
            AddItem(kasa);
            var chest = new LeatherChest { Hue = 0x01 };
            AddItem(chest);
            var legs = new LeatherLegs { Hue = 0x01 };
            AddItem(legs);
            AddItem(new NinjaTabi());
            AddItem(new CompositeBow());
            AddItem(new Arrow(150));
            AddItem(new Shuriken(20));
        }

        public ArcherExpert(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
            PackGold(600, 1800);
            PackItem(new Arrow(Utility.RandomMinMax(20, 60)));
            PackItem(new Shuriken(Utility.RandomMinMax(5, 15)));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }


    // =========================================================
    // ARCHETYPE 7 — SAMPIRE (Vampire Warrior)
    // =========================================================

    public class SampireNewbie : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "I feed on your pain.", "Your blood sustains me.",
            "Every hit heals me.", "You cannot outlast me.",
            "Vampiric Embrace!"
        };
        protected override string[] KillLines => new[]
        {
            "Your blood was... adequate.", "Sustenance acquired.",
            "I am renewed.", "Feed complete.", "Warm."
        };

        [Constructable]
        public SampireNewbie() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the blood warrior";

            SetStr(62, 68);
            SetDex(57, 63);
            SetInt(22, 28);
            SetHits(62, 68);
            SetStam(57, 63);
            SetMana(22, 28);

            SetSkill(SkillName.Swords,      77.0, 82.0);
            SetSkill(SkillName.Tactics,     72.0, 77.0);
            SetSkill(SkillName.Bushido,     77.0, 82.0);
            SetSkill(SkillName.Necromancy,  77.0, 82.0);
            SetSkill(SkillName.SpiritSpeak, 72.0, 77.0);
            SetSkill(SkillName.Parry,       57.0, 62.0);

            Fame         = 2000;
            Karma        = -2000;
            VirtualArmor = 20;

            AddItem(new Katana());
            AddItem(new LeatherChest());
            AddItem(new LeatherLegs());
            AddItem(new LeatherArms());
            AddItem(new Boots(Utility.RandomNeutralHue()));
            AddItem(new NecromancerSpellbook());

            // Cast Vampiric Embrace shortly after spawning
            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (!Deleted)
                    new VampiricEmbraceSpell(this, null).Cast();
            });
        }

        public SampireNewbie(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Poor);
            PackGold(75, 200);
            PackItem(new BatWing(10));
            PackItem(new PigIron(10));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class SampireAdvanced : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "I feed on your pain!", "Your blood sustains me!",
            "Lightning Strike!", "Every wound heals me stronger.",
            "Vampiric Embrace — feel it drain you."
        };
        protected override string[] KillLines => new[]
        {
            "Delicious.", "Your blood was exceptional.",
            "I am whole again.", "The vampire wins.",
            "You cannot kill what feeds on death."
        };

        [Constructable]
        public SampireAdvanced() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the blood knight";

            SetStr(72, 78);
            SetDex(72, 78);
            SetInt(27, 33);
            SetHits(72, 78);
            SetStam(72, 78);
            SetMana(27, 33);

            SetSkill(SkillName.Swords,      97.0, 100.0);
            SetSkill(SkillName.Tactics,     97.0, 100.0);
            SetSkill(SkillName.Bushido,     97.0, 100.0);
            SetSkill(SkillName.Necromancy,  97.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 97.0, 100.0);
            SetSkill(SkillName.Parry,       97.0, 100.0);

            Fame         = 7000;
            Karma        = -7000;
            VirtualArmor = 35;

            AddItem(new Katana());
            AddItem(new RingmailChest());
            AddItem(new StuddedLegs());
            AddItem(new LeatherArms());
            AddItem(new Boots(Utility.RandomNeutralHue()));
            AddItem(new NecromancerSpellbook());

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (!Deleted)
                    new VampiricEmbraceSpell(this, null).Cast();
            });
        }

        public SampireAdvanced(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Average);
            PackGold(300, 800);
            PackItem(new NecromancerSpellbook());
            PackItem(new BatWing(20));
            PackItem(new NoxCrystal(20));
            PackItem(new PigIron(20));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }

    public class SampireExpert : BasePKNPC
    {
        protected override string[] AggroLines => new[]
        {
            "I feed on your pain!", "Your blood sustains the vampire lord!",
            "Enemy of One — you are chosen.", "I am unkillable while I hit you.",
            "Lightning Strike + Consecrate — feel it!"
        };
        protected override string[] KillLines => new[]
        {
            "I am renewed completely.", "Your blood was magnificent.",
            "The vampire lord is satiated.", "Unkillable. Untouchable.",
            "Death feeds me. Death is me."
        };

        [Constructable]
        public SampireExpert() : base(AIType.AI_Melee, FightMode.Closest, 10)
        {
            Title = "the vampire lord";

            SetStr(87, 93);
            SetDex(82, 88);
            SetInt(47, 53);
            SetHits(87, 93);
            SetStam(82, 88);
            SetMana(47, 53);

            SetSkill(SkillName.Swords,      97.0, 100.0);
            SetSkill(SkillName.Tactics,     97.0, 100.0);
            SetSkill(SkillName.Bushido,     97.0, 100.0);
            SetSkill(SkillName.Necromancy,  97.0, 100.0);
            SetSkill(SkillName.SpiritSpeak, 97.0, 100.0);
            SetSkill(SkillName.Parry,       97.0, 100.0);
            SetSkill(SkillName.Chivalry,    97.0, 100.0);

            Fame         = 15000;
            Karma        = -15000;
            VirtualArmor = 45;

            AddItem(new Katana());

            var chest = new StuddedChest { Hue = 0x01 };
            AddItem(chest);
            var legs = new StuddedLegs { Hue = 0x01 };
            AddItem(legs);
            var arms = new StuddedArms { Hue = 0x01 };
            AddItem(arms);
            var gorget = new StuddedGorget { Hue = 0x01 };
            AddItem(gorget);
            AddItem(new Boots(0x01));
            AddItem(new NecromancerSpellbook());
            AddItem(new BookOfChivalry());

            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());
            PackItem(new GreaterHealPotion());

            Timer.DelayCall(TimeSpan.FromSeconds(1.5), () =>
            {
                if (!Deleted)
                    new VampiricEmbraceSpell(this, null).Cast();
            });
        }

        public SampireExpert(Serial serial) : base(serial) { }

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich);
            PackGold(800, 2500);
            PackItem(new NecromancerSpellbook());
            PackItem(new BookOfChivalry());
            PackItem(new BatWing(30));
            PackItem(new NoxCrystal(30));
            PackItem(new PigIron(30));
        }
        public override void Serialize(GenericWriter writer) { base.Serialize(writer); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); }

    }
}
