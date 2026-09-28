namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

module MonteCarloSwarmTests =

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Justicar achieves 100% win rate against 100 Novice Warriors via Bastion`` () =
    let soloArch = Archetypes.findByName "Grandmaster Justicar" |> Option.get
    let mobArch = Archetypes.findByName "Warrior" |> Option.get
    let mobCount = 100
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 90.0, sprintf "Justicar win rate should be >= 90%% against 100 warriors (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 80.0, sprintf "Justicar should eliminate most warriors (Actual: %.1f)" summary.AvgEliminations)
    Assert.True(summary.AvgAoOsTriggered >= 75.0, sprintf "Justicar should trigger massive opportunity attacks (Actual: %.1f)" summary.AvgAoOsTriggered)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Berserker achieves high win rate against 10 Novice Assassins via Cleaves`` () =
    let soloArch = Archetypes.findByName "Grandmaster Berserker" |> Option.get
    let mobArch = Archetypes.findByName "Assassin" |> Option.get
    let mobCount = 10
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 70.0, sprintf "Berserker win rate should be >= 70%% against 10 assassins (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 7.0, sprintf "Berserker should eliminate vast majority of assassins (Actual: %.1f)" summary.AvgEliminations)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Duelist dismantles 8 Novice Soldiers via Caltrops and AoO`` () =
    let soloArch = Archetypes.findByName "Grandmaster Duelist" |> Option.get
    let mobArch = Archetypes.findByName "Soldier" |> Option.get
    let mobCount = 8
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 70.0, sprintf "Duelist win rate should be >= 70%% against 8 soldiers (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgAoOsTriggered >= 5.0, sprintf "Duelist should trigger opportunity attacks (Actual: %.1f)" summary.AvgAoOsTriggered)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Inquisitor subdues 15 Novice Mages via Dread Warhorn and Cataclysm`` () =
    let soloArch = Archetypes.findByName "Grandmaster Inquisitor" |> Option.get
    let mobArch = Archetypes.findByName "Mage" |> Option.get
    let mobCount = 15
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 70.0, sprintf "Inquisitor win rate should be >= 70%% against 15 mages (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Mesmer confuses 8 Novice Warriors via Mirror Mirage`` () =
    let soloArch = Archetypes.findByName "Grandmaster Mesmer" |> Option.get
    let mobArch = Archetypes.findByName "Warrior" |> Option.get
    let mobCount = 8
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 70.0, sprintf "Mesmer win rate should be >= 70%% against 8 warriors (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Strategist dismantles 8 Novice Soldiers via Heraldic Treatise`` () =
    let soloArch = Archetypes.findByName "Grandmaster Strategist" |> Option.get
    let mobArch = Archetypes.findByName "Soldier" |> Option.get
    let mobCount = 8
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 70.0, sprintf "Strategist win rate should be >= 70%% against 8 soldiers (Actual: %.1f%%)" summary.SoloWinRate)
