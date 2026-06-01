# ============================================================
# LethalDartsQuest_v19.py — Razor Enhanced Python
# Heartwood bowcrafter quest runner: Lethal Darts only
#
# Requirements:
#   - 10+ crossbow bolts (graphic 0x1BFB) in backpack
#   - Standing near Cillitha or Jusae
# ============================================================

VENDOR_RANGE        = 10
RETRY_DELAY_MS      = 2000
GUMP_WAIT_MS        = 1500
MAX_LOOP_ITERATIONS = 200
DEBUG               = True

BTN_QUEST_ACCEPT     = 4
BTN_QUEST_REFUSE     = 2
BTN_QUEST_COMPLETE   = 8
BTN_QUEST_ACCEPT_RWD = 5
BTN_NEXT_PAGE        = 7

def dbg(msg):
    if DEBUG:
        Misc.SendMessage("[LethalDarts] " + str(msg), 0x3B2)

def find_vendor():
    mf = Mobiles.Filter()
    mf.Enabled  = True
    mf.RangeMax = VENDOR_RANGE
    mf.IsHuman  = 1
    for mob in Mobiles.ApplyFilter(mf):
        if mob.Name in ("Cillitha", "Jusae"):
            return mob.Serial
    return -1

def fresh_gump(timeout_ms=GUMP_WAIT_MS):
    # For UseMobile — server may take a moment; WaitForGump is reliable here
    Gumps.WaitForGump(0, timeout_ms)
    if Gumps.HasGump():
        return Gumps.CurrentGump()
    return 0

def nav_gump(pause_ms=500):
    # For responses to SendAction — server replies so fast WaitForGump misses it.
    # Pause first so gump is already there, then check HasGump.
    Misc.Pause(pause_ms)
    if Gumps.HasGump():
        return Gumps.CurrentGump()
    # Fallback if server was slow
    Gumps.WaitForGump(0, 1000)
    if Gumps.HasGump():
        return Gumps.CurrentGump()
    return 0

def find_bolt():
    backpack = Player.Backpack
    if backpack is None:
        return None
    for item in backpack.Contains:
        if item.ItemID == 0x1BFB:
            return item
    return None

def get_lethal_darts_quest(vendor_serial):
    for attempt in range(100):
        if Mobiles.FindBySerial(vendor_serial) is None:
            dbg("Vendor gone!")
            return False

        # ResetGump BEFORE the action so we wait for the gump it produces
        Gumps.ResetGump()
        Mobiles.UseMobile(vendor_serial)
        gump = fresh_gump(GUMP_WAIT_MS)

        if gump == 0:
            dbg("No gump (attempt " + str(attempt + 1) + ")")
            Misc.Pause(RETRY_DELAY_MS)
            continue

        dbg("Gump: " + str(Gumps.LastGumpGetLineList()))

        # Tier 1: title match (works if RE resolves clilocs)
        if Gumps.LastGumpTextExist("Lethal Darts"):
            dbg("Lethal Darts on description — accepting")
            Gumps.SendAction(gump, BTN_QUEST_ACCEPT)
            Misc.Pause(1500)
            return True

        # Tier 2: navigate to Objectives — SendAction FIRST (serial still valid),
        # THEN ResetGump so nav_gump waits for the fresh Objectives gump
        Gumps.SendAction(gump, BTN_NEXT_PAGE)
        Gumps.ResetGump()
        obj_gump = nav_gump(500)

        if obj_gump == 0:
            dbg("No objectives gump (attempt " + str(attempt + 1) + ")")
            Misc.Pause(RETRY_DELAY_MS)
            continue

        dbg("Objectives: " + str(Gumps.LastGumpGetLineList()))

        if Gumps.LastGumpTextExist("crossbow bolts"):
            dbg("Lethal Darts via objectives — accepting")
            Gumps.SendAction(obj_gump, BTN_QUEST_ACCEPT)
            Misc.Pause(1500)
            return True

        # Wrong quest — same pattern: SendAction first, then ResetGump
        dbg("Wrong quest (attempt " + str(attempt + 1) + ")")
        Gumps.SendAction(obj_gump, BTN_QUEST_REFUSE)
        Gumps.ResetGump()
        g2 = nav_gump(500)
        if g2 != 0:
            Gumps.SendAction(g2, 0)  # button 0 = Close, no side effects
        Misc.Pause(RETRY_DELAY_MS)

    dbg("Timed out.")
    return False

def tag_bolts_as_quest_items():
    bolt = find_bolt()
    if bolt is None:
        Misc.SendMessage("[LethalDarts] No bolts!", 33)
        return False

    context = Misc.WaitForContext(Player.Serial, 2000)
    if not context:
        dbg("No context menu")
        return False

    Misc.ContextReply(Player.Serial, "Toggle Quest Item")

    # True = hide cursor so focus-loss doesn't cancel the target
    if not Target.WaitForTarget(2000, True):
        dbg("No target cursor")
        return False

    Target.TargetExecute(bolt)
    Misc.Pause(300)

    # Server loops sending another cursor after each tag — cancel silently
    if Target.WaitForTarget(600, True):
        Target.Cancel()

    return True

def turn_in_quest(vendor_serial):
    if Mobiles.FindBySerial(vendor_serial) is None:
        dbg("Vendor gone!")
        return False

    Gumps.ResetGump()
    Mobiles.UseMobile(vendor_serial)
    gump = fresh_gump(GUMP_WAIT_MS)
    if gump == 0:
        dbg("No turn-in gump.")
        return False

    dbg("Turn-in: " + str(Gumps.LastGumpGetLineList()))
    Gumps.SendAction(gump, BTN_QUEST_COMPLETE)
    Gumps.ResetGump()
    gump2 = nav_gump(500)
    if gump2 != 0:
        Gumps.SendAction(gump2, BTN_QUEST_ACCEPT_RWD)
        Misc.Pause(200)
        dbg("Reward accepted!")
        return True

    dbg("No reward gump.")
    return False

def main():
    Misc.SendMessage("[LethalDarts] Started.", 0x35)

    for i in range(MAX_LOOP_ITERATIONS):
        vendor = find_vendor()
        if vendor == -1:
            Misc.SendMessage("[LethalDarts] No bowcrafter in range. Stopping.", 33)
            break

        if not get_lethal_darts_quest(vendor):
            Misc.SendMessage("[LethalDarts] Quest failed. Stopping.", 33)
            break

        if not tag_bolts_as_quest_items():
            Misc.SendMessage("[LethalDarts] Bolt tagging failed. Stopping.", 33)
            break

        if not turn_in_quest(vendor):
            Misc.SendMessage("[LethalDarts] Turn-in failed. Stopping.", 33)
            break

        dbg("Run " + str(i + 1) + " done!")

    Misc.SendMessage("[LethalDarts] Script finished.", 0x35)

main()
