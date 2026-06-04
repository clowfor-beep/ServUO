# ============================================================
# AnimalTaming_v1.py — Razor Enhanced Python
# Trains Animal Taming to skill cap
#
# Requirements:
#   - Standing near tameable animals
#   - Bandages in backpack if HEAL_WITH = 'Bandage'
# ============================================================

# ── Config ───────────────────────────────────────────────────
RANGE_MAX         = 10     # search radius for animals
MAX_FOLLOWERS     = 1      # auto-release if followers exceed this
MAX_TAME_ATTEMPTS = 0      # 0 = unlimited per animal
RENAME_TO         = ''     # rename tamed pets to this ('' = skip)
HEAL_WITH         = 'None' # 'None', 'Bandage', or 'Magery'
FOLLOW_ANIMAL     = True   # follow moving animals
DEBUG             = True

BANDAGE_ID        = 0x0E21
BANDAGE_DELAY_MS  = 5000
RETRY_DELAY_MS    = 500    # brief pause between attempts
# ─────────────────────────────────────────────────────────────

IGNORE_NAMES = [RENAME_TO] if RENAME_TO else []

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
    for m in [m for m in mobs if m.Name in IGNORE_NAMES]:
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

def release_pet(mob):
    context = Misc.WaitForContext(mob.Serial, 2000)
    if not context:
        dbg("No context menu for release")
        return
    Misc.ContextReply(mob.Serial, 'Release')
    Gumps.ResetGump()
    Gumps.WaitForGump(0, 3000)
    if Gumps.HasGump():
        Gumps.SendAction(Gumps.CurrentGump(), 1)
    dbg("Released " + mob.Name)

def try_tame(mob):
    Target.ClearLastandQueue()
    Misc.Pause(100)
    Journal.Clear()
    Player.UseSkill('Animal Taming')
    Misc.Pause(600)
    # Check if skill is still on cooldown from a missed cursor on a previous attempt
    if Journal.SearchByType("You must wait", 'Regular'):
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
            dbg("Too far — ignoring " + mob.Name)
            Misc.IgnoreObject(mob)
            mob = None; tame_active = False; attempts = 0
            continue
        elif FOLLOW_ANIMAL and dist > 1 and not tame_active:
            follow(mob)

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

            if (Journal.SearchByType("It seems to accept you as master.", 'Regular') or
                    Journal.SearchByType("That wasn't even challenging.", 'Regular')):
                dbg("Tamed: " + mob.Name)
                if RENAME_TO:
                    Misc.PetRename(mob, RENAME_TO)
                if Player.Followers > MAX_FOLLOWERS:
                    release_pet(mob)
                Misc.IgnoreObject(mob)
                mob = None; tame_active = False; attempts = 0
                Journal.Clear()

            elif Journal.SearchByType("You fail to tame the creature.", 'Regular'):
                dbg("Failed — retrying")
                tame_active = False
                Journal.Clear()

            elif Journal.SearchByType("You must wait a few moments to use another skill.", 'Regular'):
                dbg("Skill delay — waiting")
                tame_active = False
                Journal.Clear()
                Misc.Pause(1500)

            elif (Journal.SearchByType("That is too far away.", 'Regular') or
                    Journal.SearchByType("You are too far away to continue taming.", 'Regular')):
                dbg("Too far — closing distance before retry")
                tame_active = False
                Journal.Clear()
                # Follow until within 1 tile before trying again
                if mob is not None:
                    for _ in range(10):
                        if Player.DistanceTo(mob) <= 1:
                            break
                        follow(mob)

            elif (Journal.SearchByType("You have no chance of taming this creature", 'Regular') or
                    Journal.SearchByType("That animal looks tame already.", 'Regular') or
                    Journal.SearchByType("Someone else is already taming this", 'Regular') or
                    Journal.SearchByType("This animal has had too many owners", 'Regular') or
                    Journal.SearchByType("Target cannot be seen", 'Regular')):
                dbg("Skipping " + mob.Name)
                Misc.IgnoreObject(mob)
                mob = None; tame_active = False; attempts = 0
                Journal.Clear()

        Misc.Pause(50)

    Misc.SendMessage("[Taming] Finished.", 0x35)

main()
