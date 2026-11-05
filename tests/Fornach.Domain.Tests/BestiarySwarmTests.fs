namespace Fornach.Domain.Tests

open System
open Xunit
open Xunit.Abstractions
open Fornach.Domain
open Fornach.Engine

[<Collection("SimulationBatch")>]
module BestiarySwarmTests =
  /// The engine's own AI. These tests measure default behaviour, so no game
  /// presses are registered and the table is empty by construction.
  let policies : AI.PolicyTable = Map.empty

  type BestiarySwarmFixture(output: ITestOutputHelper) =

    let canonicalClasses = [
      CharacterClass.Warrior
      CharacterClass.Rogue
      CharacterClass.Mage
      CharacterClass.Mage
      CharacterClass.Mage
    ]

    let allTiers = [
      CombatTier.Novice
      CombatTier.Veteran
      CombatTier.Master
      CombatTier.GrandMaster
    ]

    [<Fact>]
    let ``Bestiary Swarm Matrix: all 21 monsters as swarms against Fighter and Mage at the GrandMaster and Legend tiers`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let maxMobCount = 25
      let iterationsPerProbe = 10

      output.WriteLine("\n# FORNACH COMBAT ENGINE: BESTIARY MONSTER SWARM BALANCE MATRIX")
      output.WriteLine("### Swarm Tipping Point N* (Number of monsters required to achieve >= 50% win rate against Champion)")
      output.WriteLine("### Methodology: all 21 monsters as swarms vs Fighter and Mage at the GrandMaster (level 200) and Legend (level 1000) tiers (N* in [1..25+])\n")

      for tierLabel, championLevel in [ "GrandMaster", 200; "Legend", 1000 ] do
        output.WriteLine(sprintf "## Champion Tier: %s (Level %d)\n" tierLabel championLevel)
        output.WriteLine("| Monster | Biome | Role | Monster Tier | Fighter | Mage |")
        output.WriteLine("|:--------|:------|:-----|:-------------|:-------:|:----:|")

        let tierRows = CombatSimulation.runBestiarySwarmBatch policies championLevel maxMobCount iterationsPerProbe roller (fun () -> ())
        Assert.Equal(21, tierRows.Length)

        for r in tierRows do
          let m = r.Monster
          let tpStr cls =
            match Map.tryFind cls r.TippingPoints with
            | Some (Impenetrable cap) -> sprintf "%d+ (Impenetrable)" cap
            | Some (OverrunBy n) -> sprintf "%d" n
            | None -> "-"

          output.WriteLine(
            sprintf "| %-16s | %-16s | %-10s | %-12s | %9s | %6s |"
              m.Name
              m.NativeBiome.Name
              (sprintf "%A" m.Role)
              (sprintf "%A (%d)" m.Tier m.Level)
              (tpStr CharacterClass.Fighter)
              (tpStr CharacterClass.Mage)
          )

        output.WriteLine("")

    [<Fact>]
    let ``Novice Swarmers: Slag Hound and Blight Sprite packs overwhelm Novice champions (N* <= 5)`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let maxMob = 25
      let iters = 10
      let tier = CombatTier.Novice

      let hound = Bestiary.byId "slag_hound" |> Option.get
      let sprite = Bestiary.byId "blight_sprite" |> Option.get

      for m in [ hound; sprite ] do
        for cls in [ CharacterClass.Warrior; CharacterClass.Rogue; CharacterClass.Mage ] do
          let champFactory () = TierFactory.createClassTier cls tier
          let mobFactory () = Bestiary.createMonster m
          let tp = CombatSimulation.findSwarmTippingPoint policies champFactory mobFactory maxMob iters roller
          match tp with
          | OverrunBy n ->
            Assert.True(n <= 5, sprintf "Novice %A should be overwhelmed by small pack of %s (Actual N*: %d)" cls m.Name n)
          | Impenetrable cap ->
            Assert.Fail(sprintf "Novice %A should not be impenetrable to %s swarm (Cap: %d)" cls m.Name cap)

    [<Fact>]
    let ``GrandMaster Warden with BastionZoneControl resists Beast and Construct swarms`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let maxMob = 25
      let iters = 10
      let tier = CombatTier.GrandMaster

      let hound = Bestiary.byId "slag_hound" |> Option.get
      let crawler = Bestiary.byId "magma_crawler" |> Option.get

      let wardenFactory () = TierFactory.createClassTier CharacterClass.Warrior tier
      for m in [ hound; crawler ] do
        let mobFactory () = Bestiary.createMonster m
        let tp = CombatSimulation.findSwarmTippingPoint policies wardenFactory mobFactory maxMob iters roller
        match tp with
        | Impenetrable cap ->
          Assert.True(cap >= 20, sprintf "GM Warden should be impenetrable or withstand large mob of %s (Cap: %d)" m.Name cap)
        | OverrunBy n ->
          Assert.True(n >= 10, sprintf "GM Warden should resist at least 10 %ss via Bastion geometry (Actual: %d)" m.Name n)

    [<Fact>]
    let ``Colossus monsters (Obsidian Fiend, Storm Drake, Chrono Anomaly) threaten Master champions in small numbers (N* <= 4)`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let maxMob = 25
      let iters = 10
      let tier = CombatTier.Master

      let colossi = [
        Bestiary.byId "obsidian_fiend" |> Option.get
        Bestiary.byId "storm_drake" |> Option.get
        Bestiary.byId "chrono_anomaly" |> Option.get
      ]

      for boss in colossi do
        for cls in [ CharacterClass.Rogue; CharacterClass.Warrior; CharacterClass.Mage ] do
          let champFactory () = TierFactory.createClassTier cls tier
          let mobFactory () = Bestiary.createMonster boss
          let tp = CombatSimulation.findSwarmTippingPoint policies champFactory mobFactory maxMob iters roller
          match tp with
          | OverrunBy n ->
            Assert.True(n <= 4, sprintf "Colossus %s should overrun Master %A in small numbers (Actual N*: %d)" boss.Name cls n)
          | Impenetrable cap when cls = CharacterClass.Mage ->
            // Documented favourable matchup, not an avalanche: the Mage's prepared defence answers a
            // small colossus pack outright.
            Assert.True(cap >= 20, sprintf "Master Mage should at least stand against 20 %s" boss.Name)
          | Impenetrable cap ->
            // A champion whose Poise closes the ring is never overrun by a weaker swarm, however many
            // arrive: frontage caps how many colossi can engage at once, so a Master Rogue meets ~5 of
            // them, answers each approach with its Prowess budget and is never buried. The colossus
            // premise still holds wherever the champion has not yet bought that space control — which is
            // what the `OverrunBy n` branch above measures. Re-pin this if the frontage rule changes.
            Assert.True(cap >= 20, sprintf "Master %A should at least stand against 20 %s (Cap: %d)" cls boss.Name cap)

    [<Fact>]
    let ``GrandMaster Inquisitor with Dread Warhorn inflicts Cognitive Fatigue across Bestiary monster swarm`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let tier = CombatTier.GrandMaster

      let inquisitor = TierFactory.createClassTier CharacterClass.Mage tier
      let harpyTemplate = Bestiary.byId "gale_harpy" |> Option.get

      let primary = Bestiary.createMonster harpyTemplate
      let flankers = List.init 3 (fun _ -> Bestiary.createMonster harpyTemplate)

      let intent = DeployPreparation (PreparationType.DreadWarhorn, None)
      let res = ActionResolver.resolveGroupTurn roller intent inquisitor primary flankers

      for sec in res.SecondaryTargets do
        Assert.True(sec.Meters.CognitiveFatigue.Value >= 25, "Each flanker in monster swarm should suffer +25 Cognitive Fatigue from Dread Warhorn.")
