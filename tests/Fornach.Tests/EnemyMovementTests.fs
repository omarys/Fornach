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

/// Movement tests measure geometry, not pace: the pursuer's Finesse is set to the player's 60 so the
/// chase keeps its historical one tile per turn. Pace itself is pinned in PursuitPaceTests.
let private makeEnemy (id: string) (name: string) : TowerEnemy =
  let combatant = TierFactory.createClassLevel CharacterClass.Warrior 5
  { Id = id
    Name = name
    Combatant = { combatant with Stats = combatant.Stats.With(StatId.Finesse, 60) }
    DropsKeyId = None
    IsDefeated = false
    ChaseCharge = 0.0 }

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
    Consumables = []
    UnidentifiedConsumables = Set.empty
    DiscoveredEchoes = Set.empty
    IsStoryMode = false
    SafeStepsSinceHone = 0 }

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
let ``aggroed enemy off the player's row pursues`` () =
  // Regression: a non-horizontal Bresenham line used to spin forever inside Los.check,
  // freezing the game the moment an off-row enemy entered the aggro radius.
  let playerPos = { X = 3; Y = 8 }
  let enemyPos = { X = 7; Y = 5 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ enemyPos, EntityEnemy enemy ])

  let next, _ = TowerSession.stepEnemies state
  let newPos = positionOf enemy.Id next

  Assert.True(newPos.IsSome)
  Assert.True(Distance.chebyshev newPos.Value playerPos < Distance.chebyshev enemyPos playerPos)
  Assert.Equal(3, Distance.chebyshev newPos.Value playerPos)

[<Fact>]
let ``aggroed enemy on the player's row advances straight without sidestepping`` () =
  // Diagonal steps cost more than cardinal ones, so a lined-up enemy walks straight
  // at the player rather than cutting diagonally off the row.
  let playerPos = { X = 3; Y = 8 }
  let enemyPos = { X = 7; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ enemyPos, EntityEnemy enemy ])

  let next, _ = TowerSession.stepEnemies state

  Assert.Equal(Some { X = 6; Y = 8 }, positionOf enemy.Id next)

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

// ── Mid-fight reinforcements ─────────────────────────────────────────────
// reinforceFromPursuit is the enemy phase run from inside an open group fight:
// pursuers keep closing and whoever reaches the player joins the fight in progress.

let private twoTilesOff (playerPos: Point) : Point = { X = playerPos.X + 3; Y = playerPos.Y }

[<Fact>]
let ``reinforceFromPursuit returns a pursuer once it closes to adjacency`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  // Chebyshev 3: two pursuit phases away from adjacency.
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ twoTilesOff playerPos, EntityEnemy enemy ])

  let afterOne, joinedOne = TowerSession.reinforceFromPursuit state []
  Assert.Empty(joinedOne)
  Assert.Equal(2, Distance.chebyshev (positionOf enemy.Id afterOne).Value playerPos)

  let afterTwo, joinedTwo = TowerSession.reinforceFromPursuit afterOne []
  Assert.Equal(1, joinedTwo.Length)
  Assert.Equal(enemy.Id, joinedTwo.Head.Id)
  // The returned state carries the joiner at the tile it joined on, not where it started.
  Assert.Equal(1, Distance.chebyshev (positionOf enemy.Id afterTwo).Value playerPos)

[<Fact>]
let ``reinforceFromPursuit returns an adjacent pursuer on the first phase`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ { X = 5; Y = 8 }, EntityEnemy enemy ])

  let _, joined = TowerSession.reinforceFromPursuit state []

  Assert.Equal(1, joined.Length)
  Assert.Equal(enemy.Id, joined.Head.Id)

[<Fact>]
let ``reinforceFromPursuit never returns an enemy beyond the aggro radius`` () =
  let playerPos = { X = 1; Y = 1 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ { X = 13; Y = 1 }, EntityEnemy enemy ])

  let mutable current = state
  let mutable joined = []
  for _ in 1..12 do
    let next, joiners = TowerSession.reinforceFromPursuit current []
    current <- next
    joined <- joined @ joiners

  Assert.Empty(joined)

[<Fact>]
let ``reinforceFromPursuit never returns an enemy without line of sight`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let tiles = openTiles 16 16 |> Map.add { X = 4; Y = 8 } Pillar
  let state = makeState playerPos tiles (Map.ofList [ { X = 5; Y = 8 }, EntityEnemy enemy ])

  let mutable current = state
  let mutable joined = []
  for _ in 1..12 do
    let next, joiners = TowerSession.reinforceFromPursuit current []
    current <- next
    joined <- joined @ joiners

  Assert.Empty(joined)
  Assert.Equal(Some { X = 5; Y = 8 }, positionOf enemy.Id current)

[<Fact>]
let ``reinforceFromPursuit does not report an enemy already in the fight`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ { X = 5; Y = 8 }, EntityEnemy enemy ])

  let _, joined = TowerSession.reinforceFromPursuit state [ enemy.Id ]

  Assert.Empty(joined)

[<Fact>]
let ``reinforceFromPursuit returns the joiner clamped to the player's level`` () =
  let playerPos = { X = 3; Y = 8 }
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ { X = 5; Y = 8 }, EntityEnemy enemy ])

  let _, joined = TowerSession.reinforceFromPursuit state []

  let expected = TierFactory.clampEngagedToPlayerLevel enemy.Combatant state.Player
  Assert.Equal(1, joined.Length)
  Assert.True(expected.Level < enemy.Combatant.Level, "the fixture enemy should sit above the engage ceiling")
  Assert.Equal(expected.Level, joined.Head.Combatant.Level)

// ── Where a joiner ends up ───────────────────────────────────────────────
// TowerEnemy carries no position: the entity map keyed by tile is authoritative, and both
// updateEnemy and resolveEnemyDefeat locate an enemy by Id. A joiner is therefore written back
// (and its kill credited) at the tile it joined on, not at the tile it spawned on.

let private joinedEnemyState () =
  let playerPos = { X = 3; Y = 8 }
  let spawn = twoTilesOff playerPos
  let enemy = makeEnemy "mob_0" "Scavenger"
  let state = makeState playerPos (openTiles 16 16) (Map.ofList [ spawn, EntityEnemy enemy ])

  let mutable current = state
  for _ in 1..2 do
    let next, _ = TowerSession.reinforceFromPursuit current []
    current <- next

  enemy, spawn, current

[<Fact>]
let ``updateEnemy writes a joiner back at the tile it joined on, not where it spawned`` () =
  let enemy, spawn, state = joinedEnemyState ()
  Assert.Equal(Some { X = 4; Y = 8 }, positionOf enemy.Id state)

  let written = TowerSession.updateEnemy { enemy with Name = "Scavenger (bloodied)" } state

  Assert.Equal(Some { X = 4; Y = 8 }, positionOf enemy.Id written)
  Assert.False(Map.containsKey spawn written.CurrentFloor.Entities)
  Assert.Equal(
    "Scavenger (bloodied)",
    match Map.find { X = 4; Y = 8 } written.CurrentFloor.Entities with
    | EntityEnemy e -> e.Name
    | _ -> ""
  )

[<Fact>]
let ``resolveEnemyDefeat credits a joiner where it joined, not where it spawned`` () =
  let enemy, spawn, state = joinedEnemyState ()

  let resolved = TowerSession.resolveEnemyDefeat enemy.Id state

  let joinedTileDefeated =
    match Map.tryFind { X = 4; Y = 8 } resolved.CurrentFloor.Entities with
    | Some(EntityEnemy e) -> e.IsDefeated
    | _ -> false

  Assert.True(joinedTileDefeated, "the joiner must be marked defeated at the tile it joined on")
  Assert.False(Map.containsKey spawn resolved.CurrentFloor.Entities)
  Assert.Contains(resolved.MessageLog, fun (m: string) -> m.Contains("XP"))
