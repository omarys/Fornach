namespace Fornach.Spatial

open System
open System.Collections.Generic

[<RequireQualifiedAccess>]
module Fov =
  // Matrix multipliers to project Octant 0 into all 8 octants, paired with each octant's
  // angular wedge center in degrees (East = 0, South = 90, Y-down; each octant spans +-22.5 deg):
  // struct(xx, xy, yx, yy, centerDeg)
  let private octantTransforms : struct(int * int * int * int * float)[] =
    [| struct(1, 0, 0, 1, 67.5) // Octant 0: [45, 90]
       struct(0, 1, 1, 0, 22.5) // Octant 1: [0, 45]
       struct(0, -1, 1, 0, 157.5) // Octant 2: [135, 180]
       struct(-1, 0, 0, 1, 112.5) // Octant 3: [90, 135]
       struct(-1, 0, 0, -1, 247.5) // Octant 4: [225, 270]
       struct(0, -1, -1, 0, 202.5) // Octant 5: [180, 225]
       struct(0, 1, -1, 0, 337.5) // Octant 6: [315, 360]
       struct(1, 0, 0, -1, 292.5) |] // Octant 7: [270, 315]

  /// Computes all visible grid points from a given VisionCone, taking into account
  /// obstacle line-of-sight occlusion and the cone's directional aperture.
  let compute (isOpaque: Point -> bool) (cone: VisionCone) : Set<Point> =
    let maxRadius = cone.MaxRadius

    if maxRadius <= 0 then
      Set.singleton cone.Origin
    else
      let originX = cone.Origin.X
      let originY = cone.Origin.Y
      let maxRadiusSq = int64 maxRadius * int64 maxRadius

      // Precompute directional cone parameters once instead of per tile
      let isDirectional, facingDeg, halfArcDeg, fx, fy, cosHalfArc, cosHalfArcSq =
        match cone.Facing with
        | Some facing when cone.ArcDegrees < 360.0 ->
          let halfArc = cone.ArcDegrees * 0.5
          let struct(vx, vy) = VisionCone.facingUnitVector facing
          let c = Math.Cos(halfArc * (Math.PI / 180.0))
          true, Direction.toDegrees facing, halfArc, vx, vy, c, c * c
        | _ -> false, 0.0, 180.0, 0.0, 0.0, -1.0, 1.0

      // Deduplicate visited tiles with a flat bool array for typical radii (zero hashing),
      // falling back to HashSet for unusually large radii.
      let useFlatGrid = maxRadius <= 64
      let side = if useFlatGrid then 2 * maxRadius + 1 else 0
      let seenGrid = if useFlatGrid then Array.zeroCreate<bool> (side * side) else null
      let collected = if useFlatGrid then ResizeArray<Point>(min (side * side) 512) else null
      let visibleSet = if useFlatGrid then null else HashSet<Point>()

      let inline markVisible (pt: Point) (dx: int) (dy: int) =
        if useFlatGrid then
          let idx = (dy + maxRadius) * side + (dx + maxRadius)

          if not seenGrid.[idx] then
            seenGrid.[idx] <- true
            collected.Add pt
        else
          visibleSet.Add pt |> ignore

      markVisible cone.Origin 0 0

      // Scans a single octant row-by-row outward
      let scanOctant xx xy yx yy =
        let rec scanRow (depth: int) (startSlope: float) (endSlope: float) =
          if depth <= maxRadius && startSlope >= endSlope then
            let mutable newStartSlope = startSlope
            let mutable wasBlocked = false

            let fDepth = float depth
            let invDepthPlus = 1.0 / (fDepth + 0.5)
            let invDepthMinus = 1.0 / (fDepth - 0.5)

            // Clamp column scan to active [endSlope, startSlope] span (widened by 1 for float safety)
            let maxCol = min depth (int (Math.Floor(startSlope * (fDepth + 0.5) + 0.5)) + 1)
            let minCol = max 0 (int (Math.Ceiling(endSlope * (fDepth - 0.5) - 0.5)) - 1)

            // Scan tiles in this row from top slope down to bottom slope
            for col in maxCol .. -1 .. minCol do
              let fCol = float col
              let leftSlope = (fCol - 0.5) * invDepthPlus
              let rightSlope = (fCol + 0.5) * invDepthMinus

              if startSlope >= leftSlope && endSlope <= rightSlope then
                // Map octant-relative (col, depth) to global grid Point
                let dx = col * xx + depth * xy
                let dy = col * yx + depth * yy
                let pt = { X = originX + dx; Y = originY + dy }

                // If within the cone arc and max radius, mark visible
                let distSq = int64 dx * int64 dx + int64 dy * int64 dy

                if distSq <= maxRadiusSq then
                  if not isDirectional
                     || (cone.ArcDegrees >= 0.0
                         && VisionCone.inDirectionalArc fx fy cosHalfArc cosHalfArcSq dx dy distSq) then
                    markVisible pt dx dy

                let isBlocked = isOpaque pt

                if isBlocked then
                  if not wasBlocked then
                    // Hit a wall: start a new shadow segment
                    wasBlocked <- true
                    scanRow (depth + 1) newStartSlope rightSlope
                elif wasBlocked then
                  // Stepped out of a wall into open space: adjust the remaining start slope
                  wasBlocked <- false
                  newStartSlope <- leftSlope

            // If the end of the row wasn't blocked, continue to the next row outward
            if not wasBlocked then
              scanRow (depth + 1) newStartSlope endSlope

        scanRow 1 1.0 0.0

      // Run shadowcasting across octants whose 45-degree wedge intersects the cone
      if not isDirectional || cone.ArcDegrees >= 0.0 then
        let octantThreshold = halfArcDeg + 22.5 + 1e-9

        for i = 0 to octantTransforms.Length - 1 do
          let struct(xx, xy, yx, yy, centerDeg) = octantTransforms.[i]

          if not isDirectional || VisionCone.angleDifference facingDeg centerDeg <= octantThreshold then
            scanOctant xx xy yx yy

      if useFlatGrid then Set.ofSeq collected else Set.ofSeq visibleSet
