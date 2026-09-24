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
      let line = Line.bresenham start target

      // Drop the start tile (actor's own tile) and target tile (the destination)
      // Intermediate tiles must all be non-opaque (transparent)
      match line with
      | []
      | [ _ ] -> true // No intermediate tiles to check
      | _ :: rest ->
        let intermediateTiles =
          // Exclude the last tile (the target)
          rest |> List.take (rest.Length - 1)

        // Line of sight exists if NONE of the intermediate tiles block vision
        intermediateTiles |> List.forall (fun pt -> not (isOpaque pt))
