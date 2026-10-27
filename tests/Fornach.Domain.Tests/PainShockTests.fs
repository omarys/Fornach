namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

module PainShockTests =

  let private fixedRoller (result: int) : DiceRoller =
    fun _min _max -> result

  [<Fact>]
  let ``Strain meter debuffs apply at 75 threshold`` () =
    let baseFighter = TierFactory.createClassLevel CharacterClass.Fighter 1
    let normalForce = baseFighter.GetCombatStat StatId.Force
    let normalFort = baseFighter.GetCombatStat StatId.Fortitude
    let normalFinesse = baseFighter.GetCombatStat StatId.Finesse
    let normalReflex = baseFighter.GetCombatStat StatId.Reflex
    let normalPoise = baseFighter.GetCombatStat StatId.Poise

    // 1. Exhaustion >= 75 debuffs Force and Fortitude by -25%
    let exhausted =
      baseFighter
      |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + 75 })
    Assert.Equal(int (System.Math.Round(float normalForce * 0.75)), exhausted.GetCombatStat StatId.Force)
    Assert.Equal(int (System.Math.Round(float normalFort * 0.75)), exhausted.GetCombatStat StatId.Fortitude)
    Assert.Equal(normalFinesse, exhausted.GetCombatStat StatId.Finesse)

    // 2. Overwhelm >= 75 debuffs Finesse and Reflex by -25%
    let overwhelmed =
      baseFighter
      |> Combatant.updateMeters (fun m -> { m with Overwhelm = m.Overwhelm + 75 })
    Assert.Equal(int (System.Math.Round(float normalFinesse * 0.75)), overwhelmed.GetCombatStat StatId.Finesse)
    Assert.Equal(int (System.Math.Round(float normalReflex * 0.75)), overwhelmed.GetCombatStat StatId.Reflex)
    Assert.Equal(normalForce, overwhelmed.GetCombatStat StatId.Force)

    // 3. Frustration >= 75 debuffs Poise by -50%
    let frustrated =
      baseFighter
      |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 75 })
    Assert.Equal(int (System.Math.Round(float normalPoise * 0.50)), frustrated.GetCombatStat StatId.Poise)

  [<Fact>]
  let ``Single-turn damage under 15 percent does not trigger Pain Shock`` () =
    let roller = fixedRoller 5
    let attacker = TierFactory.createClassLevel CharacterClass.Warrior 1
    // Target with huge health pool so 100 damage is < 15%
    let target = Combatant.create (CombatantId.New()) "Titan" 10000 1000 attacker.Stats
    let action = StandardAttack (ForceStrike false)

    let outcome = ActionResolver.resolveEx roller action attacker target 0
    let hasPainShock =
      outcome.Events
      |> List.exists (function CombatEvent.PainShockSuffered _ -> true | _ -> false)
    Assert.False(hasPainShock, "Damage under 15% Max HP must not trigger Pain Shock")
    Assert.False(outcome.Target.IsStaggered, "Target should not be staggered")

  [<Fact>]
  let ``Single-turn damage exceeding 15 percent triggers Pain Shock and staggers on failed Poise test`` () =
    let roller = fixedRoller 100 // High roll to guarantee damage
    // High-tier attacker vs fragile target with 0 Poise
    let attacker = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster
    let fragileTarget = TierFactory.createClassLevel CharacterClass.Rogue 1
    let action = StandardAttack (ForceStrike false)

    let outcome = ActionResolver.resolveEx roller action attacker fragileTarget 0
    let painShockEvent =
      outcome.Events
      |> List.tryPick (function CombatEvent.PainShockSuffered(_, dmg, pct, poise, resisted) -> Some (dmg, pct, poise, resisted) | _ -> None)

    Assert.True(painShockEvent.IsSome, "Heavy damage should emit PainShockSuffered")
    let _, pct, _, resisted = painShockEvent.Value
    Assert.True(pct >= 15, sprintf "Damage percentage should be >= 15%% (Actual: %d%%)" pct)
    if not resisted then
      Assert.True(outcome.Target.IsStaggered, "Target should be staggered upon failing Pain Shock check")
      Assert.True(outcome.Target.Meters.Overwhelm.Value >= 25, "Overwhelm should surge +25 on Pain Shock failure")
      Assert.True(outcome.Target.Meters.Exhaustion.Value >= 15, "Exhaustion should surge +15 on Pain Shock failure")

  [<Fact>]
  let ``Staggered combatant recovers on turn clear`` () =
    let c = TierFactory.createClassLevel CharacterClass.Warrior 1 |> Combatant.stagger
    Assert.True(c.IsStaggered)
    let cleared = c |> Combatant.clearStagger
    Assert.False(cleared.IsStaggered)
