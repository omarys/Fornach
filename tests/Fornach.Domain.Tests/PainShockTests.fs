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

    // 2. Frustration >= 75 debuffs Finesse and Reflex by -25%
    let overwhelmed =
      baseFighter
      |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 75 })
    Assert.Equal(int (System.Math.Round(float normalFinesse * 0.75)), overwhelmed.GetCombatStat StatId.Finesse)
    Assert.Equal(int (System.Math.Round(float normalReflex * 0.75)), overwhelmed.GetCombatStat StatId.Reflex)
    Assert.Equal(normalForce, overwhelmed.GetCombatStat StatId.Force)

    // 3. There is no Poise strain band any more: the Prowess/Poise `Frustration` meter is gone, and the
    //    surviving Frustration is the Finesse/Reflex one, so 75% of it buys nothing from Poise.
    let frustrated =
      baseFighter
      |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 75 })
    Assert.Equal(normalPoise, frustrated.GetCombatStat StatId.Poise)

  [<Fact>]
  let ``Single-turn damage under 35 percent when unstrained does not trigger Pain Shock`` () =
    let roller = fixedRoller 5
    let attacker = TierFactory.createClassLevel CharacterClass.Warrior 1
    // Target with huge health pool so 100 damage is < 35%
    let target = Combatant.create (CombatantId.New()) "Titan" 10000 1000 attacker.Stats
    let action = StandardAttack (ForceStrike false)

    let outcome = ActionResolver.resolveEx roller action attacker target 0
    let hasPainShock =
      outcome.Events
      |> List.exists (function CombatEvent.PainShockSuffered _ -> true | _ -> false)
    Assert.False(hasPainShock, "Damage under 35% Max HP (when unstrained) must not trigger Pain Shock")
    Assert.False(outcome.Target.IsStaggered, "Target should not be staggered")

  [<Fact>]
  let ``Single-turn damage exceeding 35 percent triggers Pain Shock and yields Vor on failed Poise test`` () =
    let roller = fixedRoller 100 // High roll to guarantee damage
    // High-tier attacker vs fragile target with low Poise holding the Vor
    let attacker = TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster
    let fragileTarget = TierFactory.createClassLevel CharacterClass.Rogue 1 |> Combatant.seizeVor
    let action = StandardAttack (ForceStrike false)

    let outcome = ActionResolver.resolveEx roller action attacker fragileTarget 0
    let painShockEvent =
      outcome.Events
      |> List.tryPick (function CombatEvent.PainShockSuffered(_, dmg, pct, poise, resisted) -> Some (dmg, pct, poise, resisted) | _ -> None)

    Assert.True(painShockEvent.IsSome, "Heavy damage should emit PainShockSuffered")
    let _, pct, _, resisted = painShockEvent.Value
    Assert.True(pct >= 35, sprintf "Damage percentage should be >= 35%% (Actual: %d%%)" pct)
    if not resisted then
      Assert.False(outcome.Target.IsStaggered, "Pain Shock failure should yield tempo rather than skipping a full turn")
      Assert.True(outcome.Target.IsInNach, "Target should yield the Vor upon failing Pain Shock check")
      // Poise guards the condition tracks, so the surge is the tempered share rather than the raw 10/10.
      let frustration = ActionResolver.temperConditionAmount fragileTarget 10
      let exhaustion = ActionResolver.temperConditionAmount fragileTarget 10
      Assert.True(outcome.Target.Meters.Frustration.Value >= frustration, "Frustration should surge on Pain Shock failure, less whatever Poise holds")
      Assert.True(outcome.Target.Meters.Exhaustion.Value >= exhaustion, "Exhaustion should surge on Pain Shock failure, less whatever Poise holds")

  [<Fact>]
  let ``Staggered combatant recovers on turn clear`` () =
    let c = TierFactory.createClassLevel CharacterClass.Warrior 1 |> Combatant.stagger
    Assert.True(c.IsStaggered)
    let cleared = c |> Combatant.clearStagger
    Assert.False(cleared.IsStaggered)
