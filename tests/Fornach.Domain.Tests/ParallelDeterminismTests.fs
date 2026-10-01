namespace Fornach.Domain.Tests

open Xunit
open Fornach.Cli

/// Guards the parallel Monte-Carlo batches in src/Fornach.Cli/Simulation.fs.
///
/// `System.Random` is not thread-safe. If a single instance is shared across the
/// worker threads of `runParallelBatch`, concurrent `Next` calls corrupt its
/// internal seed state and every result becomes a function of thread scheduling.
/// These tests fail if a shared RNG is reintroduced, or if the per-iteration
/// seeding that makes results scheduling-independent is dropped.
///
/// Shares the "SimulationBatch" collection with MonteCarloSwarmTests because
/// Spectre.Console permits only one live progress display per process, and both
/// modules call into functions that open one.
[<Collection("SimulationBatch")>]
module ParallelDeterminismTests =

  [<Fact>]
  let ``runBatch: repeated invocations produce identical summaries`` () =
    let archA = Archetypes.findByName "Warrior" |> Option.get
    let archB = Archetypes.findByName "Mage" |> Option.get

    let first = Simulation.runBatch archA archB 200
    let second = Simulation.runBatch archA archB 200

    // Meta-guard: a matchup that is a clean sweep for one side produces byte-identical
    // iterations, which would make the determinism assertion below vacuous. This pairing
    // is a near coin-flip, so it genuinely exercises the roller.
    Assert.True(first.WinsA > 0 && first.WinsB > 0, "matchup no longer varies; determinism check is vacuous")

    Assert.Equal(first, second)

  [<Fact>]
  let ``runGroupBatch: repeated invocations produce identical summaries`` () =
    let solo = Archetypes.findByName "Grandmaster Justicar" |> Option.get
    let mob = Archetypes.findByName "Warrior" |> Option.get

    let first = Simulation.runGroupBatch solo mob 100 10
    let second = Simulation.runGroupBatch solo mob 100 10

    Assert.Equal(first, second)
