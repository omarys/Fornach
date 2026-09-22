namespace Fornach.Spatial

[<RequireQualifiedAccess>]
module Line =
  /// Traces a straight line between two points using Bresenham's algorithm.
  /// Returns the complete list of points from `start` to `target` (inclusive).
  let bresenham (start: Point) (target: Point) : Point list =
    let dx = abs (target.X - start.X)
    let dy = abs (target.Y - start.Y)

    let sx = if start.X < target.X then 1 else -1
    let sy = if start.Y < target.Y then 1 else -1

    /// Tail-recursive loop accumulating points in reverse
    let rec loop x y err acc =
      let current = { X = x; Y = y }
      let nextAcc = current :: acc

      if x = target.X && y = target.Y then
        // Reached the target; reverse accumulator to restore start -> target order
        List.rev nextAcc
      else
        let e2 = 2 * err
        let nextX, nextErr1 = if e2 >= dy then x + sx, err + dy else x, err
        let nextY, nextErr2 = if e2 <= dx then y + sy, nextErr1 + dx else y, nextErr1

        loop nextX nextY nextErr2 nextAcc

    loop start.X start.Y (dx + dy) []
