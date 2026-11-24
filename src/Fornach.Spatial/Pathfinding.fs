namespace Fornach.Spatial

open System.Collections.Generic

[<RequireQualifiedAccess>]
module Pathfinding =
  /// Reconstructs the path from start to goal by unwinding the cameFrom map.
  let private reconstructPath (cameFrom: Dictionary<Point, Point>) (current: Point) : Point list =
    let rec loop curr acc =
      match cameFrom.TryGetValue(curr) with
      | true, prev -> loop prev (curr :: acc)
      | false, _ -> curr :: acc

    loop current []

  let private sqrt2 = System.Math.Sqrt 2.0

  // Zero-allocation 8-way neighbor offsets paired with exact step costs (matching Point.allNeighbors order).
  // Diagonal steps cost sqrt(2) and cardinal steps 1.0, so a straight line is genuinely cheaper than a
  // diagonal staircase: mobs close in straight when they can instead of sidestepping arbitrarily.
  let private neighborSteps : struct(int * int * float)[] =
    [| struct(-1, -1, sqrt2)
       struct(0, -1, 1.0)
       struct(1, -1, sqrt2)
       struct(-1, 0, 1.0)
       struct(1, 0, 1.0)
       struct(-1, 1, sqrt2)
       struct(0, 1, 1.0)
       struct(1, 1, sqrt2) |]

  /// Octile distance: the true 8-way travel distance when cardinal steps cost 1 and
  /// diagonal steps cost sqrt(2). Admissible and consistent, so A* stays optimal.
  let private octile (a: Point) (b: Point) =
    let dx = abs (b.X - a.X)
    let dy = abs (b.Y - a.Y)
    float (max dx dy) + (sqrt2 - 1.0) * float (min dx dy)

  /// Finds the shortest 8-directional path from `start` to `goal` using A*, expanding at most
  /// `maxNodes` distinct tiles before giving up and returning `None`.
  /// `isPassable` returns true if a point can be walked through (not a wall or obstacle).
  let aStarBounded (maxNodes: int) (isPassable: Point -> bool) (start: Point) (goal: Point) : Point list option =
    if start = goal then
      Some [ start ]
    elif maxNodes <= 0 || not (isPassable goal) then
      None
    else
      let initialCap = min maxNodes 256
      // Priority queue storing struct(Point * gScore) prioritized by lowest fScore (lazy deletion)
      let openSet = PriorityQueue<struct(Point * float), float>(initialCap)
      let cameFrom = Dictionary<Point, Point>(initialCap)
      let gScore = Dictionary<Point, float>(initialCap)

      gScore.[start] <- 0.0
      openSet.Enqueue(struct(start, 0.0), octile start goal)

      let mutable expanded = 0
      let mutable foundPath : Point list option = None

      while openSet.Count > 0 && foundPath.IsNone && expanded < maxNodes do
        let struct(current, poppedG) = openSet.Dequeue()

        // Skip stale entries superseded by a strictly cheaper path
        if poppedG <= gScore.[current] then
          if current = goal then
            foundPath <- Some(reconstructPath cameFrom current)
          else
            expanded <- expanded + 1

            for i = 0 to neighborSteps.Length - 1 do
              let struct(dx, dy, stepCost) = neighborSteps.[i]
              let neighbor = { X = current.X + dx; Y = current.Y + dy }

              if isPassable neighbor then
                let tentativeG = poppedG + stepCost

                let mutable knownG = 0.0
                let hasG = gScore.TryGetValue(neighbor, &knownG)

                if not hasG || tentativeG < knownG then
                  cameFrom.[neighbor] <- current
                  gScore.[neighbor] <- tentativeG
                  let f = tentativeG + octile neighbor goal
                  openSet.Enqueue(struct(neighbor, tentativeG), f)

      foundPath

  /// Finds the shortest 8-directional path from `start` to `goal` using A*.
  /// `isPassable` returns true if a point can be walked through (not a wall or obstacle).
  /// Returns Some(points from start to goal) or None if unreachable.
  let aStar (isPassable: Point -> bool) (start: Point) (goal: Point) : Point list option =
    aStarBounded System.Int32.MaxValue isPassable start goal
