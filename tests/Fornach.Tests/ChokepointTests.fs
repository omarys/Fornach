module Fornach.Tests.ChokepointTests

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

/// A one-tile-wide east-west corridor on row 4, walled in by pillars: only the tiles ahead of and
/// behind a body standing in it are reachable.
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

/// The enemies a fight would start with: the engine raises GroupCombatTriggered with exactly the
/// participants it hands to the duel, so the event is the list to measure.
let private engagedBy (state: TowerRunState) : TowerEnemy list =
  let _, events = TowerSession.stepEnemies state

  events
  |> List.collect (function
    | TowerEvent.GroupCombatTriggered enemies -> enemies
    | _ -> [])

let private queued (ids: string list) (from: Point) (dx: int) : Map<Point, TowerEntity> =
  ids
  |> List.mapi (fun i id -> { X = from.X + dx * (i + 1); Y = from.Y }, EntityEnemy (makeEnemy id))
  |> Map.ofList

[<Fact>]
let ``a corridor queued from one side engages only the enemy at its mouth`` () =
  let playerPos = { X = 10; Y = 4 }
  let state = makeState corridor playerPos (queued [ "mob_0"; "mob_1"; "mob_2" ] playerPos 1)

  let engaged = engagedBy state

  // One tile of the corridor touches the player, so the queue behind it is waiting its turn.
  Assert.Equal(1, engaged.Length)
  Assert.Equal("mob_0", engaged.Head.Id)

  // Nor can the queue overflow the mouth over time: it stays one.
  let mutable current = state
  for _ in 1..5 do
    current <- fst (TowerSession.stepEnemies current)
    Assert.Equal(1, (engagedBy current).Length)

[<Fact>]
let ``a corridor queued from both ends engages at most two, one per end`` () =
  let playerPos = { X = 10; Y = 4 }
  let east = queued [ "east_0"; "east_1" ] playerPos 1
  let west = queued [ "west_0"; "west_1" ] playerPos -1
  let state = makeState corridor playerPos (Map.fold (fun acc pt ent -> Map.add pt ent acc) east west)

  let engaged = engagedBy state

  Assert.True(engaged.Length <= 4, sprintf "%d enemies engaged in a one-wide corridor" engaged.Length)
  // Exactly one tile per end is adjacent, so both ends together still yield two attackers.
  Assert.Equal(2, engaged.Length)
  Assert.Contains(engaged, fun e -> e.Id = "east_0")
  Assert.Contains(engaged, fun e -> e.Id = "west_0")
  Assert.DoesNotContain(engaged, fun e -> e.Id = "east_1")

[<Fact>]
let ``a corridor queue cannot feed reinforcements past the one at its mouth`` () =
  let playerPos = { X = 10; Y = 4 }
  let state = makeState corridor playerPos (queued [ "mob_0"; "mob_1"; "mob_2" ] playerPos 1)

  let mutable current = state
  let mutable joined = []

  // mob_0 opened the fight; the three behind it cannot overtake it to join.
  for _ in 1..8 do
    let advanced, joiners = TowerSession.reinforceFromPursuit current [ "mob_0" ]
    current <- advanced
    joined <- joined @ joiners

  Assert.Empty(joined)
  Assert.Equal(1, (TowerSession.adjacentEnemies current).Length)

[<Fact>]
let ``an open room lets more than two enemies engage at once`` () =
  // The cap is geometric, not a global limit: in the open the whole pack reaches the player.
  let playerPos = { X = 10; Y = 4 }
  let entities =
    [ for dx in -1 .. 1 do
        for dy in -1 .. 1 do
          if dx <> 0 || dy <> 0 then
            { X = playerPos.X + dx; Y = playerPos.Y + dy }, EntityEnemy (makeEnemy (sprintf "mob_%d_%d" dx dy)) ]
    |> Map.ofList

  let state = makeState openRoom playerPos entities
  let engaged = engagedBy state

  Assert.Equal(8, engaged.Length)
  Assert.True(engaged.Length > 2, "an open room must not be capped at the corridor limit")
