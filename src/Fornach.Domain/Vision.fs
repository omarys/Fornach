namespace Fornach.Domain

open System
open Fornach.Spatial

/// The current locomotion mode of a combatant on the spatial grid
type LocomotionState =
  | Stationary of turnsResting: int
  | Walking
  | Sprinting
  | Crouching

/// Environmental visibility constraints
type EnvironmentalFactors =
  { AmbientLight: float // 0.0 (pitch black) to 1.0 (bright daylight)
    WeatherVisibility: float } // 0.0 (dense fog/rain) to 1.0 (clear sky)
  static member Default =
    { AmbientLight = 1.0
      WeatherVisibility = 1.0 }

[<RequireQualifiedAccess>]
module VisionResolver =
  let private defaultBaseRadius = 12

  /// Calculates the active VisionCone for a combatant based on their 12-stat attributes,
  /// dynamic entropy meters, locomotion mode, facing direction, and environment.
  let resolve
    (combatant: Combatant)
    (position: Point)
    (facing: Direction option)
    (locomotion: LocomotionState)
    (env: EnvironmentalFactors)
    : VisionCone =

    let stats = combatant.Stats
    let meters = combatant.Meters

    let poise = stats.Get Poise
    let prowess = stats.Get Prowess
    let reflex = stats.Get Reflex
    let finesse = stats.Get Finesse

    // 1. Calculate Base Arc by Locomotion
    let baseArc =
      match locomotion with
      | LocomotionState.Walking -> 120.0
      | LocomotionState.Crouching -> 140.0
      | LocomotionState.Sprinting ->
        // Base sprint is a narrow 60° forward cone.
        // Agility (Reflex + Finesse) softens this constriction, expanding peripheral retention up to 110°.
        let agilityBonus = float (reflex + finesse) / 6.0
        Math.Min(110.0, 60.0 + agilityBonus)
      | LocomotionState.Stationary turnsResting ->
        // Base stationary starts at 120° and dilates outward with each turn resting.
        // Discipline (Poise) is the primary governor: high Poise expands awareness rapidly.
        // (e.g. Poise 20 -> +55°/turn; Poise 100 -> +95°/turn; Poise 150 -> +120°/turn)
        let dilationRate = 45.0 + (float poise / 2.0)
        let expanded = 120.0 + (float turnsResting * dilationRate)
        Math.Min(360.0, expanded)

    // 2. Discipline Vector Combat Tunnel-Vision Resistance
    // High Poise and Prowess maintain a wider viewing angle under pressure
    let disciplineBonus = float (poise + prowess) / 20.0

    // 3. Status Meter Penalties
    // Overwhelm and Recklessness induce panic and adrenaline tunnel-vision
    let overwhelmPenalty = float meters.Overwhelm.Value * 0.4
    let recklessnessPenalty = float meters.Recklessness.Value * 0.2
    let meterPenalty = overwhelmPenalty + recklessnessPenalty

    // Compute final arc (clamped between 30° minimum and 360° full circle)
    let rawArc = baseArc + disciplineBonus - meterPenalty
    let finalArc = Math.Clamp(rawArc, 30.0, 360.0)

    // If the arc has expanded to 360°, facing direction becomes omnidirectional
    let effectiveFacing = if finalArc >= 360.0 then None else facing

    // 4. Calculate Sight Radius
    // Exhaustion degrades visual clarity; environment (light & fog) attenuates max range
    let exhaustionFactor =
      Math.Max(0.3, 1.0 - (float meters.Exhaustion.Value / 150.0))

    let envMultiplier =
      Math.Clamp(env.AmbientLight * env.WeatherVisibility, 0.1, 1.0)

    let calculatedRadius =
      float defaultBaseRadius * exhaustionFactor * envMultiplier
      |> Math.Round
      |> int

    let finalRadius = Math.Max(1, calculatedRadius)

    { Origin = position
      Facing = effectiveFacing
      ArcDegrees = finalArc
      MaxRadius = finalRadius }
