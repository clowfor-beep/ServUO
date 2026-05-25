# DONE — SimPlayer Phase 1 Full Roster

**Branch:** `claude2/simplayer-phase1-fullroster`  
**Completed:** 2026-05-25

## Files changed

| File | Change |
|------|--------|
| `Scripts/Custom/SimPlayer.cs` | Added `ApplyTemplate()` virtual method; replaced hardcoded Wanderer stats in constructor with `ApplyTemplate()` call; added `MakeSchedule()` helper; fixed `Deserialize` to restore correct schedule per guild |
| `Scripts/Custom/SimPlayerGuilds.cs` | **NEW** — 4 guild subclasses: `CraftsmensLeagueSimPlayer`, `IronCompanySimPlayer`, `ArcaneBrotherhoodSimPlayer`, `SilverWolvesSimPlayer` |
| `Scripts/Custom/ScheduleProfile.cs` | Added 4 schedule profiles: `CraftsmensLeague`, `IronCompany`, `ArcaneBrotherhood`, `SilverWolves` |
| `Scripts/Custom/SimChatBrain.cs` | Added 4 ambient line tables + updated `GetAmbientLines()` to cover all 5 guilds |
| `Scripts/Custom/PlayerSimulatorManager.cs` | Added 11 new members (3 Craftsmen, 3 Iron Company, 3 Arcane, 2 Silver Wolves) to `CreateRosterIfEmpty()` |

## Roster: 15 SimPlayers across 5 guilds

- **The Wanderers** (4): Erik the Wanderer, Mira of the Road, Old Thomas, Lena Farwalker
- **Craftsmen's League** (3): Garrett the Smith, Fisherman Pete, Woodcutter Bram
- **Iron Company** (3): Sergeant Vale, Brother Kael, Ironhide
- **Arcane Brotherhood** (3): Scholar Aldric, Mistress Verna, The Recluse
- **Silver Wolves** (2): Captain Rowena, Scout Finn
