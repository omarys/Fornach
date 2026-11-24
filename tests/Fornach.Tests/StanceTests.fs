namespace Fornach.Tests

open Xunit
open Fornach.Domain

/// Stance affinity is applied to the combat-resolution stat view (`GetCombatStat`),
/// leaving canonical `Stats`/`GetStat` stance-neutral.
module StanceTests =

  [<Fact>]
  let ``Power stance boosts Power-vector combat stats and penalizes the plane's other vectors`` () =
    let berserker = TierFactory.createClassLevel CharacterClass.Warrior 40 // PowerStance

    Assert.Equal(CombatStance.PowerStance, berserker.Stance)
    Assert.True(berserker.GetCombatStat Force > berserker.Stats.Get Force)
    Assert.True(berserker.GetCombatStat Fortitude > berserker.Stats.Get Fortitude)
    Assert.True(berserker.GetCombatStat Finesse < berserker.Stats.Get Finesse)
    Assert.True(berserker.GetCombatStat Prowess < berserker.Stats.Get Prowess)

  [<Fact>]
  let ``Agility stance boosts Agility-vector combat stats and penalizes the plane's other vectors`` () =
    let duelist = TierFactory.createClassLevel CharacterClass.Rogue 40 // AgilityStance

    Assert.Equal(CombatStance.AgilityStance, duelist.Stance)
    Assert.True(duelist.GetCombatStat Finesse > duelist.Stats.Get Finesse)
    Assert.True(duelist.GetCombatStat Reflex > duelist.Stats.Get Reflex)
    Assert.True(duelist.GetCombatStat Force < duelist.Stats.Get Force)

  [<Fact>]
  let ``Discipline stance boosts Discipline-vector combat stats and penalizes the plane's other vectors`` () =
    let warden = { TierFactory.createClassLevel CharacterClass.Warrior 40 with Stance = CombatStance.DisciplineStance }

    Assert.Equal(CombatStance.DisciplineStance, warden.Stance)
    Assert.True(warden.GetCombatStat Prowess > warden.Stats.Get Prowess)
    Assert.True(warden.GetCombatStat Poise > warden.Stats.Get Poise)
    Assert.True(warden.GetCombatStat Finesse < warden.Stats.Get Finesse)

  [<Fact>]
  let ``canonical stats and GetStat stay stance-neutral`` () =
    let berserker = TierFactory.createClassLevel CharacterClass.Warrior 40

    Assert.Equal(berserker.Stats.Get Force, berserker.GetStat Force)
    Assert.Equal(berserker.Stats.Get Finesse, berserker.GetStat Finesse)
    Assert.Equal(berserker.Stats.Get Intellect, berserker.GetCombatStat Intellect) // off-plane, unaffected

  [<Fact>]
  let ``a mental combatant's stance boosts its own plane's vector`` () =
    let mage = TierFactory.createClassLevel CharacterClass.Mage 40 // PowerStance, Mental plane

    Assert.Equal(Plane.Mental, mage.Plane)
    Assert.True(mage.GetCombatStat Intellect > mage.Stats.Get Intellect)
    Assert.True(mage.GetCombatStat Acuity < mage.Stats.Get Acuity)

  [<Fact>]
  let ``shifting stance changes the combat stat view`` () =
    let berserker = TierFactory.createClassLevel CharacterClass.Warrior 40
    let powerForce = berserker.GetCombatStat Force
    let powerFinesse = berserker.GetCombatStat Finesse

    let shifted = { berserker with Stance = CombatStance.AgilityStance }

    Assert.True(shifted.GetCombatStat Force < powerForce)
    Assert.True(shifted.GetCombatStat Finesse > powerFinesse)

  [<Fact>]
  let ``Fighter has normalized physical stats and stance bonus creates primary vs off-stat ratios`` () =
    let fighter = TierFactory.createClassLevel CharacterClass.Fighter 200 // GrandMaster tier
    // Budget-neutral ±5% vector asymmetry around prim=841: Power pair 883, Agility pair 799, Discipline pair 841
    Assert.Equal(883, fighter.GetStat Force)
    Assert.Equal(883, fighter.GetStat Fortitude)
    Assert.Equal(799, fighter.GetStat Finesse)
    Assert.Equal(799, fighter.GetStat Reflex)
    Assert.Equal(841, fighter.GetStat Prowess)
    Assert.Equal(841, fighter.GetStat Poise)

    // In PowerStance: Force/Fortitude get +25% (1104), off-vector offense drops to 75% (599 / 631),
    // Reflex & Poise (defenses) keep their base guard (799 / 841)
    Assert.Equal(1104, fighter.GetCombatStat Force)
    Assert.Equal(1104, fighter.GetCombatStat Fortitude)
    Assert.Equal(599, fighter.GetCombatStat Finesse)
    Assert.Equal(799, fighter.GetCombatStat Reflex)
    Assert.Equal(631, fighter.GetCombatStat Prowess)
    Assert.Equal(841, fighter.GetCombatStat Poise)

    // Shift to AgilityStance:
    let agilityFighter = { fighter with Stance = CombatStance.AgilityStance }
    Assert.Equal(662, agilityFighter.GetCombatStat Force)
    Assert.Equal(883, agilityFighter.GetCombatStat Fortitude)
    Assert.Equal(999, agilityFighter.GetCombatStat Finesse)
    Assert.Equal(999, agilityFighter.GetCombatStat Reflex)
    Assert.Equal(631, agilityFighter.GetCombatStat Prowess)
    Assert.Equal(841, agilityFighter.GetCombatStat Poise)

    // Shift to DisciplineStance:
    let disciplineFighter = { fighter with Stance = CombatStance.DisciplineStance }
    Assert.Equal(662, disciplineFighter.GetCombatStat Force)
    Assert.Equal(883, disciplineFighter.GetCombatStat Fortitude)
    Assert.Equal(599, disciplineFighter.GetCombatStat Finesse)
    Assert.Equal(799, disciplineFighter.GetCombatStat Reflex)
    Assert.Equal(1051, disciplineFighter.GetCombatStat Prowess)
    Assert.Equal(1051, disciplineFighter.GetCombatStat Poise)

  [<Fact>]
  let ``Mage has normalized mental stats and complex form bonus creates primary vs off-stat ratios`` () =
    let mage = TierFactory.createClassLevel CharacterClass.Mage 200 // GrandMaster tier
    // Budget-neutral ±5% vector asymmetry around prim=841: Power pair 883, Agility pair 799, Discipline pair 841
    Assert.Equal(883, mage.GetStat Intellect)
    Assert.Equal(883, mage.GetStat Resolve)
    Assert.Equal(799, mage.GetStat Acuity)
    Assert.Equal(799, mage.GetStat Intuition)
    Assert.Equal(841, mage.GetStat Prowess)
    Assert.Equal(841, mage.GetStat Poise) // own primary; spell armour only covers Fortitude/Reflex

    // Default form is ResonanceSpike (Power):
    Assert.Equal(Some ComplexForm.ResonanceSpike, mage.ComplexForm)
    Assert.Equal(1104, mage.GetCombatStat Intellect)
    Assert.Equal(1104, mage.GetCombatStat Resolve)
    Assert.Equal(599, mage.GetCombatStat Acuity)
    Assert.Equal(799, mage.GetCombatStat Intuition)
    Assert.Equal(631, mage.GetCombatStat Prowess)
    Assert.Equal(841, mage.GetCombatStat Poise)

    // Thread PhantasmalDiffusion (Agility):
    let diffMage = { mage with ComplexForm = Some ComplexForm.PhantasmalDiffusion }
    Assert.Equal(662, diffMage.GetCombatStat Intellect)
    Assert.Equal(883, diffMage.GetCombatStat Resolve)
    Assert.Equal(999, diffMage.GetCombatStat Acuity)
    Assert.Equal(999, diffMage.GetCombatStat Intuition)
    Assert.Equal(631, diffMage.GetCombatStat Prowess)
    Assert.Equal(841, diffMage.GetCombatStat Poise)

    // Thread AegisLattice (Discipline):
    let latticeMage = { mage with ComplexForm = Some ComplexForm.AegisLattice }
    Assert.Equal(662, latticeMage.GetCombatStat Intellect)
    Assert.Equal(883, latticeMage.GetCombatStat Resolve)
    Assert.Equal(599, latticeMage.GetCombatStat Acuity)
    Assert.Equal(799, latticeMage.GetCombatStat Intuition)
    Assert.Equal(1051, latticeMage.GetCombatStat Prowess)
    Assert.Equal(1051, latticeMage.GetCombatStat Poise) // Discipline vector: 841 x1.25
