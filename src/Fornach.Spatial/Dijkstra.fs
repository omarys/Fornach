namespace Fornach.Spatial

open System.Collections.Generic

[<RequireQualifiedAccess>]
module Dijkstra =
  /// Computes a distance map from one or more goal points radiating outward up to maxDistance.
  /// Returns a Map<Point, int> where the integer represents step distance from the nearest goal.
  let compute (isPassable: Point -> bool) (goals: Point list) (maxDistance: int) : Map<Point, int> =
    let distances = Dictionary<Point, int>()
    let queue = Queue<struct(Point * int)>()

    // Initialize all unique valid goal tiles with distance 0
    for goal in goals do
      if isPassable goal && distances.TryAdd(goal, 0) then
        queue.Enqueue(struct(goal, 0))

    let offsets = Point.allNeighborOffsets

    // Breadth-first flood fill
    while queue.Count > 0 do
      let struct(current, dist) = queue.Dequeue()

      if dist < maxDistance then
        let nextDist = dist + 1

        for i = 0 to offsets.Length - 1 do
          let struct(dx, dy) = offsets.[i]
          let neighbor = { X = current.X + dx; Y = current.Y + dy }

          if not (distances.ContainsKey neighbor) && isPassable neighbor then
            distances.[neighbor] <- nextDist
            queue.Enqueue(struct(neighbor, nextDist))

    // Convert to immutable F# Map
    distances |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq

  /// Given a Dijkstra map and a current position, returns the neighboring tile
  /// that moves closer to the goal (lowest distance).
  let nextStepTowards (map: Map<Point, int>) (current: Point) : Point option =
    let offsets = Point.allNeighborOffsets
    let mutable found = false
    let mutable bestNeighbor = current
    let mutable bestDist = System.Int32.MaxValue

    for i = 0 to offsets.Length - 1 do
      let struct(dx, dy) = offsets.[i]
      let neighbor = { X = current.X + dx; Y = current.Y + dy }

      match map.TryFind neighbor with
      | Some dist when not found || dist < bestDist ->
        found <- true
        bestNeighbor <- neighbor
        bestDist <- dist
      | _ -> ()

    if not found then
      None
    else
      let currentDist = map.TryFind current |> Option.defaultValue System.Int32.MaxValue
      if bestDist < currentDist then Some bestNeighbor else None

  /// Given a Dijkstra map and a current position, returns the neighboring tile
  /// that flees away from the goal (highest distance).
  let nextStepAway (map: Map<Point, int>) (current: Point) : Point option =
    let offsets = Point.allNeighborOffsets
    let mutable found = false
    let mutable bestNeighbor = current
    let mutable bestDist = System.Int32.MinValue

    for i = 0 to offsets.Length - 1 do
      let struct(dx, dy) = offsets.[i]
      let neighbor = { X = current.X + dx; Y = current.Y + dy }

      match map.TryFind neighbor with
      | Some dist when not found || dist > bestDist ->
        found <- true
        bestNeighbor <- neighbor
        bestDist <- dist
      | _ -> ()

    if not found then
      None
    else
      let currentDist = map.TryFind current |> Option.defaultValue 0
      if bestDist > currentDist then Some bestNeighbor else None
