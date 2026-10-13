module Fornach.Tests.EnemyMovementTests

open System
open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine

let private createPlayer () =
  let stats =
    StatBlock.Create
      [ StatId.Force, 60
        StatId.Fortitude, 60
        StatId.Finesse, 60
        StatId.Reflex, 60
        StatId.Prowess, 60
        StatId.Poise, 60 ]

  Combatant.create (CombatantId.New()) "Explorer" 200 200 stats

/// A fully walkable width x height floor with no obstructions.
let private openTiles (w: int) (h: int) : Map<Point, TowerTile> =
  [ for x in 0 .. w - 1 do
      for y in 0 .. h - 1 -> { X = x; Y = y }, Floor SurfaceType.PavedStone ]
  |> Map.ofList

let private makeEnemy (id: string) (name: string) : TowerEnemy =
  { Id = id
    Name = name
    Combatant = TierFactory.createClassLevel CharacterClass.Warrior 5
    DropsKeyId = None
    IsDefeated = false }

let private makeState
  (playerPos: Point)
  (tiles: Map<Point, TowerTile>)
  (entities: Map<Point, TowerEntity>)
  : TowerRunState =
  let floor =
    { FloorNumber = 1
      Theme = FloorTheme.QuarryPlazas
      Width = 16
      Height = 16
      Tiles = tiles
      Entities = entities
      StairwayLocation = { X = 0; Y = 0 }
      SpawnLocation = playerPos
      Explored = Set.empty
      Visible = Set.empty
      ActiveQuests = [] }

  { Player = createPlayer ()
    CurrentFloor = floor
    PlayerPosition = playerPos
    Facing = Some Direction.North
    Locomotion = LocomotionState.Walking
    CollectedKeys = Set.empty
    CompletedQuests = Set.empty
    InventoryItems = []
    MessageLog = []
    Seed = 42
    Souls = 0
    Trophies = []
    DiscoveredEchoes = Set.empty
    IsStoryMode = false }

let private positionOf (enemyId: string) (state: TowerRunState) : Point option =
  state.CurrentFloor.Entities
  |> Map.toList
  |> List.tryPick (fun (p, ent) ->
    match ent with
    | EntityEnemy e when e.Id = enemyId -> Some p
    | _ -> None)

[<Fact>]
let ``aggroed enemy with clear line of sight advances exactly one tile toward the player`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemyPos = { X = 7; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ enemyPos, EntityEnemy enemy ])

  let next, _ = TowerSession.stepEnemies state
  let newPos = positionOf enemy.Id next

  Assert.True(newPos.IsSome, "the enemy should still exist after the enemy phase")
  Assert.NotEqual(enemyPos, newPos.Value)
  // Started at Chebyshev distance 4; a single pursuit step closes it to 3.
  Assert.Equal(3, Distance.chebyshev newPos.Value playerPos)

[<Fact>]
let ``enemy with blocked line of sight does not pursue`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemyPos = { X = 7; Y = 8 }
  // A pillar on the straight line between the enemy and the player blocks line of sight.
  let tiles = openTiles 16 16 |> Map.add { X = 5; Y = 8 } Pillar
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos tiles (Map.ofList [ enemyPos, EntityEnemy enemy ])

  let next, events = TowerSession.stepEnemies state

  Assert.Equal(Some enemyPos, positionOf enemy.Id next)
  Assert.Empty(events)

[<Fact>]
let ``enemy beyond the aggro radius does not pursue`` () =
  let playerPos = { X = 0; Y = 0 }
  let enemyPos = { X = 12; Y = 0 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ enemyPos, EntityEnemy enemy ])

  let next, _ = TowerSession.stepEnemies state

  Assert.Equal(Some enemyPos, positionOf enemy.Id next)

[<Fact>]
let ``boss enemy never moves even while aggroed`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemyPos = { X = 7; Y = 8 }
  let enemy = makeEnemy "boss_aspect" "Aspect of Denial"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ enemyPos, EntityEnemy enemy ])

  // Confirm the boss is in fact aggroed, so the stationary guard is what stops it.
  Assert.Contains(TowerSession.enemiesAggroed state, fun e -> e.Id = "boss_aspect")

  let next, _ = TowerSession.stepEnemies state

  Assert.Equal(Some enemyPos, positionOf enemy.Id next)

[<Fact>]
let ``adjacent enemy yields a group combat event listing it`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemyPos = { X = 4; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ enemyPos, EntityEnemy enemy ])

  let _, events = TowerSession.stepEnemies state

  let listed =
    events
    |> List.exists (function
      | TowerEvent.GroupCombatTriggered enemies -> enemies |> List.exists (fun e -> e.Id = enemy.Id)
      | _ -> false)

  Assert.True(listed, "an adjacent enemy should raise a GroupCombatTriggered event listing it")

[<Fact>]
let ``canEscape is false when every neighbouring tile is blocked`` () =
  let playerPos = { X = 8; Y = 8 }
  let tiles =
    Point.allNeighbors playerPos
    |> List.fold (fun (m: Map<Point, TowerTile>) p -> Map.add p Chasm m) (openTiles 16 16)
  let state = makeState playerPos tiles Map.empty

  Assert.False(TowerSession.canEscape state)

[<Fact>]
let ``canEscape is true when a neighbouring tile is open`` () =
  let playerPos = { X = 8; Y = 8 }
  let state = makeState playerPos (openTiles 16 16) Map.empty

  Assert.True(TowerSession.canEscape state)

[<Fact>]
let ``isSafe is false when an enemy is adjacent`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ { X = 4; Y = 8 }, EntityEnemy enemy ])

  Assert.False(TowerSession.isSafe state)

[<Fact>]
let ``isSafe is false when an enemy is aggroed at range`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ { X = 7; Y = 8 }, EntityEnemy enemy ])

  Assert.False(TowerSession.isSafe state)

[<Fact>]
let ``isSafe is true when no enemy is within aggro range`` () =
  let playerPos = { X = 0; Y = 0 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ { X = 12; Y = 0 }, EntityEnemy enemy ])

  Assert.True(TowerSession.isSafe state)

[<Fact>]
let ``isSafe is true when an in-range enemy has no line of sight`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let tiles = openTiles 16 16 |> Map.add { X = 5; Y = 8 } Pillar
  let state = makeState playerPos tiles (Map.ofList [ { X = 7; Y = 8 }, EntityEnemy enemy ])

  Assert.True(TowerSession.isSafe state)

[<Fact>]
let ``combat condition restore discards in-fight armor and weapon wear`` () =
  let player = createPlayer ()
  let worn =
    { player with
        Armor = player.Armor.Shred 25
        WeaponCondition = WeaponCondition.Broken }

  let armor, weapon = TowerSession.combatCondition player
  let restored = TowerSession.restoreCombatCondition armor weapon worn

  Assert.Equal(player.Armor.Current, restored.Armor.Current)
  Assert.Equal(WeaponCondition.Pristine, restored.WeaponCondition)

[<Fact>]
let ``combat condition restore keeps armor wear taken before the fight`` () =
  let player = createPlayer ()
  // Environmental damage (e.g. a trap) before combat must survive the post-combat restore.
  let trapWorn = { player with Armor = player.Armor.Shred 30 }
  let worn = { trapWorn with Armor = trapWorn.Armor.Shred 20; WeaponCondition = WeaponCondition.Damaged }

  let armor, weapon = TowerSession.combatCondition trapWorn
  let restored = TowerSession.restoreCombatCondition armor weapon worn

  Assert.Equal(trapWorn.Armor.Current, restored.Armor.Current)
  Assert.Equal(WeaponCondition.Pristine, restored.WeaponCondition)
