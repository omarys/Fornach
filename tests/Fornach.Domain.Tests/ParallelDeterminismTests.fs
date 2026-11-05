namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// Guards the parallel Monte-Carlo batches in src/Fornach.Engine/CombatSimulation.fs.
///
/// `System.Random` is not thread-safe. If a single instance is shared across the
/// worker threads of `runParallelBatch`, concurrent `Next` calls corrupt its
/// internal seed state and every result becomes a function of thread scheduling.
/// These tests fail if a shared RNG is reintroduced, or if the per-iteration
/// seeding that makes results scheduling-independent is dropped.
///
/// Shares the "SimulationBatch" collection with MonteCarloSwarmTests so the two
/// frontiers cannot be measured at the same time.
[<Collection("SimulationBatch")>]
module ParallelDeterminismTests =

  [<Fact>]
  let ``runBatch: repeated invocations produce identical summaries`` () =
    let warrior = BalanceBatches.novice CharacterClass.Warrior "Warrior"
    let mage = BalanceBatches.novice CharacterClass.Mage "Mage"

    let first = BalanceBatches.duelOutcomes warrior mage 200
    let second = BalanceBatches.duelOutcomes warrior mage 200

    // Meta-guard: a matchup that is a clean sweep for one side produces byte-identical
    // iterations, which would make the determinism assertion below vacuous. This pairing
    // is a near coin-flip, so it genuinely exercises the roller.
    let winsA = first |> List.filter (fun r -> r.WinnerIsA = Some true) |> List.length
    let winsB = first |> List.filter (fun r -> r.WinnerIsA = Some false) |> List.length
    Assert.True(winsA > 0 && winsB > 0, "matchup no longer varies; determinism check is vacuous")

    Assert.Equal<BalanceBatches.DuelOutcome list>(first, second)

  [<Fact>]
  let ``runGroupBatch: repeated invocations produce identical summaries`` () =
    // "Grandmaster Justicar" no longer exists: only the two player classes carry tiered archetypes.
    let solo = BalanceBatches.grandmasterFighter
    let mob = BalanceBatches.novice CharacterClass.Warrior "Warrior"

    let first = BalanceBatches.groupSummary solo mob 100 10
    let second = BalanceBatches.groupSummary solo mob 100 10

    Assert.Equal(first, second)
