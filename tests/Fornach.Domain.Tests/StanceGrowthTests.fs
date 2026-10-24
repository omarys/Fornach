namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine

/// The file outlived its original subject: stance growth (kill +5 to the stance's attack attribute,
/// blow +2 to its guard) is gone, replaced by the Flow/Momentum/Focus ramp. What remains under test is
/// that no turn moves a permanent attribute, and the stance attribute table itself.
module StanceGrowthTests =

  let private stats force fortitude finesse reflex prowess poise =
    StatBlock.Create [
      Force, force
      Fortitude, fortitude
      Finesse, finesse
      Reflex, reflex
      Prowess, prowess
      Poise, poise
      Intellect, force
      Resolve, fortitude
      Acuity, finesse
      Intuition, reflex
      Acumen, prowess
      Composure, poise
    ]

  let private fighter (force, fortitude, finesse, reflex, prowess, poise) (stance: CombatStance) =
    Combatant.create (CombatantId.New()) "TestFighter" 2000 2000 (stats force fortitude finesse reflex prowess poise)
    |> Combatant.setStance stance

  [<Fact>]
  let ``each stance names the attributes it sharpens and hardens on both planes`` () =
    let physical = fighter (100, 100, 100, 100, 100, 100) CombatStance.PowerStance

    let attributesFor (stance: CombatStance) (plane: Plane) =
      let c = { physical with Stance = stance; Class = if plane = Plane.Mental then CharacterClass.Mage else CharacterClass.Warrior }
      c.StanceAttributes

    Assert.Equal((Force, Fortitude), attributesFor CombatStance.PowerStance Plane.Physical)
    Assert.Equal((Finesse, Reflex), attributesFor CombatStance.AgilityStance Plane.Physical)
    Assert.Equal((Prowess, Poise), attributesFor CombatStance.DisciplineStance Plane.Physical)

    let mental = TierFactory.createClassLevel CharacterClass.Mage 40
    Assert.Equal(Plane.Mental, mental.Plane)
    Assert.Equal((Intellect, Resolve), { mental with Stance = CombatStance.PowerStance }.StanceAttributes)
    Assert.Equal((Acuity, Intuition), { mental with Stance = CombatStance.AgilityStance }.StanceAttributes)
    Assert.Equal((Acumen, Composure), { mental with Stance = CombatStance.DisciplineStance }.StanceAttributes)

  [<Fact>]
  let ``no exchange moves a permanent attribute on either side`` () =
    // Force/Fortitude and Finesse/Reflex belong to the combat ramp (Momentum, Flow) and to gear. A blow
    // taken and a foe dropped must leave the stat block byte-identical — the old +2 stance fortification
    // and the old kill bonus both lived here and both compounded without bound.
    let roller: DiceRoller = fun _ _ -> 6
    let actor = fighter (900, 100, 100, 100, 100, 100) CombatStance.PowerStance
    let victim =
      Combatant.create (CombatantId.New()) "TestVictim" 40 2000 (stats 10 10 10 10 10 10)
      |> Combatant.setStance CombatStance.AgilityStance

    let result = ActionResolver.resolve roller (StandardAttack(ForceStrike false)) actor victim

    Assert.True(result.Target.Health.Current <= 0, "the victim should be dropped in one blow")
    Assert.Equal<Map<StatId, int>>(actor.Stats.ToMap(), result.Actor.Stats.ToMap())
    Assert.Equal<Map<StatId, int>>(victim.Stats.ToMap(), result.Target.Stats.ToMap())

  [<Fact>]
  let ``a turn that neither lands nor takes a blow leaves both fighters untouched`` () =
    let roller: DiceRoller = fun _ _ -> 1
    let actor = fighter (100, 100, 100, 100, 100, 100) CombatStance.PowerStance
    let target = fighter (100, 100, 100, 100, 100, 100) CombatStance.PowerStance

    let result = ActionResolver.resolveWithoutLethal roller (StandardAttack(ForceStrike false)) actor target

    if
      result.Actor.Health.Current = actor.Health.Current
      && result.Actor.Morale.Current = actor.Morale.Current
    then
      Assert.Equal<Map<StatId, int>>(actor.Stats.ToMap(), result.Actor.Stats.ToMap())

    if
      result.Target.Health.Current = target.Health.Current
      && result.Target.Morale.Current = target.Morale.Current
    then
      Assert.Equal<Map<StatId, int>>(target.Stats.ToMap(), result.Target.Stats.ToMap())

  /// Two flankers whose guard sits at very different distances below the interceptor's vector. The
  /// reaction lands on both; the wider gap must hit harder.
  let private opportunityDamage (flankerReflex: int) : int =
    let roller: DiceRoller = fun _ _ -> 1
    let interceptor =
      Combatant.create (CombatantId.New()) "Interceptor" 4000 4000 (stats 100 100 400 400 400 100)
      |> Combatant.setStance CombatStance.AgilityStance
      |> fun c -> Combatant.createWithClass c.Id c.Name 4000 4000 c.Stats CharacterClass.Warrior 40
      |> Combatant.setStance CombatStance.AgilityStance

    let flanker =
      Combatant.create (CombatantId.New()) "Flanker" 4000 4000 (stats 100 100 flankerReflex flankerReflex 100 900)
      |> Combatant.setStance CombatStance.PowerStance

    let result = ActionResolver.resolveEx roller (StandardAttack(ForceStrike false)) flanker interceptor 1

    result.Events
    |> List.pick (function
      | CombatEvent.AttackOfOpportunityTriggered (_, _, _, dmg, _) -> Some dmg
      | _ -> None)

  [<Fact>]
  let ``opportunity attack damage scales with the gap between interceptor and flanker`` () =
    let wideGap = opportunityDamage 10
    let narrowGap = opportunityDamage 300

    Assert.True(narrowGap > 0, "a near-matched flanker should still eat a reaction")
    Assert.True(wideGap > narrowGap, sprintf "expected %d (wide gap) > %d (narrow gap)" wideGap narrowGap)
