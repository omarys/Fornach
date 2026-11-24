namespace Fornach.Spatial

/// Where an attacker stands relative to a defender's facing. The defender watches along `Facing`, so an
/// attack arriving outside that arc is the opening this geometry exists to model.
type Flank =
  | Front
  | Flank
  | Rear

[<RequireQualifiedAccess>]
module Flanking =

  /// Classifies an attack by the bearing from the defender to the attacker, measured against the
  /// direction the defender faces. Y grows downward, matching `Direction.toDegrees`.
  /// Within 60 degrees of the facing is met head-on (`Front`); the opposite 60-degree wedge is `Rear`;
  /// the two side wedges are `Flank`. Uses exact integer dot-product math (`cos(60°) = 1/2`),
  /// avoiding `atan2`, `sqrt`, and floating-point modulo.
  let classify (facing: Direction) (defender: Point) (attacker: Point) : Flank =
    let dx = int64 (attacker.X - defender.X)
    let dy = int64 (attacker.Y - defender.Y)

    if dx = 0L && dy = 0L then
      Front // same tile: there is no side to be on
    else
      let lenSq = dx * dx + dy * dy

      match facing with
      | Direction.East ->
        if dx > 0L && 4L * dx * dx >= lenSq then Front
        elif dx < 0L && 4L * dx * dx >= lenSq then Rear
        else Flank
      | Direction.West ->
        if dx < 0L && 4L * dx * dx >= lenSq then Front
        elif dx > 0L && 4L * dx * dx >= lenSq then Rear
        else Flank
      | Direction.South ->
        if dy > 0L && 4L * dy * dy >= lenSq then Front
        elif dy < 0L && 4L * dy * dy >= lenSq then Rear
        else Flank
      | Direction.North ->
        if dy < 0L && 4L * dy * dy >= lenSq then Front
        elif dy > 0L && 4L * dy * dy >= lenSq then Rear
        else Flank
      | Direction.SouthEast ->
        let dot = dx + dy
        if dot > 0L && 2L * dot * dot >= lenSq then Front
        elif dot < 0L && 2L * dot * dot >= lenSq then Rear
        else Flank
      | Direction.SouthWest ->
        let dot = -dx + dy
        if dot > 0L && 2L * dot * dot >= lenSq then Front
        elif dot < 0L && 2L * dot * dot >= lenSq then Rear
        else Flank
      | Direction.NorthWest ->
        let dot = -dx - dy
        if dot > 0L && 2L * dot * dot >= lenSq then Front
        elif dot < 0L && 2L * dot * dot >= lenSq then Rear
        else Flank
      | Direction.NorthEast ->
        let dot = dx - dy
        if dot > 0L && 2L * dot * dot >= lenSq then Front
        elif dot < 0L && 2L * dot * dot >= lenSq then Rear
        else Flank
