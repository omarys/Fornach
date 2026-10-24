namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine

/// Pins the shape of the combat-ramp ladder that replaced Study Stacks: three meters sharing one
/// ceiling of five, and a same-vector special that cashes its own meter at half a stack per point.
/// A pile is meant to pay off; these assertions exist so a future magnitude change is a deliberate
/// decision, not a quiet drift.
module StudyStackSpendTests =

  let private topRoll (_: int) (_: int) = 6

  let private master () = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master

  let private spentBy (res: ActionResult) =
    res.Events
    |> List.pick (function
      | CombatEvent.DisciplineGambitExecuted(_, _, spent) -> Some spent
      | _ -> None)

  [<Fact>]
  let ``every ramp meter arrives empty and shares one ceiling of five`` () =
    let fresh = master ()
    Assert.Equal(0, fresh.Flow)
    Assert.Equal(0, fresh.Momentum)
    Assert.Equal(0, fresh.Focus)
    Assert.Equal(5, Combatant.StackCap)

  [<Fact>]
  let ``addStacks raises all three meters and clamps them at the cap`` () =
    let raised = master () |> Combatant.addStacks 3
    Assert.Equal(3, raised.Flow)
    Assert.Equal(3, raised.Momentum)
    Assert.Equal(3, raised.Focus)

    let flooded = raised |> Combatant.addStacks 100
    Assert.Equal(Combatant.StackCap, flooded.Flow)
    Assert.Equal(Combatant.StackCap, flooded.Momentum)
    Assert.Equal(Combatant.StackCap, flooded.Focus)

    let drained = flooded |> Combatant.addStacks -100
    Assert.Equal(0, drained.Flow)
    Assert.Equal(0, drained.Momentum)
    Assert.Equal(0, drained.Focus)

  [<Fact>]
  let ``decayStacks takes one off every meter and floors at zero`` () =
    let holding = master () |> Combatant.addStacks 2 |> Combatant.decayStacks
    Assert.Equal(1, holding.Flow)
    Assert.Equal(1, holding.Momentum)
    Assert.Equal(1, holding.Focus)

    let floored = master () |> Combatant.decayStacks
    Assert.Equal(0, floored.Flow)
    Assert.Equal(0, floored.Momentum)
    Assert.Equal(0, floored.Focus)

  [<Fact>]
  let ``drainStacks empties only the meter that gates the vector`` () =
    let holding = master () |> Combatant.addStacks 4
    let drainedOnce = holding |> Combatant.drainStacks Vector.Power 2
    Assert.Equal(4, drainedOnce.Flow)
    Assert.Equal(2, drainedOnce.Momentum)
    Assert.Equal(4, drainedOnce.Focus)

    // stacksIn reads back the meter that gates each vector.
    Assert.Equal(4, Combatant.stacksIn Vector.Power holding)
    Assert.Equal(4, Combatant.stacksIn Vector.Agility holding)
    Assert.Equal(4, Combatant.stacksIn Vector.Discipline holding)

    // An over-drain clamps at zero instead of going negative.
    let overDrained = holding |> Combatant.drainStacks Vector.Discipline 99
    Assert.Equal(0, overDrained.Focus)
    Assert.Equal(4, overDrained.Flow)
    Assert.Equal(4, overDrained.Momentum)

  [<Fact>]
  let ``a cash-out pays 1.0 + 0.35n and nothing extra on an empty meter`` () =
    let mult stacks = ActionResolver.strikeMultiplier CalculatedFlawStrike stacks CombatStance.PowerStance
    Assert.Equal(1.0, mult 0, 6)
    Assert.Equal(1.35, mult 1, 6)
    Assert.Equal(1.7, mult 2, 6)

    // Five is the ceiling the meters clamp to, so 2.75x is the most a swing can cash for.
    Assert.Equal(2.75, mult Combatant.StackCap, 6)

  [<Fact>]
  let ``resolving a special cashes the whole meter and leaves the swing's own stack behind`` () =
    let actor = { master () with Focus = Combatant.StackCap }
    let target = master ()

    let res = ActionResolver.resolve topRoll (StandardAttack CalculatedFlawStrike) actor target

    Assert.Equal(Combatant.StackCap, spentBy res)
    // The cash-out empties Focus, then every attack feeds one stack back to all three meters.
    Assert.Equal(1, res.Actor.Focus)
    Assert.Equal(1, res.Actor.Flow)
    Assert.Equal(1, res.Actor.Momentum)

  [<Fact>]
  let ``the finisher threshold sits at a full five-stack meter, not two`` () =
    // Measured against a same-tier Master pool: the 2.75x a topped-out meter cashes is the biggest
    // hit in the game and still leaves an equal-tier opponent standing (one-shot threshold 3.07x at
    // Master, 2.79x at GrandMaster). If this needs retuning the dial is the per-stack 0.35 in
    // ActionResolver.spentPerStack.
    let pool = (master ()).Health.Maximum

    let dealt stacks =
      let target = master ()
      let actor = { master () with Focus = stacks }
      let res = ActionResolver.resolve topRoll (StandardAttack CalculatedFlawStrike) actor target
      target.Health.Current - res.Target.Health.Current

    Assert.True(dealt 2 < pool, sprintf "two stacks must leave the target standing: %d of %d" (dealt 2) pool)

    Assert.True(
      dealt Combatant.StackCap < pool,
      sprintf "a full meter must not delete a same-tier pool: %d of %d" (dealt Combatant.StackCap) pool
    )

    // ... but it is still the finisher: it takes the great majority of that pool.
    Assert.True(
      dealt Combatant.StackCap * 100 >= pool * 85,
      sprintf "a full meter should still be near-lethal: %d of %d" (dealt Combatant.StackCap) pool
    )

  [<Fact>]
  let ``one stack is a normal hit, a full pile is a finisher`` () =
    let pool = (master ()).Health.Maximum

    let dealt stacks =
      let target = master ()
      let actor = { master () with Focus = stacks }
      let res = ActionResolver.resolve topRoll (StandardAttack CalculatedFlawStrike) actor target
      target.Health.Current - res.Target.Health.Current

    let single = dealt 1
    Assert.True(single < pool, sprintf "a one-stack spend must not delete a same-tier pool: %d of %d" single pool)
    Assert.True(dealt Combatant.StackCap > single, sprintf "a full meter must outhit a single stack: %d vs %d" (dealt Combatant.StackCap) single)
