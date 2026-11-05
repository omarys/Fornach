namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

module FlankDamageTests =

  let private fixedRoller (n: int) : DiceRoller = fun _ _ -> n

  let private makeAttacker () =
    TierFactory.createClassLevel CharacterClass.Warrior 20
    |> fun c -> { c with PlayerStyle = true; Tempo = TacticalTiming.Vor }

  let private makeTarget () =
    TierFactory.createClassLevel CharacterClass.Fighter 1
    |> fun c -> { c with Tempo = TacticalTiming.Nach }

  /// Health the defender actually loses to one identical strike at the given geometric multiplier.
  let private healthLost (mult: float) =
    let target = makeTarget ()

    let res =
      ActionResolver.resolveExWithDefenseFlank
        (fixedRoller 4)
        (StandardAttack(ForceStrike false))
        None
        (makeAttacker ())
        target
        0
        mult

    target.Health.Current - res.Target.Health.Current

  [<Fact>]
  let ``a rear strike lands harder than a head-on one`` () =
    let front = healthLost 1.0
    let flank = healthLost 1.15
    let rear = healthLost 1.30

    Assert.True(front > 0, sprintf "the strike must land at all (front lost %d)" front)
    Assert.True(flank >= front, sprintf "flank (%d) must not be weaker than front (%d)" flank front)
    Assert.True(rear > flank, sprintf "rear (%d) must exceed flank (%d)" rear flank)

  [<Fact>]
  let ``a neutral multiplier reproduces the old damage exactly`` () =
    // resolveExWithDefense must stay bit-identical to the pre-flanking resolver: every existing
    // balance number and test depends on it.
    let targetA = makeTarget ()

    let viaWrapper =
      ActionResolver.resolveExWithDefense (fixedRoller 4) (StandardAttack(ForceStrike false)) None (makeAttacker ()) targetA 0

    let targetB = makeTarget ()

    let viaFlank =
      ActionResolver.resolveExWithDefenseFlank (fixedRoller 4) (StandardAttack(ForceStrike false)) None (makeAttacker ()) targetB 0 1.0

    Assert.Equal(targetA.Health.Current - viaWrapper.Target.Health.Current, targetB.Health.Current - viaFlank.Target.Health.Current)
    Assert.True(targetA.Health.Current - viaWrapper.Target.Health.Current > 0, "the comparison is vacuous unless the strike lands")
