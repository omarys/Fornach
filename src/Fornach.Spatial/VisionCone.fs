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
  let private invSqrt2 = 1.0 / Math.Sqrt 2.0

  /// Unit direction vector for a compass direction (Y-down grid).
  let internal facingUnitVector (dir: Direction) : struct(float * float) =
    match dir with
    | Direction.East -> struct(1.0, 0.0)
    | Direction.SouthEast -> struct(invSqrt2, invSqrt2)
    | Direction.South -> struct(0.0, 1.0)
    | Direction.SouthWest -> struct(-invSqrt2, invSqrt2)
    | Direction.West -> struct(-1.0, 0.0)
    | Direction.NorthWest -> struct(-invSqrt2, -invSqrt2)
    | Direction.North -> struct(0.0, -1.0)
    | Direction.NorthEast -> struct(invSqrt2, -invSqrt2)

  /// Normalizes an angle into the range [0.0, 360.0]
  let normalizeDegrees (deg: float) : float =
    let rem = deg % 360.0
    if rem < 0.0 then rem + 360.0 else rem

  /// Calculates the smallest angular difference between two angles in degrees [0.0, 180.0]
  let angleDifference (a: float) (b: float) : float =
    let diff = abs (normalizeDegrees a - normalizeDegrees b)
    if diff > 180.0 then 360.0 - diff else diff

  /// Checks whether an offset `(dx, dy)` with squared length `distSq > 0` lies inside a directional
  /// cone given its unit facing vector `(fx, fy)`, `cosHalfArc`, and `cosHalfArcSq`.
  let inline internal inDirectionalArc
    (fx: float)
    (fy: float)
    (cosHalfArc: float)
    (cosHalfArcSq: float)
    (dx: int)
    (dy: int)
    (distSq: int64)
    : bool =
    let dot = float dx * fx + float dy * fy

    if cosHalfArc > 1e-12 then
      dot >= 0.0 && dot * dot + 1e-9 >= cosHalfArcSq * float distSq
    elif cosHalfArc < -1e-12 then
      dot >= 0.0 || dot * dot <= cosHalfArcSq * float distSq + 1e-9
    else
      dot >= -1e-9

  /// Checks if a target point lies within the angular arc and max radius of the cone.
  /// Does NOT check obstacles (obstacle occlusion is handled by FOV shadowcasting).
  let inCone (cone: VisionCone) (target: Point) : bool =
    if target = cone.Origin then
      true // The origin point is always in the cone
    elif cone.MaxRadius < 0 then
      false
    else
      let dx = target.X - cone.Origin.X
      let dy = target.Y - cone.Origin.Y
      let distSq = int64 dx * int64 dx + int64 dy * int64 dy
      let maxRadiusSq = int64 cone.MaxRadius * int64 cone.MaxRadius

      if distSq > maxRadiusSq then
        false // Outside max radius
      else
        match cone.Facing with
        | None -> true // Omnidirectional vision
        | Some _ when cone.ArcDegrees >= 360.0 -> true // Full circle vision
        | Some _ when cone.ArcDegrees < 0.0 -> false
        | Some facing ->
          let struct(fx, fy) = facingUnitVector facing
          let cosHalfArc = Math.Cos(cone.ArcDegrees * (Math.PI / 360.0))
          inDirectionalArc fx fy cosHalfArc (cosHalfArc * cosHalfArc) dx dy distSq
