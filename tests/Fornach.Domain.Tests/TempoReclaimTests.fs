namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// A foe that ambushes from out of sight takes the Vor, pinning the player in the Nach. These pin the
/// ways back out of that pin, and what happens when a combatant finally breaks.
module TempoReclaimTests =

  let private player (c: Combatant) = { c with PlayerStyle = true }

  let private bandit () =
    TierFactory.createClassTier CharacterClass.Warrior CombatTier.Veteran
    |> Combatant.seizeVor

  [<Fact>]
  let ``Guarding reclaims the Vor from the foe that holds it`` () =
    // The reported case: pinned in the Nach by an ambusher, guard stacks banked, still no tempo.
    // The old condition was `IsInNach && not target.IsInVor`, so a foe holding the Vor could never lose
    // it to a defended round -- and a braced strike lands-but-is-soaked, so the whiff path never fired.
    let fighter =
      (TierFactory.createClassLevel CharacterClass.Fighter 25 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }
      |> Combatant.addGuardStacks 25

    let foe = bandit ()
    Assert.True(fighter.IsInNach, "precondition: player is in the Nach")
    Assert.True(foe.IsInVor, "precondition: foe holds the Vor")

    let outcome = ActionResolver.resolveActiveDefense (FortitudeGuard true) fighter foe

    Assert.True(outcome.Actor.IsInVor, "a defended round must reclaim the Vor")
    Assert.True(outcome.Target.IsInNach, "the defended foe must be put in the Nach")

  [<Fact>]
  let ``Guarding takes the Vor only from the foe it answered`` () =
    // Seizure is per-target: the rest of a pack keeps whatever tempo it had.
    let fighter =
      (TierFactory.createClassLevel CharacterClass.Fighter 25 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }

    let answered = bandit ()
    let bystander = bandit ()

    let outcome = ActionResolver.resolveActiveDefense (FortitudeGuard true) fighter answered

    Assert.True(outcome.Actor.IsInVor, "the defended foe's Vor must be taken")
    Assert.True(bystander.IsInVor, "an unengaged pack member keeps its own tempo")

  [<Fact>]
  let ``Collapse surrenders the Vor`` () =
    // Reaching 100% on a status meter used to do nothing but mark the combatant execute-eligible, so a
    // collapsed foe kept the Vor and kept swinging.
    let breaking =
      TierFactory.createClassTier CharacterClass.Warrior CombatTier.Veteran
      |> Combatant.seizeVor
      |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + 100 })

    Assert.True(breaking.IsInVor, "precondition: the foe holds the Vor")

    let collapsed = Combatant.evaluateCollapse breaking

    Assert.True(CollapseState.isCollapsed collapsed.Collapse, "100% Exhaustion must collapse the foe")
    Assert.True(collapsed.IsInNach, "a collapsed combatant surrenders the Vor")
    Assert.True(collapsed.IsExecuteEligible, "a collapsed combatant stays execute-eligible")

  [<Fact>]
  let ``A fully absorbed strike taxes the attacker and hands over the Vor`` () =
    // Braced Fortitude Guard soaks a strike to zero rather than making it miss, so before this the
    // attacker paid nothing: it "landed" for 0 and neither the whiff nor the posture path fired.
    let mutable call = 0
    let alternating min max =
      call <- call + 1
      if call % 2 = 1 then max else min

    let attacker =
      (TierFactory.createClassLevel CharacterClass.Warrior 20 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }

    let defender =
      (TierFactory.createClassLevel CharacterClass.Warrior 20 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Nach }
      |> Combatant.addWard 100000

    let outcome =
      ActionResolver.resolveExWithDefense
        alternating
        (StandardAttack (ForceStrike false))
        None
        attacker
        defender
        0

    let damageDealt =
      outcome.Events
      |> List.sumBy (function CombatEvent.DamageApplied e -> e.Amount | _ -> 0)

    Assert.Equal(0, damageDealt)
    Assert.True(
      outcome.Actor.Meters.Exhaustion.Value >= 20,
      sprintf "an absorbed strike must exhaust the attacker (got %d)" outcome.Actor.Meters.Exhaustion.Value
    )
    Assert.True(outcome.Target.IsInVor, "the defender who absorbed the strike must hold the Vor")

  [<Fact>]
  let ``Status pressure bleeds off out of combat`` () =
    // The 1% a defended round costs is meant to be temporary, so it must not compound across a floor.
    let tired =
      TierFactory.createClassLevel CharacterClass.Fighter 20 |> player
      |> Combatant.updateMeters (fun m ->
        { m with
            Exhaustion = m.Exhaustion + 40
            Frustration = m.Frustration + 40
            Recklessness = m.Recklessness + 40 })

    let rested = TowerSession.recoverOutOfCombat tired 5 0 0 0

    Assert.Equal(30, rested.Meters.Exhaustion.Value)
    Assert.Equal(30, rested.Meters.Frustration.Value)
    Assert.Equal(30, rested.Meters.Recklessness.Value)

  [<Fact>]
  let ``Status decay floors at zero`` () =
    let clean = TierFactory.createClassLevel CharacterClass.Fighter 20 |> player

    let rested = TowerSession.recoverOutOfCombat clean 5 0 0 0

    Assert.Equal(0, rested.Meters.Exhaustion.Value)
    Assert.Equal(0, rested.Meters.Frustration.Value)

  [<Fact>]
  let ``Tempo is per engagement: landing while holding the Vor leaves the defender alone`` () =
    // Reported case: a pack ambush where every sprite held the Vor and the player held one too. The rules
    // treated the Vor as a single pool, so a chip hit from a non-focus foe stripped the tempo the player
    // had won against a different one. That defeder's Vor is only stake in *their* engagement.
    let defender =
      (TierFactory.createClassLevel CharacterClass.Fighter 25 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }
      |> Combatant.addGuardStacks 25

    let insisting = bandit () |> Combatant.seizeVor

    let _, defenderAfter, moved = ActionResolver.resolveLandedTempo insisting defender 3
    Assert.False(moved, "landing while already holding the Vor moves no tempo")
    Assert.True(defenderAfter.IsInVor, "the defender's own engagement is untouched")

  [<Fact>]
  let ``A decisive Nach strike moves the tempo in its own pairing`` () =
    let holder =
      (TierFactory.createClassLevel CharacterClass.Warrior 30 |> player)
      |> fun c -> { c with Tempo = TacticalTiming.Vor }

    let striker = bandit () |> Combatant.yieldVor

    let strikerAfter, holderAfter, moved = ActionResolver.resolveLandedTempo striker holder 2
    Assert.True(moved, "two net hits from the Nach still takes the Vor")
    Assert.True(strikerAfter.IsInVor)
    Assert.True(holderAfter.IsInNach)
