namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain

/// `Combatant.growFromEncounter` replaced the old Study Stack ramp: a won fight leaves a permanent
/// mark on Prowess and Poise plus one off-type tick, weighted by the strongest foe's level.
module EncounterGrowthTests =

  let private statOf (stat: StatId) (c: Combatant) = c.Stats.Get stat

  let private foe (cls: CharacterClass) (tier: CombatTier) = TierFactory.createClassTier cls tier

  [<Fact>]
  let ``a novice victory raises Prowess and Poise by one apiece`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let grown = Combatant.growFromEncounter [ foe CharacterClass.Warrior CombatTier.Novice ] before

    Assert.Equal(statOf StatId.Prowess before + 1, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 1, statOf StatId.Poise grown)

  [<Fact>]
  let ``a master foe teaches two points and a grandmaster three`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master

    let grownAgainst tier =
      Combatant.growFromEncounter [ foe CharacterClass.Warrior tier ] before

    let againstMaster = grownAgainst CombatTier.Master
    let againstGrandMaster = grownAgainst CombatTier.GrandMaster

    Assert.Equal(statOf StatId.Prowess before + 2, statOf StatId.Prowess againstMaster)
    Assert.Equal(statOf StatId.Poise before + 2, statOf StatId.Poise againstMaster)
    Assert.Equal(statOf StatId.Prowess before + 3, statOf StatId.Prowess againstGrandMaster)
    Assert.Equal(statOf StatId.Poise before + 3, statOf StatId.Poise againstGrandMaster)

  [<Fact>]
  let ``the strongest foe in the pack sets the gain`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let pack = [ foe CharacterClass.Warrior CombatTier.Novice; foe CharacterClass.Warrior CombatTier.GrandMaster ]

    let grown = Combatant.growFromEncounter pack before
    Assert.Equal(statOf StatId.Prowess before + 3, statOf StatId.Prowess grown)

  [<Fact>]
  let ``a Fighter sharpens Intuition and Composure, a Mage Acumen and Composure`` () =
    let fighter = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.Master
    let novices = [ foe CharacterClass.Warrior CombatTier.Novice ]

    let grownFighter = Combatant.growFromEncounter novices fighter
    Assert.Equal(statOf StatId.Intuition fighter + 1, statOf StatId.Intuition grownFighter)
    Assert.Equal(statOf StatId.Composure fighter + 1, statOf StatId.Composure grownFighter)
    Assert.Equal(statOf StatId.Acumen fighter, statOf StatId.Acumen grownFighter)
    Assert.Equal(statOf StatId.Force fighter, statOf StatId.Force grownFighter)
    Assert.Equal(statOf StatId.Finesse fighter, statOf StatId.Finesse grownFighter)

    let grownMage = Combatant.growFromEncounter novices mage
    Assert.Equal(statOf StatId.Acumen mage + 1, statOf StatId.Acumen grownMage)
    Assert.Equal(statOf StatId.Composure mage + 1, statOf StatId.Composure grownMage)
    Assert.Equal(statOf StatId.Intuition mage, statOf StatId.Intuition grownMage)

  [<Fact>]
  let ``a Mage grows Reflex and Poise instead of Prowess, and at no tier grows Prowess`` () =
    let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.Master
    let novices = [ foe CharacterClass.Warrior CombatTier.Novice ]
    let grandMasters = [ foe CharacterClass.Warrior CombatTier.GrandMaster ]

    let noviceWin = Combatant.growFromEncounter novices mage
    Assert.Equal(statOf StatId.Reflex mage + 1, statOf StatId.Reflex noviceWin)
    Assert.Equal(statOf StatId.Poise mage + 1, statOf StatId.Poise noviceWin)
    Assert.Equal(statOf StatId.Prowess mage, statOf StatId.Prowess noviceWin)

    // The scholarly pair carries the tier weight; Reflex and Poise stay a flat tick.
    let hardWin = Combatant.growFromEncounter grandMasters mage
    Assert.Equal(statOf StatId.Acumen mage + 3, statOf StatId.Acumen hardWin)
    Assert.Equal(statOf StatId.Composure mage + 3, statOf StatId.Composure hardWin)
    Assert.Equal(statOf StatId.Reflex mage + 1, statOf StatId.Reflex hardWin)
    Assert.Equal(statOf StatId.Prowess mage, statOf StatId.Prowess hardWin)

  [<Fact>]
  let ``the growth actually shows up on the returned combatant's stats`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Novice
    let grown = Combatant.growFromEncounter [ foe CharacterClass.Warrior CombatTier.Novice ] before

    Assert.True(grown.GetStat StatId.Prowess > before.GetStat StatId.Prowess, "Prowess must rise on the returned combatant")
    Assert.True(grown.GetStat StatId.Poise > before.GetStat StatId.Poise, "Poise must rise on the returned combatant")
    Assert.True(grown.GetStat StatId.Intuition > before.GetStat StatId.Intuition, "A Fighter's Intuition must rise on the returned combatant")
