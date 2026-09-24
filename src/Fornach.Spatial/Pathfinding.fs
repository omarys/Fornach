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
      let initialH = float (Distance.chebyshev start goal)
      openSet.Enqueue(start, initialH)

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
                // In roguelike movement, orthogonal and diagonal steps both cost 1.0
                let tentativeG = currentG + 1.0

                let knownG =
                  match gScore.TryGetValue(neighbor) with
                  | true, g -> g
                  | false, _ -> System.Double.PositiveInfinity

                if tentativeG < knownG then
                  cameFrom.[neighbor] <- current
                  gScore.[neighbor] <- tentativeG
                  let h = float (Distance.chebyshev neighbor goal)
                  let f = tentativeG + h

                  if not (inOpenSet.Contains(neighbor)) then
                    openSet.Enqueue(neighbor, f)
                    inOpenSet.Add(neighbor) |> ignore

            search ()

      // Kick off the search (aligned with let rec search)
      search ()
