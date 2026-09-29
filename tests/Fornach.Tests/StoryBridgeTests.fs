module Fornach.Tests.StoryBridgeTests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Story
open Fornach.Cli

let private createTestPlayer () =
  let stats =
    StatBlock.Create
      [ StatId.Force, 50
        StatId.Fortitude, 50
        StatId.Finesse, 50
        StatId.Reflex, 50
        StatId.Prowess, 50
        StatId.Poise, 50 ]

  Combatant.create (CombatantId.New()) "Protagonist" 150 150 stats

[<Fact>]
let ``StoryRunner properly invokes bound F# callbacks when stepping through Ink knots``
  ()
  =
  let player = createTestPlayer ()
  let inkJson = StoryRunner.LoadPrologueJson ()

  let awardedMemories = ResizeArray<string>()

  let runner =
    StoryRunner(
      inkJson,
      player,
      onMemoryAwarded = (fun mem -> awardedMemories.Add mem.Id)
    )

  // 1. Initial waking text
  let initialEvents = runner.ContinueToNextEvent()
  Assert.NotEmpty initialEvents
  Assert.Equal(2, runner.CurrentChoices.Length)

  // 2. Select choice 0: Scavenge the belt for weapons -> leads to boy_encounter
  runner.ChooseChoice 0
  let boyEvents = runner.ContinueToNextEvent()
  Assert.NotEmpty boyEvents

  // Assert callback was invoked for rain_and_headlights
  Assert.Contains("rain_and_headlights", awardedMemories)

[<Fact>]
let ``Memory tags awarded in story accurately reflect on the player's aggregate state``
  ()
  =
  let player = createTestPlayer ()
  let inkJson = StoryRunner.LoadPrologueJson ()
  let runner = StoryRunner(inkJson, player)

  // Step waking -> choice 1 (inspect pouches -> prepared_mind)
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 1
  runner.ContinueToNextEvent() |> ignore

  // Both prepared_mind and rain_and_headlights should be in runner.Memories
  Assert.True(runner.State.HasMemory "prepared_mind")
  Assert.True(runner.State.HasMemory "rain_and_headlights")

  let prepMem = runner.Memories.["prepared_mind"]
  Assert.Equal("prepared mind", prepMem.Name)
  Assert.NotEmpty prepMem.Description

  let rainMem = runner.Memories.["rain_and_headlights"]
  Assert.Contains("headlights", rainMem.Description)

[<Fact>]
let ``Losing an encounter successfully reroutes the Ink story engine to the game_over_respawn path``
  ()
  =
  let player = createTestPlayer ()
  let inkJson = StoryRunner.LoadPrologueJson ()
  let mutable deathAnimationTriggered = false

  let runner =
    StoryRunner(
      inkJson,
      player,
      onDeathAnimation = (fun () -> deathAnimationTriggered <- true)
    )

  // Step to quarry ambush
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // Scavenge
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // "Stay hidden here" -> quarry_ambush
  let combatEvents = runner.ContinueToNextEvent()

  // Verify combat was initiated against Guilt_Aspect
  let combatEvt =
    combatEvents
    |> List.tryPick (function
      | StoryEvent.CombatInitiated(id, enemy) -> Some(id, enemy)
      | _ -> None)

  Assert.True combatEvt.IsSome
  let enemyId, enemy = combatEvt.Value
  Assert.Equal("Guilt_Aspect", enemyId)
  Assert.Equal("Aspect of Guilt", enemy.Name)

  // Simulate player defeat in combat
  runner.ResolveCombat CombatOutcome.PlayerDefeated

  // Verify death animation callback fired
  Assert.True deathAnimationTriggered
  Assert.True runner.State.IsGameOver

  // Continue story from game_over_respawn
  let respawnEvents = runner.ContinueToNextEvent()
  Assert.NotEmpty respawnEvents

  // Verify text mentions oncoming truck / phantom horn and loops back to waking
  let textJoined =
    respawnEvents
    |> List.choose (function
      | StoryEvent.TextProduced t -> Some t
      | _ -> None)
    |> String.concat " "

  Assert.Contains("oncoming truck", textJoined)
  Assert.Contains("phantom horn", textJoined)
  // Story has looped back to waking choices
  Assert.Equal(2, runner.CurrentChoices.Length)

[<Fact>]
let ``Winning combat encounter advances story to post_combat and awards shattered windshield``
  ()
  =
  let player = createTestPlayer ()
  let inkJson = StoryRunner.LoadPrologueJson ()
  let runner = StoryRunner(inkJson, player)

  // Step through waking -> boy -> quarry_ambush
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0
  runner.ContinueToNextEvent() |> ignore

  // Simulate victory over Aspect of Guilt
  runner.ResolveCombat CombatOutcome.PlayerVictorious

  let postCombatEvents = runner.ContinueToNextEvent()
  Assert.NotEmpty postCombatEvents

  // Verify post_combat text and shattered windshield memory
  let textJoined =
    postCombatEvents
    |> List.choose (function
      | StoryEvent.TextProduced t -> Some t
      | _ -> None)
    |> String.concat " "

  Assert.Contains("broken windshield", textJoined)
  Assert.True(runner.State.HasMemory "shattered_windshield")

[<Fact>]
let ``StoryBosses resolves all 5 Grief Aspects with correct stances and pools`` () =
  let testCases =
    [ ("denial_aspect", "Aspect of Denial", CombatStance.AgilityStance, 240)
      ("anger_aspect", "Aspect of Anger", CombatStance.PowerStance, 420)
      ("bargaining_aspect", "Aspect of Bargaining", CombatStance.DisciplineStance, 310)
      ("depression_aspect", "Aspect of Depression", CombatStance.DisciplineStance, 480)
      ("acceptance_aspect", "Aspect of Acceptance", CombatStance.DisciplineStance, 350) ]

  for (enemyId, expectedName, expectedStance, expectedHealth) in testCases do
    let boss = StoryBosses.createEnemy enemyId
    Assert.Equal(expectedName, boss.Name)
    Assert.Equal(expectedStance, boss.Stance)
    Assert.Equal(expectedHealth, boss.Health.Current)
    Assert.NotEmpty boss.EquippedItems

[<Fact>]
let ``Denial Aspect initializes with mirror clones and evasion focus`` () =
  let denial = StoryBosses.createEnemy "denial"
  Assert.Equal(2, denial.MirrorClones)
  Assert.True(denial.Stats.Get StatId.Finesse >= 100)
  Assert.True(denial.Stats.Get StatId.Reflex >= 100)
  Assert.Equal(30, denial.Armor.Max)

[<Fact>]
let ``Anger Aspect initializes with high initial recklessness and overpowering kinetic stats`` () =
  let anger = StoryBosses.createEnemy "anger"
  Assert.Equal(45, anger.Meters.Recklessness.Value)
  Assert.Equal(40, anger.Meters.Frustration.Value)
  Assert.True(anger.Stats.Get StatId.Force >= 140)
  Assert.Equal(1, anger.Stats.Get StatId.Composure) // Clamped to domain minimum 1 (zero composure)

[<Fact>]
let ``Depression Aspect initializes with cognitive fatigue and impenetrable fortitude`` () =
  let depression = StoryBosses.createEnemy "depression"
  Assert.Equal(40, depression.Meters.CognitiveFatigue.Value)
  Assert.Equal(120, depression.Armor.Max)
  Assert.True(depression.Stats.Get StatId.Fortitude >= 130)

[<Fact>]
let ``Acceptance Aspect initializes with equilibrium meters and high resolve`` () =
  let acceptance = StoryBosses.createEnemy "acceptance"
  Assert.Equal(0, acceptance.Meters.Recklessness.Value)
  Assert.Equal(0, acceptance.Meters.CognitiveFatigue.Value)
  Assert.Equal(500, acceptance.Morale.Current)
  Assert.True(acceptance.Stats.Get StatId.Resolve >= 90)

[<Fact>]
let ``Full narrative progression unlocks all 5 grief memories and leads to the crosswalk and Tower entrance``
  ()
  =
  let player = createTestPlayer ()
  let inkJson = StoryRunner.LoadPrologueJson ()
  let runner = StoryRunner(inkJson, player)

  // 1. Prologue: Waking -> Boy -> Quarry (Guilt)
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0
  let combat1 = runner.ContinueToNextEvent()
  Assert.Contains(
    combat1,
    function
    | StoryEvent.CombatInitiated("Guilt_Aspect", _) -> true
    | _ -> false
  )

  // Defeat Guilt -> post_combat -> Shrouded Grove
  runner.ResolveCombat(CombatOutcome.PlayerVictorious, "post_combat")
  runner.ContinueToNextEvent() |> ignore
  Assert.True(runner.State.HasMemory "shattered_windshield")

  runner.ChooseChoice 0 // Follow tracks into forest
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // Steel mind
  let combat2 = runner.ContinueToNextEvent()
  Assert.Contains(
    combat2,
    function
    | StoryEvent.CombatInitiated("Denial_Aspect", _) -> true
    | _ -> false
  )

  // Defeat Denial -> post_denial -> Caldera
  runner.ResolveCombat(CombatOutcome.PlayerVictorious, "post_denial")
  runner.ContinueToNextEvent() |> ignore
  Assert.True(runner.State.HasMemory "broken_umbrella")

  runner.ChooseChoice 0 // Ascend caldera
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // Channel fury
  let combat3 = runner.ContinueToNextEvent()
  Assert.Contains(
    combat3,
    function
    | StoryEvent.CombatInitiated("Anger_Aspect", _) -> true
    | _ -> false
  )

  // Defeat Anger -> post_anger -> Promontory
  runner.ResolveCombat(CombatOutcome.PlayerVictorious, "post_anger")
  runner.ContinueToNextEvent() |> ignore
  Assert.True(runner.State.HasMemory "shouting_in_the_hallway")

  runner.ChooseChoice 0 // Follow wind to cliffs
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // Take whatever it costs
  let combat4 = runner.ContinueToNextEvent()
  Assert.Contains(
    combat4,
    function
    | StoryEvent.CombatInitiated("Bargaining_Aspect", _) -> true
    | _ -> false
  )

  // Defeat Bargaining -> post_bargaining -> Sunken Metropolis
  runner.ResolveCombat(CombatOutcome.PlayerVictorious, "post_bargaining")
  runner.ContinueToNextEvent() |> ignore
  Assert.True(runner.State.HasMemory "hospital_monitors")

  runner.ChooseChoice 0 // Descend into sunken city
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // Force leaden legs
  let combat5 = runner.ContinueToNextEvent()
  Assert.Contains(
    combat5,
    function
    | StoryEvent.CombatInitiated("Depression_Aspect", _) -> true
    | _ -> false
  )

  // Defeat Depression -> post_depression -> White Meadow
  runner.ResolveCombat(CombatOutcome.PlayerVictorious, "post_depression")
  runner.ContinueToNextEvent() |> ignore
  Assert.True(runner.State.HasMemory "empty_bedroom")

  runner.ChooseChoice 0 // Ascend sunlit meadow
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // Who are you?
  let combat6 = runner.ContinueToNextEvent()
  Assert.Contains(
    combat6,
    function
    | StoryEvent.CombatInitiated("Acceptance_Aspect", _) -> true
    | _ -> false
  )

  // Defeat Acceptance -> post_acceptance -> Intersection Transition
  runner.ResolveCombat(CombatOutcome.PlayerVictorious, "post_acceptance")
  let transitionEvents = runner.ContinueToNextEvent()
  Assert.True(runner.State.HasMemory "twin_smile")

  let textJoined =
    transitionEvents
    |> List.choose (function
      | StoryEvent.TextProduced t -> Some t
      | _ -> None)
    |> String.concat " "

  Assert.Contains("crosswalk", textJoined)
  Assert.Contains("The Tower", textJoined)

  // Enter the Tower
  Assert.Equal(1, runner.CurrentChoices.Length)
  runner.ChooseChoice 0
  let towerEvents = runner.ContinueToNextEvent()
  let towerText =
    towerEvents
    |> List.choose (function
      | StoryEvent.TextProduced t -> Some t
      | _ -> None)
    |> String.concat " "

  Assert.Contains("endless vaulted stone halls", towerText)

[<Fact>]
let ``Death in Shrouded Grove respawns player at the forest checkpoint after truck collision``
  ()
  =
  let player = createTestPlayer ()
  let inkJson = StoryRunner.LoadPrologueJson ()
  let runner = StoryRunner(inkJson, player)

  // Reach forest entry (checkpoint is set to forest)
  runner.ContinueToNextEvent() |> ignore // waking
  runner.ChooseChoice 0 // boy
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // quarry
  runner.ContinueToNextEvent() |> ignore
  runner.ResolveCombat(CombatOutcome.PlayerVictorious, "post_combat")
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // follow tracks -> sets checkpoint = forest

  // Now in forest_entry, advance to denial_ambush
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0 // steel mind
  runner.ContinueToNextEvent() |> ignore

  // Simulate player death against Denial Aspect
  runner.ResolveCombat CombatOutcome.PlayerDefeated
  let respawnEvents = runner.ContinueToNextEvent()

  let textJoined =
    respawnEvents
    |> List.choose (function
      | StoryEvent.TextProduced t -> Some t
      | _ -> None)
    |> String.concat " "

  Assert.Contains("oncoming truck", textJoined)
  Assert.Contains("Shrouded Grove", textJoined)
  Assert.Equal(2, runner.CurrentChoices.Length) // 2 choices at forest_entry

[<Fact>]
let ``EnvironmentScenes maps each Grief boss to its specific thematic scene`` () =
  let testCases =
    [ ("guilt_aspect", "The Abandoned Iron Quarry", "PROLOGUE: GUILT & HESITATION")
      ("denial_aspect", "The Shrouded Grove", "STAGE 1: DENIAL")
      ("anger_aspect", "The Basalt Caldera", "STAGE 2: ANGER")
      ("bargaining_aspect", "The Shattered Promontory", "STAGE 3: BARGAINING")
      ("depression_aspect", "The Sunken Metropolis", "STAGE 4: DEPRESSION")
      ("acceptance_aspect", "The White Meadow", "STAGE 5: ACCEPTANCE") ]

  for (enemyId, expectedLocation, expectedStage) in testCases do
    let scene = EnvironmentScenes.getSceneForEnemy enemyId
    Assert.Equal(expectedLocation, scene.LocationName)
    Assert.Equal(expectedStage, scene.StageTitle)
    Assert.NotEmpty scene.AsciiArt
    Assert.NotEmpty scene.AtmosphereSensory
    Assert.False(String.IsNullOrWhiteSpace scene.PsychologicalTheme)

[<Fact>]
let ``EnvironmentScenes renders scene header panel and boss encounter card without errors`` () =
  let scene = EnvironmentScenes.ShroudedGroveScene
  let boss = StoryBosses.createEnemy "denial_aspect"

  let headerPanel = EnvironmentScenes.renderSceneHeader scene
  Assert.NotNull headerPanel

  let bossCard = EnvironmentScenes.renderBossEncounterCard boss scene
  Assert.NotNull bossCard

[<Fact>]
let ``Crosswalk intersection scene has distinctive high beams and tower portal ASCII art`` () =
  let scene = EnvironmentScenes.CrosswalkIntersectionScene
  Assert.Contains("Crosswalk Intersection", scene.LocationName)
  let artJoined = String.concat " " scene.AsciiArt
  Assert.Contains("CROSSWALK", artJoined)
  Assert.Contains("THE MONOLITHIC TOWER", artJoined)
  Assert.NotEmpty scene.AtmosphereSensory

[<Fact>]
let ``Denial Phase Shift gambit conjures a mirror clone and inflicts confusion`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let actor = StoryBosses.createEnemy "denial_aspect"
  let target = createTestPlayer ()
  let initialClones = actor.MirrorClones

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack DenialPhaseShift)) actor target
  Assert.Equal(initialClones + 1, result.Actor.MirrorClones)
  Assert.True(result.Target.Meters.Confusion.Value > target.Meters.Confusion.Value)

[<Fact>]
let ``Basalt Eruption gambit smashes target armor and inflicts overwhelm`` () =
  let roller : DiceRoller = fun _ _ -> 5
  let actor = StoryBosses.createEnemy "anger_aspect"
  let target = { (createTestPlayer ()) with Armor = ArmorIntegrity.Create 50 }

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack BasaltEruption)) actor target
  Assert.True(result.Target.Armor.Current <= 25)
  Assert.True(result.Target.Meters.Overwhelm.Value > 0)
  Assert.True(result.Actor.Meters.Recklessness.Value > actor.Meters.Recklessness.Value)

[<Fact>]
let ``Coercive Bargain drains target morale to restore actor vitality and inflicts provoke`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let actor = { (StoryBosses.createEnemy "bargaining_aspect") with Health = { Current = 100; Maximum = 310 } }
  let target = createTestPlayer ()
  let initialTargetMorale = target.Morale.Current

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack CoerciveBargain)) actor target
  Assert.True(result.Target.Morale.Current < initialTargetMorale)
  Assert.True(result.Actor.Health.Current > 100)
  Assert.True(result.Target.Meters.Provoke.Value > 0)

[<Fact>]
let ``Apathy Doldrums inflicts heavy cognitive fatigue and exhaustion`` () =
  let roller : DiceRoller = fun _ _ -> 3
  let actor = StoryBosses.createEnemy "depression_aspect"
  let target = createTestPlayer ()

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack ApathyDoldrums)) actor target
  Assert.True(result.Target.Meters.CognitiveFatigue.Value >= 40)
  Assert.True(result.Target.Meters.Exhaustion.Value >= 25)

[<Fact>]
let ``Serene Resolution calms recklessness to zero and restores morale to both combatants`` () =
  let roller : DiceRoller = fun _ _ -> 3
  let actor = { (StoryBosses.createEnemy "acceptance_aspect") with Meters = { StatusMeters.Zero with Recklessness = Meter.Create 30 }; Morale = { Current = 200; Maximum = 500 } }
  let target = { (createTestPlayer ()) with Meters = { StatusMeters.Zero with Recklessness = Meter.Create 40 }; Morale = { Current = 50; Maximum = 150 } }

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack SereneResolution)) actor target
  Assert.Equal(0, result.Actor.Meters.Recklessness.Value)
  Assert.Equal(0, result.Target.Meters.Recklessness.Value)
  Assert.True(result.Actor.Morale.Current >= 250)
  Assert.True(result.Target.Morale.Current >= 100)

[<Fact>]
let ``AI accurately selects thematic trauma gambits for each Grief Aspect`` () =
  let denial = { (StoryBosses.createEnemy "denial_aspect") with MirrorClones = 0 }
  let anger = StoryBosses.createEnemy "anger_aspect"
  let bargaining = StoryBosses.createEnemy "bargaining_aspect"
  let depression = StoryBosses.createEnemy "depression_aspect"
  let acceptance = StoryBosses.createEnemy "acceptance_aspect"
  let target = createTestPlayer ()

  let denialIntent = AI.chooseIntent denial target
  Assert.Equal(StandardAttack (TraumaAttack DenialPhaseShift), denialIntent)

  let angerIntent = AI.chooseIntent anger target
  Assert.Equal(StandardAttack (TraumaAttack BasaltEruption), angerIntent)

  let bargainingIntent = AI.chooseIntent bargaining target
  Assert.Equal(StandardAttack (TraumaAttack CoerciveBargain), bargainingIntent)

  let depressionIntent = AI.chooseIntent depression target
  Assert.Equal(StandardAttack (TraumaAttack ApathyDoldrums), depressionIntent)

  let acceptanceIntent = AI.chooseIntent acceptance target
  Assert.Equal(StandardAttack (TraumaAttack SereneResolution), acceptanceIntent)
