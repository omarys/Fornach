# Enemy Level Spread Is Capped by Depth

## Status

Accepted and Implemented.

## Context

`TowerSession.rescaleFloorEnemies` rebuilds a generated floor at the player's level, offsetting each enemy by a per-id salt:

```fsharp
player.Level + (salt % 6)
```

That offset is drawn fresh for every enemy on every floor, so it is not a difficulty curve — it is a constant. A level-1 player opening the tower met a pack of four enemies of levels 1 through 6, with no gear, no hone and no rest, and the same `+0..+5` was waiting on floor 40. The tower could not teach anything: floor 1 and floor 30 differed only in the player's own level.

`--run-sim 40 --floors 8` ([`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs)) measured the consequence: **55% floor-1 clear, 55% floor 2, 33% floor 3**, average depth **1.23**, 1 of 40 runs finishing. With growth already reduced (Prowess/Poise per kill halved) and the tempo rules reworked in [ADR 0007](file:///home/omary/Dev/fornach/docs/adr/0007-tempo-turns-on-decisive-answers.md), the spread was still the largest term by an order of magnitude.

## Decision

Cap the offset by the floor being generated:

```fsharp
let depthCap = 2 + floor.FloorNumber / 2
player.Level + min (salt % 6) depthCap
```

Floor 1 answers at `+0..+2`, floor 4 at `+4`, and from floor 8 the original full `+0..+5` is live — the late tower keeps its teeth. Bosses are untouched (`player.Level + 10`).

`2` rather than `1` is deliberate: a `+1` cap made floors 1–2 free (100% clear at 100% health), which is not a tutorial so much as a corridor. `2 + floor/2` keeps the opening tense and moves the first real wall to floors 3–4, where the player has levels to spend.

## Consequences

Measured with the same harness (40 runs × 8 floors, then 30 runs × 14 floors; pessimistic model — whole pack at once, no rest, honing, consumables or gear):

| Floor | Clear % before | Clear % after | Avg HP % after |
| :--- | :---: | :---: | :---: |
| 1 | 55% | 98% | 97% |
| 2 | 55% | 90% | 80% |
| 3 | 33% | 69% | 63% |
| 4 | (few) | 58% | 50% |
| 5–8 | (few) | 71 / 70 / 71 / 100% | 60→66% |

Average depth **1.23 → 3.48**, runs finishing all eight floors **1/40 → 5/40**. Over 14 floors the shape holds: teach (97%, 93%), bite (70%, 58%), then a slow grind as health drifts from 60% to 33% — attrition, not a wall (avg depth 4.13, 2/30 reaching floor 14).

- The early game is now a curve instead of a coin flip, and the tower still ends runs: no tuning reaches a point where a pessimistic player is expected to finish.
- The alternative (`1 + floor/2`) is measured and available: 100 / 100 / 85 / 76 / 77 / 85 / 65 / 64%, avg depth 4.88, 7/40 cleared. Softer opening, flatter middle.
- The cap is a function of floor number only, so it stays deterministic per seed and the `--balance-matrix` sweep is unaffected (that sweep builds combatants directly, never through a floor).
- Re-run `--run-sim` after any future change to growth, enemy pools or XP value: this table is the only place the progression curve is visible as a whole.
