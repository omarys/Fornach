namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// A held round is the tower's one pass-turn: it buys the bearer's own defensive reset (Raise Shield for
/// a fighter, Center Mind for a mage), hands the Vor to the engaged foe that does not already hold it,
/// and leaves the combatant only the attack of opportunity a provoking foe grants. These tests pin each
/// half of that bargain.
module WaitTests =

  let private roller (seed: int) : DiceRoller =
    let rng = System.Random(seed)
    fun min max -> rng.Next(min, max + 1)

  let private player () =
    { TierFactory.createClassLevel CharacterClass.Fighter 30 with
        PlayerStyle = true }

  let private foeOf (p: Combatant) =
    TierFactory.clampEngagedToPlayerLevel (Bestiary.createMonster Bestiary.blightSprite) p

  /// A player mid-fight: strain on all three gauges the Steady Form recovery bleeds, and Morale to restore.
  let private strained (actor: Combatant) =
    let meters =
      { actor.Meters with
          Exhaustion = Meter.Create 60
          Recklessness = Meter.Create 55
          Confusion = Meter.Create 40 }

    { actor with
        Meters = meters
        Morale = actor.Morale.ApplyDelta -200 }

  let private isTempoShift (evt: CombatEvent) =
    match evt with
    | CombatEvent.TempoShifted _ -> true
    | _ -> false

  [<Fact>]
  let ``a held round performs the bearer's own defensive reset`` () =
    let actor = strained (player ())
    let foe = foeOf actor

    let waited = ActionResolver.resolve (roller 17) Wait actor foe
    let shielded = ActionResolver.resolve (roller 17) (RecoveryAction RaiseShield) actor foe

    // Same effect as the Raise Shield reset: the fighter's hold is the shield up, not a fourth reset.
    Assert.Equal(shielded.Actor.Armor, waited.Actor.Armor)
    Assert.Equal(shielded.Actor.Meters, waited.Actor.Meters)

    Assert.True(
      waited.Actor.Meters.Exhaustion.Value < actor.Meters.Exhaustion.Value,
      "Raise Shield should bleed strain"
    )

    Assert.True(
      waited.Actor.Meters.Recklessness.Value < actor.Meters.Recklessness.Value,
      "Raise Shield should bleed recklessness"
    )

    Assert.True(waited.Actor.Armor.Boost > 0, "the fighter's hold erects shield plating, not armor repair")
    Assert.Equal(actor.Armor.Current, waited.Actor.Armor.Current)

    Assert.True(
      waited.Actor.Inertia > actor.Inertia,
      "the hold should bank guard stacks (Inertia), not the ramp (Flow/Momentum)"
    )
    Assert.Equal(actor.Momentum, waited.Actor.Momentum)

  [<Fact>]
  let ``a held round performs the mage's Center Mind reset`` () =
    let actor =
      strained { TierFactory.createClassLevel CharacterClass.Mage 30 with PlayerStyle = true }
    let foe = foeOf actor

    let waited = ActionResolver.resolve (roller 17) Wait actor foe
    let centered = ActionResolver.resolve (roller 17) (RecoveryAction CenterMind) actor foe

    Assert.Equal(centered.Actor.ArcaneWard, waited.Actor.ArcaneWard)
    Assert.True(
      waited.Actor.Focus > actor.Focus,
      "the mage's hold should bank guard stacks (Focus), not the ramp"
    )

  [<Fact>]
  let ``a held round makes no attack`` () =
    let actor = player ()
    let foe = foeOf actor

    let res = ActionResolver.resolve (roller 23) Wait actor foe

    Assert.Equal(foe.Health.Current, res.Target.Health.Current)
    Assert.Equal(foe.Health.Maximum, res.Target.Health.Maximum)
    Assert.Equal(foe.Morale.Current, res.Target.Morale.Current)

  [<Fact>]
  let ``a player holding the Vor is in Nach after waiting and cedes the Vor to the foe`` () =
    let actor = player () |> Combatant.seizeVor
    let foe = foeOf actor |> Combatant.yieldVor
    Assert.True(actor.IsInVor)
    Assert.True(foe.IsInNach)

    let res = ActionResolver.resolve (roller 29) Wait actor foe

    Assert.True(res.Actor.IsInNach, "a held round gives up the initiative")
    Assert.True(res.Target.IsInVor, "the engaged foe takes the Vor that was ceded")
    Assert.Contains(res.Events, isTempoShift)

  [<Fact>]
  let ``shield plating is a one-round hold that comes down at the next commitment`` () =
    let actor = strained (player ())
    let foe = foeOf actor

    let waited = ActionResolver.resolve (roller 17) Wait actor foe
    Assert.True(waited.Actor.Armor.Boost > 0, "the hold erects shield plating")

    // Plating is a held shield, not a permanent stat: the bearer's next intent (a strike here)
    // fights unshielded, and only the foe's incoming blow meets the plating in between.
    let struck =
      ActionResolver.resolve (roller 17) (StandardAttack (ForceStrike false)) waited.Actor foe

    Assert.True(struck.Actor.Armor.Boost = 0, "committing to a new intent takes the plating down")

  [<Fact>]
  let ``a foe already holding the Vor keeps it`` () =
    let actor = player () |> Combatant.yieldVor
    let foe = foeOf actor |> Combatant.seizeVor
    Assert.True(foe.IsInVor)

    let res = ActionResolver.resolve (roller 31) Wait actor foe

    Assert.True(res.Target.IsInVor, "a foe already in Vor is not moved again")
    Assert.Equal(foe.Tempo, res.Target.Tempo)

  [<Fact>]
  let ``a waiting player keeps the attack of opportunity a provoking foe grants`` () =
    let actor = player () |> Combatant.yieldVor
    // The blight sprite is Arcane/Mental only and cannot throw the physical strike that provokes, so the
    // engaged foe here is a physical one (the wait itself is exercised against the sprite above).
    let foe =
      TierFactory.clampEngagedToPlayerLevel (Bestiary.createMonster Bestiary.slagHound) actor
      |> Combatant.seizeVor

    let strike = StandardAttack(ForceStrike false)
    let midRoll: DiceRoller = fun _ _ -> 55

    let isOpportunity (evt: CombatEvent) =
      match evt with
      | CombatEvent.AttackOfOpportunityTriggered _ -> true
      | _ -> false

    // The gate counts the defences the defender already answered this round (`priorDefenses`), so a combatant
    // with no spent action is not eligible on the first provocation at all -- that is the baseline.
    let unwaiting = ActionResolver.resolveEx midRoll strike foe actor 0
    Assert.False(unwaiting.Events |> List.exists isOpportunity, "no spent action, no reactive strike")

    let waited = ActionResolver.resolve (roller 37) Wait actor foe
    Assert.True(waited.Actor.HeldRound, "the held round is the spent action")

    let provoked = ActionResolver.resolveEx midRoll strike foe waited.Actor 0
    Assert.True(
      provoked.Events |> List.exists isOpportunity,
      "the foe provoked, so the held round pays for the reactive strike"
    )

    // The credit belongs to that round only: acting again clears it.
    let resumed =
      ActionResolver.resolve (roller 41) (StandardAttack(ForceStrike false)) waited.Actor foe

    Assert.False(resumed.Actor.HeldRound, "acting clears the held round")
