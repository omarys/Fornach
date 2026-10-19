namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

// Spectre.Console allows only ONE live progress display per process, and throws
// InvalidOperationException if a second one starts concurrently. runGroupBatch opens such
// a display, so every test module that calls it must share this collection to stay serial.
//
// The Monte-Carlo frontier measures the two player classes against plain opposition: the mobs are the
// generic NPC archetypes (Warrior / Rogue / Soldier / Mage) with no preparations, no stance or Complex
// Form transitions, and a single basic attack type (see Combatant.PlayerStyle). Fluid stances, Complex
// Forms and varied attacks are exactly what sets the player apart, so they only appear on the Fighter
// and Mage side of these simulations.
[<Collection("SimulationBatch")>]
module MonteCarloSwarmTests =

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Fighter holds a 100-warrior press via Bastion and the eight-tile limit`` () =
    let soloArch = Archetypes.findByName "Grandmaster Fighter" |> Option.get
    let mobArch = Archetypes.findByName "Warrior" |> Option.get

    let summary = Simulation.runGroupBatch soloArch mobArch 100 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Fighter should survive 100 warriors (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 80.0, sprintf "Fighter should eliminate most warriors (Actual: %.1f)" summary.AvgEliminations)
    Assert.True(summary.AvgAoOsTriggered >= 20.0, sprintf "Fighter should punish the press with opportunity attacks (Actual: %.1f)" summary.AvgAoOsTriggered)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Fighter dismantles 12 novice soldiers`` () =
    let soloArch = Archetypes.findByName "Grandmaster Fighter" |> Option.get
    let mobArch = Archetypes.findByName "Soldier" |> Option.get

    let summary = Simulation.runGroupBatch soloArch mobArch 12 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Fighter should beat 12 soldiers (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 10.0, sprintf "Fighter should clear most soldiers (Actual: %.1f)" summary.AvgEliminations)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Fighter outclasses 10 novice rogues`` () =
    let soloArch = Archetypes.findByName "Grandmaster Fighter" |> Option.get
    let mobArch = Archetypes.findByName "Rogue" |> Option.get

    let summary = Simulation.runGroupBatch soloArch mobArch 10 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Fighter should beat 10 rogues (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Mage burns down 15 novice mages without their disciplines`` () =
    let soloArch = Archetypes.findByName "Grandmaster Mage" |> Option.get
    let mobArch = Archetypes.findByName "Mage" |> Option.get

    let summary = Simulation.runGroupBatch soloArch mobArch 15 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Mage should beat 15 plain casters (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Mage repels 8 novice warriors through Complex Forms`` () =
    let soloArch = Archetypes.findByName "Grandmaster Mage" |> Option.get
    let mobArch = Archetypes.findByName "Warrior" |> Option.get

    let summary = Simulation.runGroupBatch soloArch mobArch 8 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Mage should repel 8 warriors (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Mage outlasts 8 novice soldiers`` () =
    let soloArch = Archetypes.findByName "Grandmaster Mage" |> Option.get
    let mobArch = Archetypes.findByName "Soldier" |> Option.get

    let summary = Simulation.runGroupBatch soloArch mobArch 8 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Mage should outlast 8 soldiers (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: a Novice Fighter is overrun where the Grandmaster is not`` () =
    // The frontier should be a mastery cliff, not a free win: the same mobs that a Grandmaster holds
    // off must still beat a novice.
    let novice = Archetypes.findByName "Fighter" |> Option.get
    let grandmaster = Archetypes.findByName "Grandmaster Fighter" |> Option.get
    let mobArch = Archetypes.findByName "Warrior" |> Option.get

    let noviceSummary = Simulation.runGroupBatch novice mobArch 8 10
    let grandmasterSummary = Simulation.runGroupBatch grandmaster mobArch 8 10

    Assert.True(noviceSummary.SoloWinRate < 50.0, sprintf "8 warriors should overwhelm a novice Fighter (Actual: %.1f%%)" noviceSummary.SoloWinRate)
    Assert.True(grandmasterSummary.SoloWinRate >= 95.0, sprintf "The Grandmaster should hold the same press (Actual: %.1f%%)" grandmasterSummary.SoloWinRate)
