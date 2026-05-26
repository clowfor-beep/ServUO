# DONE — SimPlayer Phase 2: Banking + Shadow Hand
**Completed:** 2026-05-26

## Files Modified

### Scripts/Custom/SimStateMachine.cs
- Added `Banking` state between `Travelling` and `Dead`

### Scripts/Custom/SimPlayer.cs
- Added `_homeLocation` → promoted to `protected` for ShadowHandSimPlayer.FleeFrom()
- Added banking fields: `_inBankingCycle`, `_nextBankingTime`, `_bankUntil`, `_travelIsBankTrip`
- Added `StartBankingTrip()` — travels to BountyBoard_Britain_Bank ±4 tiles
- Added `TickBanking()` — calls TryBankSpeech, exits after 3–8 minutes
- Modified `ArriveAtDest()` — switches to Banking state when `_travelIsBankTrip`
- Modified `TickIdle()` — checks `_nextBankingTime`, calls `OnTickIdle()` virtual hook
- Added `protected virtual OnTickIdle()` — empty base, overridden by ShadowHandSimPlayer
- Added `protected StartTravelTo(dest, timeout)` — for subclass flee/travel overrides
- Added `ShadowHand` → `ScheduleProfile.ShadowHand` in `MakeSchedule()`
- Bumped Serialize version 0 → 1, added `_nextBankingTime` read/write
- Deserialize now handles Banking state (resets to Idle on load)

### Scripts/Custom/SimChatBrain.cs
- Added `ShadowHandAmbient` (5 innocent-sounding lines)
- Added `BankAmbient` (15 lines — WTS/LFG/bank crowd chatter)
- Added `TryBankSpeech()` — lower frequency than ambient (20% chance per window)
- Updated `GetAmbientLines()` with ShadowHand case

### Scripts/Custom/ScheduleProfile.cs
- Added `ShadowHand()` factory — peak hours 10:00–22:00 (80%), morning 50%, night 10/30%

### Scripts/Custom/SimPlayerGuilds.cs
- Added `ShadowHandSimPlayer` class: Karma -500, thief skills, dark robe+boots (hue 1109)
- `OnTickIdle()`: hides every 20–60s, flees Silver Wolves within 12 tiles
- `FleeFrom()`: moves opposite to threat direction, goes hidden on flee

### Scripts/Custom/PlayerSimulatorManager.cs
- Added 3 Shadow Hand members: Fingers Malory, The Whisper, Slick Fen
- Roster count updated to 18 SimPlayers across 6 guilds

## Commit
`feat: SimPlayer Phase 2 — banking behavior + Shadow Hand guild (3 members)`
