# Session Handoff: Fornach.Spatial Implementation

## Overview
This document captures the current implementation state of `Fornach.Spatial` so that a subsequent agent or session can resume seamlessly. The overarching design is documented in [docs/adr/0001-decoupled-spatial-engine-and-dynamic-fov.md](file:///home/omary/Dev/fornach/docs/adr/0001-decoupled-spatial-engine-and-dynamic-fov.md).

---

## Current Status: 68 Tests Passing

### What is Completed & Passing:
1. **Scaffolding**:
   - `src/Fornach.Spatial/Fornach.Spatial.fsproj` (Library, `net10.0`, zero external dependencies)
   - `tests/Fornach.Spatial.Tests/Fornach.Spatial.Tests.fsproj` (xUnit + FsCheck)
   - Both registered in [Fornach.slnx](file:///home/omary/Dev/fornach/Fornach.slnx)
2. **Phase 1 — Geometry Primitives**:
   - `Point.fs`: Struct record, vector operators (`+`, `-`, `*`), cardinal and 8-way neighbor generators.
   - `Direction.fs`: 8-way compass directions with `toDelta`, `opposite`, and degree conversion.
   - `Distance.fs`: Manhattan, Chebyshev, and Euclidean metrics.
   - Tested in `tests/Fornach.Spatial.Tests/PointTests.fs`.
3. **Phase 2 — Raycasting & Line of Sight**:
   - `Line.fs`: Tail-recursive Bresenham line generation.
   - `Los.fs`: Higher-order obstacle check (`isOpaque: Point -> bool`).
   - Tested in `tests/Fornach.Spatial.Tests/LineTests.fs`.
4. **Phase 3 — Directional Obstacle Shadowcasting**:
   - `VisionCone.fs`: Parametric `VisionCone` (`Origin`, `Facing`, `ArcDegrees`, `MaxRadius`) and angular difference math.
   - `Fov.fs`: Octant-based recursive shadowcasting with beam/tile slope overlap (`startSlope >= leftSlope && endSlope <= rightSlope`).
   - Tested in `tests/Fornach.Spatial.Tests/FovTests.fs`.

---

## Where We Stopped (In Progress)

The user has **not yet completed** Task 4.1 ($A^*$ Pathfinding). The instructions were provided, but the code files have not been created yet.

### Pending Tasks:

#### **Task 4.1: $A^*$ Pathfinding**
- Create `src/Fornach.Spatial/Pathfinding.fs` with 8-directional $A^*$ search using .NET `PriorityQueue<Point, float>`.
- Add `Pathfinding.fs` to `src/Fornach.Spatial/Fornach.Spatial.fsproj`.
- Create `tests/Fornach.Spatial.Tests/PathfindingTests.fs` (direct path, obstacle navigation, walled off path).
- Add `PathfindingTests.fs` to `tests/Fornach.Spatial.Tests/Fornach.Spatial.Tests.fsproj`.
- Verify with `dotnet test`.

#### **Task 4.2: Dijkstra Flow Fields**
- Create `src/Fornach.Spatial/Dijkstra.fs` for breadth-first distance fields from target points (multi-actor navigation / fleeing).
- Add tests in `tests/Fornach.Spatial.Tests/DijkstraTests.fs`.

#### **Phase 5: Domain Vision Resolver (Connecting to Fornach Mechanics)**
- Implement `LocomotionState` and `VisionState` in `Fornach.Domain`.
- Implement `VisionResolver` according to ADR 0001:
  - **Discipline (Prowess & Poise)**: Mitigates combat/adrenaline tunnel vision and accelerates awareness dilation when stopping.
  - **Agility (Finesse & Reflex)**: Preserves peripheral arc during high-speed locomotion (sprinting/dodging).
  - **Status Meters**: `Overwhelm`, `Recklessness`, and `Exhaustion` constricting aperture and sight radius.
  - **Environment**: Light level and weather visibility scaling max distance.

---

## Resume Instructions for the Next Agent

1. Prompt the user gently on whether they want to write `src/Fornach.Spatial/Pathfinding.fs` themselves or have the agent scaffold it.
2. Ensure file ordering in `Fornach.Spatial.fsproj` remains top-to-bottom:
   ```xml
   <ItemGroup>
     <Compile Include="Point.fs" />
     <Compile Include="Direction.fs" />
     <Compile Include="Distance.fs" />
     <Compile Include="Line.fs" />
     <Compile Include="Los.fs" />
     <Compile Include="VisionCone.fs" />
     <Compile Include="Fov.fs" />
     <Compile Include="Pathfinding.fs" />
   </ItemGroup>
   ```
3. Run `dotnet test` to ensure all tests pass (expected 71 tests once $A^*$ tests are added).

---

## Suggested Skills for the Next Agent
- `fsharp-testing`: For writing xUnit and FsCheck property tests in F#.
- `codebase-design`: For maintaining deep module boundaries between `Fornach.Spatial` and `Fornach.Engine`.
- `domain-modeling`: For keeping Fornach's 12-stat domain glossary consistent with new spatial types.
- `roguelike`: For standard roguelike conventions and algorithms.
