namespace Fornach.Domain.Tests

open System
open Xunit
open Xunit.Abstractions
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

module OverallBalanceTests =

  type ChampionBalanceRow = {
    ArchetypeName: string
    Class: CharacterClass
    Tier: CombatTier
    WarriorTippingPoint: Simulation.TippingPointResult
    RogueTippingPoint: Simulation.TippingPointResult
    MageTippingPoint: Simulation.TippingPointResult
    TacticalNote: string
  }

  let private getTacticalObservations (cls: CharacterClass) (tier: CombatTier) : string =
    match cls, tier with
    | CharacterClass.Warrior, GrandMaster ->
      "Berserk Tincture deadens 35% physical damage & unleashes Frenzy bonus swings; cleaves up to 5 adjacent foes."
    | CharacterClass.Warrior, _ ->
      "Brute kinetic Force & high HP pool; vulnerable to compounding flank penalties over prolonged duels."
    | CharacterClass.Rogue, GrandMaster ->
      "Caltrop Pouch strips flank penalties for 5 turns; Agility disparity triggers lethal AoO counters."
    | CharacterClass.Rogue, _ ->
      "High Finesse & Reflex dodge initial attacks; overwhelmed once caltrops expire against large mobs."
    | CharacterClass.Mage, GrandMaster ->
      "Dread Warhorn inflicts +25 Cognitive Fatigue on all attackers; devastates Mage morale & triggers mental routs."
    | CharacterClass.Mage, _ ->
      "Formidable mental dominance; vulnerable if physical brute force bypasses lower physical armor."
    | _ -> "Standard archetype profile."

  // =========================================================================
  // Overall Balance Suite
  // =========================================================================

  type OverallBalanceFixture(output: ITestOutputHelper) =

    [<Fact>]
    let ``Overall Balance Matrix: Determine average base class swarm required to defeat each tier of all 5 archetypes`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let iterationsPerProbe = 10
      let maxMobCount = 100

      let championClasses = [
        CharacterClass.Warrior
        CharacterClass.Rogue
        CharacterClass.Mage
        CharacterClass.Mage
        CharacterClass.Mage
      ]

      let tiers = [
        CombatTier.Novice
        CombatTier.Veteran
        CombatTier.Master
        CombatTier.GrandMaster
      ]

      let rows = ResizeArray<ChampionBalanceRow>()

      for cls in championClasses do
        for tier in tiers do
          let champFactory () = TierFactory.createClassTier cls tier

          let warriorTP =
            Simulation.findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Warrior Novice) maxMobCount iterationsPerProbe roller

          let rogueTP =
            Simulation.findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Rogue Novice) maxMobCount iterationsPerProbe roller

          let mageTP =
            Simulation.findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Mage Novice) maxMobCount iterationsPerProbe roller

          let row = {
            ArchetypeName = sprintf "%s %s" (tier.ToString()) cls.Name
            Class = cls
            Tier = tier
            WarriorTippingPoint = warriorTP
            RogueTippingPoint = rogueTP
            MageTippingPoint = mageTP
            TacticalNote = getTacticalObservations cls tier
          }
          rows.Add row

      // Print Markdown Table
      output.WriteLine("\n# FORNACH COMBAT ENGINE: COMPREHENSIVE ARCHETYPE BALANCE MATRIX")
      output.WriteLine("### Swarm Tipping Point N* (Average base class mob count required to achieve >= 50% win rate against Champion)\n")

      output.WriteLine("| Champion Archetype | Tier | vs. Warrior (Power) | vs. Rogue (Finesse) | vs. Mage (Arcane) | Tactical Observations |")
      output.WriteLine("|:-------------------|:-----|:-------------------:|:-------------------:|:-----------------:|:----------------------|")

      for r in rows do
        output.WriteLine(
          sprintf "| %-18s | %-12s | %-19s | %-19s | %-17s | %s |"
            r.Class.Name
            (r.Tier.ToString())
            (r.WarriorTippingPoint.ToString())
            (r.RogueTippingPoint.ToString())
            (r.MageTippingPoint.ToString())
            r.TacticalNote
        )

      output.WriteLine("\n---\n")

      // Verification Assertions:
      // 1. Sanity: All tipping points must be >= 1
      for r in rows do
        Assert.True(r.WarriorTippingPoint.Value >= 1, sprintf "%s vs Warrior tipping point must be >= 1" r.ArchetypeName)
        Assert.True(r.RogueTippingPoint.Value >= 1, sprintf "%s vs Rogue tipping point must be >= 1" r.ArchetypeName)
        Assert.True(r.MageTippingPoint.Value >= 1, sprintf "%s vs Mage tipping point must be >= 1" r.ArchetypeName)

      // 2. Progression Monotonicity: GrandMaster >= Novice across all classes and mobs
      for cls in championClasses do
        let noviceRow = rows |> Seq.find (fun r -> r.Class = cls && r.Tier = CombatTier.Novice)
        let gmRow = rows |> Seq.find (fun r -> r.Class = cls && r.Tier = CombatTier.GrandMaster)

        Assert.True(gmRow.WarriorTippingPoint.Value >= noviceRow.WarriorTippingPoint.Value,
          sprintf "%s GrandMaster Warrior tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.WarriorTippingPoint.Value noviceRow.WarriorTippingPoint.Value)
        Assert.True(gmRow.RogueTippingPoint.Value >= noviceRow.RogueTippingPoint.Value,
          sprintf "%s GrandMaster Rogue tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.RogueTippingPoint.Value noviceRow.RogueTippingPoint.Value)
        Assert.True(gmRow.MageTippingPoint.Value >= noviceRow.MageTippingPoint.Value,
          sprintf "%s GrandMaster Mage tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.MageTippingPoint.Value noviceRow.MageTippingPoint.Value)

    [<Fact>]
    let ``Berserker: Swarm endurance scales with tier and is most vulnerable to Arcane Mages`` () =
      let rng = Random(123)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let noviceBerserker = fun () -> TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice
      let gmBerserker = fun () -> TierFactory.createClassTier CharacterClass.Warrior CombatTier.GrandMaster
      let mageMob = fun () -> TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice
      let warriorMob = fun () -> TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

      let tpNoviceWarrior = Simulation.findSwarmTippingPoint noviceBerserker warriorMob 50 10 roller
      let tpGMWarrior = Simulation.findSwarmTippingPoint gmBerserker warriorMob 50 10 roller
      let tpGMMage = Simulation.findSwarmTippingPoint gmBerserker mageMob 50 10 roller

      Assert.True(tpGMWarrior.Value > tpNoviceWarrior.Value, "GrandMaster Berserker holds against significantly more Warriors than Novice.")
      Assert.True(tpGMWarrior.Value >= tpGMMage.Value, "Berserker should be more vulnerable to Arcane Mages targeting Mental plane than to Physical Warriors.")

    [<Fact>]
    let ``Duelist: Superior Agility disparity and Caltrops punish agile Rogue mobs`` () =
      let rng = Random(789)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let gmDuelist = fun () -> TierFactory.createClassTier CharacterClass.Rogue CombatTier.GrandMaster
      let rogueMob = fun () -> TierFactory.createClassTier CharacterClass.Rogue CombatTier.Novice

      let tpGMRogue = Simulation.findSwarmTippingPoint gmDuelist rogueMob 50 10 roller
      Assert.True(tpGMRogue.Value >= 25, sprintf "GrandMaster Duelist should withstand at least 25 Rogues (Actual: %d)" tpGMRogue.Value)

    [<Fact>]
    let ``Mage: mental-plane resilience is now in line with the physical classes`` () =
      // Was "Mage dominates the mental plane 5x over the Fighter". The class reduction removed the
      // low-intelligence trade-off, so physical classes now carry real mental defence (offDef = sec)
      // and the mage's mental-plane edge is a lean, not a chasm. Measured at Master, where the swarm
      // sizes still discriminate (GrandMaster saturates the harness ceiling for every archetype).
      let rng = Random(101)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let masterMage = fun () -> TierFactory.createClassTier CharacterClass.Mage CombatTier.Master
      let masterFighter = fun () -> TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master
      let mageMob = fun () -> TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice

      let tpMage = Simulation.findSwarmTippingPoint masterMage mageMob 100 10 roller
      let tpFighter = Simulation.findSwarmTippingPoint masterFighter mageMob 100 10 roller

      Assert.True(tpMage.Value >= 10, sprintf "Master Mage should withstand 10+ Mages (Actual: %d)" tpMage.Value)
      Assert.True(tpFighter.Value >= 10, sprintf "Master Fighter should withstand 10+ Mages (Actual: %d)" tpFighter.Value)
      Assert.True(tpMage.Value <= tpFighter.Value * 2,
        sprintf "Mage must stay in line with the Fighter, not dominate (Mage: %d, Fighter: %d)" tpMage.Value tpFighter.Value)
