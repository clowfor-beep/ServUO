# ============================================================
# AnimalTaming_v1.py — Razor Enhanced Python
# Trains Animal Taming to skill cap
#
# Requirements:
#   - Standing near tameable animals
#   - Bandages in backpack if HEAL_WITH = 'Bandage'
# ============================================================

# ── Config ───────────────────────────────────────────────────
RANGE_MAX              = 10     # search radius for animals
RELEASE_AFTER_TAME     = True   # release every pet right after taming (best for training)
MAX_FOLLOWERS          = 0      # only used when RELEASE_AFTER_TAME = False: keep this many, release the rest
MAX_TAME_ATTEMPTS      = 0      # 0 = unlimited attempts per animal
RENAME_TO              = ''     # rename tamed pets to this ('' = skip)
HEAL_WITH              = 'None' # 'None', 'Bandage', or 'Magery'
FOLLOW_ANIMAL          = True   # follow moving animals
DEBUG                  = True

BANDAGE_ID             = 0x0E21
BANDAGE_DELAY_MS       = 5000
RETRY_DELAY_MS         = 500    # brief pause between attempts

# Mob names (lowercase substrings) to never attempt taming — add undead/monsters here
SKIP_NAMES = [
    'skeleton', 'zombie', 'shade', 'spectre', 'wraith', 'lich',
    'ghoul', 'mummy', 'vampire', 'revenant', 'bone',
]
# ─────────────────────────────────────────────────────────────

IGNORE_NAMES  = [RENAME_TO] if RENAME_TO else []
_my_pet_serials = []  # tracks serials of mobs we've tamed but not yet released

def dbg(msg):
    if DEBUG:
        Misc.SendMessage("[Taming] " + str(msg), 0x3B2)

def find_animal():
    mf = Mobiles.Filter()
    mf.Enabled           = True
    mf.RangeMax          = RANGE_MAX
    mf.IsHuman           = 0
    mf.IsGhost           = 0
    mf.CheckIgnoreObject = True
    mobs = Mobiles.ApplyFilter(mf)

    # Remove own pets, ignored names, and untameable mob types
    to_remove = [m for m in mobs if
                 m.Serial in _my_pet_serials or
                 m.Name in IGNORE_NAMES or
                 any(skip in m.Name.lower() for skip in SKIP_NAMES)]
    for m in to_remove:
        mobs.Remove(m)

    if mobs.Count == 0:
        return None
    if mobs.Count == 1:
        return mobs[0]
    return Mobiles.Select(mobs, 'Nearest')

def find_bandage():
    backpack = Player.Backpack
    if backpack is None:
        return None
    for item in backpack.Contains:
        if item.ItemID == BANDAGE_ID:
            return item
    return None

def heal():
    if Player.Hits >= Player.HitsMax:
        return
    if HEAL_WITH == 'Bandage':
        b = find_bandage()
        if b is None:
            dbg("No bandages!")
            return
        Target.ClearLastandQueue()
        Misc.Pause(100)
        Items.UseItem(b)
        if Target.WaitForTarget(2000, True):
            Target.Self()
    elif HEAL_WITH == 'Magery':
        if (Player.HitsMax - Player.Hits) > 30:
            Spells.CastMagery('Greater Heal')
        else:
            Spells.CastMagery('Heal')

def follow(mob):
    if Player.DistanceTo(mob) <= 2:
        return
    px, py = Player.Position.X, Player.Position.Y
    mx, my = mob.Position.X, mob.Position.Y
    dx, dy = mx - px, my - py
    if   dx > 0 and dy > 0: d = 'Down'
    elif dx < 0 and dy > 0: d = 'Left'
    elif dx > 0 and dy < 0: d = 'Right'
    elif dx < 0 and dy < 0: d = 'Up'
    elif dx > 0:             d = 'East'
    elif dx < 0:             d = 'West'
    elif dy > 0:             d = 'South'
    else:                    d = 'North'
    Player.Walk(d)
    Misc.Pause(150)

RELEASE_GUMP_ID = 0x5d4d1678  # ConfirmReleaseGump TypeID on this shard

def release_pet(mob):
    before = Player.Followers
    Misc.WaitForContext(mob.Serial, 2000)
    Misc.ContextReply(mob.Serial, 9)                  # index 9 = Release
    Gumps.WaitForGump(RELEASE_GUMP_ID, 5000)
    Gumps.SendAction(RELEASE_GUMP_ID, 2)              # button 2 = CONTINUE
    Misc.Pause(800)
    if Player.Followers < before:
        dbg("Released " + mob.Name)
        return True
    dbg("Release failed for " + mob.Name)
    return False

def release_all_followers():
    """Release all tracked pets."""
    global _my_pet_serials
    if not _my_pet_serials:
        return True
    released = []
    for serial in list(_my_pet_serials):
        m = Mobiles.FindBySerial(serial)
        if m is None:
            released.append(serial)
        elif release_pet(m):
            released.append(serial)
    _my_pet_serials[:] = [s for s in _my_pet_serials if s not in released]
    return len(_my_pet_serials) == 0

def try_tame(mob):
    Target.ClearLastandQueue()
    Misc.Pause(100)
    Journal.Clear()
    Player.UseSkill('Animal Taming')
    Misc.Pause(600)
    # Check if skill is still on cooldown from a missed cursor on a previous attempt
    if Journal.Search("You must wait"):
        dbg("Skill on cooldown — waiting...")
        Journal.Clear()
        Misc.Pause(3000)
        return False
    if not Target.HasTarget():
        Target.WaitForTarget(2000, True)
    if Target.HasTarget():
        Target.TargetExecute(mob)
        return True
    dbg("No target cursor")
    return False

def main():
    Misc.SendMessage("[Taming] Started.", 0x35)
    Journal.Clear()
    Misc.ClearIgnore()
    Player.SetWarMode(False)

    if HEAL_WITH == 'Bandage':
        Timer.Create('bandage_timer', 1)

    mob         = None
    tame_active = False
    attempts    = 0

    while not Player.IsGhost:
        val = Player.GetRealSkillValue('Animal Taming')
        cap = Player.GetSkillCap('Animal Taming')
        if val >= cap:
            Misc.SendMessage("[Taming] Skill capped! Done.", 0x35)
            break

        # ── Heal ───────────────────────────────────────────────
        if HEAL_WITH != 'None' and not Timer.Check('bandage_timer'):
            heal()
            Timer.Create('bandage_timer', BANDAGE_DELAY_MS)

        # ── Validate mob ───────────────────────────────────────
        if mob is not None and Mobiles.FindBySerial(mob.Serial) is None:
            dbg("Animal gone — finding new one")
            mob = None; tame_active = False; attempts = 0

        # ── Max attempts ───────────────────────────────────────
        if mob is not None and MAX_TAME_ATTEMPTS > 0 and attempts >= MAX_TAME_ATTEMPTS:
            dbg("Max attempts — skipping " + mob.Name)
            Misc.IgnoreObject(mob)
            mob = None; tame_active = False; attempts = 0

        # ── Find animal ────────────────────────────────────────
        if mob is None:
            tame_active = False
            mob = find_animal()
            if mob is None:
                dbg("No animals nearby — waiting...")
                Misc.Pause(3000)
                continue
            dbg("Target: " + mob.Name)

        # ── Follow ─────────────────────────────────────────────
        dist = Player.DistanceTo(mob)
        if dist > 15:
            dbg("Too far — dropping " + mob.Name + " (not ignoring, may retarget)")
            mob = None; tame_active = False; attempts = 0
            continue
        elif FOLLOW_ANIMAL and dist > 1 and not tame_active:
            follow(mob)

        # ── Pre-tame: retry releasing any pets that failed to release earlier
        if not tame_active and _my_pet_serials:
            dbg("Retrying release on " + str(len(_my_pet_serials)) + " tracked pet(s)")
            release_all_followers()
            Misc.Pause(500)

        # ── Start tame ─────────────────────────────────────────
        if not tame_active:
            fired = try_tame(mob)
            if fired:
                tame_active = True
                attempts   += 1
                dbg("Attempt " + str(attempts) + " on " + mob.Name +
                    " (skill: " + str(round(val, 1)) + ")")
            else:
                Misc.Pause(RETRY_DELAY_MS)

        # ── Journal ────────────────────────────────────────────
        if tame_active:
            Misc.Pause(300)

            if (Journal.Search("It seems to accept you as master.") or
                    Journal.Search("That wasn't even challenging.")):
                dbg("Tamed: " + mob.Name)
                Player.ChatSay('all guard me')
                Misc.Pause(300)
                if RENAME_TO:
                    Misc.PetRename(mob, RENAME_TO)
                # Always track the serial so release_all_followers can find it if release fails
                if mob.Serial not in _my_pet_serials:
                    _my_pet_serials.append(mob.Serial)
                # Always release when training; only remove from tracking if it actually worked
                if RELEASE_AFTER_TAME or Player.Followers > MAX_FOLLOWERS:
                    if release_pet(mob):
                        _my_pet_serials[:] = [s for s in _my_pet_serials if s != mob.Serial]
                Misc.IgnoreObject(mob)
                mob = None; tame_active = False; attempts = 0
                Journal.Clear()

            elif Journal.Search("You fail to tame the creature."):
                dbg("Failed — retrying")
                tame_active = False
                Journal.Clear()

            elif Journal.Search("You must wait a few moments to use another skill."):
                dbg("Skill delay — waiting")
                tame_active = False
                Journal.Clear()
                Misc.Pause(1500)

            elif (Journal.Search("That is too far away.") or
                    Journal.Search("You are too far away to continue taming.")):
                dbg("Too far — closing distance before retry")
                tame_active = False
                Journal.Clear()
                if mob is not None:
                    for _ in range(20):
                        if Player.DistanceTo(mob) <= 2:
                            break
                        follow(mob)

            elif Journal.Search("You have too many followers"):
                dbg("Too many followers — releasing all and retrying")
                tame_active = False
                Journal.Clear()
                mob = None; attempts = 0
                release_all_followers()

            elif (Journal.Search("You have no chance of taming this creature") or
                    Journal.Search("That animal looks tame already.") or
                    Journal.Search("Someone else is already taming this") or
                    Journal.Search("This animal has had too many owners") or
                    Journal.Search("Target cannot be seen")):
                dbg("Skipping " + mob.Name)
                Misc.IgnoreObject(mob)
                mob = None; tame_active = False; attempts = 0
                Journal.Clear()

        Misc.Pause(50)

    Misc.SendMessage("[Taming] Finished.", 0x35)

main()
