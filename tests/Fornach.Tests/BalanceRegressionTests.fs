namespace Fornach.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine

/// Regression tests for the Thorn Weaver story-floor-2 one-shot report.
/// The root-cause fix is encounter placement: story stages 1-3 must not spawn
/// Veteran-tier mobs against the level-35 protagonist.
module BalanceRegressionTests =

  [<Fact>]
  let ``story floors 2 and 3 do not spawn the Veteran Thorn Weaver`` () =
    for floorNumber in [ 2; 3 ] do
      let floor = TowerGenerator.generateFloorWithMode 4242 floorNumber true

      let enemyNames =
        floor.Entities
        |> Map.toList
        |> List.choose (function
          | _, EntityEnemy e -> Some e.Name
          | _ -> None)

      Assert.DoesNotContain("Thorn Weaver", enemyNames)

  [<Fact>]
  let ``guardians never exceed the level their floor's progression assumes`` () =
    // The roster rotation alone put the level-160 Obsidian Fiend on tower floor 3, where a Finesse
    // attack cannot land at all: defender floor hits 45 vs attacker ceiling 33. Where a floor's
    // theme offers a template at or below the level that floor's progression assumes, placement
    // must pick one of those rather than an over-tiered GrandMaster.
    for isStory in [ true; false ] do
      for floorNumber in 1..28 do
        // Story floors 1-7 hand the guardian to an authored boss instead of the biome roster.
        if not isStory || floorNumber > 7 then
          let floor = TowerGenerator.generateFloorWithMode 4242 floorNumber isStory
          let ceiling = ProgressionScale.storyGuardianLevel floorNumber
          let roster = Bestiary.byBiome floor.Theme

          let guardians =
            floor.Entities
            |> Map.toList
            |> List.choose (function
              | _, EntityEnemy e when e.Id.StartsWith("enemy_guardian_") -> Some e
              | _ -> None)

          if roster |> List.exists (fun t -> t.Level <= ceiling) then
            for g in guardians do
              Assert.True(
                g.Combatant.Level <= ceiling,
                sprintf
                  "stage %d (story=%b) guardian %s is level %d, ceiling %d"
                  floorNumber
                  isStory
                  g.Name
                  g.Combatant.Level
                  ceiling
              )

  [<Fact>]
  let ``tower floor 3 does not spawn a GrandMaster guardian`` () =
    let floor = TowerGenerator.generateFloorWithMode 4242 3 false

    let guardians =
      floor.Entities
      |> Map.toList
      |> List.choose (function
        | _, EntityEnemy e when e.Id.StartsWith("enemy_guardian_") -> Some e
        | _ -> None)

    Assert.DoesNotContain("Obsidian Fiend", guardians |> List.map (fun g -> g.Name))

  [<Fact>]
  let ``ExecuteStrike still kills a collapsed target`` () =
    let actor = TierFactory.createClassLevel CharacterClass.Berserker 1
    let target = TierFactory.createClassLevel CharacterClass.Berserker 1
    let collapsed = { target with Collapse = CollapseState.Collapsed SomaticAnoxia }
    let roller (_min: int) (_max: int) = 6

    let res = ActionResolver.resolveEx roller (ExecuteStrike Physical) actor collapsed 0

    Assert.Equal(0, res.Target.Health.Current)
    Assert.Contains(res.Events, (function CombatEvent.Executed _ -> true | _ -> false))

  [<Fact>]
  let ``level-up preserves hand-authored stats and adds growth`` () =
    let protagonist = StoryBosses.createProloguePlayer "berserker" // Lv35, hand-authored Force 210
    let forceBefore = protagonist.Stats.Get Force

    let leveled = TierFactory.rescaleToLevel protagonist 36

    Assert.True(leveled.Stats.Get Force > forceBefore, sprintf "Force regressed: %d -> %d" forceBefore (leveled.Stats.Get Force))
    Assert.True(leveled.Health.Maximum >= protagonist.Health.Maximum)
