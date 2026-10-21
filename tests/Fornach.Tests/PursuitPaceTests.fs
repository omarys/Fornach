module Fornach.Tests.PursuitPaceTests

open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine

/// Everything except Finesse is held constant, so the only thing that separates these combatants is pace.
let private combatant name finesse =
  let stats =
    StatBlock.Create
      [ StatId.Force, 100
        StatId.Fortitude, 100
        StatId.Finesse, finesse
        StatId.Reflex, 100
        StatId.Prowess, 100
        StatId.Poise, 100 ]

  Combatant.create (CombatantId.New()) name 400 400 stats

let private makeEnemy (id: string) (finesse: int) : TowerEnemy =
  { Id = id
    Name = "Pursuer"
    Combatant = combatant "Pursuer" finesse
    DropsKeyId = None
    IsDefeated = false
    ChaseCharge = 0.0 }

/// A featureless strip of floor: no walls, so every measurement here is pure pace.
let private strip : Map<Point, TowerTile> =
  [ for x in 0 .. 39 do
      for y in 0 .. 8 -> { X = x; Y = y }, Floor SurfaceType.PavedStone ]
  |> Map.ofList

let private makeState (playerFinesse: int) (playerPos: Point) (entities: Map<Point, TowerEntity>) : TowerRunState =
  let floor =
    { FloorNumber = 1
      Theme = FloorTheme.QuarryPlazas
      Width = 40
      Height = 9
      Tiles = strip
      Entities = entities
      StairwayLocation = { X = 0; Y = 0 }
      SpawnLocation = playerPos
      Explored = Set.empty
      Visible = Set.empty
      ActiveQuests = [] }

  { Player = combatant "Runner" playerFinesse
    CurrentFloor = floor
    PlayerPosition = playerPos
    Facing = Some Direction.West
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

let private positionOf (enemyId: string) (state: TowerRunState) : Point =
  state.CurrentFloor.Entities
  |> Map.toList
  |> List.pick (fun (p, ent) ->
    match ent with
    | EntityEnemy e when e.Id = enemyId -> Some p
    | _ -> None)

/// The player always moves exactly one tile per keypress; fleeing is just that, in a straight line.
let private fleeTurn (state: TowerRunState) : TowerRunState =
  let stepped = { state with PlayerPosition = { state.PlayerPosition with X = state.PlayerPosition.X - 1 } }
  fst (TowerSession.stepEnemies stepped)

[<Fact>]
let ``pace is one tile per turn at parity and clamps at 0.4 and 1.6`` () =
  Assert.Equal(1.0, TowerSession.paceOf 120 120)
  // 200 points of gap is the full swing of the model, so the clamps saturate there.
  Assert.Equal(0.6, TowerSession.paceOf 200 120, 9)
  Assert.Equal(1.4, TowerSession.paceOf 120 200, 9)
  Assert.Equal(0.4, TowerSession.paceOf 400 60, 9)
  Assert.Equal(1.6, TowerSession.paceOf 60 400, 9)

[<Fact>]
let ``equal Finesse still moves each pursuer exactly one tile per turn`` () =
  // Feature A must leave the historical chase untouched at parity.
  let playerPos = { X = 10; Y = 4 }
  let nearEnemy = makeEnemy "mob_a" 120
  let farEnemy = makeEnemy "mob_b" 120
  let entities = Map.ofList [ { X = 14; Y = 4 }, EntityEnemy nearEnemy; { X = 16; Y = 4 }, EntityEnemy farEnemy ]
  let mutable state = makeState 120 playerPos entities

  for turn in 1..3 do
    state <- fst (TowerSession.stepEnemies state)
    Assert.Equal(4 - turn, Distance.chebyshev playerPos (positionOf nearEnemy.Id state))
    Assert.Equal(6 - turn, Distance.chebyshev playerPos (positionOf farEnemy.Id state))

[<Fact>]
let ``a nimble player outruns a slower pursuer`` () =
  // Finesse 300 against 60 pins the pursuer at the 0.4 floor: two steps in six turns.
  let playerPos = { X = 24; Y = 4 }
  let enemyPos = { X = 27; Y = 4 }
  let enemy = makeEnemy "mob_0" 60
  let start = makeState 300 playerPos (Map.ofList [ enemyPos, EntityEnemy enemy ])
  let startGap = Distance.chebyshev playerPos enemyPos

  let mutable state = start
  for _ in 1..6 do
    state <- fleeTurn state

  let endGap = Distance.chebyshev state.PlayerPosition (positionOf enemy.Id state)
  Assert.Equal(startGap + 4, endGap)

[<Fact>]
let ``a clumsy player is run down`` () =
  // Finesse 60 against 400 pins the pursuer at the 1.6 ceiling: it gains a tile on most turns.
  let playerPos = { X = 20; Y = 4 }
  let enemy = makeEnemy "mob_0" 400
  let mutable state = makeState 60 playerPos (Map.ofList [ { X = 24; Y = 4 }, EntityEnemy enemy ])

  // Fleeing does not shake it: the gap closes even though the player never stops running.
  for _ in 1..3 do
    state <- fleeTurn state

  Assert.True(Distance.chebyshev state.PlayerPosition (positionOf enemy.Id state) < 4)

  // Once the player has to stop it closes to arm's length.
  for _ in 1..3 do
    state <- fst (TowerSession.stepEnemies state)

  Assert.Equal(1, Distance.chebyshev state.PlayerPosition (positionOf enemy.Id state))

[<Fact>]
let ``a pace above one covers more ground than a pursuer at parity`` () =
  let playerPos = { X = 30; Y = 4 }

  let travelled (enemyFinesse: int) =
    let enemy = makeEnemy "mob_0" enemyFinesse
    let mutable state = makeState 60 playerPos (Map.ofList [ { X = 38; Y = 4 }, EntityEnemy enemy ])

    // Stand still so the pursuer is never adjacent and the only variable is pace.
    for _ in 1..4 do
      state <- fst (TowerSession.stepEnemies state)

    let startX = 38
    startX - (positionOf enemy.Id state).X

  Assert.Equal(4, travelled 60) // pace pinned to 1.0 by the ceiling: one tile per turn
  Assert.True(travelled 400 > travelled 60) // pace 1.6 must actually buy ground

[<Fact>]
let ``a long adjacent hold does not bank into a burst`` () =
  let playerPos = { X = 10; Y = 4 }
  let enemyPos = { X = 9; Y = 4 }
  let enemy = makeEnemy "mob_0" 400
  let mutable state = makeState 60 playerPos (Map.ofList [ enemyPos, EntityEnemy enemy ])

  // Twenty turns pinned against the player, with nowhere to step: no charge is banked while engaged.
  for _ in 1..20 do
    state <- fst (TowerSession.stepEnemies state)

  Assert.Equal(enemyPos, positionOf enemy.Id state)

  // Two tiles of daylight open up in a single turn; one step is affordable from a standing start.
  let escaped = { state with PlayerPosition = { X = 12; Y = 4 } }
  let after = fst (TowerSession.stepEnemies escaped)

  Assert.Equal({ X = 10; Y = 4 }, positionOf enemy.Id after)
  Assert.Equal(2, Distance.chebyshev escaped.PlayerPosition (positionOf enemy.Id after))
