# Decoupled Spatial Engine and Dynamic Discipline-Modulated FOV

## Status
Accepted

## Context
Fornach requires spatial reasoning—specifically Field of View (FOV), Line of Sight (LOS) obstacle occlusion, and pathfinding—to support tactical positioning, ranged combat, and roguelike exploration. Historically, roguelike engines couple these algorithms directly to external C libraries (such as `libtcod`) or embed grid coordinates directly into combat resolution.

Furthermore, we require a dynamic, realistic visual model:
- Movement (sprinting, walking) and combat engagement should narrow the character's FOV (simulating tunnel vision and physical exertion).
- Pausing or remaining stationary should cause the FOV to gradually expand back outward as the character takes in their surroundings.
- Actions (crouching, weapon aiming) and environmental factors (ambient lighting, weather/fog) must modulate visibility.
- LOS must strictly respect environmental occluders (walls, pillars, trees, terrain).

## Decision

1. **Decouple Spatial Topology into an Independent Engine Module (`Fornach.Spatial`)**:
   - Algorithms for directional obstacle shadowcasting and pathfinding (A*, Dijkstra maps) reside in a dedicated, pure spatial module.
   - The spatial engine is completely decoupled from `Fornach.Engine` (combat mechanics) and world-building/UI layers. It operates strictly on generic geometric abstractions (`Point`, `VisionCone`) and higher-order functions (`isOpaque: Point -> bool`, `cost: Point -> Point -> float`), completely free of external dependencies like `libtcod`.

2. **Parametric Directional Obstacle Shadowcasting**:
   - The FOV algorithm calculates visibility from a `VisionCone`:
     ```fsharp
     type VisionCone = {
         Origin: Point
         Facing: Direction option   // None = 360° omnidirectional awareness
         ArcDegrees: float          // e.g., 60° (focused tunnel vision) to 360° (full awareness)
         MaxRadius: int             // Distance threshold attenuated by light/weather
     }
     ```
   - Obstacles cast geometric shadows across the cone, ensuring walls, trees, and obstacles block LOS regardless of arc width.

3. **Attribute-Driven Aperture and Dilation**:
   - The player's dynamic `VisionCone` is calculated each tick/turn by a domain resolver governed by their state, meters, and attribute matrix:
     - **Discipline Vector (Prowess & Poise)** — *Primary Governor*: Mental focus and martial composure directly mitigate adrenaline-induced tunnel vision. High Prowess and Poise retain a wider field of view during high-intensity combat exchanges and rapid maneuvers, and significantly accelerate the recovery rate (dilation back to 360° awareness) upon stopping.
     - **Agility Vector (Finesse & Reflex)** — *Secondary Governor*: Spatial coordination and kinetic reaction soften the aperture constriction during rapid locomotion (sprinting, dodging), preserving peripheral situational awareness.
     - **Locomotion & Stance**: Sprinting narrows arc width and increases forward focus; crouching lowers profile and max distance while stabilizing a wide front arc; standing stationary increments an awareness counter that dilates the arc over successive turns.
     - **Entropy Meters**: Stacking `Overwhelm`, `Recklessness`, or `Exhaustion` narrows the view arc and degrades maximum sight radius.
     - **Environment**: Ambient light level and weather visibility scale the effective `MaxRadius` cutoff.

## Considered Options

- **Integrating FOV into `Fornach.Engine` (Combat)**: Rejected. Exploration, peaceful patrol, stealth, and dungeon generation connectivity require spatial calculations when no combat is active. Coupling them creates an unmanageable dependency between combat rules and grid geometry.
- **Delegating FOV to the UI / CLI Layer**: Rejected. Vision and LOS directly affect AI decisions, stealth mechanics, and targeting validation. Moving them to the presentation layer prevents headless simulation, balance profiling, and property-based testing.
- **Adopting `libtcod` or Native Bindings**: Rejected. Native C bindings add packaging friction, cross-platform compilation overhead, and impedance mismatch with idiomatic F# immutable data structures.

## Consequences

- The combat system remains cleanly isolated: it asks spatial questions only via boolean flags (`hasLineOfSight`) or scalar distances (`Distance.chebyshev`), never inspecting the map directly.
- Vision calculations can be fully simulated and verified headlessly in `Fornach.Cli` balance harnesses and property-based test suites ([fsharp-testing](../../.agents/skills/fsharp-testing/SKILL.md)).
- World-building systems supply the `isOpaque` predicate from their tile maps without dictating how visibility or combat calculations occur.
