namespace Fornach.Spatial

[<RequireQualifiedAccess>]
module Distance =
  /// Manhattan distance: |x1 - x2| + |y1 - y2|
  /// Used for 4-directional grid movement where diagonals are forbidden.
  let manhattan (a: Point) (b: Point) : int = abs (a.X - b.X) + abs (a.Y - b.Y)

  /// Chebyshev distance: max(|x1 - x2|, |y1 - y2|)
  /// Standard roguelike distance where diagonal moves cost 1 turn, just like orthogonal moves.
  let chebyshev (a: Point) (b: Point) : int = max (abs (a.X - b.X)) (abs (a.Y - b.Y))

  /// Euclidean distance: sqrt((x1 - x2)^2 + (y1 - y2)^2)
  /// True straight-line distance, used for circular FOV radius checks and cone math.
  let euclidean (a: Point) (b: Point) : float =
    let dx = float (a.X - b.X)
    let dy = float (a.Y - b.Y)
    sqrt (dx * dx + dy * dy)
