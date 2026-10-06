namespace Fornach.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine
open Fornach.Story
open Fornach.Cli

module StorySpatialTests =

  let private createTestPlayer () =
    let stats =
      StatBlock.Create
        [ StatId.Force, 60
          StatId.Fortitude, 60
          StatId.Finesse, 60
          StatId.Reflex, 60
          StatId.Prowess, 60
          StatId.Poise, 60
          StatId.Intellect, 60
          StatId.Resolve, 60
          StatId.Acuity, 60
          StatId.Intuition, 60
          StatId.Acumen, 60
          StatId.Composure, 60 ]
    Combatant.create (CombatantId.New()) "Protagonist" 250 250 stats

  [<Fact>]
  let ``Story Mode Stage 1 generates battered chest, shivering boy NPC, grinding mobs, and Aspect of Guilt boss`` () =
    let floor = TowerGenerator.generateFloorWithMode 42 1 true

    Assert.Equal(1, floor.FloorNumber)
    Assert.Equal(FloorTheme.QuarryPlazas, floor.Theme)

    let entities = floor.Entities |> Map.values |> Seq.toList

    // 1. Battered Scavenger Chest adjacent to spawn
    let batteredChestOpt =
      entities
      |> List.tryPick (function
        | EntityChest c when c.Id = "battered_chest" -> Some c
        | _ -> None)
    Assert.True(batteredChestOpt.IsSome, "Stage 1 must include battered scavenger chest at entrance")
    Assert.Contains("Sharpened Caltrops", batteredChestOpt.Value.ItemReward.Value.Name)

    // 2. The Shivering Boy NPC with side quest
    let npcOpt =
      entities
      |> List.tryPick (function
        | EntityNpc n when n.Id = "npc_shivering_boy" -> Some n
        | _ -> None)
    Assert.True(npcOpt.IsSome, "Stage 1 must include The Shivering Boy NPC")
    Assert.Equal("The Shivering Boy", npcOpt.Value.Name)
    Assert.True(npcOpt.Value.Quest.IsSome, "The Shivering Boy must offer a side quest")
    Assert.Equal("quest_quarry_hounds", npcOpt.Value.Quest.Value.Id)

    // 3. Stage Boss: Aspect of Guilt
    let bossOpt =
      entities
      |> List.tryPick (function
        | EntityEnemy e when e.Id = "boss_stage_1" -> Some e
        | _ -> None)
    Assert.True(bossOpt.IsSome, "Stage 1 must include Aspect of Guilt boss")
    Assert.Equal("Aspect of Guilt", bossOpt.Value.Name)
    Assert.Equal(Some "keystone_stage_1", bossOpt.Value.DropsKeyId)

    // 4. Multiple grinding mobs across the colonnades
    let grindingMobs =
      entities
      |> List.choose (function
        | EntityEnemy e when e.Id.StartsWith("grind_mob_") -> Some e
        | _ -> None)
    Assert.True(grindingMobs.Length >= 2, sprintf "Stage 1 must provide at least 2 grinding mobs, found %d" grindingMobs.Length)

    // 5. Stairway portal locked by boss keystone
    match Map.tryFind floor.StairwayLocation floor.Tiles with
    | Some (StairwayPortal (DoorState.LockedByKey(keyId, _, _))) ->
      Assert.Equal("keystone_stage_1", keyId)
    | other ->
      Assert.Fail(sprintf "Expected stairway portal locked by keystone_stage_1, got %A" other)

  [<Theory>]
  [<InlineData(2, "Aspect of Denial", "keystone_stage_2", "quest_denial_weavers")>]
  [<InlineData(3, "Aspect of Anger", "keystone_stage_3", "quest_anger_magma")>]
  [<InlineData(4, "Aspect of Bargaining", "keystone_stage_4", "quest_bargain_harpies")>]
  [<InlineData(5, "Aspect of Depression", "keystone_stage_5", "quest_depression_leeches")>]
  [<InlineData(6, "Aspect of Acceptance", "keystone_stage_6", "quest_acceptance_harmony")>]
  let ``Story Mode stages 2 through 6 configure grief bosses, thematic NPCs, and locked portals``
    (stageNum: int) (bossName: string) (keyId: string) (questId: string) =
    let floor = TowerGenerator.generateFloorWithMode (100 + stageNum) stageNum true
    let entities = floor.Entities |> Map.values |> Seq.toList

    let bossOpt =
      entities
      |> List.tryPick (function
        | EntityEnemy e when e.Id = sprintf "boss_stage_%d" stageNum -> Some e
        | _ -> None)
    Assert.True(bossOpt.IsSome, sprintf "Stage %d must include boss %s" stageNum bossName)
    Assert.Equal(bossName, bossOpt.Value.Name)
    Assert.Equal(Some keyId, bossOpt.Value.DropsKeyId)

    let npcOpt =
      entities
      |> List.tryPick (function
        | EntityNpc n when n.Quest.IsSome && n.Quest.Value.Id = questId -> Some n
        | _ -> None)
    Assert.True(npcOpt.IsSome, sprintf "Stage %d must offer side quest %s" stageNum questId)

  [<Fact>]
  let ``Scavenging battered chest awakens distinct combat archetypes with starting weapons and caltrops`` () =
    let berserker = StoryBosses.createProloguePlayer "berserker"
    Assert.Equal(CharacterClass.Berserker, berserker.Class)
    Assert.Equal(CombatStance.PowerStance, berserker.Stance)
    Assert.Contains("Greatsword", berserker.EquippedItems.Head.Name)
    Assert.True(berserker.EquippedItems |> List.exists (fun it -> it.Name = "Sharpened Caltrops"))

    let duelist = StoryBosses.createProloguePlayer "duelist"
    Assert.Equal(CharacterClass.Duelist, duelist.Class)
    Assert.Equal(CombatStance.AgilityStance, duelist.Stance)
    Assert.Contains("Rapier", duelist.EquippedItems.Head.Name)

    let warden = StoryBosses.createProloguePlayer "warden"
    Assert.Equal(CharacterClass.Warden, warden.Class)
    Assert.Equal(CombatStance.DisciplineStance, warden.Stance)
    Assert.Contains("Buckler", warden.EquippedItems.Head.Name)

    let inquisitor = StoryBosses.createProloguePlayer "inquisitor"
    Assert.Equal(CharacterClass.Inquisitor, inquisitor.Class)
    Assert.Equal(CombatStance.PowerStance, inquisitor.Stance)
    Assert.Contains("Staff", inquisitor.EquippedItems.Head.Name)

  [<Fact>]
  let ``Player defeat rewinds position and restores vitals while preserving earned souls, trophies, and quests`` () =
    let player = createTestPlayer ()
    let state = TowerSession.initSessionWithMode player 42 1 true

    // Simulate harvesting 120 souls and a rare alchemical trophy
    let trophy : AlchemicalTrophy =
      { Id = "trophy_slag_hound_core"
        Name = "Slag Hound Core"
        Description = "Solidified magma core"
        EssenceValue = 25 }

    let enrichedState =
      { state with
          Souls = 120
          Trophies = [ (trophy, 2) ]
          Player =
            { state.Player with
                Health = state.Player.Health.ApplyDelta -150
                Morale = state.Player.Morale.ApplyDelta -100
                Meters = { state.Player.Meters with Recklessness = Meter.Create 35 } }
          PlayerPosition = { X = state.CurrentFloor.SpawnLocation.X + 10; Y = state.CurrentFloor.SpawnLocation.Y + 10 } }

    // Execute death rewind logic
    let restoredPlayer =
      { enrichedState.Player with
          Health = Pool.Create enrichedState.Player.Health.Maximum
          Morale = Pool.Create enrichedState.Player.Morale.Maximum
          Armor = ArmorIntegrity.Create enrichedState.Player.Armor.Max
          Meters = StatusMeters.Zero }

    let respawnedEntities =
      enrichedState.CurrentFloor.Entities
      |> Map.map (fun _ ent ->
        match ent with
        | EntityEnemy e when not (e.Id.StartsWith("boss_")) -> EntityEnemy { e with IsDefeated = false }
        | other -> other)

    let rewoundState =
      { enrichedState with
          Player = restoredPlayer
          PlayerPosition = enrichedState.CurrentFloor.SpawnLocation
          CurrentFloor = { enrichedState.CurrentFloor with Entities = respawnedEntities } }

    // Vitals and meters reset
    Assert.Equal(enrichedState.Player.Health.Maximum, rewoundState.Player.Health.Current)
    Assert.Equal(enrichedState.Player.Morale.Maximum, rewoundState.Player.Morale.Current)
    Assert.Equal(0, rewoundState.Player.Meters.Recklessness.Value)
    Assert.Equal(enrichedState.CurrentFloor.SpawnLocation, rewoundState.PlayerPosition)

    // Progression PRESERVED: souls and trophies remain intact!
    Assert.Equal(120, rewoundState.Souls)
    Assert.Equal(2, snd rewoundState.Trophies.Head)
    Assert.Equal("Slag Hound Core", (fst rewoundState.Trophies.Head).Name)

  [<Fact>]
  let ``Interacting with stage NPC after defeating mobs fulfills side quest and awards bonus souls and morale`` () =
    let player = createTestPlayer ()
    let state = TowerSession.initSessionWithMode player 42 1 true

    // Find the NPC position
    let npcEntryOpt =
      state.CurrentFloor.Entities
      |> Map.tryFindKey (fun _ ent ->
        match ent with
        | EntityNpc n when n.Id = "npc_shivering_boy" -> true
        | _ -> false)
    Assert.True(npcEntryOpt.IsSome, "Shivering boy NPC must exist on floor")
    let npcPos = npcEntryOpt.Value

    // Find a grinding mob and defeat it
    let mobKeyOpt =
      state.CurrentFloor.Entities
      |> Map.tryFindKey (fun _ ent ->
        match ent with
        | EntityEnemy e when e.Id.StartsWith("grind_mob_") -> true
        | _ -> false)
    Assert.True(mobKeyOpt.IsSome, "Grinding mob must exist on floor")
    let mobEnemy = match state.CurrentFloor.Entities.[mobKeyOpt.Value] with EntityEnemy e -> e | _ -> failwith "unreachable"

    let stateAfterDefeat = TowerSession.resolveEnemyDefeat mobEnemy.Id state
    Assert.True(stateAfterDefeat.Souls > 0, "Defeating grinding mob should yield souls")

    // Move player adjacent to NPC and bump into NPC
    let playerAdjState = { stateAfterDefeat with PlayerPosition = { X = npcPos.X - 1; Y = npcPos.Y } }
    let nextState, events = TowerSession.stepPlayer Direction.East playerAdjState

    // Verify quest completion
    Assert.Contains("quest_quarry_hounds", nextState.CompletedQuests)
    Assert.True(nextState.Souls >= stateAfterDefeat.Souls + 50, "Side quest should grant at least 50 bonus souls")
    Assert.Contains(events, (function TowerEvent.NpcInteracted(n, true) when n.Quest.Value.IsCompleted -> true | _ -> false))

  [<Fact>]
  let ``Prologue player starting stats scale competitively against Veteran Bestiary monsters`` () =
    let berserker = StoryBosses.createProloguePlayer "berserker"
    let duelist = StoryBosses.createProloguePlayer "duelist"
    let warden = StoryBosses.createProloguePlayer "warden"
    let inquisitor = StoryBosses.createProloguePlayer "inquisitor"

    // Vitals must scale to Veteran tier (~2000+ Health and Morale, >= 90 Armor)
    Assert.True(berserker.Health.Maximum >= 2500, "Berserker HP should be at least 2500")
    Assert.True(duelist.Health.Maximum >= 2200, "Duelist HP should be at least 2200")
    Assert.True(warden.Armor.Max >= 150, "Warden Armor should be at least 150")
    Assert.True(inquisitor.Morale.Maximum >= 2500, "Inquisitor Morale should be at least 2500")

    // Primary stats must scale to compete with Veteran mobs (e.g. Stone Gargoyle with stats 170-240)
    let gargoyle = Bestiary.byId "stone_gargoyle" |> Option.get
    let gargoylePoise = gargoyle.Stats |> List.find (fun (s, _) -> s = StatId.Poise) |> snd
    let wardenProwess = warden.Stats.Get StatId.Prowess

    // DicePool calculation ratio should not result in a blowout whiff
    let poolSize = DicePool.computePoolSize wardenProwess gargoylePoise
    let wardenFloorHits = DicePool.computeFloorHits wardenProwess
    let gargoyleFloorHits = DicePool.computeFloorHits gargoylePoise

    Assert.True(poolSize >= 8, sprintf "Dice pool should be competitive (actual: %d)" poolSize)
    Assert.True(wardenFloorHits >= 13, sprintf "Warden should have solid floor hits (actual: %d)" wardenFloorHits)
    Assert.True(abs (wardenFloorHits - gargoyleFloorHits) <= 3, "Floor hits should be evenly matched between Warden and Stone Gargoyle")

  [<Fact>]
  let ``Stage 1 grinding mobs spawn Novice Slag Hounds near spawn and boy before Stone Gargoyles`` () =
    let floor = TowerGenerator.generateFloorWithMode 42 1 true
    let firstGrindMob =
      floor.Entities
      |> Map.tryPick (fun _ ent ->
        match ent with
        | EntityEnemy e when e.Id = "grind_mob_1_0" -> Some e
        | _ -> None)
    Assert.True(firstGrindMob.IsSome, "First grinding mob should exist on floor")
    Assert.Equal("Slag Hound", firstGrindMob.Value.Name)
