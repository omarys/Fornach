namespace Fornach.Tests

open Xunit
open System
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine
open Fornach.Cli

/// Regression tests for the rain-soaked forest tutorial restructure: the story now opens
/// at level 1 with real class gear, a bandit-occupied floor 1, and rapid tutorial leveling
/// that lands the protagonist in the thirties once the Bandit Leader falls.
module StoryTutorialTests =

  [<Theory>]
  [<InlineData("fighter")>]
  [<InlineData("mage")>]
  [<InlineData("berserker")>]
  [<InlineData("duelist")>]
  [<InlineData("warden")>]
  [<InlineData("inquisitor")>]
  [<InlineData("mesmer")>]
  [<InlineData("abjurer")>]
  let ``Tutorial protagonist starts at level 1 with canonical class stats and real gear bonuses`` (className: string) =
    let player = StoryBosses.createProloguePlayer className
    let expectedClass =
      match className with
      | "fighter" -> CharacterClass.Fighter
      | "mage" -> CharacterClass.Mage
      | "berserker" -> CharacterClass.Warrior
      | "duelist" -> CharacterClass.Rogue
      | "warden" -> CharacterClass.Soldier
      | "mesmer" -> CharacterClass.Mage
      | "abjurer" -> CharacterClass.Mage
      | _ -> CharacterClass.Mage
    Assert.Equal("Protagonist", player.Name)
    Assert.Equal(1, player.Level)
    Assert.Equal(expectedClass, player.Class)

    // Canonical level-1 template, then the equipped weapon + caltrops bonuses applied.
    let expected =
      player.EquippedItems
      |> List.fold (fun (c: Combatant) item -> Combatant.applyItemBonus item c) (TierFactory.createClassLevel expectedClass 1)

    let allStats =
      [ StatId.Force
        StatId.Fortitude
        StatId.Finesse
        StatId.Reflex
        StatId.Prowess
        StatId.Poise
        StatId.Intellect
        StatId.Resolve
        StatId.Acuity
        StatId.Intuition
        StatId.Acumen
        StatId.Composure ]
    for statId in allStats do
      Assert.Equal(expected.Stats.Get statId, player.Stats.Get statId)

    Assert.Equal(expected.Health.Maximum, player.Health.Maximum)
    Assert.Equal(expected.Morale.Maximum, player.Morale.Maximum)
    Assert.Equal(expected.Armor.Max, player.Armor.Max)

  [<Fact>]
  let ``Picking up an item permanently raises the relevant stat and stores it in inventory`` () =
    let player = StoryBosses.createProloguePlayer "berserker"
    let state = TowerSession.initSessionWithMode player 42 1 true

    let reflexBefore = state.Player.Stats.Get StatId.Reflex
    let inventoryBefore = state.InventoryItems.Length

    // The in-map battered chest now starts plundered (the prologue already looted it), so put a
    // fresh chest carrying the same caltrops reward back on that tile to exercise the pickup.
    let chestPos =
      state.CurrentFloor.Entities
      |> Map.tryFindKey (fun _ ent ->
        match ent with
        | EntityChest c when c.Id = "battered_chest" -> true
        | _ -> false)
    Assert.True(chestPos.IsSome, "The tutorial battered chest must sit beside the spawn")
    let chestPosition = chestPos.Value

    let reward =
      match Map.find chestPosition state.CurrentFloor.Entities with
      | EntityChest c -> c.ItemReward
      | _ -> None

    let freshChest =
      EntityChest
        { Id = "test_pickup_chest"
          Description = "A battered iron chest, freshly restocked."
          LootKeyId = None
          ItemReward = reward
          IsOpen = false }

    let approachState =
      { state with
          PlayerPosition = { X = chestPosition.X - 1; Y = chestPosition.Y }
          CurrentFloor =
            { state.CurrentFloor with
                Entities = state.CurrentFloor.Entities |> Map.add chestPosition freshChest } }

    let nextState, events = TowerSession.stepPlayer Direction.East approachState

    Assert.Contains(events, (function TowerEvent.ChestOpened(_, _, _) -> true | _ -> false))
    // Sharpened Caltrops grant Reflex +5, folded permanently into the base stats.
    Assert.Equal(reflexBefore + 5, nextState.Player.Stats.Get StatId.Reflex)
    Assert.Equal(inventoryBefore + 1, nextState.InventoryItems.Length)
    Assert.Contains(nextState.InventoryItems, (fun (it: EquipmentItem) -> it.Name = "Sharpened Caltrops"))

  [<Fact>]
  let ``Story floor 1 fields a Bandit Leader and never a grief aspect`` () =
    let floor = TowerGenerator.generateFloorWithMode 42 1 true
    Assert.Equal(FloorTheme.PineCloisters, floor.Theme)

    let enemyNames =
      floor.Entities
      |> Map.values
      |> Seq.choose (function
        | EntityEnemy e -> Some e.Name
        | _ -> None)
      |> Seq.toList

    Assert.Contains("Bandit Leader", enemyNames)
    Assert.DoesNotContain(enemyNames, fun n -> n.StartsWith "Aspect of")

    let boss =
      floor.Entities
      |> Map.values
      |> Seq.tryPick (function
        | EntityEnemy e when e.Id = "boss_stage_1" -> Some e
        | _ -> None)
    Assert.True(boss.IsSome, "The tutorial floor must spawn a guardian boss")
    Assert.Equal("Bandit Leader", boss.Value.Name)
    Assert.True(boss.Value.Combatant.Level >= 30 && boss.Value.Combatant.Level <= 35, sprintf "Bandit Leader should sit in the level 30-35 band, got %d" boss.Value.Combatant.Level)

  [<Fact>]
  let ``Clearing every floor 1 enemy levels the protagonist into the thirties`` () =
    let player = StoryBosses.createProloguePlayer "berserker"
    let state = TowerSession.initSessionWithMode player 42 1 true
    Assert.Equal(1, state.Player.Level)

    let enemyIds =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.choose (function
        | _, EntityEnemy e -> Some e.Id
        | _ -> None)

    let cleared = enemyIds |> List.fold (fun s id -> TowerSession.resolveEnemyDefeat id s) state

    Assert.True(
      cleared.Player.Level >= 30 && cleared.Player.Level <= 40,
      sprintf "Clearing the tutorial floor should land the protagonist in levels 30-40, got %d" cleared.Player.Level)

    Assert.Contains("keystone_stage_1", cleared.CollectedKeys)

  [<Fact>]
  let ``Each story stage gate levels the protagonist for the next biome's monsters`` () =
    // Per-kill experience (~0.3 levels per floor after the tutorial) cannot keep pace with the biome
    // tiers, so each stage's keystone guardian grants the level the next biome's monsters assume.
    // Without it the protagonist entered the Basalt Calderas at level 33 against a level-95 Master
    // golem.
    let player = StoryBosses.createProloguePlayer "berserker"
    let mutable state = TowerSession.initSessionWithMode player 4242 1 true

    let gateLevels = [ 1, 32; 2, 45; 3, 95; 4, 105; 5, 110; 6, 185 ]

    for floorNumber, expectedLevel in gateLevels do
      Assert.Equal(floorNumber, state.CurrentFloor.FloorNumber)

      let guardian =
        state.CurrentFloor.Entities
        |> Map.toList
        |> List.tryPick (function
          | _, EntityEnemy e when e.DropsKeyId.IsSome -> Some e
          | _ -> None)

      Assert.True(guardian.IsSome, sprintf "floor %d must guard its keystone with an enemy" floorNumber)
      state <- TowerSession.resolveEnemyDefeat guardian.Value.Id state
      Assert.Equal(expectedLevel, state.Player.Level)
      Assert.Contains(sprintf "keystone_stage_%d" floorNumber, state.CollectedKeys)

      if floorNumber < 7 then
        state <- TowerSession.ascendFloor state

  [<Fact>]
  let ``Tutorial enemies cannot one-shot the level-1 protagonist`` () =
    let player = StoryBosses.createProloguePlayer "berserker"
    let floor = TowerGenerator.generateFloorWithMode 42 1 true

    // The tutorial guard applies to every floor-1 enemy, including the Bandit Leader.
    let enemies =
      floor.Entities
      |> Map.values
      |> Seq.choose (function
        | EntityEnemy e -> Some e.Combatant
        | _ -> None)
      |> Seq.toList

    Assert.NotEmpty(enemies)
    let rnd = Random(20251011)
    let roller (_min: int) (_max: int) = rnd.Next(1, 7)

    for enemy in enemies do
      for _ in 1 .. 2000 do
        let intent = AI.chooseIntent enemy player
        let res = ActionResolver.resolveWithoutLethal roller intent enemy player
        Assert.False(res.Target.Health.IsDepleted, sprintf "%s one-shot the protagonist's Health" enemy.Name)
        Assert.False(res.Target.Morale.IsDepleted, sprintf "%s one-shot the protagonist's Morale" enemy.Name)

  [<Fact>]
  let ``Lyra only completes the rescue quest after the Bandit Leader is defeated`` () =
    let player = StoryBosses.createProloguePlayer "warden"
    let state = TowerSession.initSessionWithMode player 42 1 true

    let lyraPos =
      state.CurrentFloor.Entities
      |> Map.tryFindKey (fun _ ent ->
        match ent with
        | EntityNpc n when n.Id = "npc_lyra" -> true
        | _ -> false)
    Assert.True(lyraPos.IsSome, "Lyra must stand beside the Bandit Leader")
    let position = lyraPos.Value

    // Interacting before the leader falls must not complete the rescue.
    let approach = { state with PlayerPosition = { X = position.X - 1; Y = position.Y } }
    let blockedState, _ = TowerSession.stepPlayer Direction.East approach
    Assert.DoesNotContain("quest_save_lyra", blockedState.CompletedQuests)

    // Defeat the leader, then speak with Lyra again.
    let afterLeader = TowerSession.resolveEnemyDefeat "boss_stage_1" blockedState
    let reunionState, events = TowerSession.stepPlayer Direction.East afterLeader
    Assert.Contains("quest_save_lyra", reunionState.CompletedQuests)
    Assert.Contains(events, (function TowerEvent.NpcInteracted(n, true) when n.Name = "Lyra" -> true | _ -> false))
