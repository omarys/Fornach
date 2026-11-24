namespace Fornach.Spatial

[<RequireQualifiedAccess>]
module Los =
  /// Checks if there is an unobstructed line of sight between start and target
  /// `isOpaque` is a higher-order predicate returning true if a tile blocks vision.
  /// Note: If the target tile itself is opaque (e.g. inspecting a wall), it is still visible.
  let check (isOpaque: Point -> bool) (start: Point) (target: Point) : bool =
    if start = target then
      true
    else
      let dx = abs (target.X - start.X)
      let dy = -abs (target.Y - start.Y)
      let sx = if start.X < target.X then 1 else -1
      let sy = if start.Y < target.Y then 1 else -1

      let mutable x = start.X
      let mutable y = start.Y
      let mutable err = dx + dy
      let mutable clear = true
      let mutable finished = false

      while not finished do
        let e2 = 2 * err

        if e2 >= dy then
          err <- err + dy
          x <- x + sx

        if e2 <= dx then
          err <- err + dx
          y <- y + sy

        if x = target.X && y = target.Y then
          finished <- true
        elif isOpaque { X = x; Y = y } then
          clear <- false
          finished <- true

      clear
