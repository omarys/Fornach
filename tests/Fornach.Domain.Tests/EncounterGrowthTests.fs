namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain

/// `Combatant.growFromEncounter` implements ADR 0009 bestiary familiarity growth:
/// Global Prowess/Acumen rises strictly by +1 per encounter.
/// Species-specific familiarity experience is tracked separately in BestiaryKnowledge (5 -> 3 -> 1 -> 1...).
/// Poise rises by +1 per survived encounter, and each mental defence gains +1.
module EncounterGrowthTests =

  let private statOf (stat: StatId) (c: Combatant) = c.Stats.Get stat

  let private foe (cls: CharacterClass) (tier: CombatTier) = TierFactory.createClassTier cls tier

  [<Fact>]
  let ``a won fight raises Prowess by 1 globally, Poise by 1, and records 5 species familiarity`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let pack = [ foe CharacterClass.Warrior CombatTier.Novice ]
    let foeSpecies = ProgressionProfile.NormalizeSpeciesName (List.head pack).Name
    let grown = Combatant.growFromEncounter pack before

    Assert.Equal(statOf StatId.Prowess before + 1, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 1, statOf StatId.Poise grown)
    let knowledge = grown.Progression.BestiaryKnowledge |> Map.tryFind foeSpecies |> Option.defaultValue 0
    Assert.Equal(5, knowledge)

  [<Fact>]
  let ``Bestiary familiarity encounters diminish from 5 to 3 and 1 uncapped while global Prowess rises by 1 per fight`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Veteran
    let enemy = foe CharacterClass.Warrior CombatTier.Novice
    let foeSpecies = ProgressionProfile.NormalizeSpeciesName enemy.Name

    // Encounter 1: Global +1, Familiarity 5
    let e1 = Combatant.growFromEncounter [ enemy ] before
    Assert.Equal(statOf StatId.Prowess before + 1, statOf StatId.Prowess e1)
    Assert.Equal(5, e1.Progression.BestiaryKnowledge.[foeSpecies])

    // Encounter 2: Global +2, Familiarity 5 + 3 = 8
    let e2 = Combatant.growFromEncounter [ enemy ] e1
    Assert.Equal(statOf StatId.Prowess before + 2, statOf StatId.Prowess e2)
    Assert.Equal(8, e2.Progression.BestiaryKnowledge.[foeSpecies])

    // Encounter 3: Global +3, Familiarity 8 + 1 = 9
    let e3 = Combatant.growFromEncounter [ enemy ] e2
    Assert.Equal(statOf StatId.Prowess before + 3, statOf StatId.Prowess e3)
    Assert.Equal(9, e3.Progression.BestiaryKnowledge.[foeSpecies])

    // Encounter 4: Global +4, Familiarity 9 + 1 = 10
    let e4 = Combatant.growFromEncounter [ enemy ] e3
    Assert.Equal(statOf StatId.Prowess before + 4, statOf StatId.Prowess e4)
    Assert.Equal(10, e4.Progression.BestiaryKnowledge.[foeSpecies])

    // Encounter 5+: Global +5, Familiarity 10 + 1 = 11
    let e5 = Combatant.growFromEncounter [ enemy ] e4
    Assert.Equal(statOf StatId.Prowess before + 5, statOf StatId.Prowess e5)
    Assert.Equal(11, e5.Progression.BestiaryKnowledge.[foeSpecies])

    let e6 = Combatant.growFromEncounter [ enemy ] e5
    Assert.Equal(statOf StatId.Prowess before + 6, statOf StatId.Prowess e6)
    Assert.Equal(12, e6.Progression.BestiaryKnowledge.[foeSpecies])

  [<Fact>]
  let ``multiple different species in a pack award their individual familiarity grants while global Prowess rises by 1`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master

    let foe1 = foe CharacterClass.Warrior CombatTier.GrandMaster
    let foe2 = foe CharacterClass.Rogue CombatTier.GrandMaster
    let sp1 = ProgressionProfile.NormalizeSpeciesName foe1.Name
    let sp2 = ProgressionProfile.NormalizeSpeciesName foe2.Name
    let pack = [ foe1; foe2 ]

    let grown = Combatant.growFromEncounter pack before
    Assert.Equal(statOf StatId.Prowess before + 1, statOf StatId.Prowess grown)
    Assert.Equal(5, grown.Progression.BestiaryKnowledge.[sp1])
    Assert.Equal(5, grown.Progression.BestiaryKnowledge.[sp2])

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
    // Mage gains Acumen (+1 per encounter) and Composure (1 + 1 = 2)
    Assert.Equal(statOf StatId.Acumen mage + 1, statOf StatId.Acumen grownMage)
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

    Assert.Equal(statOf StatId.Prowess before + 1, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 1, statOf StatId.Poise grown)
    Assert.Equal(statOf StatId.Resolve before + 1, statOf StatId.Resolve grown)
    Assert.Equal(statOf StatId.Composure before + 1, statOf StatId.Composure grown)
    Assert.Equal(statOf StatId.Force before, statOf StatId.Force grown)
    Assert.Equal(statOf StatId.Finesse before, statOf StatId.Finesse grown)

  [<Fact>]
  let ``NormalizeSpeciesName strips level prefixes correctly for bestiary grouping`` () =
    Assert.Equal("Forest Wolf", ProgressionProfile.NormalizeSpeciesName "Lv.1 Forest Wolf")
    Assert.Equal("Forest Wolf", ProgressionProfile.NormalizeSpeciesName "Lv.35 Forest Wolf")
    Assert.Equal("Bandit Leader", ProgressionProfile.NormalizeSpeciesName "Bandit Leader")
    Assert.Equal("Bandit Leader", ProgressionProfile.NormalizeSpeciesName "Lv.11 Bandit Leader")
    Assert.Equal("", ProgressionProfile.NormalizeSpeciesName "")

  [<Fact>]
  let ``grantBossPoise awards milestone +3 Poise`` () =
    let before = TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
    let bossCleared = Combatant.grantBossPoise before
    Assert.Equal(statOf StatId.Poise before + 3, statOf StatId.Poise bossCleared)
