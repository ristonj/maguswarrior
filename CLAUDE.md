# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**maguswarrior** is a game project in early planning/setup. The repo currently contains Mage Knight board game reference materials (PDFs and images in the root) and a BMAD AI agent framework scaffold — no game engine code exists yet.

## BMAD Framework

This project uses [BMAD](https://github.com/bmad-code-org) v6.6.0 with two active modules:

- **bmm** — business/product management (PRDs, architecture, stories, epics)
- **gds** (v0.4.0, `bmad-game-dev-studio`) — game development studio (GDDs, playtesting, game architecture, sprint planning)

All AI agent skills are invoked via slash commands (e.g. `/gds-create-gdd`, `/bmad-agent-pm`). See `.claude/skills/` for available skills or run `/bmad-help`.

### Key Paths

| Path | Purpose |
|------|---------|
| `_bmad-output/planning-artifacts/` | GDDs, PRDs, architecture docs, epics, stories |
| `_bmad-output/implementation-artifacts/` | Code specs, test plans |
| `docs/` | Project knowledge base (persists across sessions) |
| `_bmad/config.toml` | Installer-managed config (read-only) |
| `_bmad/custom/config.toml` | Team overrides (committed) |
| `_bmad/custom/config.user.toml` | Personal overrides (gitignored) |

### Configured Agents

**BMM team:** Mary (BA), John (PM), Sally (UX), Winston (Architect), Amelia (Dev), Paige (Tech Writer)

**GDS team:** Samus Shepard (Game Designer), Cloud Dragonborn (Game Architect), Link Freeman (Game Dev), Indie (Solo Dev), Paige (Tech Writer)

## Reference Materials

The root PDFs and images are Mage Knight board game references for design inspiration:
- `MKUE_Rulebook_BOOKLET.pdf` — full rulebook
- `MK_card_overview.pdf` — card reference
- `Mage-Knight-Board-Game-Ultimate-Edition-Walkthrough-September-2018.pdf` — walkthrough
- `Mage_Knight_Starting_Action_Cards.pdf` — starting cards
- `daytactics.jpg` / `nighttactics.jpg` — tactics tiles
- `pic1540686.jpg` — component overview

## Workflow

1. Start with `/gds-create-game-brief` or `/gds-brainstorm-game` to establish the game concept
2. Create GDD with `/gds-create-gdd`, then validate with `/gds-validate-gdd`
3. Create PRD with `/gds-create-prd`, validate with `/gds-validate-prd`
4. Design architecture with `/gds-game-architecture`
5. Break into epics/stories with `/gds-create-epics-and-stories`
6. Implement stories with `/gds-dev-story` or `/gds-agent-game-dev`

All planning output lands in `_bmad-output/planning-artifacts/`. Keep `docs/` updated with stable project knowledge so it persists as the authoritative reference across sessions.

## Testing

**TDD is required.** Write the failing test first, confirm it fails, then implement the minimum code to make it pass. Never mark a task complete without running the tests and seeing them green.

`dotnet test tests/maguswarrior.Tests.csproj` may be run at any time without asking for permission.

## Platform

Primary platform is not yet decided — unity, unreal, godot, and other are all listed in config. This will be set once the GDD is established.
