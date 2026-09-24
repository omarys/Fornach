namespace Fornach.Spatial

[<RequireQualifiedAccess>]
module Fov =
  // Matrix multipliers to project Octant 0 into all 8 octants:
  // (xx, xy, yx, yy)
  let private octantTransforms =
    [ 1, 0, 0, 1 // Octant 0
      0, 1, 1, 0 // Octant 1
      0, -1, 1, 0 // Octant 2
      -1, 0, 0, 1 // Octant 3
      -1, 0, 0, -1 // Octant 4
      0, -1, -1, 0 // Octant 5
      0, 1, -1, 0 // Octant 6
      1, 0, 0, -1 ] // Octant 7

  /// Computes all visible grid points from a given VisionCone, taking into account
  /// obstacle line-of-sight occlusion and the cone's directional aperture.
  let compute (isOpaque: Point -> bool) (cone: VisionCone) : Set<Point> =
    let mutable visible = Set.empty.Add cone.Origin

    // Scans a single octant row-by-row outward
    let scanOctant (xx, xy, yx, yy) =
      let rec scanRow (depth: int) (startSlope: float) (endSlope: float) =
        if depth <= cone.MaxRadius && startSlope >= endSlope then
          let mutable newStartSlope = startSlope
          let mutable wasBlocked = false

          // Scan tiles in this row from top slope down to bottom slope
          for col in depth .. -1 .. 0 do
            let leftSlope = (float col - 0.5) / (float depth + 0.5)
            let rightSlope = (float col + 0.5) / (float depth - 0.5)

            if startSlope >= leftSlope && endSlope <= rightSlope then
              // Map octant-relative (col, depth) to global grid Point
              let globalX = cone.Origin.X + col * xx + depth * xy
              let globalY = cone.Origin.Y + col * yx + depth * yy
              let pt = { X = globalX; Y = globalY }

              // If within theh cone arc and max radius, mark visible
              if VisionCone.inCone cone pt then
                visible <- visible.Add pt

              let isBlocked = isOpaque pt

              if isBlocked then
                if not wasBlocked then
                  // Hit a wall: start a new shadow segment
                  wasBlocked <- true
                  let wallStartSlope = (float col + 0.5) / (float depth - 0.5)
                  scanRow (depth + 1) newStartSlope wallStartSlope
                else
                  // Continue the shadow segment
                  ()
              else if wasBlocked then
                // Stepped out of a wall into open space: adjust the remaining start slope
                wasBlocked <- false
                newStartSlope <- (float col - 0.5) / (float depth + 0.5)
              else
                // Still in open space: continue scanning
                ()
          // If the end of the row wasn't blocked, continue to the next row outward
          if not wasBlocked then
            scanRow (depth + 1) newStartSlope endSlope

      scanRow 1 1.0 0.0
    // Run shadowcasting across all 8 octants
    for transform in octantTransforms do
      scanOctant transform

    visible
