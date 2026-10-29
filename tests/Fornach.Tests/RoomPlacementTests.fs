/// Floor layout invariants for the two rooms that matter most: the entrance chamber the player
/// spawns in, and the chamber holding the stairway. The boss must be in neither.
module Fornach.Tests.RoomPlacementTests

open Xunit
open Fornach.Domain
open Fornach.Engine

let private seeds = [ 1; 7; 4242; 99999; 31337 ]

/// Every enemy on the floor with its position and name.
let private enemies (floor: TowerFloor) =
  floor.Entities
  |> Map.toList
  |> List.choose (fun (pt, entity) ->
    match entity with
    | EntityEnemy combatant -> Some(pt, combatant.Name)
    | _ -> None)

let private isStoryBoss (name: string) =
  name = "Bandit Leader" || name.StartsWith("Aspect of", System.StringComparison.Ordinal)

[<Theory>]
[<InlineData(true)>]
[<InlineData(false)>]
let ``no enemy spawns in the entrance room or on the stairway`` (isStory: bool) =
  for seed in seeds do
    for floorNumber in 1..8 do
      let floor = TowerGenerator.generateFloorWithMode seed floorNumber isStory
      for (pt, name) in enemies floor do
        // Anchors sit on room centres, so "the guardian is in the entrance room" and "the guardian
        // is on the spawn tile" are the same statement — which is why the tile check is enough.
        Assert.False(
          (pt = floor.SpawnLocation),
          $"seed {seed} floor {floorNumber}: {name} spawns in the entrance room"
        )
        Assert.False(
          (pt = floor.StairwayLocation),
          $"seed {seed} floor {floorNumber}: {name} stands on the stairway"
        )

[<Fact>]
let ``the story boss is placed away from both anchors on every stage`` () =
  for seed in seeds do
    for floorNumber in 1..7 do
      let floor = TowerGenerator.generateFloorWithMode seed floorNumber true
      let bosses = enemies floor |> List.filter (snd >> isStoryBoss)
      Assert.True((bosses.Length > 0), $"seed {seed} floor {floorNumber}: no story boss present")
      for (pt, name) in bosses do
        Assert.True(
          (pt <> floor.SpawnLocation),
          $"seed {seed} floor {floorNumber}: {name} spawns in the entrance room"
        )
        Assert.True(
          (pt <> floor.StairwayLocation),
          $"seed {seed} floor {floorNumber}: {name} stands on the stairway"
        )

[<Fact>]
let ``the entrance room and the stairway are different rooms`` () =
  for seed in seeds do
    for floorNumber in 1..8 do
      let floor = TowerGenerator.generateFloorWithMode seed floorNumber false
      Assert.True(
        (floor.SpawnLocation <> floor.StairwayLocation),
        $"seed {seed} floor {floorNumber}: stairway shares the entrance room"
      )

/// The guardian path has always clamped its template to the bestiary magnitude this stage's
/// template ladder assumes. The roaming mobs did not, so story floor 5 placed a level-175 Storm Drake
/// three tiles from the spawn — a GrandMaster template with `EtherealCarapace 0.30` in the entrance
/// chamber.
[<Fact>]
let ``no roaming mob on a story stage is authored above its progression ceiling`` () =
  for seed in seeds do
    for floorNumber in 1..7 do
      let floor = TowerGenerator.generateFloorWithMode seed floorNumber true
      let ceiling = ProgressionScale.stageTemplateCeiling floorNumber

      for (pt, name) in enemies floor do
        if not (isStoryBoss name) then
          let level =
            match Map.tryFind pt floor.Entities with
            | Some (EntityEnemy e) -> e.Combatant.Level
            | _ -> 0

          Assert.True(
            (level <= ceiling),
            $"seed {seed} floor {floorNumber}: {name} ({level}) exceeds the stage ceiling {ceiling}"
          )
