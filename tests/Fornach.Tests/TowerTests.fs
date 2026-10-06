module Fornach.Tests.TowerTests

open System
open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine
open Fornach.Cli

let private createTestPlayer () =
  let stats =
    StatBlock.Create
      [ StatId.Force, 60
        StatId.Fortitude, 60
        StatId.Finesse, 60
        StatId.Reflex, 60
        StatId.Prowess, 60
        StatId.Poise, 60 ]

  Combatant.create (CombatantId.New()) "Explorer" 200 200 stats

[<Fact>]
let ``TowerGenerator cycles through all 7 distinct grief and transcendence biomes`` () =
  let expectedThemes =
    [ 1, FloorTheme.QuarryPlazas
      2, FloorTheme.PineCloisters
      3, FloorTheme.BasaltCalderas
      4, FloorTheme.TempestTerraces
      5, FloorTheme.SunkenBoulevards
      6, FloorTheme.ElysianSanctuaries
      7, FloorTheme.CelestialSpires
      8, FloorTheme.QuarryPlazas ]

  for (floorNum, expectedTheme) in expectedThemes do
    let floor = TowerGenerator.generateFloor 42 floorNum
    Assert.Equal(expectedTheme, floor.Theme)
    Assert.False(String.IsNullOrWhiteSpace(floor.Theme.Name))
    Assert.False(String.IsNullOrWhiteSpace(floor.Theme.Description))
    Assert.NotEqual(' ', floor.Theme.FloorGlyph)
    Assert.NotEqual(' ', floor.Theme.PillarGlyph)

[<Fact>]
let ``TowerGenerator creates expansive architectural plazas and valid A-Star path from spawn to stairs`` () =
  for floorNum in 1 .. 7 do
    let floor = TowerGenerator.generateFloor (1234 + floorNum) floorNum
    Assert.True(floor.Width >= 50)
    Assert.True(floor.Height >= 30)

    // Verify spawn and stairway locations exist and are distinct
    Assert.NotEqual(floor.SpawnLocation, floor.StairwayLocation)

    let spawnTile = Map.tryFind floor.SpawnLocation floor.Tiles
    Assert.True(spawnTile.IsSome)
    Assert.True(spawnTile.Value.IsWalkable)

    let stairTile = Map.tryFind floor.StairwayLocation floor.Tiles
    Assert.True(stairTile.IsSome)
    match stairTile.Value with
    | StairwayPortal _ -> ()
    | other -> Assert.Fail(sprintf "Expected StairwayPortal at stairway location, got %A" other)

    // Validate connectivity from spawn to stairway via A* pathfinding
    let isPassable (p: Point) =
      match Map.tryFind p floor.Tiles with
      | Some t -> t.IsWalkable
      | None -> false

    let pathOpt = Pathfinding.aStar isPassable floor.SpawnLocation floor.StairwayLocation
    Assert.True(pathOpt.IsSome, sprintf "Floor %d failed A* connectivity between spawn and stairs" floorNum)
    let path = pathOpt.Value
    Assert.NotEmpty(path)
    Assert.Equal(floor.StairwayLocation, List.last path)

[<Fact>]
let ``TowerGenerator populates floor with NPC, Shrine, Chest, and Guardian Enemy`` () =
  let floor = TowerGenerator.generateFloor 999 1
  let entities = floor.Entities |> Map.toList |> List.map snd

  let hasNpc =
    entities
    |> List.exists (function EntityNpc npc -> not (String.IsNullOrWhiteSpace npc.Name) | _ -> false)
  Assert.True(hasNpc, "Floor should contain at least one dialogue NPC")

  let hasShrine =
    entities
    |> List.exists (function EntityShrine s -> not s.IsUsed | _ -> false)
  Assert.True(hasShrine, "Floor should contain a restoration shrine")

  let hasChest =
    entities
    |> List.exists (function EntityChest c -> not c.IsOpen && c.ItemReward.IsSome | _ -> false)
  Assert.True(hasChest, "Floor should contain a relic treasure chest")

  let hasGuardian =
    entities
    |> List.exists (function EntityEnemy e -> not e.IsDefeated && e.Combatant.Health.Current > 0 | _ -> false)
  Assert.True(hasGuardian, "Floor should contain a guardian enemy")

[<Fact>]
let ``TowerSession collision blocks movement into Chasms and Pillars`` () =
  let player = createTestPlayer ()
  let state = TowerSession.initSession player 42 1

  // Find a chasm and a pillar
  let chasmPtOpt =
    state.CurrentFloor.Tiles
    |> Map.tryPick (fun pt t -> if t = Chasm then Some pt else None)

  Assert.True(chasmPtOpt.IsSome, "Floor should have chasm tiles")
  let chasmPt = chasmPtOpt.Value

  // Teleport player right next to chasm and try moving into it
  let testPos = { X = chasmPt.X - 1; Y = chasmPt.Y }
  let floorWithWalkable =
    { state.CurrentFloor with
        Tiles = Map.add testPos (Floor SurfaceType.PavedStone) state.CurrentFloor.Tiles }
  let adjacentState = { state with CurrentFloor = floorWithWalkable; PlayerPosition = testPos }

  let nextState, events = TowerSession.stepPlayer Direction.East adjacentState
  // Player position must remain unchanged
  Assert.Equal(testPos, nextState.PlayerPosition)
  Assert.Empty(events)
  Assert.Contains("Path blocked", nextState.MessageLog.Head)

[<Fact>]
let ``TowerSession stepping on environmental hazards applies appropriate effects`` () =
  let player = createTestPlayer ()
  let state = TowerSession.initSession player 42 1

  let startPos = { X = 10; Y = 10 }
  let targetPos = { X = 11; Y = 10 }

  // 1. LavaRift deals 15 HP damage
  let floorLava =
    { state.CurrentFloor with
        Tiles =
          state.CurrentFloor.Tiles
          |> Map.add startPos (Floor SurfaceType.BasaltRock)
          |> Map.add targetPos (Hazard HazardType.LavaRift) }
  let lavaState = { state with CurrentFloor = floorLava; PlayerPosition = startPos }
  let lavaResult, lavaEvents = TowerSession.stepPlayer Direction.East lavaState
  Assert.Equal(targetPos, lavaResult.PlayerPosition)
  Assert.Equal(player.Health.Current - 15, lavaResult.Player.Health.Current)
  Assert.Contains(lavaEvents, (function TowerEvent.HazardStepped(HazardType.LavaRift, _) -> true | _ -> false))

  // 2. CalmingSpores resets recklessness
  let playerWithReckless =
    { player with Meters = { player.Meters with Recklessness = Meter.Create 50 } }
  let floorSpores =
    { state.CurrentFloor with
        Tiles =
          state.CurrentFloor.Tiles
          |> Map.add startPos (Floor SurfaceType.LilyPetals)
          |> Map.add targetPos (Hazard HazardType.CalmingSpores) }
  let sporesState = { state with Player = playerWithReckless; CurrentFloor = floorSpores; PlayerPosition = startPos }
  let sporesResult, _ = TowerSession.stepPlayer Direction.East sporesState
  Assert.Equal(0, sporesResult.Player.Meters.Recklessness.Value)

[<Fact>]
let ``TowerSession bumping into chest awards relic and marks chest open`` () =
  let player = createTestPlayer ()
  let state = TowerSession.initSession player 42 1

  let chestPtOpt =
    state.CurrentFloor.Entities
    |> Map.tryPick (fun pt ent ->
      match ent with
      | EntityChest c -> Some (pt, c)
      | _ -> None)

  Assert.True(chestPtOpt.IsSome, "Floor should contain a chest")
  let chestPt, chest = chestPtOpt.Value

  let testPos = { X = chestPt.X - 1; Y = chestPt.Y }
  let floorPrep =
    { state.CurrentFloor with
        Tiles = Map.add testPos (Floor SurfaceType.PavedStone) state.CurrentFloor.Tiles }
  let preStepState = { state with CurrentFloor = floorPrep; PlayerPosition = testPos }

  let postStepState, events = TowerSession.stepPlayer Direction.East preStepState
  Assert.NotEmpty(events)
  let chestOpened =
    events |> List.exists (function TowerEvent.ChestOpened _ -> true | _ -> false)
  Assert.True(chestOpened)

  // Player position remains before the chest (bump interaction)
  Assert.Equal(testPos, postStepState.PlayerPosition)

  // Chest is marked open in entities
  match Map.tryFind chestPt postStepState.CurrentFloor.Entities with
  | Some (EntityChest c) -> Assert.True(c.IsOpen)
  | other -> Assert.Fail(sprintf "Expected opened chest, got %A" other)

  // Item was added to inventory
  if chest.ItemReward.IsSome then
    Assert.Contains(chest.ItemReward.Value, postStepState.InventoryItems)

[<Fact>]
let ``TowerSession bumping into shrine fully restores Health, Morale, Armor, and clears Recklessness`` () =
  let basePlayer = createTestPlayer ()
  let strainedPlayer =
    { basePlayer with
        Health = basePlayer.Health.ApplyDelta -80
        Morale = basePlayer.Morale.ApplyDelta -50
        Armor = basePlayer.Armor.Shred 30
        WeaponCondition = WeaponCondition.Damaged
        BleedStacks = 3
        Meters = { basePlayer.Meters with Recklessness = Meter.Create 60 } }

  let state = TowerSession.initSession strainedPlayer 42 1

  let shrinePtOpt =
    state.CurrentFloor.Entities
    |> Map.tryPick (fun pt ent ->
      match ent with
      | EntityShrine s -> Some (pt, s)
      | _ -> None)

  Assert.True(shrinePtOpt.IsSome, "Floor should contain a shrine")
  let shrinePt, _ = shrinePtOpt.Value

  let testPos = { X = shrinePt.X - 1; Y = shrinePt.Y }
  let floorPrep =
    { state.CurrentFloor with
        Tiles = Map.add testPos (Floor SurfaceType.PavedStone) state.CurrentFloor.Tiles }
  let preStepState = { state with CurrentFloor = floorPrep; PlayerPosition = testPos }

  let postStepState, events = TowerSession.stepPlayer Direction.East preStepState
  let shrineActivated =
    events |> List.exists (function TowerEvent.ShrineActivated _ -> true | _ -> false)
  Assert.True(shrineActivated)

  // Full restoration of Health, Morale, Armor, and Weapon; Bleed purged; Recklessness reset to 0
  Assert.Equal(strainedPlayer.Health.Maximum, postStepState.Player.Health.Current)
  Assert.Equal(strainedPlayer.Morale.Maximum, postStepState.Player.Morale.Current)
  Assert.Equal(strainedPlayer.Armor.Max, postStepState.Player.Armor.Current)
  Assert.Equal(WeaponCondition.Pristine, postStepState.Player.WeaponCondition)
  Assert.Equal(0, postStepState.Player.BleedStacks)
  Assert.Equal(0, postStepState.Player.Meters.Recklessness.Value)

[<Fact>]
let ``TowerSession Ascension Door mechanics: locked by key, unlocking, and ascension`` () =
  let player = createTestPlayer ()
  let state = TowerSession.initSession player 42 1

  let keyId = "quarry_master_key"
  let startPos = { X = 20; Y = 20 }
  let doorPos = { X = 21; Y = 20 }

  let lockedDoorTile = StairwayPortal (DoorState.LockedByKey(keyId, "Quarry Master Key", "Found on guardian"))
  let floorWithLockedDoor =
    { state.CurrentFloor with
        StairwayLocation = doorPos
        Tiles =
          state.CurrentFloor.Tiles
          |> Map.add startPos (Floor SurfaceType.PavedStone)
          |> Map.add doorPos lockedDoorTile }

  let lockedState = { state with CurrentFloor = floorWithLockedDoor; PlayerPosition = startPos }

  // 1. Try stepping onto door WITHOUT key -> remains locked, door locked event emitted
  let noKeyResult, noKeyEvents = TowerSession.stepPlayer Direction.East lockedState
  Assert.Equal(startPos, noKeyResult.PlayerPosition)
  Assert.Contains(noKeyEvents, (function TowerEvent.DoorLockedMessage _ -> true | _ -> false))
  Assert.False(noKeyResult.CurrentFloor.IsStairwayOpen)

  // 2. Award key and step onto door -> unlocks door!
  let stateWithKey = { lockedState with CollectedKeys = lockedState.CollectedKeys.Add keyId }
  let unlockResult, unlockEvents = TowerSession.stepPlayer Direction.East stateWithKey
  Assert.True(unlockResult.CurrentFloor.IsStairwayOpen)
  Assert.Contains("Unlocked the Ascension Door", unlockResult.MessageLog.Head)

  // 3. Step onto now-open door -> Ascends to Floor 2!
  let ascendResult, ascendEvents = TowerSession.stepPlayer Direction.East unlockResult
  Assert.Contains(ascendEvents, (function TowerEvent.StairwayAscended 2 -> true | _ -> false))
  Assert.Equal(2, ascendResult.CurrentFloor.FloorNumber)
  Assert.Equal(FloorTheme.PineCloisters, ascendResult.CurrentFloor.Theme)
  Assert.Equal(ascendResult.CurrentFloor.SpawnLocation, ascendResult.PlayerPosition)

[<Fact>]
let ``TowerDisplay rendering components smoke test`` () =
  let player = createTestPlayer ()
  let state = TowerSession.initSession player 42 1

  // Viewport rendering
  let viewport = TowerDisplay.renderViewport state 45 21
  Assert.NotNull(viewport)

  // HUD rendering
  let hud = TowerDisplay.renderHud state
  Assert.NotNull(hud)

  // Message log rendering
  let log = TowerDisplay.renderMessageLog state 5
  Assert.NotNull(log)

[<Fact>]
let ``TowerDisplay inspectTile correctly inspects player position, AcidSlag hazard, unopened chest, and dark fog`` () =
  let player = createTestPlayer ()
  let baseState = TowerSession.initSession player 42 1
  let playerPos = baseState.PlayerPosition
  let hazardPos = { X = playerPos.X + 1; Y = playerPos.Y }
  let chestPos = { X = playerPos.X; Y = playerPos.Y + 1 }
  let fogPos = { X = playerPos.X + 15; Y = playerPos.Y + 15 }

  let testChest =
    EntityChest
      { Id = "test-chest-1"
        Description = "Ancient iron vault chest"
        LootKeyId = None
        ItemReward = None
        IsOpen = false }

  let floorWithDetails =
    { baseState.CurrentFloor with
        Tiles =
          baseState.CurrentFloor.Tiles
          |> Map.add hazardPos (Hazard HazardType.AcidSlag)
          |> Map.add chestPos (Floor SurfaceType.PavedStone)
        Entities =
          baseState.CurrentFloor.Entities
          |> Map.add chestPos testChest
        Explored =
          baseState.CurrentFloor.Explored
          |> Set.add playerPos
          |> Set.add hazardPos
          |> Set.add chestPos
          |> Set.remove fogPos }

  let state = { baseState with CurrentFloor = floorWithDetails }

  // 1. Inspect player tile
  let playerInspect = TowerDisplay.inspectTile state playerPos
  Assert.Equal(0, playerInspect.Distance)
  Assert.Contains("Within Active Vision", playerInspect.VisibilityText)
  Assert.True(playerInspect.EntitySummary.IsSome)
  Assert.Contains("@ YOU", playerInspect.EntitySummary.Value)
  Assert.Contains("Explorer", playerInspect.EntitySummary.Value)

  // 2. Inspect AcidSlag hazard
  let hazardInspect = TowerDisplay.inspectTile state hazardPos
  Assert.Equal(1, hazardInspect.Distance)
  Assert.Contains("≈", hazardInspect.TileGlyph)
  Assert.Equal("Corrosive Acid Slag", hazardInspect.TileName)
  Assert.Contains("toxic acidic runoff", hazardInspect.TileDescription)
  Assert.True(hazardInspect.HazardWarning.IsSome)
  Assert.Contains("CAUTION:", hazardInspect.HazardWarning.Value)
  Assert.Contains("-15 Armor durability", hazardInspect.HazardWarning.Value)

  // 3. Inspect unopened chest
  let chestInspect = TowerDisplay.inspectTile state chestPos
  Assert.Equal(1, chestInspect.Distance)
  Assert.True(chestInspect.EntitySummary.IsSome)
  Assert.Contains("VAULT CHEST", chestInspect.EntitySummary.Value)
  Assert.Contains("Unopened", chestInspect.EntitySummary.Value)

  // 4. Inspect distant fog of war
  let fogInspect = TowerDisplay.inspectTile state fogPos
  Assert.Equal("?", fogInspect.TileGlyph)
  Assert.Equal("Unexplored Darkness", fogInspect.TileName)
  Assert.Contains("Shrouded in Darkness", fogInspect.VisibilityText)
  Assert.True(fogInspect.HazardWarning.IsNone)
  Assert.True(fogInspect.EntitySummary.IsNone)

  // 5. Inspect hostile guardian in direct line-of-sight
  let enemyPos = { X = playerPos.X; Y = playerPos.Y - 1 }
  let enemyCombatant = TierFactory.createClassLevel CharacterClass.Berserker 5
  let testEnemy =
    EntityEnemy
      { Id = "enemy1"
        Name = "Plaza Sentinel"
        Combatant = enemyCombatant
        DropsKeyId = None
        IsDefeated = false }
  let stateWithEnemy =
    { state with
        CurrentFloor =
          { state.CurrentFloor with
              Tiles = state.CurrentFloor.Tiles |> Map.add enemyPos (Floor SurfaceType.PavedStone)
              Entities = state.CurrentFloor.Entities |> Map.add enemyPos testEnemy
              Visible = state.CurrentFloor.Visible |> Set.add enemyPos } }
  let enemyInspect = TowerDisplay.inspectTile stateWithEnemy enemyPos
  Assert.True(enemyInspect.EntitySummary.IsSome)
  Assert.Contains("HOSTILE GUARDIAN: Plaza Sentinel", enemyInspect.EntitySummary.Value)
  Assert.Contains("Lv.5 Berserker", enemyInspect.EntitySummary.Value)

[<Fact>]
let ``TowerDisplay inspectTile identifies all distinct hazard types and provides tactical warning`` () =
  let player = createTestPlayer ()
  let baseState = TowerSession.initSession player 42 1
  let origin = baseState.PlayerPosition

  let hazardCases =
    [ HazardType.AcidSlag, "Corrosive Acid Slag", "-15 Armor durability"
      HazardType.LavaRift, "Molten Lava Rift", "-15 direct Health"
      HazardType.DeepCurrent, "Deep Floodwater Current", "+15 Exhaustion"
      HazardType.CalmingSpores, "Calming Spore Blossom", "resets Recklessness to 0" ]

  for (hazardType, expectedName, expectedWarning) in hazardCases do
    let hazardPos = { X = origin.X + 1; Y = origin.Y }
    let state =
      { baseState with
          CurrentFloor =
            { baseState.CurrentFloor with
                Tiles = baseState.CurrentFloor.Tiles |> Map.add hazardPos (Hazard hazardType)
                Explored = baseState.CurrentFloor.Explored |> Set.add hazardPos } }

    let detail = TowerDisplay.inspectTile state hazardPos
    Assert.Equal(expectedName, detail.TileName)
    Assert.True(detail.HazardWarning.IsSome, sprintf "Expected warning for %A" hazardType)
    Assert.Contains(expectedWarning, detail.HazardWarning.Value)

[<Fact>]
let ``TowerDisplay renderInspectionPanel and renderViewportWithCursor execute cleanly`` () =
  let player = createTestPlayer ()
  let state = TowerSession.initSession player 42 1

  let panel = TowerDisplay.renderInspectionPanel state state.PlayerPosition
  Assert.NotNull(panel)

  let cursorViewport = TowerDisplay.renderViewportWithCursor state (Some state.PlayerPosition) 45 21
  Assert.NotNull(cursorViewport)
