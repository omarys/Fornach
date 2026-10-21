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
let ``layoutWidths keeps the HUD from squeezing the map viewport`` () =
  // On a wide terminal the map gets its natural width and the HUD exactly the remainder.
  let viewW, hudW = TowerDisplay.layoutWidths 190 64
  Assert.Equal(64, viewW)
  Assert.Equal(190 - 64 - 7, hudW)

  // The pinned columns, the map's border/padding and the grid gutter must land exactly on the
  // terminal width: a frame one cell wider wraps every line and scrambles the whole crawl view.
  for termW in [ 80; 120; 160; 190; 240; 320 ] do
    let v, h = TowerDisplay.layoutWidths termW 64
    Assert.True(v <= 64, sprintf "termW=%d viewW=%d" termW v)
    let frame = TowerDisplay.mapColumnWidth v + h + 3
    Assert.True(frame <= termW, sprintf "termW=%d frame=%d" termW frame)

  // The HUD keeps its readable width wherever the terminal can afford it.
  Assert.True((TowerDisplay.layoutWidths 120 64 |> snd) >= 58)
  Assert.True((TowerDisplay.layoutWidths 190 64 |> snd) >= 58)

[<Fact>]
let ``crawl panels never render a line wider than their column`` () =
  // Spectre measures a nerd-font icon as one cell while it occupies two UTF-16 units and pads the
  // line one cell too wide, which makes a panel's right border jitter and pushes the frame past the
  // terminal. Panels therefore strip those glyphs from their text (Display.stripNerdGlyphs).
  let player = createTestPlayer ()
  let state = TowerSession.initSessionWithMode player 88888 1 true

  let widestLine (width: int) (write: Spectre.Console.IAnsiConsole -> unit) =
    use writer = new System.IO.StringWriter()
    let console =
      Spectre.Console.AnsiConsole.Create(
        Spectre.Console.AnsiConsoleSettings(Out = Spectre.Console.AnsiConsoleOutput(writer)))
    console.Profile.Width <- width
    console.Profile.Height <- 60
    write console
    let withoutAnsi = System.Text.RegularExpressions.Regex.Replace(writer.ToString(), "\u001b\\[[0-9;]*m", "")
    withoutAnsi.Split('\n') |> Array.map (fun l -> l.TrimEnd('\r').Length) |> Array.max

  for width in [ 60; 80; 107; 130; 190 ] do
    let hud = widestLine width (fun c -> c.Write(TowerDisplay.renderHud state))
    Assert.True(hud <= width, sprintf "HUD rendered a %d-cell line in a %d-cell column" hud width)
    let log = widestLine width (fun c -> c.Write(TowerDisplay.renderMessageLog state 4))
    Assert.True(log <= width, sprintf "Log rendered a %d-cell line in a %d-cell column" log width)
    let map = widestLine (TowerDisplay.mapColumnWidth 64) (fun c -> c.Write(TowerDisplay.renderViewport state 64 6))
    Assert.True(map <= TowerDisplay.mapColumnWidth 64, sprintf "Map rendered a %d-cell line" map)

[<Fact>]
let ``map viewport is not wrapped by the column the crawl loop pins it to`` () =
  // A map panel is 2 borders plus 1 cell of padding wider than its content. Pinning the crawl
  // grid column to a width narrower than mapColumnWidth leaves the content area short, and
  // Spectre then wraps every map row into the next one — which slid the player marker onto the
  // following row and out to the panel's right edge. The pinned width and an unconstrained
  // render must place the marker in the same cell.
  let player = createTestPlayer ()
  let state = TowerSession.initSessionWithMode player 88888 1 true
  let viewW, viewH = 64, 6

  let playerCell (width: int) =
    use writer = new System.IO.StringWriter()
    let console =
      Spectre.Console.AnsiConsole.Create(
        Spectre.Console.AnsiConsoleSettings(Out = Spectre.Console.AnsiConsoleOutput(writer)))
    console.Profile.Width <- width
    console.Profile.Height <- 80
    console.Write(TowerDisplay.renderViewport state viewW viewH)
    writer.ToString().Split('\n')
    |> Array.mapi (fun i line -> (i, line.IndexOf('@')))
    |> Array.tryFind (fun (_, column) -> column >= 0)

  let pinnedCell = playerCell (TowerDisplay.mapColumnWidth viewW)
  let freeCell = playerCell (TowerDisplay.mapColumnWidth viewW + 8)

  Assert.True(freeCell.IsSome, "the reference viewport render must contain the player marker")
  Assert.Equal(freeCell, pinnedCell)

[<Fact>]
let ``traumaReset restores mobs to their spawn tiles and clears player debuffs`` () =
  let player = createTestPlayer ()
  let state = TowerSession.initSessionWithMode player 42 1 false

  let mobPositions (s: TowerRunState) =
    s.CurrentFloor.Entities
    |> Map.toList
    |> List.choose (fun (p, ent) -> match ent with EntityEnemy e -> Some (e.Id, p) | _ -> None)

  let original = mobPositions state
  Assert.False(original.IsEmpty, "the generated floor should have mobs")

  // Simulate a losing fight: one mob shoved off its spawn tile and wounded, the player bleeding.
  let firstId, firstPt = original |> List.head
  let firstEnemy =
    state.CurrentFloor.Entities
    |> Map.toList
    |> List.pick (fun (_, ent) -> match ent with EntityEnemy e when e.Id = firstId -> Some e | _ -> None)
  let shifted = { X = firstPt.X + 2; Y = firstPt.Y }
  let entities =
    state.CurrentFloor.Entities
    |> Map.remove firstPt
    |> Map.add shifted (EntityEnemy { firstEnemy with Combatant = { firstEnemy.Combatant with Health = { firstEnemy.Combatant.Health with Current = 1 } } })
  let damaged =
    { state with
        Player = { state.Player with BleedStacks = 3; Health = { state.Player.Health with Current = 1 } }
        CurrentFloor = { state.CurrentFloor with Entities = entities } }

  let reset = TowerDisplay.traumaReset damaged

  Assert.Equal(0, reset.Player.BleedStacks)
  Assert.Equal(reset.Player.Health.Maximum, reset.Player.Health.Current)
  Assert.Equal<Set<string * Point>>(Set.ofList original, Set.ofList (mobPositions reset))
  for (_, ent) in Map.toList reset.CurrentFloor.Entities do
    match ent with
    | EntityEnemy e -> Assert.Equal(e.Combatant.Health.Maximum, e.Combatant.Health.Current)
    | _ -> ()

[<Fact>]
let ``battered chest starts plundered so it cannot re-offer the class selection`` () =
  let floor = TowerGenerator.generateFloorWithMode 42 1 true
  let chest =
    floor.Entities
    |> Map.toList
    |> List.tryPick (fun (_, ent) -> match ent with EntityChest c when c.Id = "battered_chest" -> Some c | _ -> None)
  Assert.True(chest.IsSome, "story floor 1 keeps the battered chest")
  Assert.True(chest.Value.IsOpen, "the in-map chest must start plundered")

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
  let enemyCombatant = TierFactory.createClassLevel CharacterClass.Warrior 5
  let testEnemy =
    EntityEnemy
      { Id = "enemy1"
        Name = "Plaza Sentinel"
        Combatant = enemyCombatant
        DropsKeyId = None
        IsDefeated = false
        ChaseCharge = 0.0 }
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
  Assert.Contains("Lv.5 Warrior", enemyInspect.EntitySummary.Value)

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

let private walkableAt (floor: TowerFloor) (p: Point) =
  match Map.tryFind p floor.Tiles with
  | Some t -> t.IsWalkable
  | None -> false

let private grindMobCount (floor: TowerFloor) =
  floor.Entities
  |> Map.toList
  |> List.filter (fun (_, ent) ->
    match ent with
    | EntityEnemy e -> e.Id.StartsWith "grind_mob_"
    | _ -> false)
  |> List.length

[<Fact>]
let ``every floor reaches every dynamic encounter anchor and the stairway from spawn`` () =
  for isStory in [ false; true ] do
    for floorNum in 1 .. 8 do
      let floor = TowerGenerator.generateFloorWithMode (500 + floorNum) floorNum isStory

      let anchors =
        floor.Entities
        |> Map.toList
        |> List.choose (fun (pt, ent) ->
          match ent with
          | EntityEncounter _ -> Some pt
          | _ -> None)

      Assert.NotEmpty anchors

      let targets = anchors @ [ floor.StairwayLocation ]

      for target in targets do
        let path = Pathfinding.aStar (walkableAt floor) floor.SpawnLocation target
        Assert.True(
          path.IsSome,
          sprintf "story=%b floor %d: no path from spawn to %A" isStory floorNum target
        )
        Assert.Equal(target, List.last path.Value)

[<Fact>]
let ``different seeds on the same floor produce different tile maps`` () =
  let a = TowerGenerator.generateFloor 11 3
  let b = TowerGenerator.generateFloor 12 3
  Assert.NotEqual<Map<Point, TowerTile>>(a.Tiles, b.Tiles)

[<Fact>]
let ``the same seed and floor number reproduce an identical tile map`` () =
  let a = TowerGenerator.generateFloor 2024 4
  let b = TowerGenerator.generateFloor 2024 4
  Assert.Equal<Map<Point, TowerTile>>(a.Tiles, b.Tiles)
  Assert.Equal(a.SpawnLocation, b.SpawnLocation)
  Assert.Equal(a.StairwayLocation, b.StairwayLocation)

[<Fact>]
let ``tower floors 2 through 6 field more than ten roaming mobs`` () =
  // The fixed six-plaza layout could only ever place six grinding mobs per floor.
  for floorNum in 2 .. 6 do
    let floor = TowerGenerator.generateFloor 77 floorNum
    let mobs = grindMobCount floor
    Assert.True(mobs > 10, sprintf "floor %d had only %d grind mobs" floorNum mobs)

[<Fact>]
let ``the tutorial floor stays gentle with at most thirty roaming mobs`` () =
  for seed in [ 1; 42; 4242; 999 ] do
    let floor = TowerGenerator.generateFloorWithMode seed 1 true
    let mobs = grindMobCount floor
    Assert.True(mobs >= 12, sprintf "seed %d tutorial had only %d mobs" seed mobs)
    Assert.True(mobs <= 30, sprintf "seed %d tutorial flooded the floor with %d mobs" seed mobs)

[<Fact>]
let ``consecutive floors on one seed are laid out differently`` () =
  let mutable distinct = 0
  for floorNum in 1 .. 7 do
    let a = TowerGenerator.generateFloor 31337 floorNum
    let b = TowerGenerator.generateFloor 31337 (floorNum + 1)
    if a.SpawnLocation <> b.SpawnLocation || a.Tiles <> b.Tiles then
      distinct <- distinct + 1

  Assert.Equal(7, distinct)

[<Fact>]
let ``a newly entered floor is seen in full circle, then narrows as you walk`` () =
  let player = TierFactory.createClassLevel CharacterClass.Fighter 36
  let state = TowerSession.initSession player 42 1

  let arcOf s =
    (VisionResolver.resolve s.Player s.PlayerPosition s.Facing s.Locomotion EnvironmentalFactors.Default).ArcDegrees

  Assert.Equal(360.0, arcOf state)
  // One step narrows one focus step, so the shrink is gradual rather than a snap back to the cone.
  let afterOneStep = arcOf { state with Locomotion = TowerSession.focusOnMove state.Locomotion }

  let walkingArc =
    arcOf { state with Locomotion = LocomotionState.Walking }

  Assert.True(afterOneStep < 360.0)
  Assert.True(afterOneStep > walkingArc)
  Assert.Equal(walkingArc, arcOf { state with Locomotion = TowerSession.focusOnMove (TowerSession.focusOnMove (TowerSession.focusOnMove state.Locomotion)) })

[<Fact>]
let ``waiting on the map actually reveals more than the walking cone`` () =
  let player = TierFactory.createClassLevel CharacterClass.Fighter 36
  // A floor starts with the full circle in view, so the walking cone has to be built explicitly.
  let state =
    { TowerSession.initSession player 42 1 with Locomotion = LocomotionState.Walking }

  let walking = TowerSession.updateFov state

  let widened =
    { walking with
        Locomotion = TowerSession.focusOnWait (TowerSession.focusOnWait (TowerSession.focusOnWait LocomotionState.Walking)) }
    |> TowerSession.updateFov

  let fullCircle = TowerSession.focusOnWait (TowerSession.focusOnWait (TowerSession.focusOnWait LocomotionState.Walking))
  let arc = VisionResolver.resolve widened.Player widened.PlayerPosition widened.Facing fullCircle EnvironmentalFactors.Default

  Assert.Equal(360.0, arc.ArcDegrees)
  Assert.True(widened.CurrentFloor.Visible.Count > walking.CurrentFloor.Visible.Count)
  Assert.True(Set.isSubset walking.CurrentFloor.Visible widened.CurrentFloor.Visible)
