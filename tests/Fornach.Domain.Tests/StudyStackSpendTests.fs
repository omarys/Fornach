namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine

/// Pins the shape of the study-stack spend ladder (§1 X). A pile is meant to pay off; these
/// assertions exist so a future magnitude change is a deliberate decision, not a quiet drift.
module StudyStackSpendTests =

  let private topRoll (_: int) (_: int) = 6

  let private flawStrike stacks =
    { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master with StudyStacks = stacks }

  [<Fact>]
  let ``a spender pays 1.2 + 0.44n and nothing at one stack`` () =
    let mult n = ActionResolver.strikeMultiplier CalculatedFlawStrike 0 n CombatStance.PowerStance
    Assert.Equal(1.0, mult 1, 6)
    Assert.Equal(2.08, mult 2, 6)
    Assert.Equal(6.48, mult 12, 6)

  [<Fact>]
  let ``the finisher threshold sits at five stacks at Master, not two`` () =
    let pool = (TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master).Health.Maximum
    let single =
      let target = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
      let res = ActionResolver.resolve topRoll (StandardAttack CalculatedFlawStrike) (flawStrike 1) target
      target.Health.Current - res.Target.Health.Current

    // Multiplier needed to delete an equal-tier pool at a top roll, and where the ladder crosses it.
    let needed = float pool / float single
    let mult n = ActionResolver.strikeMultiplier CalculatedFlawStrike 0 n CombatStance.PowerStance

    Assert.True(mult 4 < needed, sprintf "four stacks must not already delete a same-tier pool (%f vs %f)" (mult 4) needed)
    Assert.True(mult 5 >= needed, sprintf "five stacks should reach the one-shot threshold (%f vs %f)" (mult 5) needed)

  [<Fact>]
  let ``one stack is a normal hit, a full pile is a finisher`` () =
    let dealt stacks =
      let target = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
      let res = ActionResolver.resolve topRoll (StandardAttack CalculatedFlawStrike) (flawStrike stacks) target
      target.Health.Current - res.Target.Health.Current

    let pool = (TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master).Health.Maximum
    let single = dealt 1
    let pile = dealt 12

    Assert.True(single < pool, sprintf "a one-stack spend must not delete a same-tier pool: %d of %d" single pool)
    // A banked pile is a finisher (§1 G permits it from five stacks on — see the threshold test).
    Assert.True(pile >= pool, sprintf "a twelve-stack pile should delete a same-tier pool: %d of %d" pile pool)
