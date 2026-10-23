namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain

/// `Combatant.growFromEncounter` replaced the old Study Stack ramp: a won fight leaves a permanent
/// +2 on the Discipline pair and +1 on each mental defence, regardless of what was beaten.
module EncounterGrowthTests =

  let private statOf (stat: StatId) (c: Combatant) = c.Stats.Get stat

  let private foe (cls: CharacterClass) (tier: CombatTier) = TierFactory.createClassTier cls tier

  [<Fact>]
  let ``a won fight raises Prowess and Poise by two apiece`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let grown = Combatant.growFromEncounter [ foe CharacterClass.Warrior CombatTier.Novice ] before

    Assert.Equal(statOf StatId.Prowess before + 2, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 2, statOf StatId.Poise grown)

  [<Fact>]
  let ``the gain is flat, so a grandmaster teaches no more than a novice`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master

    let grownAgainst tier =
      Combatant.growFromEncounter [ foe CharacterClass.Warrior tier ] before

    for tier in [ CombatTier.Novice; CombatTier.Veteran; CombatTier.Master; CombatTier.GrandMaster ] do
      let grown = grownAgainst tier
      Assert.Equal(statOf StatId.Prowess before + 2, statOf StatId.Prowess grown)
      Assert.Equal(statOf StatId.Poise before + 2, statOf StatId.Poise grown)

  [<Fact>]
  let ``the pack size does not compound the gain`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master

    let pack =
      [ foe CharacterClass.Warrior CombatTier.GrandMaster
        foe CharacterClass.Rogue CombatTier.GrandMaster
        foe CharacterClass.Soldier CombatTier.GrandMaster ]

    let grown = Combatant.growFromEncounter pack before
    Assert.Equal(statOf StatId.Prowess before + 2, statOf StatId.Prowess grown)

  [<Fact>]
  let ``both player classes harden Resolve, Intuition and Composure, and neither grows the other's tick`` () =
    let fighter = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let mage = TierFactory.createClassTier CharacterClass.Mage CombatTier.Master
    let novices = [ foe CharacterClass.Warrior CombatTier.Novice ]

    let grownFighter = Combatant.growFromEncounter novices fighter
    Assert.Equal(statOf StatId.Resolve fighter + 1, statOf StatId.Resolve grownFighter)
    Assert.Equal(statOf StatId.Intuition fighter + 1, statOf StatId.Intuition grownFighter)
    Assert.Equal(statOf StatId.Composure fighter + 1, statOf StatId.Composure grownFighter)
    Assert.Equal(statOf StatId.Acumen fighter, statOf StatId.Acumen grownFighter)
    Assert.Equal(statOf StatId.Force fighter, statOf StatId.Force grownFighter)
    Assert.Equal(statOf StatId.Finesse fighter, statOf StatId.Finesse grownFighter)

    let grownMage = Combatant.growFromEncounter novices mage
    Assert.Equal(statOf StatId.Acumen mage + 2, statOf StatId.Acumen grownMage)
    // Composure is in the Discipline pair and again in the defensive trio, so it takes both.
    Assert.Equal(statOf StatId.Composure mage + 3, statOf StatId.Composure grownMage)
    Assert.Equal(statOf StatId.Resolve mage + 1, statOf StatId.Resolve grownMage)
    Assert.Equal(statOf StatId.Intuition mage + 1, statOf StatId.Intuition grownMage)
    // A caster gains no physical stat at all, and no Prowess.
    Assert.Equal(statOf StatId.Reflex mage, statOf StatId.Reflex grownMage)
    Assert.Equal(statOf StatId.Prowess mage, statOf StatId.Prowess grownMage)
    Assert.Equal(statOf StatId.Poise mage, statOf StatId.Poise grownMage)

  [<Fact>]
  let ``a generic class takes the same Discipline pair and defensive trio as a Fighter`` () =
    let before = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Master
    let grown = Combatant.growFromEncounter [ foe CharacterClass.Warrior CombatTier.Novice ] before

    Assert.Equal(statOf StatId.Prowess before + 2, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 2, statOf StatId.Poise grown)
    Assert.Equal(statOf StatId.Resolve before + 1, statOf StatId.Resolve grown)
    Assert.Equal(statOf StatId.Composure before + 1, statOf StatId.Composure grown)
    // Force and Finesse are outside the growth set now.
    Assert.Equal(statOf StatId.Force before, statOf StatId.Force grown)
    Assert.Equal(statOf StatId.Finesse before, statOf StatId.Finesse grown)

  [<Fact>]
  let ``the growth actually shows up on the returned combatant's stats`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Novice
    let grown = Combatant.growFromEncounter [ foe CharacterClass.Warrior CombatTier.Novice ] before

    Assert.True(grown.GetStat StatId.Prowess > before.GetStat StatId.Prowess, "Prowess must rise on the returned combatant")
    Assert.True(grown.GetStat StatId.Poise > before.GetStat StatId.Poise, "Poise must rise on the returned combatant")
    Assert.True(grown.GetStat StatId.Resolve > before.GetStat StatId.Resolve, "A Fighter's Resolve must rise on the returned combatant")
