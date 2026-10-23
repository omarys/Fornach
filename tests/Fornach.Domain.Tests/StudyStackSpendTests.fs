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
    // Measured crossing points (top roll, same-tier Fighter champion, damage linear in the multiplier
    // and clamped by the pool): four stacks GrandMaster, five Master, six Veteran. GrandMaster being
    // earliest is the direction §1 G fought against, so if this ever needs flattening the dial is a
    // per-tier rate — fitting one flat constant to three points is guesswork without a duel sim.
    let firstKill stacks tier =
      let pool = (TierFactory.createClassTier CharacterClass.Fighter tier).Health.Maximum
      let dealt n =
        let target = TierFactory.createClassTier CharacterClass.Fighter tier
        let actor = { TierFactory.createClassTier CharacterClass.Fighter tier with StudyStacks = n }
        let res = ActionResolver.resolve topRoll (StandardAttack CalculatedFlawStrike) actor target
        target.Health.Current - res.Target.Health.Current
      Assert.True(stacks < 2 || dealt (stacks - 1) < pool, sprintf "%A: %d stacks should not already delete the pool" tier (stacks - 1))
      Assert.True(dealt stacks >= pool, sprintf "%A: %d stacks should reach the one-shot threshold" tier stacks)
      stacks

    Assert.Equal(4, firstKill 4 CombatTier.GrandMaster)
    Assert.Equal(5, firstKill 5 CombatTier.Master)
    Assert.Equal(6, firstKill 6 CombatTier.Veteran)

  [<Fact>]
  let ``a two-stack spend is not a finisher`` () =
    let pool = (TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master).Health.Maximum
    let target = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let res = ActionResolver.resolve topRoll (StandardAttack CalculatedFlawStrike) (flawStrike 2) target
    let dealt = target.Health.Current - res.Target.Health.Current
    Assert.True(dealt < pool, sprintf "two stacks must leave the target standing: %d of %d" dealt pool)

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
