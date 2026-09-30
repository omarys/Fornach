# Deterministic Parallel Simulation and Swarm Balance Matrix Architecture

## Status
Accepted

## Context
Fornach uses Monte-Carlo combat simulation in `Fornach.Cli` (`--sim`, `--group`, and `--balance-matrix`) to balance class archetypes, preparations, combat stances, and swarm survivability:
- Monte-Carlo duel and group simulations run up to 200,000 iterations to verify statistical win rates and combat event distributions.
- The 96-matchup Archetype Balance Benchmark ([`renderBalanceMatrix`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs#L744-L860)) sweeps 24 archetype-tier combinations (Novice through GrandMaster across 6 champion classes) against 4 opponent types (Warrior, Assassin, Soldier, Mage) up to 100 mobs using binary search tipping point detection.

Historically, [`renderBalanceMatrix`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs#L744) was executed entirely serially on a single thread using a single `Random(42)` instance, taking ~6 to 11 seconds to render. While Monte-Carlo batch loops had already migrated to `Array.Parallel.init`, parallelizing balance matrix sweeps and multi-combatant simulations is constrained by three critical architectural factors:

1. **PRNG Thread Safety & Reproducibility**:
   - `System.Random` is not thread-safe. Concurrent calls corrupt internal state.
   - Wrapping a shared `Random` with a lock or using `Random.Shared` ensures thread safety but destroys reproducibility: thread scheduling changes the order of operations, causing match results to drift between runs.
2. **Spectre.Console Process-Level Concurrency Limit**:
   - Spectre.Console permits only one active progress display per process. Calling `AnsiConsole.Progress().Start(...)` concurrently from multiple threads throws `InvalidOperationException: Trying to run one or more interactive functions concurrently`.
3. **GC Allocation Pressure**:
   - Parallelizing high-allocation loops across worker threads induces severe GC lock contention and CPU burn (addressed by [ADR 0002](file:///home/omary/Dev/fornach/docs/adr/0002-array-backed-statblock-and-static-baseline.md)).

## Decision

We establish an architecture for deterministic, multi-threaded simulation and balance matrix sweeping that guarantees thread safety, scheduling-independent reproducibility, and Spectre.Console compatibility.

### 1. Cell-Level Parallel Matrix Sweep
Parallelize [`renderBalanceMatrix`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs#L744) across the 24 archetype-tier cells using `Array.Parallel.init`:

```fsharp
let allCells = [
  for cls in championClasses do
    for tier in tiers do
      yield (cls, tier)
] |> List.toArray

let results =
  Array.Parallel.init allCells.Length (fun i ->
    let (cls, tier) = allCells[i]
    let cellSeed = baseSeed + i
    let rng = Random(cellSeed)
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
    let champFactory () = TierFactory.createClassTier cls tier
    let wTP = findSwarmTippingPoint champFactory warriorFactory maxMobCount iterations roller
    let aTP = findSwarmTippingPoint champFactory assassinFactory maxMobCount iterations roller
    let sTP = findSwarmTippingPoint champFactory soldierFactory maxMobCount iterations roller
    let mTP = findSwarmTippingPoint champFactory mageFactory maxMobCount iterations roller
    lock gate (fun () -> task.Increment 1.0)
    (cls, tier, wTP, aTP, sTP, mTP))
```

### 2. Scheduling-Independent Per-Cell Seeding
Each cell receives an independent, deterministic PRNG seed derived from the cell index (`baseSeed + i` or an immutable seed array). Because cell $i$ always rolls the exact same numbers regardless of which thread executes it, the balance matrix output is 100% deterministic and byte-identical across runs and machines.

### 3. Spectre.Console Interactive Display Isolation
To strictly respect Spectre.Console's single-display constraint:
- `AnsiConsole.Progress().Start(...)` is invoked once at the root of the operation.
- Worker threads update progress solely through a synchronized callback: `lock gate (fun () -> task.Increment 1.0)`.
- Computation is completely decoupled from table mutation: worker threads return row data to an ordered array, and `matrixTable.AddRow` is populated sequentially after the parallel sweep completes.
- In unit tests, all test modules invoking functions with Spectre.Console displays share the `[<Collection("SimulationBatch")>]` attribute to enforce serial execution across test suites.

### 4. Lightweight Simulation Identifiers
In high-volume simulations involving millions of ephemeral combatants (e.g. 100 mobs $\times$ 10,000 matches), generating `CombatantId` via `Guid.NewGuid()` incurs heap allocations and UUID generation overhead (~170–240 ns each).
- Headless simulation factories may supply sequential numeric IDs using an atomic `Interlocked.Increment` counter.
- Full `Guid`-based identifiers remain in domain models for persistent saves, story mode, and database serialization.

### 5. Determinism Guardrail Tests
The test suite maintains dedicated determinism regression guards in [`tests/Fornach.Domain.Tests/ParallelDeterminismTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/ParallelDeterminismTests.fs), ensuring that repeated invocations of parallel batches produce identical summary structures and win rates.

## Considered Options

- **Parallelizing Inner `runHeadlessGroupBatch` Probes**: Rejected. Inner probe batches consist of only 10 iterations; thread dispatch overhead and synchronization overhead degrade throughput on small work units. The 24-cell level provides substantial, evenly balanced work units (~400–500 ms each).
- **Shared `Random` with Mutex Lock**: Rejected. While thread-safe, thread scheduling order determines which match draws which numbers, destroying benchmark reproducibility.
- **Multiple Concurrent `AnsiConsole.Progress` Contexts**: Rejected. Throws `InvalidOperationException` due to Spectre.Console's process-level terminal state management.
- **PRNG Algorithm Replacement (`splitmix64`)**: Rejected as premature. Microbenchmarks showed contradictory results due to dispatch overhead, while profiling confirmed that RNG draws account for less than 1% of simulation runtime.

## Consequences

- **Matrix Sweep Throughput**: Rendering the 96-matchup balance matrix drops from **5,769 ms** to **980 ms** on an 8-core CPU—a **5.89× wall-clock speedup**.
- **Determinism Guaranteed**: Simulation and balance matrix outputs are reproducible down to the individual dice roll, preventing balance regressions from non-deterministic variance.
- **Console Stability**: Eliminates the risk of `InvalidOperationException` from Spectre.Console concurrency violations.
- **Scalability**: Multi-threaded sweeps scale linearly with available CPU cores when combined with [ADR 0002](file:///home/omary/Dev/fornach/docs/adr/0002-array-backed-statblock-and-static-baseline.md).
