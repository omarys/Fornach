module Fornach.Tests.LevelDesignTests

open System.Collections.Generic
open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine

let private cardinalNeighbors (p: Point) : Point[] =
  [| { X = p.X + 1; Y = p.Y }
     { X = p.X - 1; Y = p.Y }
     { X = p.X; Y = p.Y + 1 }
     { X = p.X; Y = p.Y - 1 } |]

let private reachableCardinalFromSpawn (floor: TowerFloor) : HashSet<Point> =
  let visited = HashSet<Point>()
  let q = Queue<Point>()
  visited.Add floor.SpawnLocation |> ignore
  q.Enqueue floor.SpawnLocation

  while q.Count > 0 do
    let cur = q.Dequeue()
    for n in cardinalNeighbors cur do
      if not (visited.Contains n) && floor.IsWalkableTile n then
        visited.Add n |> ignore
        q.Enqueue n

  visited

[<Fact>]
let ``floor generation is strictly deterministic for identical seeds across all 7 themes`` () =
  for floorNum in 1 .. 7 do
    let a = TowerGenerator.generateFloorWithMode 4242 floorNum false
    let b = TowerGenerator.generateFloorWithMode 4242 floorNum false
    Assert.Equal(a.Theme, b.Theme)
    Assert.Equal(a.SpawnLocation, b.SpawnLocation)
    Assert.Equal(a.StairwayLocation, b.StairwayLocation)
    Assert.Equal(a.Tiles.Count, b.Tiles.Count)
    Assert.Equal(a.Entities.Count, b.Entities.Count)
    for kv in a.Tiles do
      Assert.Equal(kv.Value, b.Tiles.[kv.Key])

[<Fact>]
let ``every walkable tile, room, alcove, stairway, and entity is 4-way reachable from spawn across floors 1..12`` () =
  for isStory in [ true; false ] do
    for floorNum in 1 .. 12 do
      for seed in [ 1; 7; 42; 4242; 9001; 31337 ] do
        let floor = TowerGenerator.generateFloorWithMode seed floorNum isStory
        let reachable = reachableCardinalFromSpawn floor

        Assert.True(
          reachable.Contains floor.StairwayLocation,
          sprintf "Floor %d (story=%b, seed=%d, %A): stairway %A unreachable via 4-way movement from spawn %A" floorNum isStory seed floor.Theme floor.StairwayLocation floor.SpawnLocation
        )

        for kv in floor.Entities do
          let pt = kv.Key
          Assert.True(
            reachable.Contains pt,
            sprintf "Floor %d (story=%b, seed=%d, %A): entity at %A unreachable via 4-way movement from spawn %A" floorNum isStory seed floor.Theme pt floor.SpawnLocation
          )

        for kv in floor.Tiles do
          if kv.Value.IsWalkable then
            Assert.True(
              reachable.Contains kv.Key,
              sprintf "Floor %d (story=%b, seed=%d, %A): walkable tile %A (%A) is isolated by pillars/chasms from spawn %A" floorNum isStory seed floor.Theme kv.Key kv.Value floor.SpawnLocation
            )

[<Fact>]
let ``archways are placed on chamber thresholds rather than on spawn or stairway anchors`` () =
  for floorNum in 1 .. 7 do
    let floor = TowerGenerator.generateFloorWithMode 777 floorNum false
    let archways =
      floor.Tiles
      |> Map.toList
      |> List.choose (fun (pt, t) -> match t with Archway -> Some pt | _ -> None)

    Assert.True(archways.Length >= 4, sprintf "Floor %d (%A) should have perimeter threshold archways, got %d" floorNum floor.Theme archways.Length)
    Assert.DoesNotContain(floor.SpawnLocation, archways)
    Assert.DoesNotContain(floor.StairwayLocation, archways)

[<Fact>]
let ``all 7 themes generate rich architectural features (pillars, hazards, and alcove rewards)`` () =
  for floorNum in 1 .. 7 do
    let floor = TowerGenerator.generateFloorWithMode 9001 floorNum false
    let pillarCount =
      floor.Tiles |> Map.values |> Seq.filter (function Pillar -> true | _ -> false) |> Seq.length
    let hazardCount =
      floor.Tiles |> Map.values |> Seq.filter (function Hazard _ -> true | _ -> false) |> Seq.length
    let chestCount =
      floor.Entities |> Map.values |> Seq.filter (function EntityChest _ -> true | _ -> false) |> Seq.length

    Assert.True(pillarCount >= 20, sprintf "Floor %d (%A) expected >= 20 pillars, got %d" floorNum floor.Theme pillarCount)
    Assert.True(hazardCount >= 3, sprintf "Floor %d (%A) expected >= 3 hazard tiles, got %d" floorNum floor.Theme hazardCount)
    Assert.True(chestCount >= 1, sprintf "Floor %d (%A) expected at least 1 chest, got %d" floorNum floor.Theme chestCount)

[<Fact>]
let ``all enemies spawn on walkable floor tiles and never on pillars or chasms`` () =
  for floorNum in 1 .. 7 do
    let floor = TowerGenerator.generateFloorWithMode 1234 floorNum false
    for kv in floor.Entities do
      match kv.Value with
      | EntityEnemy enemy ->
        let tile = floor.Tiles.[kv.Key]
        Assert.True(
          tile.IsWalkable,
          sprintf "Floor %d: enemy %s at %A spawned on non-walkable tile %A" floorNum enemy.Name kv.Key tile
        )
      | _ -> ()
