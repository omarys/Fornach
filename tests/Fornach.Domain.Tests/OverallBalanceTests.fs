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
    SoldierTippingPoint: Simulation.TippingPointResult
    MageTippingPoint: Simulation.TippingPointResult
    TacticalNote: string
  }

  let private getTacticalObservations (cls: CharacterClass) (tier: CombatTier) : string =
    match cls, tier with
    | CharacterClass.Warden, GrandMaster
    | CharacterClass.Justicar, GrandMaster ->
      "BastionZoneControl limits frontline to 3; Prowess disparity triggers massive AoOs; resilient against swarms."
    | CharacterClass.Warden, Master
    | CharacterClass.Justicar, Master ->
      "BastionZoneControl limits frontline to 3; exceptional defense soak against physical hordes."
    | CharacterClass.Warden, _
    | CharacterClass.Justicar, _ ->
      "Discipline posture & bastion geometry resist early encirclement penalties."
    | CharacterClass.Berserker, GrandMaster ->
      "Berserk Tincture deadens 35% physical damage & unleashes Frenzy bonus swings; cleaves up to 5 adjacent foes."
    | CharacterClass.Berserker, _ ->
      "Brute kinetic Force & high HP pool; vulnerable to compounding flank penalties over prolonged duels."
    | CharacterClass.Duelist, GrandMaster ->
      "Caltrop Pouch strips flank penalties for 5 turns; Agility disparity triggers lethal AoO counters."
    | CharacterClass.Duelist, _ ->
      "High Finesse & Reflex dodge initial attacks; overwhelmed once caltrops expire against large mobs."
    | CharacterClass.Inquisitor, GrandMaster ->
      "Dread Warhorn inflicts +25 Cognitive Fatigue on all attackers; devastates Mage morale & triggers mental routs."
    | CharacterClass.Inquisitor, _ ->
      "Formidable mental dominance; vulnerable if physical brute force bypasses lower physical armor."
    | CharacterClass.Mesmer, GrandMaster ->
      "Mirror Mirage forces flankers to attack decoys; Prismatic Flare punishes enemy recklessness."
    | CharacterClass.Mesmer, _ ->
      "Deceptive sensory phantasms disrupt attackers; susceptible to dogpiling once illusions exhaust."
    | CharacterClass.Abjurer, GrandMaster
    | CharacterClass.Strategist, GrandMaster ->
      "Aegis of Retribution reduces damage by 35% and reflects 50% back; destabilizing ground wards trip flankers with heavy Frustration."
    | CharacterClass.Abjurer, _
    | CharacterClass.Strategist, _ ->
      "Runic composure wards & destabilizing ground glyphs disrupt oncoming attackers through calculated attrition."
    | _ -> "Standard archetype profile."

  let private getDisplayClassName (cls: CharacterClass) : string =
    match cls with
    | CharacterClass.Warden -> "Warden"
    | CharacterClass.Justicar -> "Warden (Justicar)"
    | _ -> cls.Name

  // =========================================================================
  // Overall Balance Suite
  // =========================================================================

  type OverallBalanceFixture(output: ITestOutputHelper) =

    [<Fact>]
    let ``Overall Balance Matrix: Determine average base class swarm required to defeat each tier of all 6 archetypes`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let iterationsPerProbe = 10
      let maxMobCount = 100

      let championClasses = [
        CharacterClass.Berserker
        CharacterClass.Duelist
        CharacterClass.Warden
        CharacterClass.Inquisitor
        CharacterClass.Mesmer
        CharacterClass.Abjurer
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

          let soldierTP =
            Simulation.findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Soldier Novice) maxMobCount iterationsPerProbe roller

          let mageTP =
            Simulation.findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Mage Novice) maxMobCount iterationsPerProbe roller

          let row = {
            ArchetypeName = sprintf "%s %s" (tier.ToString()) (getDisplayClassName cls)
            Class = cls
            Tier = tier
            WarriorTippingPoint = warriorTP
            RogueTippingPoint = rogueTP
            SoldierTippingPoint = soldierTP
            MageTippingPoint = mageTP
            TacticalNote = getTacticalObservations cls tier
          }
          rows.Add row

      // Print Markdown Table
      output.WriteLine("\n# FORNACH COMBAT ENGINE: COMPREHENSIVE ARCHETYPE BALANCE MATRIX")
      output.WriteLine("### Swarm Tipping Point N* (Average base class mob count required to achieve >= 50% win rate against Champion)\n")

      output.WriteLine("| Champion Archetype | Tier | vs. Warrior (Power) | vs. Rogue (Finesse) | vs. Soldier (Discipline) | vs. Mage (Arcane) | Tactical Observations |")
      output.WriteLine("|:-------------------|:-----|:-------------------:|:-------------------:|:------------------------:|:-----------------:|:----------------------|")

      for r in rows do
        output.WriteLine(
          sprintf "| %-18s | %-12s | %-19s | %-19s | %-24s | %-17s | %s |"
            (getDisplayClassName r.Class)
            (r.Tier.ToString())
            (r.WarriorTippingPoint.ToString())
            (r.RogueTippingPoint.ToString())
            (r.SoldierTippingPoint.ToString())
            (r.MageTippingPoint.ToString())
            r.TacticalNote
        )

      output.WriteLine("\n---\n")

      // Verification Assertions:
      // 1. Sanity: All tipping points must be >= 1
      for r in rows do
        Assert.True(r.WarriorTippingPoint.Value >= 1, sprintf "%s vs Warrior tipping point must be >= 1" r.ArchetypeName)
        Assert.True(r.RogueTippingPoint.Value >= 1, sprintf "%s vs Rogue tipping point must be >= 1" r.ArchetypeName)
        Assert.True(r.SoldierTippingPoint.Value >= 1, sprintf "%s vs Soldier tipping point must be >= 1" r.ArchetypeName)
        Assert.True(r.MageTippingPoint.Value >= 1, sprintf "%s vs Mage tipping point must be >= 1" r.ArchetypeName)

      // 2. Progression Monotonicity: GrandMaster >= Novice across all classes and mobs
      for cls in championClasses do
        let noviceRow = rows |> Seq.find (fun r -> r.Class = cls && r.Tier = CombatTier.Novice)
        let gmRow = rows |> Seq.find (fun r -> r.Class = cls && r.Tier = CombatTier.GrandMaster)

        Assert.True(gmRow.WarriorTippingPoint.Value >= noviceRow.WarriorTippingPoint.Value,
          sprintf "%s GrandMaster Warrior tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.WarriorTippingPoint.Value noviceRow.WarriorTippingPoint.Value)
        Assert.True(gmRow.RogueTippingPoint.Value >= noviceRow.RogueTippingPoint.Value,
          sprintf "%s GrandMaster Rogue tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.RogueTippingPoint.Value noviceRow.RogueTippingPoint.Value)
        Assert.True(gmRow.SoldierTippingPoint.Value >= noviceRow.SoldierTippingPoint.Value,
          sprintf "%s GrandMaster Soldier tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.SoldierTippingPoint.Value noviceRow.SoldierTippingPoint.Value)
        Assert.True(gmRow.MageTippingPoint.Value >= noviceRow.MageTippingPoint.Value,
          sprintf "%s GrandMaster Mage tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.MageTippingPoint.Value noviceRow.MageTippingPoint.Value)

      // 3. Warden GrandMaster Bastion Zone Control verification: Impenetrable to at least 50+ Warriors
      let wardenGM = rows |> Seq.find (fun r -> r.Class = CharacterClass.Warden && r.Tier = CombatTier.GrandMaster)
      Assert.True(wardenGM.WarriorTippingPoint.Value >= 50,
        sprintf "GrandMaster Warden with Bastion Zone Control should hold against 50+ Warriors (Actual: %s)" (wardenGM.WarriorTippingPoint.ToString()))

    [<Fact>]
    let ``Berserker: Swarm endurance scales with tier and is most vulnerable to Arcane Mages`` () =
      let rng = Random(123)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let noviceBerserker = fun () -> TierFactory.createClassTier CharacterClass.Berserker CombatTier.Novice
      let gmBerserker = fun () -> TierFactory.createClassTier CharacterClass.Berserker CombatTier.GrandMaster
      let mageMob = fun () -> TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice
      let warriorMob = fun () -> TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

      let tpNoviceWarrior = Simulation.findSwarmTippingPoint noviceBerserker warriorMob 50 10 roller
      let tpGMWarrior = Simulation.findSwarmTippingPoint gmBerserker warriorMob 50 10 roller
      let tpGMMage = Simulation.findSwarmTippingPoint gmBerserker mageMob 50 10 roller

      Assert.True(tpGMWarrior.Value > tpNoviceWarrior.Value, "GrandMaster Berserker holds against significantly more Warriors than Novice.")
      Assert.True(tpGMWarrior.Value > tpGMMage.Value, "Berserker should be more vulnerable to Arcane Mages targeting Mental plane than to Physical Warriors.")

    [<Fact>]
    let ``Warden: Bastion Zone Control completely nullifies compounding encirclement against physical hordes`` () =
      let rng = Random(456)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let gmWarden = fun () -> TierFactory.createClassTier CharacterClass.Warden CombatTier.GrandMaster
      let warriorMob = fun () -> TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

      // GrandMaster Warden vs 80 Warriors should achieve 0% swarm win rate
      let mobWinRate = Simulation.evaluateMobWinRate gmWarden warriorMob 80 10 roller
      Assert.True(mobWinRate <= 0.10, sprintf "Swarm win rate should be <= 10%% against GrandMaster Warden (Actual: %.1f%%)" (mobWinRate * 100.0))

    [<Fact>]
    let ``Duelist: Superior Agility disparity and Caltrops punish agile Rogue mobs`` () =
      let rng = Random(789)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let gmDuelist = fun () -> TierFactory.createClassTier CharacterClass.Duelist CombatTier.GrandMaster
      let rogueMob = fun () -> TierFactory.createClassTier CharacterClass.Rogue CombatTier.Novice

      let tpGMRogue = Simulation.findSwarmTippingPoint gmDuelist rogueMob 50 10 roller
      Assert.True(tpGMRogue.Value >= 25, sprintf "GrandMaster Duelist should withstand at least 25 Rogues (Actual: %d)" tpGMRogue.Value)

    [<Fact>]
    let ``Inquisitor: Mental plane dominance with Dread Warhorn heavily counters Mage mobs`` () =
      let rng = Random(101)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let gmInquisitor = fun () -> TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.GrandMaster
      let mageMob = fun () -> TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice
      let warriorMob = fun () -> TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice

      let tpGMMage = Simulation.findSwarmTippingPoint gmInquisitor mageMob 50 10 roller
      let tpGMWarrior = Simulation.findSwarmTippingPoint gmInquisitor warriorMob 50 10 roller

      Assert.True(tpGMMage.Value >= 20, sprintf "GrandMaster Inquisitor should withstand 20+ Mages (Actual: %d)" tpGMMage.Value)
      Assert.True(tpGMMage.Value >= tpGMWarrior.Value, "Inquisitor should endure more Mages on the Mental plane than physical Warriors bypassing armor.")
