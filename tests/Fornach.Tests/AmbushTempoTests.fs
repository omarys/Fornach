module Fornach.Tests.AmbushTempoTests

open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine

let private combatant name =
  let stats =
    StatBlock.Create
      [ StatId.Force, 100
        StatId.Fortitude, 100
        StatId.Finesse, 100
        StatId.Reflex, 100
        StatId.Prowess, 100
        StatId.Poise, 100 ]

  Combatant.create (CombatantId.New()) name 400 400 stats

let private makeEnemy (id: string) : TowerEnemy =
  { Id = id
    Name = "Pack Hound"
    Combatant = combatant "Pack Hound"
    DropsKeyId = None
    IsDefeated = false
    ChaseCharge = 0.0 }

/// A one-tile-wide east-west corridor on row 4: nothing off the row has line of sight to the player.
let private corridor : Map<Point, TowerTile> =
  [ for x in 0 .. 19 do
      for y in 0 .. 8 ->
        { X = x; Y = y }, (if y = 4 then Floor SurfaceType.PavedStone else Pillar) ]
  |> Map.ofList

/// An open 20x9 room: every surrounding tile of a body in the middle is floor.
let private openRoom : Map<Point, TowerTile> =
  [ for x in 0 .. 19 do
      for y in 0 .. 8 -> { X = x; Y = y }, Floor SurfaceType.PavedStone ]
  |> Map.ofList

let private makeState (tiles: Map<Point, TowerTile>) (playerPos: Point) (entities: Map<Point, TowerEntity>) : TowerRunState =
  let floor =
    { FloorNumber = 1
      Theme = FloorTheme.QuarryPlazas
      Width = 20
      Height = 9
      Tiles = tiles
      Entities = entities
      StairwayLocation = { X = 0; Y = 0 }
      SpawnLocation = playerPos
      Explored = Set.empty
      Visible = Set.empty
      SeenEnemies = Set.empty
      ActiveQuests = [] }

  { Player = combatant "Delver"
    CurrentFloor = floor
    PlayerPosition = playerPos
    Facing = Some Direction.East
    Locomotion = LocomotionState.Walking
    CollectedKeys = Set.empty
    CompletedQuests = Set.empty
    InventoryItems = []
    MessageLog = []
    Seed = 42
    Souls = 0
    Trophies = []
    Consumables = []
    UnidentifiedConsumables = Set.empty
    DiscoveredEchoes = Set.empty
    IsStoryMode = false
    SafeStepsSinceHone = 0 }

let private foeAt (id: string) (pt: Point) : Map<Point, TowerEntity> =
  Map.ofList [ pt, EntityEnemy(makeEnemy id) ]

[<Fact>]
let ``a foe the player never saw takes the Vor and the player opens in the Nach`` () =
  let playerPos = { X = 10; Y = 4 }
  let state = makeState openRoom playerPos (foeAt "mob_0" { X = 11; Y = 4 })

  let after, events = TowerSession.stepPlayer Direction.East state

  let engaged =
    events
    |> List.pick (function
      | TowerEvent.CombatTriggered enemy -> Some enemy
      | _ -> None)

  Assert.Equal(TacticalTiming.Vor, engaged.Combatant.Tempo)
  Assert.Equal(TacticalTiming.Nach, after.Player.Tempo)
  Assert.Contains("Nach", after.MessageLog.Head) |> ignore

[<Fact>]
let ``a foe already in view meets a player who still holds the Vor`` () =
  let playerPos = { X = 10; Y = 4 }
  let state = makeState openRoom playerPos (foeAt "mob_0" { X = 11; Y = 4 })
  let alerted = { state with CurrentFloor = { state.CurrentFloor with SeenEnemies = Set.singleton "mob_0" } }

  let after, events = TowerSession.stepPlayer Direction.East alerted

  let engaged =
    events
    |> List.pick (function
      | TowerEvent.CombatTriggered enemy -> Some enemy
      | _ -> None)

  Assert.Equal(TacticalTiming.Vor, after.Player.Tempo)
  Assert.Equal(TacticalTiming.Vor, engaged.Combatant.Tempo)

[<Fact>]
let ``a pack of unseen foes closing on the player also opens in the Nach`` () =
  let playerPos = { X = 10; Y = 4 }
  let entities = Map.ofList [ { X = 11; Y = 4 }, EntityEnemy(makeEnemy "mob_0") ]
  let state = makeState openRoom playerPos entities

  let after, events = TowerSession.stepEnemies state

  let pack =
    events
    |> List.pick (function
      | TowerEvent.GroupCombatTriggered enemies -> Some enemies
      | _ -> None)

  // One foe springs the ambush and carries the initiative; the rest of the pack piles on from the Nach.
  Assert.Single(pack |> List.filter (fun e -> e.Combatant.Tempo = TacticalTiming.Vor)) |> ignore
  Assert.Equal(TacticalTiming.Nach, after.Player.Tempo)

[<Fact>]
let ``only one foe of an unseen pack carries the Vor`` () =
  let playerPos = { X = 10; Y = 4 }

  let entities =
    Map.ofList
      [ { X = 11; Y = 4 }, EntityEnemy(makeEnemy "mob_0")
        { X = 9; Y = 4 }, EntityEnemy(makeEnemy "mob_1") ]

  let state = makeState openRoom playerPos entities
  let after, events = TowerSession.stepEnemies state

  let pack =
    events
    |> List.pick (function
      | TowerEvent.GroupCombatTriggered enemies -> Some enemies
      | _ -> None)

  // Handing the Vor to every unseen foe let each one "insist" on it (resolveLandedTempo), so a player who
  // seized it back lost it again to the next foe in the same phase.
  Assert.Equal(1, pack |> List.filter (fun e -> e.Combatant.Tempo = TacticalTiming.Vor) |> List.length)
  Assert.Equal(pack.Length - 1, pack |> List.filter (fun e -> e.Combatant.Tempo = TacticalTiming.Nach) |> List.length)
  Assert.Equal(TacticalTiming.Nach, after.Player.Tempo)

[<Fact>]
let ``the field of view remembers the foes in front of the player and not the ones behind`` () =
  let playerPos = { X = 10; Y = 4 }

  let entities =
    Map.ofList
      [ { X = 12; Y = 4 }, EntityEnemy(makeEnemy "mob_front")
        { X = 8; Y = 4 }, EntityEnemy(makeEnemy "mob_behind") ]

  let seen = TowerSession.updateFov (makeState openRoom playerPos entities)

  Assert.Contains("mob_front", seen.CurrentFloor.SeenEnemies)
  Assert.DoesNotContain("mob_behind", seen.CurrentFloor.SeenEnemies)
