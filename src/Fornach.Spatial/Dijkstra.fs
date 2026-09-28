namespace Fornach.Spatial

open System.Collections.Generic

[<RequireQualifiedAccess>]
module Dijkstra =
  /// Computes a distance map from one or more goal points radiating outward up to maxDistance.
  /// Returns a Map<Point, int> where the integer represents step distance from the nearest goal.
  let compute (isPassable: Point -> bool) (goals: Point list) (maxDistance: int) : Map<Point, int> =
    let distances = Dictionary<Point, int>()
    let queue = Queue<Point * int>()

    // Initialize all valid goal tiles with distance 0
    for goal in goals do
      if isPassable goal then
        distances.[goal] <- 0
        queue.Enqueue(goal, 0)

    // Breadth-first flood fill
    while queue.Count > 0 do
      let current, dist = queue.Dequeue()

      if dist < maxDistance then
        for neighbor in Point.allNeighbors current do
          if isPassable neighbor && not (distances.ContainsKey neighbor) then
            let nextDist = dist + 1
            distances.[neighbor] <- nextDist
            queue.Enqueue(neighbor, nextDist)

    // Convert to immutable F# Map
    distances |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq

  /// Given a Dijkstra map and a current position, returns the neighboring tile
  /// that moves closer to the goal (lowest distance).
  let nextStepTowards (map: Map<Point, int>) (current: Point) : Point option =
    let neighbors = Point.allNeighbors current

    let validSteps =
      neighbors
      |> List.choose (fun n -> map.TryFind n |> Option.map (fun dist -> n, dist))

    match validSteps with
    | [] -> None
    | steps ->
      let currentDist = map.TryFind current |> Option.defaultValue System.Int32.MaxValue
      // Find neighbor with minimum distance strictly lower than current
      let bestNeighbor, bestDist = steps |> List.minBy snd
      if bestDist < currentDist then Some bestNeighbor else None

  /// Given a Dijkstra map and a current position, returns the neighboring tile
  /// that flees away from the goal (highest distance).
  let nextStepAway (map: Map<Point, int>) (current: Point) : Point option =
    let neighbors = Point.allNeighbors current

    let validSteps =
      neighbors
      |> List.choose (fun n -> map.TryFind n |> Option.map (fun dist -> n, dist))

    match validSteps with
    | [] -> None
    | steps ->
      let currentDist = map.TryFind current |> Option.defaultValue 0
      // Find neighbor with maximum distance strictly greater than current
      let bestNeighbor, bestDist = steps |> List.maxBy snd
      if bestDist > currentDist then Some bestNeighbor else None
