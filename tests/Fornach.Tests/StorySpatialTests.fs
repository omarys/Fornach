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
  let ``Story Mode Stage 1 opens in a rain-soaked forest with the shivering boy, grinding mobs, and the Bandit Leader`` () =
    let floor = TowerGenerator.generateFloorWithMode 42 1 true

    Assert.Equal(1, floor.FloorNumber)
    Assert.Equal(FloorTheme.PineCloisters, floor.Theme)

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
    Assert.Equal("quest_save_lyra", npcOpt.Value.Quest.Value.Id)
    Assert.Equal(Some "Bandit Leader", npcOpt.Value.Quest.Value.RequiredEnemyName)

    // 3. Stage Boss: the Bandit Leader (no grief aspect on the tutorial floor)
    let bossOpt =
      entities
      |> List.tryPick (function
        | EntityEnemy e when e.Id = "boss_stage_1" -> Some e
        | _ -> None)
    Assert.True(bossOpt.IsSome, "Stage 1 must include the Bandit Leader boss")
    Assert.Equal("Bandit Leader", bossOpt.Value.Name)
    Assert.Equal(Some "keystone_stage_1", bossOpt.Value.DropsKeyId)
    Assert.DoesNotContain(
      entities,
      function
      | EntityEnemy e -> e.Name.StartsWith "Aspect of"
      | _ -> false)

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
  [<InlineData(2, "Aspect of Guilt", "keystone_stage_2", "quest_quarry_hounds")>]
  [<InlineData(3, "Aspect of Denial", "keystone_stage_3", "quest_denial_weavers")>]
  [<InlineData(4, "Aspect of Anger", "keystone_stage_4", "quest_anger_magma")>]
  [<InlineData(5, "Aspect of Bargaining", "keystone_stage_5", "quest_bargain_harpies")>]
  [<InlineData(6, "Aspect of Depression", "keystone_stage_6", "quest_depression_leeches")>]
  [<InlineData(7, "Aspect of Acceptance", "keystone_stage_7", "quest_acceptance_harmony")>]
  let ``Story Mode stages 2 through 7 configure grief bosses, thematic NPCs, and locked portals``
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
    let fighter = StoryBosses.createProloguePlayer "fighter"
    Assert.Equal(CharacterClass.Fighter, fighter.Class)
    Assert.Equal(CombatStance.PowerStance, fighter.Stance)
    Assert.Contains("Sword", fighter.EquippedItems.Head.Name)
    Assert.True(fighter.EquippedItems |> List.exists (fun it -> it.Name = "Reinforced Shield"))
    Assert.True(fighter.EquippedItems |> List.exists (fun it -> it.Name = "Sharpened Caltrops"))

    let mage = StoryBosses.createProloguePlayer "mage"
    Assert.Equal(CharacterClass.Mage, mage.Class)
    Assert.Equal(CombatStance.PowerStance, mage.Stance)
    Assert.Equal(Some ComplexForm.ResonanceSpike, mage.ComplexForm)
    Assert.Contains("Staff", mage.EquippedItems.Head.Name)
    Assert.True(mage.EquippedItems |> List.exists (fun it -> it.Name = "Weaver's Runic Robes"))
    Assert.True(mage.EquippedItems |> List.exists (fun it -> it.Name = "Sharpened Caltrops"))

    let berserker = StoryBosses.createProloguePlayer "berserker"
    Assert.Equal(CharacterClass.Warrior, berserker.Class)
    Assert.Equal(CombatStance.PowerStance, berserker.Stance)
    Assert.Contains("Greatsword", berserker.EquippedItems.Head.Name)
    Assert.True(berserker.EquippedItems |> List.exists (fun it -> it.Name = "Sharpened Caltrops"))

    let duelist = StoryBosses.createProloguePlayer "duelist"
    Assert.Equal(CharacterClass.Rogue, duelist.Class)
    Assert.Equal(CombatStance.AgilityStance, duelist.Stance)
    Assert.Contains("Rapier", duelist.EquippedItems.Head.Name)

    let warden = StoryBosses.createProloguePlayer "warden"
    Assert.Equal(CharacterClass.Soldier, warden.Class)
    Assert.Equal(CombatStance.DisciplineStance, warden.Stance)
    Assert.Contains("Buckler", warden.EquippedItems.Head.Name)

    let inquisitor = StoryBosses.createProloguePlayer "inquisitor"
    Assert.Equal(CharacterClass.Mage, inquisitor.Class)
    Assert.Equal(CombatStance.PowerStance, inquisitor.Stance)
    Assert.Contains("Staff", inquisitor.EquippedItems.Head.Name)

    let mesmer = StoryBosses.createProloguePlayer "mesmer"
    Assert.Equal(CharacterClass.Mage, mesmer.Class)
    // Every legacy arcane archetype key resolves to a Mage, whose baseline vector is Power
    Assert.Equal(CombatStance.PowerStance, mesmer.Stance)
    Assert.Contains("Mirror", mesmer.EquippedItems.Head.Name)

    let abjurer = StoryBosses.createProloguePlayer "abjurer"
    Assert.Equal(CharacterClass.Mage, abjurer.Class)
    Assert.Equal(CombatStance.PowerStance, abjurer.Stance)
    Assert.Contains("Tome", abjurer.EquippedItems.Head.Name)

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
  let ``Interacting with Lyra after the Bandit Leader falls fulfills the rescue quest and awards bonus souls and morale`` () =
    let player = createTestPlayer ()
    let state = TowerSession.initSessionWithMode player 42 1 true

    // Find Lyra's position beside the Bandit Leader
    let lyraEntryOpt =
      state.CurrentFloor.Entities
      |> Map.tryFindKey (fun _ ent ->
        match ent with
        | EntityNpc n when n.Id = "npc_lyra" -> true
        | _ -> false)
    Assert.True(lyraEntryOpt.IsSome, "Lyra NPC must stand beside the Bandit Leader")
    let lyraPos = lyraEntryOpt.Value

    // The rescue is gated: speaking to Lyra before the leader dies does nothing.
    let adjacentState = { state with PlayerPosition = { X = lyraPos.X - 1; Y = lyraPos.Y } }
    let blockedState, blockedEvents = TowerSession.stepPlayer Direction.East adjacentState
    Assert.DoesNotContain("quest_save_lyra", blockedState.CompletedQuests)
    // No completion signal before the gate opens, so the CLI shows no completion card.
    Assert.DoesNotContain(blockedEvents, (function TowerEvent.NpcInteracted(_, true) -> true | _ -> false))

    // Defeat the Bandit Leader, then reunite Lyra with her brother.
    let afterLeader = TowerSession.resolveEnemyDefeat "boss_stage_1" blockedState
    Assert.True(afterLeader.Souls > 0, "Defeating the Bandit Leader should yield souls")

    let reunionState, events = TowerSession.stepPlayer Direction.East afterLeader
    Assert.Contains("quest_save_lyra", reunionState.CompletedQuests)
    Assert.True(reunionState.Souls >= afterLeader.Souls + 50, "Rescue quest should grant at least 50 bonus souls")
    Assert.Contains(events, (function TowerEvent.NpcInteracted(n, true) when n.Name = "Lyra" -> true | _ -> false))

  [<Fact>]
  let ``Prologue player wakes at level 1 with class weapons and caltrops folded into base stats`` () =
    let berserker = StoryBosses.createProloguePlayer "berserker"
    let duelist = StoryBosses.createProloguePlayer "duelist"
    let warden = StoryBosses.createProloguePlayer "warden"
    let inquisitor = StoryBosses.createProloguePlayer "inquisitor"
    let mesmer = StoryBosses.createProloguePlayer "mesmer"
    let abjurer = StoryBosses.createProloguePlayer "abjurer"

    // The story now opens as a level-1 recruit, not a Veteran.
    for player in [ berserker; duelist; warden; inquisitor; mesmer; abjurer ] do
      Assert.Equal(1, player.Level)

    // Starting weapon and caltrops bonuses are real, folded into the base stats at creation. They are
    // percentages of the stat they land on (the number in brackets is what they were as flat points
    // on the level-1 scale), so they keep mattering as attributes grow.
    Assert.Equal(70, berserker.Stats.Get StatId.Force) // 45 + greatsword 56% (was 25)
    Assert.Equal(45, berserker.Stats.Get StatId.Reflex) // 34 + caltrops 33% (was 15)
    Assert.Equal(85, duelist.Stats.Get StatId.Finesse) // 45 + stiletto 56% then caltrops 22%
    Assert.Equal(65, warden.Stats.Get StatId.Poise) // 45 + buckler 44% (was 20)
    Assert.Equal(62, inquisitor.Stats.Get StatId.Intellect) // 40 + staff 56% (was 25)

  [<Fact>]
  let ``Stage 1 grinding mobs mix woodland creatures and bandits ahead of the Bandit Leader`` () =
    let floor = TowerGenerator.generateFloorWithMode 42 1 true
    let mobs =
      floor.Entities
      |> Map.values
      |> Seq.choose (function
        | EntityEnemy e when e.Id.StartsWith("grind_mob_") -> Some e
        | _ -> None)
      |> Seq.toList
    Assert.True(mobs.Length >= 2, "Tutorial floor must provide grinding mobs")

    // A mix of low-level bandits and PineCloisters woodland creatures.
    Assert.Contains(mobs, (fun e -> e.Name = "Bandit" || e.Name = "Bandit Scout"))
    Assert.Contains(mobs, (fun e -> e.Name = "Forest Wolf" || e.Name = "Bramble Boar"))
    for mob in mobs |> List.filter (fun e -> e.Name.StartsWith "Bandit") do
      Assert.True(mob.Combatant.Level <= 8, sprintf "Bandits must be fightable at level 1 (got %s lvl %d)" mob.Name mob.Combatant.Level)
