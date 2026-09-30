# Array-Backed StatBlock and Static Baseline Attributes

## Status
Accepted

## Context
In Fornach, character attributes are structured around a 12-stat matrix spanning two planes (Physical and Mental), three tactical vectors (Power, Agility, Discipline), and two orientations (Offense and Defense):
- **Physical**: Force, Fortitude, Finesse, Reflex, Prowess, Poise
- **Mental**: Intellect, Resolve, Acuity, Intuition, Acumen, Composure

Historically, [`StatBlock`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Attributes.fs#L190-L253) was modeled as an immutable balanced tree via `Map<StatId, int>`. While mathematically clean and idiomatic in functional programming, empirical profiling of combat loops and swarm simulations revealed severe performance bottlenecks:

1. **Massive Allocation Overhead on Instantiation**:
   - Building a `StatBlock` via `StatBlock.Create` seeded 12 defaults through sequential `Map.add` operations, allocating ~4.9 KB to 6.5 KB of balanced tree nodes and intermediate tuples per combatant.
   - In 1 vs 100 swarm combat simulations, instantiating 100 mobs allocated over 600 KB of heap garbage before round 1 even executed.
2. **Unmemoized Static Property Churn**:
   - `StatBlock.Baseline` was defined as a dynamic property (`static member Baseline = StatBlock.Create Seq.empty`), re-constructing a full 12-node `Map` and allocating 2,680 bytes on *every access*.
3. **Tree-Walk Latency in the Inner Combat Loop**:
   - Looking up an attribute (`bs.Get StatId.Force`) required a tree walk taking ~100–140 ns. By contrast, a flat array index access takes ~2.3 ns (~40–50× faster).
   - In high-throughput simulations, `Combatant.GetStat` is invoked dozens of times per exchange (93 call sites across `ActionResolver.fs`), compounding this tree-walk cost.
4. **Cross-Thread GC Contention**:
   - Under parallel Monte-Carlo simulations, concurrent threads allocating thousands of `Map` nodes simultaneously saturated the .NET Garbage Collector, burning significant CPU time on cross-thread memory reclamation.

## Decision

We adopt a two-phase optimization of the attribute subsystem to eliminate heap churn while maintaining complete immutability and API compatibility.

### Phase 1: Memoized Static Baseline
Convert `StatBlock.Baseline` in [`src/Fornach.Domain/Attributes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Attributes.fs#L252) from a property getter to a static cached instance:

```fsharp
let private baselineInstance = StatBlock.Create Seq.empty

type StatBlock =
  ...
  /// Empty / Baseline StatBlock where every stat is exactly 10 (cached static instance)
  static member Baseline = baselineInstance
```

This immediately eliminates 2,680 B of heap allocation per read with zero API changes.

### Phase 2: Array-Backed Immutable `StatBlock`
Replace the internal `Map<StatId, int>` backing store with a flat 12-element `int[]` wrapped in a struct or lightweight type.

1. **Direct Ordinal Mapping**:
   Map the 12 [`StatId`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Attributes.fs#L7-L22) cases directly to fixed array indices:
   ```fsharp
   let inline private statToIndex (stat: StatId) : int =
     match stat with
     | Force -> 0 | Fortitude -> 1 | Finesse -> 2 | Reflex -> 3
     | Prowess -> 4 | Poise -> 5 | Intellect -> 6 | Resolve -> 7
     | Acuity -> 8 | Intuition -> 9 | Acumen -> 10 | Composure -> 11
   ```
2. **O(1) Indexed Lookups**:
   `member this.Get(stat: StatId)` executes an indexer read directly against the backing array, dropping access latency from ~110 ns to ~5.8 ns.
3. **Preserving Immutability via Array Cloning**:
   Methods that produce modified instances (`With`, `Modify`, `ApplyModifiers`) clone the 12-element backing array via `Array.copy` and update the target index. Copying 12 integers requires copying only 48 contiguous bytes (allocating ~72 bytes total), compared to allocating multiple tree node objects in `Map.add`.
4. **Interface Preservation**:
   All existing members—`Get`, `With`, `Modify`, `ApplyModifiers`, `ContainsExplicit`, `ToString()`, `Equals`, and `GetHashCode`—retain their signatures and semantics. `Combatant.GetStat` in [`Combatant.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs#L96-L104) remains the sole read seam, requiring zero edits across [`ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs).

## Considered Options

- **Retaining `Map<StatId, int>`**: Rejected. The ~5 KB allocation per combatant and tree-walk overhead directly bottleneck multi-threaded batch simulations and CLI sweeps.
- **Mutable Combatant Attribute Fields**: Rejected. Mutability compromises the safety of functional state transitions, makes rollback/undo impossible, and introduces data races in concurrent simulation contexts.
- **12-Field Plain Record (`{ Force: int; Fortitude: int; ... }`)**: Rejected. While allocation-free on stack/registers, querying dynamic attributes (`stat: StatId`) requires a 12-way match expression on every lookup, and sequence-based modifications (`ApplyModifiers`) become verbose and slow.
- **Struct Wrapping `int[]` with Static Caching**: Selected. Combines minimal allocation, O(1) indexed reads, fast 48-byte copy-on-write semantics, and full backward compatibility.

## Consequences

- **Instantiation Speed & Memory**: `StatBlock.Create` drops from **5,296 ns (4,928 B)** to **~580 ns (112 B)**—a **9× speedup** and **44× reduction** in allocated bytes.
- **Lookup Latency**: Attribute access drops from **~110 ns** to **~5.8 ns** (**17× faster**).
- **Baseline Allocation**: `StatBlock.Baseline` access allocation drops from **2,680 B** to **0 B**.
- **GC Pressure in Parallel Execution**: Eliminates the root cause of cross-thread GC contention during Monte-Carlo batch simulations.
- **Downstream Compatibility**: No breaking changes to `Combatant`, `ActionResolver`, or test suites; tests pass without modification.
