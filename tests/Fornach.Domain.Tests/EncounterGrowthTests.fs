namespace Fornach.Domain.Tests
open Xunit
open Fornach.Domain

/// ADR 0009 (stress-growth rework): discipline is earned under pressure, not per encounter.
/// `Combatant.growFromEncounter` records bestiary familiarity only (+1 per species per encounter,
/// uncapped; the curve below moves 1% per encounter across the +/-25% span, so fifty encounters
/// reach full familiarity in ActionResolver.familiarityFactor).
/// `Combatant.growFromStressedVictory` banks +5 on a victory that cost blood/morale/status,
/// +1 more above half Recklessness. `Combatant.growFromDefeat` banks +10 on collapse or death.
/// All four growth stats (Prowess/Poise or Prowess/Poise) rise together under stress.
module EncounterGrowthTests =
  let private statOf (stat: StatId) (c: Combatant) = c.Stats.Get stat
  let private foe (cls: CharacterClass) (tier: CombatTier) = TierFactory.createClassTier cls tier
  let private unharmed cls = TierFactory.createClassTier cls CombatTier.Master
  let private bloodied cls =
    let c = unharmed cls
    { c with Health = c.Health.ApplyDelta -10 }
  let private reckless cls value =
    let c = bloodied cls
    { c with Meters = { c.Meters with Recklessness = Meter.Create value } }

  [<Fact>]
  let ``a clean flawless victory earns nothing but familiarity`` () =
    // Discipline must be earned under stress. Walk over a novice untouched and the bestiary
    // learns the species, but no stat grows.
    let before = unharmed CharacterClass.Fighter
    let pack = [ foe CharacterClass.Warrior CombatTier.Novice ]
    let foeSpecies = ProgressionProfile.NormalizeSpeciesName (List.head pack).Name
    let grown = Combatant.growFromEncounter pack before |> Combatant.growFromStressedVictory
    Assert.Equal(1, grown.Progression.BestiaryKnowledge |> Map.tryFind foeSpecies |> Option.defaultValue 0)
    Assert.Equal(statOf StatId.Prowess before, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before, statOf StatId.Poise grown)
    // The mental defences keep their per-encounter tick (Resolve/Intuition); only the
    // discipline pair is stress-gated.
    Assert.Equal(statOf StatId.Resolve before + 1, statOf StatId.Resolve grown)
    Assert.Equal(statOf StatId.Intuition before + 1, statOf StatId.Intuition grown)

  [<Fact>]
  let ``a victory paid for in blood banks +5 in the class discipline pair`` () =
    let before = bloodied CharacterClass.Fighter
    let grown = Combatant.growFromStressedVictory before
    Assert.Equal(statOf StatId.Prowess before + 5, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 5, statOf StatId.Poise grown)
    // Resolve/Intuition keep only their per-encounter tick; stress growth never touches them, and the
    // merged pair is the only discipline stat there is now, so there is no "other class's pair" to leave alone.
    Assert.Equal(statOf StatId.Resolve before, statOf StatId.Resolve grown)
    Assert.Equal(statOf StatId.Intuition before, statOf StatId.Intuition grown)

  [<Fact>]
  let ``morale loss or a lingering status counts as stress, not just wounds`` () =
    let base_ = unharmed CharacterClass.Mage
    let demoralised = { base_ with Morale = base_.Morale.ApplyDelta -10 }
    let frustrated = { base_ with Meters = { base_.Meters with Frustration = Meter.Create 20 } }
    let grewFromMorale = Combatant.growFromStressedVictory demoralised
    let grewFromStatus = Combatant.growFromStressedVictory frustrated
    Assert.Equal(statOf StatId.Prowess base_ + 5, statOf StatId.Prowess grewFromMorale)
    Assert.Equal(statOf StatId.Prowess base_ + 5, statOf StatId.Prowess grewFromStatus)

  [<Fact>]
  let ``a victory above half Recklessness adds +1 to the +5`` () =
    let before = reckless CharacterClass.Fighter 51
    let grown = Combatant.growFromStressedVictory before
    Assert.Equal(statOf StatId.Prowess before + 6, statOf StatId.Prowess grown)
    // A reckless flawless victory earns the +1 edge bonus on top of the status-stress +5 —
    // Recklessness is itself a stress, so 60 pays 6.
    let flawless = { (unharmed CharacterClass.Fighter) with Meters = { (unharmed CharacterClass.Fighter).Meters with Recklessness = Meter.Create 60 } }
    let grownFlawless = Combatant.growFromStressedVictory flawless
    Assert.Equal(statOf StatId.Prowess flawless + 6, statOf StatId.Prowess grownFlawless)

  [<Fact>]
  let ``a mage grows the same shared discipline pair a fighter does`` () =
    // Prowess and Poise serve both planes, so there is one pair to grow and both classes grow it.
    let before = bloodied CharacterClass.Mage
    let grown = Combatant.growFromStressedVictory before
    Assert.Equal(statOf StatId.Prowess before + 5, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 5, statOf StatId.Poise grown)

  [<Fact>]
  let ``defeat banks +10 in the class discipline pair — the deepest growth in the game`` () =
    let before = unharmed CharacterClass.Fighter
    let grown = Combatant.growFromDefeat before
    Assert.Equal(statOf StatId.Prowess before + 10, statOf StatId.Prowess grown)
    Assert.Equal(statOf StatId.Poise before + 10, statOf StatId.Poise grown)
    Assert.Equal(statOf StatId.Resolve before, statOf StatId.Resolve grown)
    let mage = unharmed CharacterClass.Mage
    let grownMage = Combatant.growFromDefeat mage
    Assert.Equal(statOf StatId.Prowess mage + 10, statOf StatId.Prowess grownMage)
    Assert.Equal(statOf StatId.Poise mage + 10, statOf StatId.Poise grownMage)

  [<Fact>]
  let ``bestiary familiarity counts +1 per encounter per species, uncapped`` () =
    let before = unharmed CharacterClass.Fighter
    let enemy = foe CharacterClass.Warrior CombatTier.Novice
    let foeSpecies = ProgressionProfile.NormalizeSpeciesName enemy.Name
    let e1 = Combatant.growFromEncounter [ enemy ] before
    Assert.Equal(1, e1.Progression.BestiaryKnowledge.[foeSpecies])
    let e2 = Combatant.growFromEncounter [ enemy ] e1
    Assert.Equal(2, e2.Progression.BestiaryKnowledge.[foeSpecies])
    // A pack counts each base species once: tier titles are challenge bands, not species, so the
    // GrandMaster Warrior banks into the same "Warrior" curve the Novice encounters already fed.
    let pack = [ foe CharacterClass.Warrior CombatTier.GrandMaster; foe CharacterClass.Rogue CombatTier.GrandMaster ]
    let grown = Combatant.growFromEncounter pack e2
    Assert.Equal(3, grown.Progression.BestiaryKnowledge.["Warrior"])
    Assert.Equal(1, grown.Progression.BestiaryKnowledge.["Rogue"])
    Assert.True(
      statOf StatId.Prowess before = statOf StatId.Prowess grown,
      "no species encounter may grow a stat"
    )

  [<Fact>]
  let ``NormalizeSpeciesName strips level prefixes correctly for bestiary grouping`` () =
    Assert.Equal("Forest Wolf", ProgressionProfile.NormalizeSpeciesName "Lv.1 Forest Wolf")
    // Tier titles are challenge bands, not species: one familiarity curve per base species.
    Assert.Equal("Warrior", ProgressionProfile.NormalizeSpeciesName "Novice Warrior")
    Assert.Equal("Warrior", ProgressionProfile.NormalizeSpeciesName "GrandMaster Warrior")
    Assert.Equal("Forest Wolf", ProgressionProfile.NormalizeSpeciesName "Lv.3 Veteran Forest Wolf")
    Assert.Equal("Forest Wolf", ProgressionProfile.NormalizeSpeciesName "Lv.35 Forest Wolf")
    Assert.Equal("Bandit Leader", ProgressionProfile.NormalizeSpeciesName "Bandit Leader")
    Assert.Equal("Bandit Leader", ProgressionProfile.NormalizeSpeciesName "Lv.11 Bandit Leader")
    Assert.Equal("", ProgressionProfile.NormalizeSpeciesName "")

  [<Fact>]
  let ``grantBossPoise awards milestone +3 Poise`` () =
    let before = unharmed CharacterClass.Fighter
    let bossCleared = Combatant.grantBossPoise before
    Assert.Equal(statOf StatId.Poise before + 3, statOf StatId.Poise bossCleared)

  [<Fact>]
  let ``clean mastery victory via Execution or Indes earns +3 Prowess and Poise even at full vitality`` () =
    let before = unharmed CharacterClass.Fighter
    let masteryGrown = Combatant.growFromVictory true before
    Assert.Equal(statOf StatId.Prowess before + 3, statOf StatId.Prowess masteryGrown)
    Assert.Equal(statOf StatId.Poise before + 3, statOf StatId.Poise masteryGrown)
    // Stressed victory still pays +5 even when cleanMasteryVictory is true
    let stressedBefore = bloodied CharacterClass.Fighter
    let stressedMastery = Combatant.growFromVictory true stressedBefore
    Assert.Equal(statOf StatId.Prowess stressedBefore + 5, statOf StatId.Prowess stressedMastery)
    Assert.Equal(statOf StatId.Poise stressedBefore + 5, statOf StatId.Poise stressedMastery)
