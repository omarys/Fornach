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

  /// Octile distance: the true 8-way travel distance when cardinal steps cost 1 and
  /// diagonal steps cost sqrt(2). Admissible and consistent, so A* stays optimal.
  let private octile (a: Point) (b: Point) =
    let dx = abs (b.X - a.X)
    let dy = abs (b.Y - a.Y)
    float (max dx dy) + (sqrt2 - 1.0) * float (min dx dy)

  /// Finds the shortest 8-directional path from `start` to `goal` using A*.
  /// `isPassable` returns true if a point can be walked through (not a wall or obstacle).
  /// Returns Some(points from start to goal) or None if unreachable.
  let aStar (isPassable: Point -> bool) (start: Point) (goal: Point) : Point list option =
    if start = goal then
      Some [ start ]
    elif not (isPassable goal) then
      // If goal is not passable, no path exists.
      None
    else
      // Priority queue prioritizing lowest fScore
      let openSet = PriorityQueue<Point, float>()
      let cameFrom = Dictionary<Point, Point>()
      let gScore = Dictionary<Point, float>()

      gScore.[start] <- 0.0
      openSet.Enqueue(start, octile start goal)

      let inOpenSet = HashSet<Point>()
      inOpenSet.Add(start) |> ignore

      let rec search () : Point list option =
        if openSet.Count = 0 then
          None // Exhausted all reachable tiles without reaching goal
        else
          let current = openSet.Dequeue()
          inOpenSet.Remove(current) |> ignore

          if current = goal then
            Some(reconstructPath cameFrom current)
          else
            let currentG = gScore.[current]
            let neighbors = Point.allNeighbors current

            for neighbor in neighbors do
              // Only consider neighbor if it's walkable or the goal itself
              if isPassable neighbor then
                // Diagonal steps cost sqrt(2) and cardinal steps 1.0, so a straight line
                // is genuinely cheaper than a diagonal staircase: mobs close in straight
                // when they can instead of sidestepping arbitrarily.
                let diagonal = neighbor.X <> current.X && neighbor.Y <> current.Y
                let tentativeG = currentG + (if diagonal then sqrt2 else 1.0)

                let knownG =
                  match gScore.TryGetValue(neighbor) with
                  | true, g -> g
                  | false, _ -> System.Double.PositiveInfinity

                if tentativeG < knownG then
                  cameFrom.[neighbor] <- current
                  gScore.[neighbor] <- tentativeG
                  let f = tentativeG + octile neighbor goal

                  if not (inOpenSet.Contains(neighbor)) then
                    openSet.Enqueue(neighbor, f)
                    inOpenSet.Add(neighbor) |> ignore

            search ()

      // Kick off the search (aligned with let rec search)
      search ()
