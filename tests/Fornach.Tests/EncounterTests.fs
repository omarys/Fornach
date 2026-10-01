module Fornach.Tests.EncounterTests

open System
open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine
open Fornach.Cli

let private createTestPlayer () =
  let stats =
    StatBlock.Create
      [ StatId.Force, 50
        StatId.Fortitude, 50
        StatId.Finesse, 40
        StatId.Reflex, 40
        StatId.Prowess, 40
        StatId.Poise, 40
        StatId.Intellect, 50
        StatId.Resolve, 50
        StatId.Acuity, 40
        StatId.Intuition, 40
        StatId.Acumen, 40
        StatId.Composure, 40 ]

  Combatant.create (CombatantId.New()) "Explorer" 200 200 stats

let private getOk = function Ok v -> v | Error e -> failwithf "Expected Ok, got: %s" e
let private getError = function Error e -> e | Ok _ -> failwith "Expected Error, got Ok"

[<Fact>]
let ``TowerGenerator procedurally populates 2 to 4 dynamic encounters per floor`` () =
  for floorNum in 1 .. 7 do
    let floor = TowerGenerator.generateFloor (42 + floorNum) floorNum
    let encounters =
      floor.Entities
      |> Map.toList
      |> List.choose (fun (pt, ent) ->
        match ent with
        | EntityEncounter enc -> Some (pt, enc)
        | _ -> None)

    Assert.True(encounters.Length >= 2 && encounters.Length <= 4, sprintf "Floor %d had %d encounters (expected 2-4)" floorNum encounters.Length)

    for (pt, _) in encounters do
      Assert.NotEqual(floor.SpawnLocation, pt)
      Assert.NotEqual(floor.StairwayLocation, pt)
      let tileOpt = Map.tryFind pt floor.Tiles
      Assert.True(tileOpt.IsSome, "Encounter point should exist in floor tiles")
      Assert.True(tileOpt.Value.IsWalkable, "Encounter tile must be walkable")

[<Fact>]
let ``SacrificialAltar applies costs and rewards correctly and prevents double-use`` () =
  let player = createTestPlayer ()
  let state = TowerSession.initSession player 42 1

  let altarPos = { X = 15; Y = 15 }
  let testAltar =
    { Id = "altar_test"
      Name = "Test Altar of Blood"
      Description = "A dark stone demanding blood for strength."
      Cost = AltarCost.SacrificeHealth 25
      Reward = AltarReward.StatBuff(StatId.Force, 15)
      IsUsed = false }

  let floorWithAltar =
    { state.CurrentFloor with
        Tiles = state.CurrentFloor.Tiles |> Map.add altarPos (Floor SurfaceType.PavedStone)
        Entities = state.CurrentFloor.Entities |> Map.add altarPos (EntityEncounter (FloorEncounter.SacrificialAltar testAltar)) }

  let testState = { state with CurrentFloor = floorWithAltar }

  // Initial stats
  let initialHp = testState.Player.Health.Current
  let initialForce = testState.Player.Stats.Get StatId.Force

  // Apply sacrifice
  let result = TowerSession.applyAltarSacrifice altarPos testState
  Assert.True(result.IsOk)
  let updatedState = getOk result

  // Assert Cost applied: -25 HP
  Assert.Equal(initialHp - 25, updatedState.Player.Health.Current)
  // Assert Reward applied: +15 Force
  Assert.Equal(initialForce + 15, updatedState.Player.Stats.Get StatId.Force)

  // Assert Altar marked used
  match Map.tryFind altarPos updatedState.CurrentFloor.Entities with
  | Some (EntityEncounter (FloorEncounter.SacrificialAltar a)) -> Assert.True(a.IsUsed)
  | other -> Assert.Fail(sprintf "Expected SacrificialAltar, got %A" other)

  // Re-attempting sacrifice returns Error
  let secondAttempt = TowerSession.applyAltarSacrifice altarPos updatedState
  Assert.True(secondAttempt.IsError)

[<Fact>]
let ``WanderingTrader handles purchasing wares with Souls and Alchemical Trophies`` () =
  let player = createTestPlayer ()
  let baseState = TowerSession.initSession player 42 1

  let traderPos = { X = 12; Y = 12 }
  let testItem : EquipmentItem =
    { Name = "Cinder Mantle"
      Slot = EquipmentSlot.Armor
      Description = "Volcanic scale armor."
      StatModifiers = [ StatId.Fortitude, 10 ]
      HealthBonus = 10
      MoraleBonus = 0
      StartingRecklessnessDelta = 0
      Triggers = [] }

  let testMerchant =
    { Id = "merchant_test"
      Name = "Ashen Trader"
      Title = "Relic Merchant"
      Dialogue = [ "Greetings, traveler." ]
      Wares =
        [ { Item = testItem
            CostSouls = 50
            RequiredTrophy = Some (Bestiary.slagHoundCore, 1)
            IsPurchased = false } ]
      HasTraded = false }

  let floorWithMerchant =
    { baseState.CurrentFloor with
        Tiles = baseState.CurrentFloor.Tiles |> Map.add traderPos (Floor SurfaceType.PavedStone)
        Entities = baseState.CurrentFloor.Entities |> Map.add traderPos (EntityEncounter (FloorEncounter.WanderingTrader testMerchant)) }

  // 1. Attempt with insufficient Souls
  let poorState = { baseState with CurrentFloor = floorWithMerchant; Souls = 30; Trophies = [ (Bestiary.slagHoundCore, 1) ] }
  let failResult = TowerSession.buyFromTrader traderPos 0 poorState
  Assert.True(failResult.IsError)
  Assert.Contains("Insufficient Souls", getError failResult)

  // 2. Attempt with missing trophy
  let noTrophyState = { baseState with CurrentFloor = floorWithMerchant; Souls = 100; Trophies = [] }
  let failTrophyResult = TowerSession.buyFromTrader traderPos 0 noTrophyState
  Assert.True(failTrophyResult.IsError)
  Assert.Contains("Missing required alchemical trophy", getError failTrophyResult)

  // 3. Successful purchase
  let readyState = { baseState with CurrentFloor = floorWithMerchant; Souls = 100; Trophies = [ (Bestiary.slagHoundCore, 1) ] }
  let successResult = TowerSession.buyFromTrader traderPos 0 readyState
  Assert.True(successResult.IsOk)
  let boughtState = getOk successResult

  Assert.Equal(50, boughtState.Souls)
  Assert.Empty(boughtState.Trophies)
  Assert.Contains<EquipmentItem>(testItem, boughtState.InventoryItems)

  // Ware marked purchased
  match Map.tryFind traderPos boughtState.CurrentFloor.Entities with
  | Some (EntityEncounter (FloorEncounter.WanderingTrader m)) ->
    Assert.True(m.Wares.[0].IsPurchased)
    Assert.True(m.HasTraded)
  | other -> Assert.Fail(sprintf "Expected WanderingTrader, got %A" other)

[<Fact>]
let ``TreasureVault puzzle mechanics: key check and stat check`` () =
  let player = createTestPlayer ()
  let baseState = TowerSession.initSession player 42 1

  let vaultPos = { X = 18; Y = 18 }
  let relicItem : EquipmentItem =
    { Name = "Vault Amulet"
      Slot = EquipmentSlot.MentalRelic
      Description = "Ancient amulet."
      StatModifiers = [ StatId.Resolve, 15 ]
      HealthBonus = 0
      MoraleBonus = 30
      StartingRecklessnessDelta = 0
      Triggers = [] }

  // A. KeyholeLock
  let keyVault =
    { Id = "vault_key_test"
      Name = "Ironclad Vault"
      Description = "A sealed iron vault."
      Puzzle = VaultPuzzle.KeyholeLock("quarry_vault_key", "Quarry Key", "On guardian")
      Relics = [ relicItem ]
      BonusSouls = 75
      IsOpen = false }

  let floorKeyVault =
    { baseState.CurrentFloor with
        Tiles = baseState.CurrentFloor.Tiles |> Map.add vaultPos (Floor SurfaceType.PavedStone)
        Entities = baseState.CurrentFloor.Entities |> Map.add vaultPos (EntityEncounter (FloorEncounter.TreasureVault keyVault)) }

  let stateNoKey = { baseState with CurrentFloor = floorKeyVault; CollectedKeys = Set.empty }
  let failKey = TowerSession.attemptOpenVault vaultPos stateNoKey
  Assert.True(failKey.IsError)

  let stateWithKey = { stateNoKey with CollectedKeys = Set.ofList [ "quarry_vault_key" ] }
  let successKey = TowerSession.attemptOpenVault vaultPos stateWithKey
  Assert.True(successKey.IsOk)
  let unlockedState = getOk successKey
  Assert.Equal(75, unlockedState.Souls)
  Assert.Contains<EquipmentItem>(relicItem, unlockedState.InventoryItems)

  // B. StatCheck
  let statVault =
    { Id = "vault_stat_test"
      Name = "Clockwork Vault"
      Description = "A delicate tumbler."
      Puzzle = VaultPuzzle.StatCheck(StatId.Finesse, 50, "Deft dexterity check")
      Relics = [ relicItem ]
      BonusSouls = 60
      IsOpen = false }

  let floorStatVault =
    { baseState.CurrentFloor with
        Entities = baseState.CurrentFloor.Entities |> Map.add vaultPos (EntityEncounter (FloorEncounter.TreasureVault statVault)) }

  // Player has Finesse = 40 (fails threshold 50)
  let lowStatState = { baseState with CurrentFloor = floorStatVault }
  let failStat = TowerSession.attemptOpenVault vaultPos lowStatState
  Assert.True(failStat.IsError)

  // Boost Finesse to 55 (passes threshold 50)
  let highStatPlayer = { player with Stats = player.Stats.With(StatId.Finesse, 55) }
  let highStatState = { lowStatState with Player = highStatPlayer }
  let successStat = TowerSession.attemptOpenVault vaultPos highStatState
  Assert.True(successStat.IsOk)

[<Fact>]
let ``MechanicalTrapGauntlet disarms on high stat and triggers damage on low stat`` () =
  let player = createTestPlayer ()
  let baseState = TowerSession.initSession player 42 1

  let startPos = { X = 10; Y = 10 }
  let trapPos = { X = 11; Y = 10 }

  let trapData =
    { Id = "trap_test"
      Name = "Floor Spikes"
      TrapType = TrapType.FloorSpikes 25
      DisarmStat = StatId.Reflex
      DisarmThreshold = 45
      IsDisarmed = false
      IsTriggered = false }

  let floorWithTrap =
    { baseState.CurrentFloor with
        Tiles =
          baseState.CurrentFloor.Tiles
          |> Map.add startPos (Floor SurfaceType.PavedStone)
          |> Map.add trapPos (Floor SurfaceType.PavedStone)
        Entities =
          baseState.CurrentFloor.Entities
          |> Map.add trapPos (EntityEncounter (FloorEncounter.MechanicalTrapGauntlet trapData)) }

  // 1. Low Reflex (40 < 45) -> Triggers trap!
  let lowReflexState = { baseState with CurrentFloor = floorWithTrap; PlayerPosition = startPos }
  let trippedState, events = TowerSession.stepPlayer Direction.East lowReflexState
  Assert.Equal(trapPos, trippedState.PlayerPosition)
  Assert.Equal(player.Health.Current - 25, trippedState.Player.Health.Current)
  Assert.Contains(events, (function TowerEvent.TrapTriggered _ -> true | _ -> false))

  // 2. High Reflex (50 >= 45) -> Disarms trap!
  let highReflexPlayer = { player with Stats = player.Stats.With(StatId.Reflex, 50) }
  let highReflexState = { baseState with Player = highReflexPlayer; CurrentFloor = floorWithTrap; PlayerPosition = startPos }
  let disarmedState, disarmEvents = TowerSession.stepPlayer Direction.East highReflexState
  Assert.Equal(trapPos, disarmedState.PlayerPosition)
  Assert.Equal(highReflexPlayer.Health.Current, disarmedState.Player.Health.Current) // No damage
  Assert.Contains(disarmEvents, (function TowerEvent.TrapDisarmed _ -> true | _ -> false))

[<Fact>]
let ``MemoryEchoFragment restores morale and adds to discovered echoes`` () =
  let player = createTestPlayer ()
  let strainedPlayer = { player with Morale = player.Morale.ApplyDelta -50 }
  let baseState = TowerSession.initSession strainedPlayer 42 1

  let startPos = { X = 8; Y = 8 }
  let echoPos = { X = 9; Y = 8 }

  let echoData =
    { Id = "echo_wet_asphalt"
      Title = "The Scent of Wet Asphalt"
      SensoryDetail = "Cold rain on slick tires."
      MemoryTranscript = [ "\"Are you ready to head home?\"" ]
      MoraleRecovery = 30
      IsCommuned = false }

  let floorWithEcho =
    { baseState.CurrentFloor with
        Tiles =
          baseState.CurrentFloor.Tiles
          |> Map.add startPos (Floor SurfaceType.PavedStone)
          |> Map.add echoPos (Floor SurfaceType.PavedStone)
        Entities =
          baseState.CurrentFloor.Entities
          |> Map.add echoPos (EntityEncounter (FloorEncounter.MemoryEchoFragment echoData)) }

  let stepState = { baseState with CurrentFloor = floorWithEcho; PlayerPosition = startPos }
  let nextState, events = TowerSession.stepPlayer Direction.East stepState

  Assert.Equal(echoPos, nextState.PlayerPosition)
  Assert.Equal(strainedPlayer.Morale.Current + 30, nextState.Player.Morale.Current)
  Assert.True(nextState.DiscoveredEchoes.Contains "echo_wet_asphalt")
  Assert.Contains(events, (function TowerEvent.EchoDiscovered _ -> true | _ -> false))

[<Fact>]
let ``AmbushLair springs on step and spawns pack enemies onto adjacent tiles`` () =
  let player = createTestPlayer ()
  let baseState = TowerSession.initSession player 42 1

  let startPos = { X = 20; Y = 20 }
  let ambushPos = { X = 21; Y = 20 }

  let ambushData =
    { Id = "ambush_hound_pack"
      Name = "Slag Hound Ambush"
      Pack = { Leader = Bestiary.slagHound; Minions = [ Bestiary.slagHound ] }
      TriggerDescription = "Slag hounds burst from the shadows!"
      IsTriggered = false }

  let floorWithAmbush =
    { baseState.CurrentFloor with
        Tiles =
          baseState.CurrentFloor.Tiles
          |> Map.add startPos (Floor SurfaceType.PavedStone)
          |> Map.add ambushPos (Floor SurfaceType.PavedStone)
          |> Map.add { X = 21; Y = 21 } (Floor SurfaceType.PavedStone)
          |> Map.add { X = 22; Y = 20 } (Floor SurfaceType.PavedStone)
        Entities =
          baseState.CurrentFloor.Entities
          |> Map.add ambushPos (EntityEncounter (FloorEncounter.AmbushLair ambushData)) }

  let stepState = { baseState with CurrentFloor = floorWithAmbush; PlayerPosition = startPos }
  let nextState, events = TowerSession.stepPlayer Direction.East stepState

  Assert.Equal(ambushPos, nextState.PlayerPosition)
  Assert.Contains(events, (function TowerEvent.AmbushTriggered _ -> true | _ -> false))

  // Verify spawned enemies exist in floor entities
  let spawnedCount =
    nextState.CurrentFloor.Entities
    |> Map.toList
    |> List.choose (fun (_, ent) ->
      match ent with
      | EntityEnemy e when e.Id.StartsWith("ambush_hound_pack") -> Some e
      | _ -> None)
    |> List.length

  Assert.True(spawnedCount >= 1, "At least one ambush enemy should be spawned onto the floor")

[<Fact>]
let ``Defeating Bestiary monster awards Souls and rolled Trophies`` () =
  let player = createTestPlayer ()
  let baseState = TowerSession.initSession player 42 1

  let houndCombatant = Bestiary.createMonster Bestiary.slagHound
  let enemy =
    { Id = "hound_foe_1"
      Name = Bestiary.slagHound.Name
      Combatant = houndCombatant
      DropsKeyId = Some "quarry_keystone"
      IsDefeated = false }

  let enemyPos = { X = 25; Y = 25 }
  let floorWithEnemy =
    { baseState.CurrentFloor with
        Entities = baseState.CurrentFloor.Entities |> Map.add enemyPos (EntityEnemy enemy) }

  let state = { baseState with CurrentFloor = floorWithEnemy; Souls = 10; Trophies = [] }
  let defeatedState = TowerSession.resolveEnemyDefeat enemy.Id state

  // Verify Souls increased
  Assert.True(defeatedState.Souls > 10, "Souls should increase after monster defeat")
  // Verify key was awarded
  Assert.True(defeatedState.CollectedKeys.Contains "quarry_keystone")

[<Fact>]
let ``TowerDisplay inspectTile formats inspection detail for encounters`` () =
  let player = createTestPlayer ()
  let baseState = TowerSession.initSession player 42 1

  let altarPos = { X = baseState.PlayerPosition.X + 1; Y = baseState.PlayerPosition.Y }
  let altarData =
    { Id = "altar_inspect"
      Name = "Altar of the Iron Quarryman"
      Description = "Granite crucible"
      Cost = AltarCost.SacrificeHealth 25
      Reward = AltarReward.StatBuff(StatId.Force, 15)
      IsUsed = false }

  let state =
    { baseState with
        CurrentFloor =
          { baseState.CurrentFloor with
              Tiles = baseState.CurrentFloor.Tiles |> Map.add altarPos (Floor SurfaceType.PavedStone)
              Entities = baseState.CurrentFloor.Entities |> Map.add altarPos (EntityEncounter (FloorEncounter.SacrificialAltar altarData))
              Visible = baseState.CurrentFloor.Visible |> Set.add altarPos } }

  let detail = TowerDisplay.inspectTile state altarPos
  Assert.True(detail.EntitySummary.IsSome)
  Assert.Contains("SACRIFICIAL ALTAR: Altar of the Iron Quarryman", detail.EntitySummary.Value)
  Assert.Contains("Sacrifice 25 Health", detail.EntitySummary.Value)
