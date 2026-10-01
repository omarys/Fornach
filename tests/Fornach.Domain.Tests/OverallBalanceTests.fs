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
    AssassinTippingPoint: Simulation.TippingPointResult
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
    | CharacterClass.Juggernaut, GrandMaster ->
      "Iron Colossus armor soak & Shockwave Slam shatter enemy formations; massive Force & Fortitude outlast physical swarms."
    | CharacterClass.Juggernaut, _ ->
      "Power & Discipline juggernaut; high physical armor absorption with Shockwave Slam cleave reinforcement."
    | CharacterClass.Ranger, GrandMaster ->
      "Caltrop Pouch & fluid skirmishing punish advancing flankers; high Prowess & Finesse maintain reactive AoO zone."
    | CharacterClass.Ranger, _ ->
      "Discipline & Agility skirmisher; relies on Caltrop Pouch and opportunist reactive counters against mobs."
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
    let ``Overall Balance Matrix: Determine average base class swarm required to defeat each tier of all 8 archetypes`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let iterationsPerProbe = 10
      let maxMobCount = 100

      let championClasses = [
        CharacterClass.Berserker
        CharacterClass.Juggernaut
        CharacterClass.Duelist
        CharacterClass.Warden
        CharacterClass.Inquisitor
        CharacterClass.Mesmer
        CharacterClass.Abjurer
        CharacterClass.Ranger
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

          let assassinTP =
            Simulation.findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Assassin Novice) maxMobCount iterationsPerProbe roller

          let soldierTP =
            Simulation.findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Soldier Novice) maxMobCount iterationsPerProbe roller

          let mageTP =
            Simulation.findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Mage Novice) maxMobCount iterationsPerProbe roller

          let row = {
            ArchetypeName = sprintf "%s %s" (tier.ToString()) (getDisplayClassName cls)
            Class = cls
            Tier = tier
            WarriorTippingPoint = warriorTP
            AssassinTippingPoint = assassinTP
            SoldierTippingPoint = soldierTP
            MageTippingPoint = mageTP
            TacticalNote = getTacticalObservations cls tier
          }
          rows.Add row

      // Print Markdown Table
      output.WriteLine("\n# FORNACH COMBAT ENGINE: COMPREHENSIVE ARCHETYPE BALANCE MATRIX")
      output.WriteLine("### Swarm Tipping Point N* (Average base class mob count required to achieve >= 50% win rate against Champion)\n")

      output.WriteLine("| Champion Archetype | Tier | vs. Warrior (Power) | vs. Assassin (Finesse) | vs. Soldier (Discipline) | vs. Mage (Arcane) | Tactical Observations |")
      output.WriteLine("|:-------------------|:-----|:-------------------:|:----------------------:|:------------------------:|:-----------------:|:----------------------|")

      for r in rows do
        output.WriteLine(
          sprintf "| %-18s | %-12s | %-19s | %-22s | %-24s | %-17s | %s |"
            (getDisplayClassName r.Class)
            (r.Tier.ToString())
            (r.WarriorTippingPoint.ToString())
            (r.AssassinTippingPoint.ToString())
            (r.SoldierTippingPoint.ToString())
            (r.MageTippingPoint.ToString())
            r.TacticalNote
        )

      output.WriteLine("\n---\n")

      // Verification Assertions:
      // 1. Sanity: All tipping points must be >= 1
      for r in rows do
        Assert.True(r.WarriorTippingPoint.Value >= 1, sprintf "%s vs Warrior tipping point must be >= 1" r.ArchetypeName)
        Assert.True(r.AssassinTippingPoint.Value >= 1, sprintf "%s vs Assassin tipping point must be >= 1" r.ArchetypeName)
        Assert.True(r.SoldierTippingPoint.Value >= 1, sprintf "%s vs Soldier tipping point must be >= 1" r.ArchetypeName)
        Assert.True(r.MageTippingPoint.Value >= 1, sprintf "%s vs Mage tipping point must be >= 1" r.ArchetypeName)

      // 2. Progression Monotonicity: GrandMaster >= Novice across all classes and mobs
      for cls in championClasses do
        let noviceRow = rows |> Seq.find (fun r -> r.Class = cls && r.Tier = CombatTier.Novice)
        let gmRow = rows |> Seq.find (fun r -> r.Class = cls && r.Tier = CombatTier.GrandMaster)

        Assert.True(gmRow.WarriorTippingPoint.Value >= noviceRow.WarriorTippingPoint.Value,
          sprintf "%s GrandMaster Warrior tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.WarriorTippingPoint.Value noviceRow.WarriorTippingPoint.Value)
        Assert.True(gmRow.AssassinTippingPoint.Value >= noviceRow.AssassinTippingPoint.Value,
          sprintf "%s GrandMaster Assassin tipping point (%d) must be >= Novice (%d)" cls.Name gmRow.AssassinTippingPoint.Value noviceRow.AssassinTippingPoint.Value)
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
    let ``Duelist: Superior Agility disparity and Caltrops punish agile Assassin mobs`` () =
      let rng = Random(789)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let gmDuelist = fun () -> TierFactory.createClassTier CharacterClass.Duelist CombatTier.GrandMaster
      let assassinMob = fun () -> TierFactory.createClassTier CharacterClass.Assassin CombatTier.Novice

      let tpGMAssassin = Simulation.findSwarmTippingPoint gmDuelist assassinMob 50 10 roller
      Assert.True(tpGMAssassin.Value >= 25, sprintf "GrandMaster Duelist should withstand at least 25 Assassins (Actual: %d)" tpGMAssassin.Value)

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

    [<Fact>]
    let ``Juggernaut: Iron Colossus endurance holds against physical hordes while vulnerable to Arcane Mages`` () =
      let rng = Random(202)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let gmJuggernaut = fun () -> TierFactory.createClassTier CharacterClass.Juggernaut CombatTier.GrandMaster
      let warriorMob = fun () -> TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice
      let mageMob = fun () -> TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice

      let tpGMWarrior = Simulation.findSwarmTippingPoint gmJuggernaut warriorMob 50 10 roller
      let tpGMMage = Simulation.findSwarmTippingPoint gmJuggernaut mageMob 50 10 roller

      Assert.True(tpGMWarrior.Value >= 40, sprintf "GrandMaster Juggernaut should withstand 40+ Warriors (Actual: %d)" tpGMWarrior.Value)
      Assert.True(tpGMWarrior.Value > tpGMMage.Value, "Juggernaut physical armor soak should endure significantly more Warriors than Arcane Mages.")

    [<Fact>]
    let ``Ranger: Caltrop Pouch and opportunist agility dismantle advancing physical flankers`` () =
      let rng = Random(303)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let gmRanger = fun () -> TierFactory.createClassTier CharacterClass.Ranger CombatTier.GrandMaster
      let masterRanger = fun () -> TierFactory.createClassTier CharacterClass.Ranger CombatTier.Master
      let soldierMob = fun () -> TierFactory.createClassTier CharacterClass.Soldier CombatTier.Novice
      let mageMob = fun () -> TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice

      let tpGMSoldier = Simulation.findSwarmTippingPoint gmRanger soldierMob 50 10 roller
      let tpMasterSoldier = Simulation.findSwarmTippingPoint masterRanger soldierMob 50 10 roller
      let tpMasterMage = Simulation.findSwarmTippingPoint masterRanger mageMob 50 10 roller

      Assert.True(tpGMSoldier.Value >= 40, sprintf "GrandMaster Ranger should withstand 40+ Soldiers (Actual: %d)" tpGMSoldier.Value)
      Assert.True(tpMasterSoldier.Value > tpMasterMage.Value, "At Master tier, Ranger physical mobility and Caltrops should endure more Soldiers than Arcane Mages.")
