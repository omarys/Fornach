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
