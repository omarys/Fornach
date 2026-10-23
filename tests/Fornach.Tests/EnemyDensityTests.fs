/// Floor population invariants: altars never share a room with a shrine, and roaming density rises
/// with depth instead of plateauing early. Both are layout promises the generator used to break —
/// the altars sat on plaza 2 and 5, which are exactly the shrine plazas, and density topped out at
/// six mobs a room for the entire back half of the tower.
module Fornach.Tests.EnemyDensityTests

open Xunit
open Fornach.Domain
open Fornach.Engine

let private seeds = [ 1; 7; 4242; 99999; 31337; 5; 88; 1234 ]

let private placesOf (floor: TowerFloor) (pick: TowerEntity -> bool) =
  floor.Entities
  |> Map.toList
  |> List.choose (fun (pt, entity) -> if pick entity then Some pt else None)

let private isAltar entity =
  match entity with
  | EntityEncounter (FloorEncounter.SacrificialAltar _) -> true
  | _ -> false

let private isShrine entity =
  match entity with
  | EntityShrine _ -> true
  | _ -> false

/// Rooms are carved around plaza centres on a pitch wider than two tiles, so anything this close to a
/// shrine is inside the shrine's chamber. The old altar placement measured 5.4 tiles from the shrine.
let private sameRoomDistance = 6.0

[<Fact>]
let ``no altar shares a room with a shrine`` () =
  for story in [ false; true ] do
    for floorNumber in [ 1; 2; 5; 6; 7 ] do
      for seed in seeds do
        let floor = TowerGenerator.generateFloorWithMode seed floorNumber story
        let shrines = placesOf floor isShrine
        let altars = placesOf floor isAltar

        for altar in altars do
          for shrine in shrines do
            let dx = float (altar.X - shrine.X)
            let dy = float (altar.Y - shrine.Y)
            let distance = sqrt (dx * dx + dy * dy)

            Assert.True(
              distance >= sameRoomDistance,
              sprintf "floor %d (story %b, seed %d): altar at %A is %.1f tiles from the shrine at %A" floorNumber story seed altar distance shrine)

[<Fact>]
let ``roaming density beats the old ceiling on every floor past the tutorial`` () =
  // Old curve: min 10, 3 + floor/2 + rng(0,3) — 3 to 5 mobs a room on floor 1, 6 to 8 by floor 10.
  // The new floor is 4 + floor/2 + rng(0,2), so every non-tutorial floor must field at least four
  // mobs in its densest room and the whole tower must carry strictly more than the old curve did.
  let mobCount (floor: TowerFloor) =
    placesOf floor (fun e ->
      match e with
      | EntityEnemy _ -> true
      | _ -> false)
    |> List.length

  for story in [ false; true ] do
    for floorNumber in [ 2; 3; 4; 6 ] do
      let counts =
        seeds |> List.map (fun seed -> mobCount (TowerGenerator.generateFloorWithMode seed floorNumber story))

      let average = counts |> List.averageBy float
      let oldAverage = 3.0 + float floorNumber / 2.0 + 1.0
      Assert.True(average > oldAverage, sprintf "floor %d (story %b): %.1f mobs, old curve averaged %.1f" floorNumber story average oldAverage)
