namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain

/// `Combatant.growFromEncounter` implements ADR 0009 bestiary familiarity growth:
/// Prowess rises by 25 -> 5 -> 3 -> 2 -> 1 uncapped per species encounter.
/// Poise rises by +1 per survived encounter, and each mental defence gains +1.
module EncounterGrowthTests =

  let private statOf (stat: StatId) (c: Combatant) = c.Stats.Get stat

  let private foe (cls: CharacterClass) (tier: CombatTier) = TierFactory.createClassTier cls tier

  [<Fact>]
  let ``a won fight raises Prowess by 25 on first encounter and Poise by 1`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let pack = [ foe CharacterClass.Warrior CombatTier.Novice ]
    let grown = Combatant.growFromEncounter pack before

    Assert.Equal(statOf StatId.Prowess before + 25, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 1, statOf StatId.Poise grown)

  [<Fact>]
  let ``Prowess encounters diminish from 25 to 5, 3, 2, and 1 uncapped`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Veteran
    let enemy = foe CharacterClass.Warrior CombatTier.Novice

    // Encounter 1: +25
    let e1 = Combatant.growFromEncounter [ enemy ] before
    Assert.Equal(statOf StatId.Prowess before + 25, statOf StatId.Prowess e1)

    // Encounter 2: +5
    let e2 = Combatant.growFromEncounter [ enemy ] e1
    Assert.Equal(statOf StatId.Prowess e1 + 5, statOf StatId.Prowess e2)

    // Encounter 3: +3
    let e3 = Combatant.growFromEncounter [ enemy ] e2
    Assert.Equal(statOf StatId.Prowess e2 + 3, statOf StatId.Prowess e3)

    // Encounter 4: +2
    let e4 = Combatant.growFromEncounter [ enemy ] e3
    Assert.Equal(statOf StatId.Prowess e3 + 2, statOf StatId.Prowess e4)

    // Encounter 5+: +1 uncapped
    let e5 = Combatant.growFromEncounter [ enemy ] e4
    Assert.Equal(statOf StatId.Prowess e4 + 1, statOf StatId.Prowess e5)

    let e6 = Combatant.growFromEncounter [ enemy ] e5
    Assert.Equal(statOf StatId.Prowess e5 + 1, statOf StatId.Prowess e6)

  [<Fact>]
  let ``multiple different species in a pack award their individual familiarity grants`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master

    let pack =
      [ foe CharacterClass.Warrior CombatTier.GrandMaster
        foe CharacterClass.Rogue CombatTier.GrandMaster ]

    // First time fighting both Warrior and Rogue: 25 + 25 = 50 Prowess
    let grown = Combatant.growFromEncounter pack before
    Assert.Equal(statOf StatId.Prowess before + 50, statOf StatId.Prowess grown)

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
    // Mage gains Acumen (25 on first encounter) and Composure (1 + 1 = 2)
    Assert.Equal(statOf StatId.Acumen mage + 25, statOf StatId.Acumen grownMage)
    Assert.Equal(statOf StatId.Composure mage + 2, statOf StatId.Composure grownMage)
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

    Assert.Equal(statOf StatId.Prowess before + 25, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 1, statOf StatId.Poise grown)
    Assert.Equal(statOf StatId.Resolve before + 1, statOf StatId.Resolve grown)
    Assert.Equal(statOf StatId.Composure before + 1, statOf StatId.Composure grown)
    Assert.Equal(statOf StatId.Force before, statOf StatId.Force grown)
    Assert.Equal(statOf StatId.Finesse before, statOf StatId.Finesse grown)

  [<Fact>]
  let ``grantBossPoise awards milestone +3 Poise`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let bossCleared = Combatant.grantBossPoise before
    Assert.Equal(statOf StatId.Poise before + 3, statOf StatId.Poise bossCleared)
