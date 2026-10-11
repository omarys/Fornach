namespace Fornach.Tests

open Xunit
open Fornach.Domain

/// Stance affinity is applied to the combat-resolution stat view (`GetCombatStat`),
/// leaving canonical `Stats`/`GetStat` stance-neutral.
module StanceTests =

  [<Fact>]
  let ``Power stance boosts Power-vector combat stats and penalizes the plane's other vectors`` () =
    let berserker = TierFactory.createClassLevel CharacterClass.Berserker 40 // PowerStance

    Assert.Equal(CombatStance.PowerStance, berserker.Stance)
    Assert.True(berserker.GetCombatStat Force > berserker.Stats.Get Force)
    Assert.True(berserker.GetCombatStat Fortitude > berserker.Stats.Get Fortitude)
    Assert.True(berserker.GetCombatStat Finesse < berserker.Stats.Get Finesse)
    Assert.True(berserker.GetCombatStat Prowess < berserker.Stats.Get Prowess)

  [<Fact>]
  let ``Agility stance boosts Agility-vector combat stats and penalizes the plane's other vectors`` () =
    let duelist = TierFactory.createClassLevel CharacterClass.Duelist 40 // AgilityStance

    Assert.Equal(CombatStance.AgilityStance, duelist.Stance)
    Assert.True(duelist.GetCombatStat Finesse > duelist.Stats.Get Finesse)
    Assert.True(duelist.GetCombatStat Reflex > duelist.Stats.Get Reflex)
    Assert.True(duelist.GetCombatStat Force < duelist.Stats.Get Force)

  [<Fact>]
  let ``Discipline stance boosts Discipline-vector combat stats and penalizes the plane's other vectors`` () =
    let warden = TierFactory.createClassLevel CharacterClass.Warden 40 // DisciplineStance

    Assert.Equal(CombatStance.DisciplineStance, warden.Stance)
    Assert.True(warden.GetCombatStat Prowess > warden.Stats.Get Prowess)
    Assert.True(warden.GetCombatStat Poise > warden.Stats.Get Poise)
    Assert.True(warden.GetCombatStat Finesse < warden.Stats.Get Finesse)

  [<Fact>]
  let ``canonical stats and GetStat stay stance-neutral`` () =
    let berserker = TierFactory.createClassLevel CharacterClass.Berserker 40

    Assert.Equal(berserker.Stats.Get Force, berserker.GetStat Force)
    Assert.Equal(berserker.Stats.Get Finesse, berserker.GetStat Finesse)
    Assert.Equal(berserker.Stats.Get Intellect, berserker.GetCombatStat Intellect) // off-plane, unaffected

  [<Fact>]
  let ``a mental combatant's stance boosts its own plane's vector`` () =
    let mage = TierFactory.createClassLevel CharacterClass.Inquisitor 40 // PowerStance, Mental plane

    Assert.Equal(Plane.Mental, mage.Plane)
    Assert.True(mage.GetCombatStat Intellect > mage.Stats.Get Intellect)
    Assert.True(mage.GetCombatStat Acuity < mage.Stats.Get Acuity)

  [<Fact>]
  let ``shifting stance changes the combat stat view`` () =
    let berserker = TierFactory.createClassLevel CharacterClass.Berserker 40
    let powerForce = berserker.GetCombatStat Force
    let powerFinesse = berserker.GetCombatStat Finesse

    let shifted = { berserker with Stance = CombatStance.AgilityStance }

    Assert.True(shifted.GetCombatStat Force < powerForce)
    Assert.True(shifted.GetCombatStat Finesse > powerFinesse)
