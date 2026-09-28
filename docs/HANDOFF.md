# Fornach.Spatial Implementation & ADR 0001 Completion

## Status: All 5 Phases Complete (132 Tests Passing)

This document records the completed implementation of `Fornach.Spatial` and its integration with `Fornach.Domain`, as specified in [docs/adr/0001-decoupled-spatial-engine-and-dynamic-fov.md](file:///home/omary/Dev/fornach/docs/adr/0001-decoupled-spatial-engine-and-dynamic-fov.md).

---

## Architecture Summary

```
┌─────────────────────────────────────────────────────────────┐
│  Fornach.Spatial                                            │
│  - Zero external dependencies (.NET 10 library)             │
│  - Geometry Primitives: Point, Direction, Distance          │
│  - Line of Sight: Line (Bresenham), Los (higher-order check)│
│  - FOV: VisionCone, Fov (directional shadowcasting)         │
│  - Pathfinding: Pathfinding (A*), Dijkstra (flow fields)    │
└──────────────────────────────┬──────────────────────────────┘
                               │ (ProjectReference)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│  Fornach.Domain                                             │
│  - 12-Stat Matrix & Status Meters                           │
│  - Vision.fs (LocomotionState, EnvironmentalFactors)        │
│  - VisionResolver (Discipline/Poise dilation & Agility cone)│
└─────────────────────────────────────────────────────────────┘
```

---

## Completed Modules & Test Coverage

### 1. `Fornach.Spatial`
* **`Point.fs`**: Struct record with operator overloads (`+`, `-`, `*`), cardinal and 8-way Chebyshev neighbor generators.
* **`Direction.fs`**: 8-way compass directions, `toDelta`, `opposite`, and degree conversion.
* **`Distance.fs`**: Manhattan, Chebyshev (roguelike standard), and Euclidean metrics.
* **`Line.fs`**: Pure tail-recursive Bresenham line generator.
* **`Los.fs`**: Higher-order obstacle Line-of-Sight check (`isOpaque: Point -> bool`).
* **`VisionCone.fs`**: `VisionCone` specification (`Origin`, `Facing`, `ArcDegrees`, `MaxRadius`) and angular difference logic.
* **`Fov.fs`**: Octant-based recursive shadowcasting with beam/tile slope overlap interval testing (`startSlope >= leftSlope && endSlope <= rightSlope`).
* **`Pathfinding.fs`**: 8-directional $A^*$ search using .NET `PriorityQueue<Point, float>` with path reconstruction.
* **`Dijkstra.fs`**: Multi-goal breadth-first flow fields with `nextStepTowards` (hunting) and `nextStepAway` (fleeing).

### 2. `Fornach.Domain`
* **`Vision.fs`**:
  * `LocomotionState`: `Stationary of turnsResting: int`, `Walking`, `Sprinting`, `Crouching`.
  * `EnvironmentalFactors`: `AmbientLight`, `WeatherVisibility`.
  * `VisionResolver.resolve`:
    * **Discipline Vector (Prowess & Poise)**: Primary governor. High Poise accelerates awareness dilation upon stopping ($360^\circ$ achieved in 2 turns vs. 5 turns for novices). High Prowess & Poise resist combat tunnel vision.
    * **Agility Vector (Finesse & Reflex)**: Secondary governor. High Reflex & Finesse soften sprint constriction (expanding from $60^\circ$ up to $110^\circ$).
    * **Entropy Meters**: High `Overwhelm` and `Recklessness` force severe tunnel vision; `Exhaustion` attenuates sight radius.
    * **Environment**: Dark/fog scales effective maximum radius.

### 3. Test Suites (132 tests total)
* `tests/Fornach.Spatial.Tests`:
  * `PointTests.fs`
  * `LineTests.fs`
  * `FovTests.fs`
  * `PathfindingTests.fs`
  * `DijkstraTests.fs`
* `tests/Fornach.Domain.Tests`:
  * `VisionResolverTests.fs` (and all prior domain/balance suites)
