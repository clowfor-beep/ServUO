# ServUO Codebase Guide

## Language & Runtime

**C#** targeting **.NET Framework 4.7+**. All source files are `.cs`. Scripts are **auto-compiled at server startup** — there is no manual build step for gameplay scripts. The server compiles everything in `Scripts/` on launch and hot-patches changes on restart.

The `Server/` folder contains the core engine (compiled separately via `Server.csproj`). The `Application/` folder contains the entry point (`ServUO.cs`). Scripts in `Scripts/` are compiled as a second assembly on top of the engine.

---

## Project Structure

```
Scripts/          <- Gameplay logic. Auto-compiled at startup. Edit here for most changes.
  Custom/         <- Your custom additions go here
  Items/          <- Item definitions
  Mobiles/        <- NPC and player mobile classes
  Spells/         <- Spell implementations
  Skills/         <- Skill handler callbacks
  Gumps/          <- UI windows shown to players
  Quests/         <- Quest systems
  Commands/       <- Staff commands
  Regions/        <- Region definitions and effects

Server/           <- Core engine (networking, persistence, base classes)
Config/           <- Key=Value config files loaded at startup
Data/             <- Bulk order lists, spawn data, decoration files
Spawns/           <- Spawn definition files (.xml)
```

---

## Core Base Classes

| Class | Purpose |
|-------|---------|
| `Item` | Base for every item in the world |
| `Mobile` | Base for every living entity (players + NPCs) |
| `PlayerMobile` | The player character — extends `Mobile` |
| `BaseCreature` | NPCs and monsters — extends `Mobile` |
| `BaseVendor` | Shopkeeper NPC |
| `BaseAddon` | Multi-tile decorative objects |
| `BaseWeapon` / `BaseArmor` | Equipment |
| `Container` | Items that hold other items (bags, chests) |
| `Spell` / `MagerySpell` | Spells |

---

## Essential Patterns

### 1. Every persistent class needs two constructors

```csharp
// Called when spawning/creating the object in-game
[Constructable]
public MyItem() : base(0x1F14)  // 0x1F14 = item graphic ID
{
    Name   = "My Item";
    Weight = 1.0;
    Hue    = 1150;
}

// Called by the deserialization system when loading saves — never add logic here
public MyItem(Serial serial) : base(serial)
{
}
```

`[Constructable]` marks the constructor that staff can use with `[add MyItem` in-game.  
The `Serial` constructor **must exist** on every persistent class or the server will crash on load.

---

### 2. Serialize / Deserialize — save and load your fields

Every persistent class must override both methods. Always version them.

```csharp
public override void Serialize(GenericWriter writer)
{
    base.Serialize(writer);   // ALWAYS call base first

    writer.Write(1);          // version number — increment when you add fields

    writer.Write(_myString);
    writer.Write(_myInt);
    writer.Write(_myBool);
    writer.Write(_myMobile);  // writes a Mobile reference (Serial internally)
    writer.Write(_myItem);    // writes an Item reference
}

public override void Deserialize(GenericReader reader)
{
    base.Deserialize(reader); // ALWAYS call base first

    int version = reader.ReadInt();

    if (version >= 1)
    {
        _myString = reader.ReadString();
        _myInt    = reader.ReadInt();
        _myBool   = reader.ReadBool();
        _myMobile = reader.ReadMobile();
        _myItem   = reader.ReadItem() as MyOtherItem;
    }
}
```

**Version rule:** When you add new fields, bump the version number and wrap the new reads in `if (version >= N)`. Old saves skip the block; new saves read it. Never change the order of existing reads.

---

### 3. Custom systems — the Initialize() pattern

Any `public static void Initialize()` method in `Scripts/` is **automatically called by the server at startup**. Use it to hook events.

```csharp
namespace Server.Custom
{
    public static class MySystem
    {
        public static void Initialize()
        {
            EventSink.Login    += OnLogin;
            EventSink.Logout   += OnLogout;
            EventSink.Death    += OnDeath;
            EventSink.Movement += OnMovement;
        }

        private static void OnLogin(LoginEventArgs e)
        {
            PlayerMobile pm = e.Mobile as PlayerMobile;
            if (pm == null) return;
            pm.SendMessage("Welcome back!");
        }
    }
}
```

Common `EventSink` events:
- `Login`, `Logout`, `Death`, `Kill`
- `Movement` — fires on every player step
- `Speech` — fires when a mobile speaks
- `ItemCreated`, `ItemDeleted`, `MobileCreated`, `MobileDeleted`
- `PlayerDeath`, `Resurrection`

---

### 4. Timers — delayed or repeating actions

```csharp
// Fire once after 3 seconds
Timer.DelayCall(TimeSpan.FromSeconds(3.0), () =>
{
    mobile.SendMessage("3 seconds later!");
});

// Fire once after 5 seconds with a parameter
Timer.DelayCall(TimeSpan.FromSeconds(5.0), target =>
{
    target.SendMessage("Hello!");
}, mobile);

// Repeating timer — subclass Timer
public class MyTimer : Timer
{
    public MyTimer() : base(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0))
    {
        Priority = TimerPriority.OneSecond;
    }

    protected override void OnTick()
    {
        // runs every second
    }
}
// Start: new MyTimer().Start();
// Stop:  timer.Stop();
```

---

### 5. Expose properties to staff (props gump)

```csharp
[CommandProperty(AccessLevel.GameMaster)]
public int MyValue { get; set; }

[CommandProperty(AccessLevel.Administrator)]
public bool AdminOnly { get; set; }
```

Staff can then click the item/mobile and see/edit these fields via the `[props` command.

---

### 6. Checking access levels

```csharp
if (from.AccessLevel >= AccessLevel.GameMaster)
{
    // staff only
}

// Hierarchy (low to high):
// Player < Counselor < GameMaster < Seer < Administrator < Developer < Owner
```

---

### 7. Sending messages to players

```csharp
mobile.SendMessage("Plain white text");
mobile.SendMessage(0x35, "Green text");              // hue 0x35 = green
mobile.SendLocalizedMessage(1049596);                 // cliloc string ID
mobile.SendLocalizedMessage(1042001);                 // "That must be in your pack."

// Overhead text (floats above head)
mobile.PublicOverheadMessage(MessageType.Regular, 0x3B2, false, "Hello!");
mobile.PrivateOverheadMessage(MessageType.Regular, 0x22, false, "Only you see this", from.NetState);
```

---

### 8. Targeting

```csharp
// Ask the player to click a target
from.Target = new InternalTarget();

private class InternalTarget : Target
{
    public InternalTarget() : base(12, false, TargetFlags.None) { }
    // range=12, allowGround=false, flags=None

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (targeted is Mobile m)
            from.SendMessage($"You targeted {m.Name}");
        else if (targeted is Item item)
            from.SendMessage($"You targeted {item.Name}");
    }

    protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
    {
        from.SendMessage("Cancelled.");
    }
}
```

`TargetFlags.Harmful` — red cursor (hostile action).  
`TargetFlags.Beneficial` — green cursor (helpful action).

---

## Adding a New Item

Create a file in `Scripts/Custom/` (or the appropriate `Scripts/Items/` subfolder):

```csharp
using Server;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
    public class MagicRock : Item
    {
        private int _charges;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Charges
        {
            get => _charges;
            set { _charges = value; InvalidateProperties(); }
        }

        [Constructable]
        public MagicRock() : base(0x1363)
        {
            Name     = "a magic rock";
            Hue      = 1153;
            Weight   = 1.0;
            _charges = 5;
        }

        public MagicRock(Serial serial) : base(serial) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack.
                return;
            }

            if (_charges <= 0)
            {
                from.SendMessage("The rock has no charges left.");
                return;
            }

            from.SendMessage("The rock glows!");
            from.FixedParticles(0x375A, 9, 20, 5016, EffectLayer.Waist);
            from.PlaySound(0x1F7);
            _charges--;
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Charges: {_charges}");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0); // version
            writer.Write(_charges);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();
            _charges = reader.ReadInt();
        }
    }
}
```

**Spawn it in-game:** `[add MagicRock` (staff command)

---

## Adding a New NPC / Creature

```csharp
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Mobiles
{
    public class ForestGuardian : BaseCreature
    {
        [Constructable]
        public ForestGuardian()
            : base(AIType.AI_Melee, FightMode.Aggressor, 10, 1, 0.2, 0.4)
        {
            Name  = "a forest guardian";
            Body  = 0x190;
            Hue   = 0x83F;
            Title = "the ancient";

            SetStr(200, 250);
            SetDex(80, 100);
            SetInt(50, 75);

            SetHits(150, 200);

            SetSkill(SkillName.Swords,  85.0, 95.0);
            SetSkill(SkillName.Tactics, 80.0, 90.0);
            SetSkill(SkillName.MagicResist, 70.0, 80.0);

            Fame  = 4000;
            Karma = 4000;

            VirtualArmor = 40;

            PackItem(new Gold(Utility.RandomMinMax(100, 200)));
        }

        public ForestGuardian(Serial serial) : base(serial) { }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);
            // Add loot or special death effects here
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
```

**AI types:** `AI_Melee`, `AI_Archer`, `AI_Mage`, `AI_Animal`, `AI_Healer`, `AI_Vendor`  
**FightMode:** `Aggressor` (retaliates), `Closest` (hunts nearest), `Weakest`, `Strongest`, `None`

---

## Adding a Staff Command

```csharp
using Server;
using Server.Commands;
using Server.Mobiles;

namespace Server.Commands
{
    public static class MyCommand
    {
        public static void Initialize()
        {
            CommandSystem.Register("mycommand", AccessLevel.GameMaster, OnCommand);
        }

        private static void OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            from.SendMessage("Command executed!");
        }
    }
}
```

Use with `[mycommand` in-game.

---

## Config Files

Files in `Config/` use `Key=Value` syntax. Read them in code via:

```csharp
// In Config/MySystem.cfg:
//   Enabled=True
//   MaxCount=10

public static class MyConfig
{
    public static bool Enabled = true;
    public static int  MaxCount = 10;

    public static void Configure()  // called automatically by the engine if named Configure()
    {
        var config = Config.Load("MySystem.cfg");
        Enabled  = config.GetBool("Enabled", true);
        MaxCount = config.GetInt("MaxCount", 10);
    }
}
```

---

## Common Utility Methods

```csharp
Utility.RandomMinMax(1, 10)        // random int between 1 and 10
Utility.RandomDouble()             // random double 0.0 – 1.0
Utility.RandomBool()               // true or false
Utility.RandomList(a, b, c)        // pick one from a list
Utility.RandomSkinHue()            // valid skin hue
Utility.RandomHairHue()            // valid hair hue

mobile.InRange(point, range)       // bool — is mobile within range tiles?
mobile.GetDistanceToSqrt(target)   // distance between two mobiles
mobile.MoveToWorld(point, map)     // teleport mobile
mobile.Kill()                      // kills a mobile
mobile.Delete()                    // permanently removes from world

item.MoveToWorld(point, map)       // place item in world
item.Delete()                      // permanently removes from world
item.IsChildOf(container)          // bool — is item inside a container?

Map.Felucca / Map.Trammel / Map.Ilshenar / Map.Malas / Map.Tokuno / Map.TerMur
```

---

## Namespace Conventions

| Namespace | Contents |
|-----------|----------|
| `Server` | Core engine types (Item, Mobile, Map, Timer, etc.) |
| `Server.Items` | All item classes |
| `Server.Mobiles` | PlayerMobile, BaseCreature, AI classes |
| `Server.Spells` | Spell base classes and implementations |
| `Server.SkillHandlers` | Skill callback implementations |
| `Server.Gumps` | UI gump classes |
| `Server.Commands` | Staff command registrations |
| `Server.Engines.*` | Crafting, quests, city loyalty, VvV, etc. |
| `Server.Custom` | Your custom systems (use this for new work) |
| `Server.Network` | Packet/netstate handling |

---

## Workflow: Making a Change

1. **Edit or create** a `.cs` file in `Scripts/Custom/` (for new systems) or the relevant `Scripts/` subfolder.
2. **Restart the server** — scripts recompile automatically on startup.
3. **Test in-game** using staff commands (`[add`, `[props`, `[go`, `[where`, `[tele`).
4. If your class is persistent (saved to world), ensure both `Serialize` and `Deserialize` are correct before saving the world (`[worldsave`).
5. For config-only changes, edit the relevant file in `Config/` and restart.

> **Never remove or reorder existing `reader.Read*()` calls** in `Deserialize` — this corrupts saves. Only add new reads inside `if (version >= N)` blocks.

---

## Server Infrastructure (Operations)

**Host**: Ubuntu VPS at `178.105.173.80`  
**Prod container**: `servuo` — external port 2593, shard name "Fun Stuff"  
**Test container**: `servuo-test` — external port 2594, shard name "Fun Stuff test"  
**Git repo on server**: `/home/servuo/` (also the prod server files)  
**Test server files**: `/home/servuo-test/`  
**UO game data**: `/home/servuo/uodata/`  
**Server timezone**: UTC (Jacob is CEST = UTC+2)

### Deploy Commands (run on server, not PC)
```bash
# Test
cd /home/servuo && git pull --no-edit && bash /home/servuo/deploy-test.sh

# Prod
cd /home/servuo && bash /home/servuo/deploy.sh
```

### Essential Check Commands
```bash
# Is prod up?
docker exec servuo bash -c "cat /proc/net/tcp | grep -q '00000000:0A21' && echo 'UP' || echo 'DOWN'"

# Is test up?
docker exec servuo-test bash -c "cat /proc/net/tcp | grep -q '00000000:0A22' && echo 'UP' || echo 'DOWN'"

# Wait for prod to finish loading
until docker exec servuo bash -c "cat /proc/net/tcp | grep -q '00000000:0A21'"; do echo "$(date +%H:%M:%S) — loading..."; sleep 5; done && echo "UP"

# Watchdog log
cat /home/servuo/watchdog.log | tail -10

# Server log
docker exec servuo bash -c "strings /home/servuo/servuo.log | tail -20"
```

### Key Rules
- **Always deploy test before prod** — never touch prod without testing first
- **Screen is broken in the test container** — test server is started via `docker exec -d`, never restart.sh
- **Check server up** with `/proc/net/tcp` — ss and netstat are not available in containers
- **Give commands separately** labelled "On PC:" and "On server:" — never mix them
- **Env configs**: `Config/env/prod/` and `Config/env/test/` — deploy scripts apply these automatically, never manually patch DataPath or Server.cfg
- **Watchdog** runs every 5 min via cron — monitors prod only, auto-restarts if hung
