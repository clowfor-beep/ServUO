# Design Documents

This folder contains design specifications for features being built on the "Fun Stuff" ServUO shard.

Each design is a standalone `.md` file. Before any code is written, the feature should have a design doc here.

## Structure

```
Design/
  README.md          ← this file
  <feature-name>.md  ← one file per feature
```

## Design Doc Template

Each design doc should cover:

- **Overview** — what this feature does and why
- **Player-facing behaviour** — what the player sees and experiences
- **Technical approach** — classes, events, serialization, config
- **Open questions** — things not yet decided
- **Status** — Draft / Ready for implementation / In progress / Done

## Workflow

1. Create a design doc here before writing any code
2. Commit the design to git so other sessions can pick it up
3. Reference the design doc when implementing
4. Update status when implementation begins and completes
