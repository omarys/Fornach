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

  Combatant.create (CombatantId.New()) "PlayerCharacter" 150 150 stats

[<Fact>]
let ``StoryRunner properly invokes bound F# callbacks when stepping through Ink knots``
  ()
  =
  let player = createTestPlayer ()
  let inkJson = StoryRunner.LoadPrologueJson ()

  let awardedMemories = ResizeArray<string>()

  let mutable chosenClass = ""
  let runner =
    StoryRunner(
      inkJson,
      player,
      onMemoryAwarded = (fun mem -> awardedMemories.Add mem.Id),
      onClassChosen = (fun cls -> chosenClass <- cls)
    )

  // 1. Initial waking text
  let initialEvents = runner.ContinueToNextEvent()
  Assert.NotEmpty initialEvents
  Assert.Equal(4, runner.CurrentChoices.Length)

  // 2. Select choice 0: Draw two-handed greatsword (Berserker) -> leads to boy_encounter
  runner.ChooseChoice 0
  let boyEvents = runner.ContinueToNextEvent()
  Assert.NotEmpty boyEvents

  // Assert callbacks were invoked
  Assert.Equal("Berserker", chosenClass)
  Assert.Contains("prepared_mind", awardedMemories)
  Assert.Contains("rain_and_headlights", awardedMemories)

[<Fact>]
let ``Memory tags awarded in story accurately reflect on the player's aggregate state``
  ()
  =
  let player = createTestPlayer ()
  let inkJson = StoryRunner.LoadPrologueJson ()
  let runner = StoryRunner(inkJson, player)

  // Step waking -> choice 0 (greatsword -> prepared_mind awarded)
  runner.ContinueToNextEvent() |> ignore
  runner.ChooseChoice 0
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
  Assert.Equal(4, runner.CurrentChoices.Length)

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
let ``StoryBosses resolves all 6 Grief Aspects with correct stances and pools`` () =
  let testCases =
    [ ("guilt_aspect", "Aspect of Guilt", CombatStance.PowerStance, 6400)
      ("denial_aspect", "Aspect of Denial", CombatStance.AgilityStance, 6500)
      ("anger_aspect", "Aspect of Anger", CombatStance.PowerStance, 7600)
      ("bargaining_aspect", "Aspect of Bargaining", CombatStance.DisciplineStance, 10200)
      ("depression_aspect", "Aspect of Depression", CombatStance.DisciplineStance, 11000)
      ("acceptance_aspect", "Aspect of Acceptance", CombatStance.DisciplineStance, 7200) ]

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
  Assert.Equal(120, denial.Armor.Max)

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
  Assert.Equal(200, depression.Armor.Max)
  Assert.True(depression.Stats.Get StatId.Fortitude >= 70) // 130 authored, x0.55 potency dial

[<Fact>]
let ``Acceptance Aspect initializes with equilibrium meters and high resolve`` () =
  let acceptance = StoryBosses.createEnemy "acceptance"
  Assert.Equal(0, acceptance.Meters.Recklessness.Value)
  Assert.Equal(0, acceptance.Meters.CognitiveFatigue.Value)
  Assert.Equal(7500, acceptance.Morale.Current)
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

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack FinesseGambit)) actor target
  Assert.Equal(initialClones + 1, result.Actor.MirrorClones)
  Assert.True(result.Target.Meters.Confusion.Value > target.Meters.Confusion.Value)

[<Fact>]
let ``Basalt Eruption gambit smashes target armor and inflicts overwhelm`` () =
  let roller : DiceRoller = fun _ _ -> 5
  let actor = StoryBosses.createEnemy "anger_aspect"
  let target = { (createTestPlayer ()) with Armor = ArmorIntegrity.Create 50 }

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack ForceGambit)) actor target
  Assert.True(result.Target.Armor.Current <= 25)
  Assert.True(result.Target.Meters.Frustration.Value > 0)
  Assert.True(result.Actor.Meters.Recklessness.Value > actor.Meters.Recklessness.Value)

[<Fact>]
let ``Coercive Bargain drains target morale to restore actor vitality and inflicts provoke`` () =
  let roller : DiceRoller = fun _ _ -> 4
  let actor = { (StoryBosses.createEnemy "bargaining_aspect") with Health = { Current = 100; Maximum = 310 } }
  let target = createTestPlayer ()
  let initialTargetMorale = target.Morale.Current

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack AcumenGambit)) actor target
  Assert.True(result.Target.Morale.Current < initialTargetMorale)
  Assert.True(result.Actor.Health.Current > 100)
  Assert.True(result.Target.Meters.Provoke.Value > 0)

[<Fact>]
let ``Apathy Doldrums inflicts heavy cognitive fatigue and a survivable exhaustion tick`` () =
  let roller : DiceRoller = fun _ _ -> 3
  let actor = StoryBosses.createEnemy "depression_aspect"
  let target = createTestPlayer ()

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack FortitudeGambit)) actor target
  Assert.True(result.Target.Meters.CognitiveFatigue.Value >= 40)
  // Exhaustion stays modest on purpose: at the old 25 a Depression fight hit 100 by round 3 and
  // executed the player regardless of how it was played.
  Assert.InRange(result.Target.Meters.Exhaustion.Value, 8, 15)

[<Fact>]
let ``Serene Resolution calms recklessness to zero and restores morale to both combatants`` () =
  let roller : DiceRoller = fun _ _ -> 3
  let actor = { (StoryBosses.createEnemy "acceptance_aspect") with Meters = { StatusMeters.Zero with Recklessness = Meter.Create 30 }; Morale = { Current = 200; Maximum = 500 } }
  let target = { (createTestPlayer ()) with Meters = { StatusMeters.Zero with Recklessness = Meter.Create 40 }; Morale = { Current = 50; Maximum = 150 } }

  let result = ActionResolver.resolve roller (StandardAttack (TraumaAttack ComposureGambit)) actor target
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

  let denialIntent = AI.chooseIntentUsing Policies.all denial target
  Assert.Equal(StandardAttack (TraumaAttack FinesseGambit), denialIntent)

  let angerIntent = AI.chooseIntentUsing Policies.all anger target
  Assert.Equal(StandardAttack (TraumaAttack ForceGambit), angerIntent)

  let bargainingIntent = AI.chooseIntentUsing Policies.all bargaining target
  Assert.Equal(StandardAttack (TraumaAttack AcumenGambit), bargainingIntent)

  let depressionIntent = AI.chooseIntentUsing Policies.all depression target
  Assert.Equal(StandardAttack (TraumaAttack FortitudeGambit), depressionIntent)

  let acceptanceIntent = AI.chooseIntentUsing Policies.all acceptance target
  Assert.Equal(StandardAttack (TraumaAttack ComposureGambit), acceptanceIntent)

[<Fact>]
let ``every Grief Aspect names a policy the story table resolves`` () =
  // The id is a string on both sides of a project boundary: the Aspect carries it and the story table
  // resolves it. A typo on either side is silent -- the combatant simply falls back to the engine's
  // generic discipline and fights a different fight -- so the contract is asserted rather than trusted.
  let aspects =
    [ "guilt_aspect"
      "denial_aspect"
      "anger_aspect"
      "bargaining_aspect"
      "depression_aspect"
      "acceptance_aspect" ]
    |> List.map StoryBosses.createEnemy

  for aspect in aspects do
    match aspect.Policy with
    | None -> failwithf "%s carries no policy id, so the engine's generic discipline would fight it" aspect.Name
    | Some id ->
      Assert.True(
        Policies.all.ContainsKey id,
        sprintf "%s names %s, which the story table does not register" aspect.Name id.Value
      )

  Assert.Equal(6, Policies.all.Count)

[<Fact>]
let ``the engine fights a Grief Aspect generically when no policy table is supplied`` () =
  // This is the point of the seam: `AI` holds no branch that knows what an Aspect is. Handed no table it
  // fights one as the GriefManifestation species it is -- Denial argues on the mental plane, so the
  // species profile hands it an acumen interrogation and never phases out of the world. The signature
  // press appears only once the story hands its own table over.
  let denial =
    { (StoryBosses.createEnemy "denial_aspect") with
        MirrorClones = 0
        Meters = { StatusMeters.Zero with Recklessness = Meter.Create 10 } }

  let target = createTestPlayer ()
  Assert.Equal(StandardAttack (AcumenInterrogation true), AI.chooseIntent denial target)
  Assert.Equal(StandardAttack (TraumaAttack FinesseGambit), AI.chooseIntentUsing Policies.all denial target)

[<Fact>]
let ``boss gear contributes its pools but not its percentages or recklessness delta`` () =
  // Bosses assigned `EquippedItems` directly, so no gear bonus ever landed. Folding the whole kit is
  // wrong in the other direction: +67% Force on a block already authored as a final stat line took
  // three aspects to a one-round kill. Only the additive pools are folded.
  let anger = StoryBosses.createEnemy "anger"
  Assert.Equal(7600, anger.Health.Current) // (authored 3300 + greatsword HealthBonus 500) x2 pool dial
  Assert.Equal(45, anger.Meters.Recklessness.Value) // authored 45, NOT 45 + the greatsword's delta

  let acceptance = StoryBosses.createEnemy "acceptance"
  Assert.Equal(7500, acceptance.Morale.Current) // authored 4100 + linen MoraleBonus 900

[<Fact>]
let ``every trauma gambit is named for its stat in the engine and for its Aspect in the CLI`` () =
  // The engine calls each press after the stat it contests; the player reads the Aspect's name. The two
  // vocabularies meet only in `Display.gambitDisplayName`, so this pins the meeting points rather than the
  // strings: a rename on either side that forgets the other fails here instead of in a fight.
  let gambits =
    [ FinesseGambit, "Finesse", "Denial Phase Shift"
      ForceGambit, "Force", "Basalt Eruption"
      AcumenGambit, "Acumen", "Coercive Bargain"
      FortitudeGambit, "Fortitude", "Apathy Doldrums"
      ComposureGambit, "Composure", "Serene Resolution" ]
  for gambit, stat, storyName in gambits do
    Assert.Equal(stat, gambit.Name)
    Assert.Equal(sprintf "Trauma Gambit: %s" stat, gambit.Label)
    Assert.Equal(storyName, Display.gambitDisplayName gambit.Label)

[<Fact>]
let ``a gambit name the CLI does not know is shown under the engine's own label`` () =
  // Ordinary gambits are declared with their own name already, so the trauma table must not swallow them.
  Assert.Equal("Social Gambit: Imperious Demand", Display.gambitDisplayName "Social Gambit: Imperious Demand")
  Assert.Equal("", Display.gambitDisplayName "")

[<Fact>]
let ``the player's mental guard is recognised by PlayerStyle and not by its name`` () =
  // The story's climber is built on a generic class, so `Class.IsPlayerClass` cannot see it and the rule
  // used to fall back to the name "PlayerCharacter" -- which braced any combatant that happened to share
  // it. `PlayerStyle` is the flag the ramp and the estimator already read, so the name test is gone.
  let caster = { createTestPlayer () with Class = CharacterClass.Mage } // attacker on the mental plane
  let player = { createTestPlayer () with PlayerStyle = true } // generic class, physical plane
  let sameStatsAsAMob = { player with PlayerStyle = false }
  // Player branch: a generic physical-plane climber takes 1.10 of the raw defence.
  Assert.Equal(110, ActionResolver.resolveMentalDefenseFor caster player 100)
  // Enemy branch at level 1 takes 0.70. Carrying the player's name must buy nothing.
  Assert.Equal(70, ActionResolver.resolveMentalDefenseFor caster sameStatsAsAMob 100)
