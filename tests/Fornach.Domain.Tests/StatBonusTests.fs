namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain

/// Bonuses on stats are percentages of the bearer's own current value. Flat points were the old rule
/// and they went stale — +30 Fortitude is +67% at level 1 and +3% at level 200 — so an authored item
/// quietly stopped mattering. These pin the rule itself, not the authored numbers.
module StatBonusTests =

  [<Fact>]
  let ``a percentage bonus lands the same proportion at every tier`` () =
    // Novice primary is 45, an endgame primary is 841. The same 56% is equally relevant on both,
    // which is exactly what flat points could not do.
    Assert.Equal(25, StatBonus.percentOf 45 56)
    Assert.Equal(471, StatBonus.percentOf 841 56)

  [<Fact>]
  let ``a percentage bonus never rounds away to nothing`` () =
    // A 22% bonus on a floor stat of 1 still has to be worth a point.
    Assert.Equal(1, StatBonus.percentOf 1 22)
    Assert.Equal(1, StatBonus.percentOf 3 1)

  [<Fact>]
  let ``equipment resolves its bonuses against the bearer's current stats`` () =
    let item =
      { Name = "Test Blade"
        Slot = EquipmentSlot.Weapon
        Description = "test"
        StatPercentBonuses = [ StatId.Force, 50 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [] }

    let bearer =
      Combatant.create (CombatantId.New()) "Bearer" 100 100 (StatBlock.Create [ StatId.Force, 100 ])

    let geared = Combatant.applyItemBonus item bearer

    Assert.Equal(150, geared.Stats.Get StatId.Force)

    // Applying the same item to a stronger bearer grants more points, not the same 50.
    let stronger =
      Combatant.create (CombatantId.New()) "Stronger" 100 100 (StatBlock.Create [ StatId.Force, 200 ])

    Assert.Equal(300, (Combatant.applyItemBonus item stronger).Stats.Get StatId.Force)
