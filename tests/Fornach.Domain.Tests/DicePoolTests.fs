namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine

module DicePoolTests =

  [<Fact>]
  let ``Floor hits scale deterministically at 1 hit per 15 stat points`` () =
    Assert.Equal(0, DicePool.computeFloorHits 14)
    Assert.Equal(1, DicePool.computeFloorHits 15)
    Assert.Equal(2, DicePool.computeFloorHits 30)
    Assert.Equal(4, DicePool.computeFloorHits 60)
    Assert.Equal(10, DicePool.computeFloorHits 150)
    Assert.Equal(20, DicePool.computeFloorHits 300)

  [<Fact>]
  let ``Pool size remains clamped between 4 and 20 dice across extreme ratios`` () =
    // Extreme attacker advantage: 500 vs 10
    let largePool = DicePool.computePoolSize 500 10
    Assert.True(largePool <= 20, sprintf "Pool was %d, expected <= 20" largePool)
    Assert.True(largePool >= 4)

    // Extreme defender advantage: 10 vs 500
    let smallPool = DicePool.computePoolSize 10 500
    Assert.True(smallPool >= 4, sprintf "Pool was %d, expected >= 4" smallPool)
    Assert.True(smallPool <= 20)

    // Parity: 100 vs 100
    let evenPool = DicePool.computePoolSize 100 100
    Assert.Equal(10, evenPool)

  [<Fact>]
  let ``Power vector hits on 5 and counts 6 as 2 hits`` () =
    // Roll deterministic sequence: [6; 5; 4; 3; 2; 1]
    let rolls = [| 6; 5; 4; 3; 2; 1 |]
    let mutable idx = 0
    let roller _ _ =
      let r = rolls.[idx % rolls.Length]
      idx <- idx + 1
      r

    let res = DicePool.evaluatePool roller Vector.Power 30 0 6
    // 30 stat -> 2 floor hits
    // Rolls [6; 5; 4; 3; 2; 1] -> 6 gives 2, 5 gives 1, others 0 -> 3 rolled hits
    Assert.Equal(2, res.FloorHits)
    Assert.Equal(3, res.RolledHits)
    Assert.Equal(5, res.TotalHits)

  [<Fact>]
  let ``Agility vector hits on 4, 5, and 6`` () =
    let rolls = [| 6; 5; 4; 3; 2; 1 |]
    let mutable idx = 0
    let roller _ _ =
      let r = rolls.[idx % rolls.Length]
      idx <- idx + 1
      r

    let res = DicePool.evaluatePool roller Vector.Agility 30 0 6
    // 4, 5, 6 each give 1 hit -> 3 rolled hits
    Assert.Equal(2, res.FloorHits)
    Assert.Equal(3, res.RolledHits)
    Assert.Equal(5, res.TotalHits)

  [<Fact>]
  let ``Discipline vector rerolls 1s up to studyStacks count`` () =
    // Initial dice: two 1s, followed by reroll results 5 and 6
    let sequence = [| 1; 1; 4; 3; 2; 2; 5; 6 |]
    let mutable idx = 0
    let roller _ _ =
      let r = sequence.[idx % sequence.Length]
      idx <- idx + 1
      r

    // 2 study stacks allow rerolling the two 1s
    let res = DicePool.evaluatePool roller Vector.Discipline 15 2 6
    // 15 stat -> 1 floor hit
    // Original 6 dice were [1; 1; 4; 3; 2; 2].
    // With 2 study stacks, both 1s reroll to 5 and 6!
    // Final dice: [5; 6; 4; 3; 2; 2] -> 5, 6, 4 hit (3 rolled hits)
    Assert.Equal(1, res.FloorHits)
    Assert.Equal(3, res.RolledHits)
    Assert.Equal(4, res.TotalHits)

  [<Fact>]
  let ``Contest results identify critical strikes and whiffs`` () =
    // Attacker rolls all 6s, Defender rolls all 1s
    let mutable critRollCount = 0
    let critRoller _ _ =
      critRollCount <- critRollCount + 1
      if critRollCount <= 10 then 6 else 1

    let critResult =
      DicePool.resolveContest critRoller Vector.Power 100 0 100 0

    Assert.True(critResult.IsCritical)
    Assert.False(critResult.IsWhiff)
    Assert.True(critResult.NetHits >= 5)

    // Attacker rolls 1s, Defender rolls 6s
    let mutable whiffRollCount = 0
    let whiffRoller _ _ =
      whiffRollCount <- whiffRollCount + 1
      if whiffRollCount <= 10 then 1 else 6

    let whiffResult =
      DicePool.resolveContest whiffRoller Vector.Power 30 0 100 0

    Assert.True(whiffResult.IsWhiff)
    Assert.False(whiffResult.IsCritical)
    Assert.True(whiffResult.NetHits <= 0)

  [<Fact>]
  let ``Tiered NetHits multipliers scale appropriately`` () =
    Assert.Equal(0.0, ActionResolver.computeTierMultiplier 0)
    Assert.Equal(0.0, ActionResolver.computeTierMultiplier -3)
    Assert.Equal(1.15, ActionResolver.computeTierMultiplier 1)
    Assert.Equal(1.30, ActionResolver.computeTierMultiplier 2)
    Assert.Equal(1.50, ActionResolver.computeTierMultiplier 3)
    Assert.Equal(1.80, ActionResolver.computeTierMultiplier 4)
    Assert.Equal(2.20, ActionResolver.computeTierMultiplier 5)
    Assert.Equal(2.50, ActionResolver.computeTierMultiplier 6)
    Assert.Equal(3.00, ActionResolver.computeTierMultiplier 7)
    Assert.Equal(3.50, ActionResolver.computeTierMultiplier 8)
    Assert.True(ActionResolver.computeTierMultiplier 10 >= 4.0)

  [<Fact>]
  let ``Whiff results in zero damage and triggers ComboReset`` () =
    let stats = StatBlock.Baseline.With(Force, 15).With(Fortitude, 150)
    let attacker = Combatant.create (CombatantId.New()) "Attacker" 1000 1000 stats
    let defender = Combatant.create (CombatantId.New()) "Defender" 1000 1000 stats

    // Roller where attacker rolls all 1s and defender rolls all 6s -> Whiff
    let result =
      ActionResolver.resolve (fun _ _ -> 1) (StandardAttack(ForceStrike false)) attacker defender

    // Target took no damage
    Assert.Equal(defender.Health.Current, result.Target.Health.Current)
    // Combo reset was emitted
    let hasComboReset =
      result.Events
      |> List.exists (function
        | CombatEvent.ComboReset _ -> true
        | _ -> false)
    Assert.True(hasComboReset)

  [<Fact>]
  let ``Massive blow automatically shreds target armor durability`` () =
    let atkStats = StatBlock.Baseline.With(Force, 100)
    let defStats = StatBlock.Baseline.With(Fortitude, 20)
    let attacker = Combatant.create (CombatantId.New()) "Attacker" 2000 2000 atkStats
    let defender = Combatant.create (CombatantId.New()) "Defender" 2000 2000 defStats
    let initialArmor = defender.Armor.Current

    // Attacker rolls all 6s -> Critical hit with massive net hits
    let result =
      ActionResolver.resolve (fun _ _ -> 6) (StandardAttack(ForceStrike false)) attacker defender

    Assert.True(result.Target.Armor.Current < initialArmor, "Target armor was not shredded")
    Assert.True(result.Target.Health.Current < defender.Health.Current, "Target health did not take damage")
