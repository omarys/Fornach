namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

// Spectre.Console allows only ONE live progress display per process, and throws
// InvalidOperationException if a second one starts concurrently. runGroupBatch opens such
// a display, so every test module that calls it must share this collection to stay serial.
[<Collection("SimulationBatch")>]
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
    Assert.True(summary.AvgAoOsTriggered >= 35.0, sprintf "Justicar should trigger massive opportunity attacks (Actual: %.1f)" summary.AvgAoOsTriggered)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Berserker achieves high win rate against 10 Novice Rogues via Cleaves`` () =
    let soloArch = Archetypes.findByName "Grandmaster Berserker" |> Option.get
    let mobArch = Archetypes.findByName "Rogue" |> Option.get
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

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Juggernaut withstands 15 Novice Warriors via Shockwave Slam and heavy armor soak`` () =
    let soloArch = Archetypes.findByName "Grandmaster Juggernaut" |> Option.get
    let mobArch = Archetypes.findByName "Warrior" |> Option.get
    let mobCount = 15
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 80.0, sprintf "Juggernaut win rate should be >= 80%% against 15 warriors (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 12.0, sprintf "Juggernaut should eliminate majority of warriors (Actual: %.1f)" summary.AvgEliminations)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Ranger repels 12 Novice Soldiers via Caltrop Pouch and reactive counters`` () =
    let soloArch = Archetypes.findByName "Grandmaster Ranger" |> Option.get
    let mobArch = Archetypes.findByName "Soldier" |> Option.get
    let mobCount = 12
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 80.0, sprintf "Ranger win rate should be >= 80%% against 12 soldiers (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgAoOsTriggered >= 5.0, sprintf "Ranger should trigger opportunity attacks (Actual: %.1f)" summary.AvgAoOsTriggered)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Justicar defeats squad of 3 Veteran Juggernauts via Bastion and Cadence Chains`` () =
    let soloArch = Archetypes.findByName "Grandmaster Justicar" |> Option.get
    let mobArch = Archetypes.findByName "Veteran Juggernaut" |> Option.get
    let mobCount = 3
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 80.0, sprintf "Justicar win rate should be >= 80%% against 3 Veteran Juggernauts (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 2.5, sprintf "Justicar should eliminate most veterans (Actual: %.1f)" summary.AvgEliminations)
    Assert.True(summary.AvgRounds >= 2.0, sprintf "Combat should last multiple rounds (Actual: %.1f)" summary.AvgRounds)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Berserker overpowers squad of 3 Veteran Duelists via Frenzy and Cleaves`` () =
    let soloArch = Archetypes.findByName "Grandmaster Berserker" |> Option.get
    let mobArch = Archetypes.findByName "Veteran Duelist" |> Option.get
    let mobCount = 3
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 80.0, sprintf "Berserker win rate should be >= 80%% against 3 Veteran Duelists (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 2.5, sprintf "Berserker should eliminate veterans (Actual: %.1f)" summary.AvgEliminations)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Berserker holds ground against pair of Master Juggernauts`` () =
    let soloArch = Archetypes.findByName "Grandmaster Berserker" |> Option.get
    let mobArch = Archetypes.findByName "Master Juggernaut" |> Option.get
    let mobCount = 2
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.SoloWinRate >= 80.0, sprintf "Berserker win rate should be >= 80%% against 2 Master Juggernauts (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 1.6, sprintf "Berserker should eliminate Master foes (Actual: %.1f)" summary.AvgEliminations)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Justicar inflicts heavy casualties when outnumbered by 2 Grandmaster Berserkers`` () =
    let soloArch = Archetypes.findByName "Grandmaster Justicar" |> Option.get
    let mobArch = Archetypes.findByName "Grandmaster Berserker" |> Option.get
    let mobCount = 2
    let iterations = 10

    let summary = Simulation.runGroupBatch soloArch mobArch mobCount iterations

    Assert.True(summary.AvgRounds >= 3.0, sprintf "Even high-tier outnumbered combat should last >= 3.0 rounds (Actual: %.1f)" summary.AvgRounds)
    Assert.True(summary.AvgEliminations >= 1.0, sprintf "Solo GM should eliminate at least 1 opposing GM on average (Actual: %.1f)" summary.AvgEliminations)
