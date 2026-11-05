namespace Fornach.Spatial

/// Where an attacker stands relative to a defender's facing. The defender watches along `Facing`, so an
/// attack arriving outside that arc is the opening this geometry exists to model.
type Flank =
  | Front
  | Flank
  | Rear

[<RequireQualifiedAccess>]
module Flanking =

  /// Half-angle of the arc a defender can watch. Within 60 degrees of the facing is met head-on; the
  /// opposite 60-degree wedge is the rear, and the two side wedges are the flanks.
  let private guardArc = 60.0

  /// Smallest angle between two bearings, normalised to 0..180 degrees.
  let private angleBetween (a: float) (b: float) =
    let d = ((a - b) % 360.0 + 360.0) % 360.0
    if d > 180.0 then 360.0 - d else d

  /// Classifies an attack by the bearing from the defender to the attacker, measured against the
  /// direction the defender faces. Y grows downward, matching `Direction.toDegrees`.
  let classify (facing: Direction) (defender: Point) (attacker: Point) : Flank =
    let dx = float (attacker.X - defender.X)
    let dy = float (attacker.Y - defender.Y)

    if dx = 0.0 && dy = 0.0 then
      Front // same tile: there is no side to be on
    else
      let bearing = System.Math.Atan2(dy, dx) * 180.0 / System.Math.PI
      let off = angleBetween bearing (Direction.toDegrees facing)

      if off <= guardArc then Front
      elif off >= 180.0 - guardArc then Rear
      else Flank
