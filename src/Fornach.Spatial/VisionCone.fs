namespace Fornach.Spatial

open System

/// Configuration defining the geometry and aperture of an actor's vision.
type VisionCone =
  { Origin: Point
    Facing: Direction option // None = 360 deg omnidirectional awareness
    ArcDegrees: float // e.g. 60 deg (tunnel vision) to 360 deg (full circle)
    MaxRadius: int } // Maximum visible distance threshold

[<RequireQualifiedAccess>]
module VisionCone =
  /// Normalizes an angle into the range [0.0, 360.0]
  let normalizeDegrees (deg: float) : float =
    let rem = deg % 360.0
    if rem < 0.0 then rem + 360.0 else rem

  /// Calculates the smallest angular difference between two angles in degrees [0.0, 180.0]
  let angleDifference (a: float) (b: float) : float =
    let diff = abs (normalizeDegrees a - normalizeDegrees b)
    if diff > 180.0 then 360.0 - diff else diff

  /// Checks if a target point lies within the angular arc and max radius of the cone.
  /// Does NOT check obstacles (obstacle occlusion is handled by FOV shadowcasting).
  let inCone (cone: VisionCone) (target: Point) : bool =
    if target = cone.Origin then
      true // The origin point is always in the cone
    else
      let dist = Distance.euclidean cone.Origin target

      if dist > float cone.MaxRadius then
        false // Outside max radius
      else
        match cone.Facing with
        | None -> true // Omnidirectional vision
        | Some facing when cone.ArcDegrees >= 360.0 -> true // Full circle vision
        | Some facing ->
          let facingAngle = Direction.toDegrees facing

          // Screen/grid coordinates: Y is positive downwards
          let dx = float (target.X - cone.Origin.X)
          let dy = float (target.Y - cone.Origin.Y)

          // Math.Atan2 returns radians in [-pi, pi]
          let rad = atan2 dy dx
          let rawDeg = rad * (180.0 / Math.PI)
          let targetAngle = normalizeDegrees rawDeg

          let diff = angleDifference facingAngle targetAngle
          diff <= cone.ArcDegrees / 2.0
