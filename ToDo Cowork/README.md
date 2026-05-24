# ToDo Cowork — Work Instructions

This folder contains work instructions issued by **Master Claude** to **Claude 2**.

---

## If you are Claude 2 and just opened this project

**Do this before anything else:**

1. Read `Design/COWORK_HANDOVER.md` — full project context, implementation status, conventions
2. Read `Design/SystemArchitecture_DesignDoc.txt` — architecture and build order
3. Read the specific work instruction file assigned to you (named `Claude2 - <topic> - <date>.md`)
4. Read the design doc(s) listed in the work instruction
5. Read any existing `.cs` files listed in the work instruction
6. Then and only then — start writing code

---

## File naming convention

```
Claude2 - <topic> - <YYYY-MM-DD>.md
```

Examples:
```
Claude2 - FBZones and FBEventBus - 2026-05-24.md
Claude2 - SimPlayer Base Class - 2026-05-25.md
Claude2 - ReputationSystem - 2026-05-26.md
```

---

## Work instruction format

Every instruction file must contain these sections:

```markdown
# Claude2 Work Instruction — <topic>
Date: YYYY-MM-DD
Branch: claude2/<topic-kebab>

## Context files to read (in this order)
1. Design/COWORK_HANDOVER.md                  <- always first
2. Design/<relevant design doc>.txt           <- spec for this task
3. Scripts/Custom/<existing file>.cs          <- interfaces to respect (if any)

## Task
[Clear description of exactly what to build. Reference design doc sections by name.]

## File(s) to create or edit
- Scripts/Custom/<filename>.cs   (CREATE)
- Scripts/Custom/<filename>.cs   (EDIT — describe what to change)

## Must NOT touch
- [list files that must not be modified]

## Interfaces to respect
[Any class names, method signatures, or namespaces from existing code that this
new module must reference or be compatible with.]

## Definition of done
- [ ] File compiles with no errors (verify by reading the code carefully)
- [ ] Follows namespace Server.Custom convention
- [ ] Serialize/Deserialize versioned correctly (if persistent class)
- [ ] Initialize() static method present (if system class)
- [ ] Equipment rule followed (books in backpack, not SetWearable)
- [ ] [task-specific checks]

## Done signal
Commit this file as your final commit:
  ToDo Cowork/DONE - Claude2 - <topic> - <YYYY-MM-DD>.md

Contents of that file: one line — "Done: <topic>. Files created/edited: <list>."
```

---

## Branching rules

- Claude 2 always works on branch: `claude2/<topic-in-kebab-case>`
- **NEVER commit directly to `main`** — hard rule, no exceptions
- Create the branch as the very first step before touching any file:
  ```
  git checkout main
  git pull
  git checkout -b claude2/<topic-in-kebab-case>
  ```
- One branch per work instruction
- Final commit on the branch must be the DONE file
- Push with: `git push origin claude2/<topic-in-kebab-case>`
- Do NOT `git push` without specifying the branch — this risks pushing to main

---

## Review flow

1. Claude 2 pushes branch and commits DONE file
2. User pulls on Master Claude machine and reports "Claude 2 done: `<topic>`"
3. Master Claude reads the diff, checks for integration issues, runs test deploy
4. Master Claude merges or requests fixes

---

## Done files

Completed instructions have a matching `DONE` file:

```
DONE - Claude2 - <topic> - <YYYY-MM-DD>.md
```

Do not delete done files — they serve as the implementation log.
