# Fornach Balance, Combat Mechanics & Class Progression Handoff

## Status: 178 Tests Passing (0 Warnings across 3 Projects)

This document provides a comprehensive handoff of the recent combat balance tuning, archetype progression overhauls, tactical preparation enhancements, swarm simulation refinements, and a solution-wide performance profile (see §4) completed in the `Fornach` codebase.

---

## 1. Executive Summary of Progress Made

### A. Berserker (Power / Physical Kinetic Juggernaut)
- **Pain Suppression / Physical Damage Shrug**: Under [`BerserkTincture`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs), adrenaline deadens pain receptors, shrugging off **35% to 50% of incoming physical damage** (scaling dynamically with Force: $0.35 + \frac{\text{Force}}{1000}$). Flank Overwhelm pressure is halved. Emits [`CombatEvent.EnrageDamageShrugged`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs). Does not mitigate Mental damage, keeping Arcane Mages as a thematic check.
- **Enraged Frenzy Bonus Attack**: While enraged, the Berserker executes an immediate second attack phase in [`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) Phase 1 each round, allowing them to clear 8–12 enemies per turn.
- **Dynamic Cleave Scaling**: Replaced hardcoded 2-target cleaves with scaling off Force ($\min(5, \max(2, \text{Force}/35))$) across tiers (Novice: 2, Veteran: 3, Master: 4, GrandMaster: 5). [`ShockwaveSlam`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs) adds +2 targets, [`BerserkTincture`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) adds +1 target and halves self-inflicted cleave Recklessness, and kinetic buffs boost cleave damage ratio to 75%.
- **Swarm AI**: In [`AI.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/AI.fs), Berserkers actively consume `BerserkTincture` in swarm combat to enrage and trigger Frenzy attacks from Round 1.

### B. Abjurer & Strategist (Discipline / Mental Buffer & Attrition Tank)
- **Aegis of Retribution**: Added [`PreparationType.AegisOfRetribution`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) (deployable to self or allies). Reduces incoming damage by **35%** and reflects **50%** back to the attacker as radiant retribution damage + **15 Frustration** (or 25% when Arcane Ward absorbs). Emits [`CombatEvent.RetributionReflected`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs).
- **Destabilizing Ground Wards & Frustration Collapse**: Ground ward stumbles (`acumenMargin >= 3`) inflict kinetic impact damage ($0.20 \times \text{Acumen}$, min 25), **+45 Frustration**, and reset combo momentum. Emits [`CombatEvent.DestabilizingWardTripped`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs). When Frustration reaches 100%, attackers immediately suffer `StanceFailure` collapse and are eliminated in [`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs).
- **Disorienting Shockwave**: Hits up to 5 adjacent flankers for 75% damage, +25 Frustration, and clears combo momentum.

### C. Mesmer (Agility / Mental Decoy & Phantasm Weaver)
- **Clone Generation & Shatter Mechanics**: Passive clone weaving replenishes decoys up to `maxClones` (strictly capped at 5). When an enemy attacks a clone, it shatters for retaliatory blast damage ($0.18 \times \text{Acuity}$, min 30) and the tile remains blocked by the dissipating phantasm for the rest of the round in [`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs).
- **Prismatic Flare**: Replaced `NeuroToxin` with [`PreparationType.PrismaticFlare`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs), which detonates when targets gain Recklessness, inflicting Morale shock and Confusion.
- **Mental Stat Adjustment**: Boosted mental stats and Intuition disparity checks so novice physical attackers are reliably baffled while grandmasters pierce the illusion.

### D. Duelist & Warden (Agility & Discipline Martial Masters)
- **Duelist Caltrop Pouch**: Flankers stepping into the 5 flanking tiles take sharp puncture damage ($0.20 \times \text{Finesse}$, min 20) and +15 Overwhelm. Encirclement penalties are capped at `Math.Min(2, priorDefenses)`.
- **Warden Bastion Zone Control**: Limits frontline attackers to 3. Reading the martial school grants persistent Study Stacks when defeating students of the same style.
- **Attacks of Opportunity (AoO)**: Window tightened, critical damage window tuned, and reactive lunge risks balanced.

---

## 2. 96-Matchup Balance Benchmark Snapshot

Results from `rtk dotnet run --project src/Fornach.Cli -- --balance-matrix`:

| Archetype | Tier | vs. Warrior (Power) | vs. Assassin (Finesse) | vs. Soldier (Discipline) | vs. Mage (Arcane) | Status |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Berserker** | Novice | 1 | 1 | 1 | 1 | Balanced Novice baseline |
| | Veteran | 21 | 28 | 16 | 12 | Kinetic slugger |
| | Master | 56 | 50 | 28 | 20 | High-power crowd clear |
| | GrandMaster | **73** | **86** | **49** | **36** | Huge physical endurance; Mages remain primary threat |
| **Duelist** | Novice | 2 | 2 | 2 | 2 | Fragile early |
| | Veteran | 22 | 100+ | 100+ | 7 | High Finesse evasion |
| | Master | 100+ | 100+ | 100+ | 100+ | Caltrops & lethal AoO counters |
| | GrandMaster | 100+ | 100+ | 100+ | 100+ | Impenetrable to mobs |
| **Warden** | Novice | 2 | 1 | 1 | 1 | Early posture resilience |
| | Veteran | 100+ | 28 | 100+ | 49 | Frontline choke control |
| | Master | 100+ | 100+ | 100+ | 100+ | Bastion defense soak |
| | GrandMaster | 100+ | 100+ | 100+ | 100+ | Unbreakable shield wall |
| **Inquisitor** | Novice | 2 | 1 | 2 | 3 | Mental dominance |
| | Veteran | 7 | 7 | 8 | 100+ | Anti-mage specialist |
| | Master | 41 | 25 | 44 | 100+ | Heavy mental pressure |
| | GrandMaster | 100+ | 100+ | 100+ | 100+ | Dread Warhorn mass routs |
| **Mesmer** | Novice | 1 | 3 | 3 | 3 | Phantasm disruption |
| | Veteran | 27 | 21 | 50 | 50 | Clone decoys absorb hits |
| | Master | 54 | 35 | 65 | 100+ | Decoys + Prismatic Flare |
| | GrandMaster | 86 | 29 | 85 | 100+ | Capped at 5 clones; resilient |
| **Abjurer** | Novice | 2 | 2 | 2 | 1 | Attrition baseline |
| | Veteran | 8 | 13 | 10 | 10 | Ground glyphs & wards |
| | Master | 20 | 28 | 29 | 27 | Consistent tanking |
| | GrandMaster | **41** | **48** | **63** | **66** | Aegis of Retribution + ground wards trip flankers |

---

## 3. Key Files & Code References

- **Core Mechanics & Combat Engine**:
  - [`src/Fornach.Engine/ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs): `applyDamage`, `resolveContestEx`, `resolveGroupTurn`, cleave scaling, ground ward tripping, retribution reflection, and AoO checks.
- **Domain Models & Events**:
  - [`src/Fornach.Domain/CombatEvent.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs): Added `EnrageDamageShrugged`, `FrenzyStrikeExecuted`, `RetributionReflected`, `DestabilizingWardTripped`.
  - [`src/Fornach.Domain/Preparations.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs): Added `AegisOfRetribution`, `PrismaticFlare`.
  - [`src/Fornach.Domain/Classes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Classes.fs): Class definitions, plane mappings, archetype vectors.
  - [`src/Fornach.Domain/TierFactory.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs): Stat scaling across Novice, Veteran, Master, GrandMaster tiers.
- **Simulation & CLI**:
  - [`src/Fornach.Cli/Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs): `runSingleGroupMatch`, Enraged Frenzy attacks, tile blocking on clone shatter, collapse elimination, and balance matrix search.
  - [`src/Fornach.Cli/AI.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/AI.fs): `chooseIntentWithContext`, swarm preparation deployment (prioritizing `BerserkTincture` for Berserker).
  - [`src/Fornach.Cli/Display.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Display.fs): ANSI formatting for all new combat events.
- **Tests**:
  - [`tests/Fornach.Domain.Tests/PreparationTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/PreparationTests.fs): Unit tests for `AegisOfRetribution`, `BerserkTincture`, and cleave scaling.
  - [`tests/Fornach.Domain.Tests/OverallBalanceTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/OverallBalanceTests.fs): Macro balance suite verifying tier scaling and plane vulnerabilities.
  - [`tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs): Swarm Monte-Carlo tests.

---

## 4. Performance Findings & Optimisation Opportunities

**Method:** Release binaries, purpose-built `#r` harness against the built DLLs, tiered compilation disabled for micro-benchmarks, median-of-5. No profiler was available on this machine (`dotnet-counters`/`-trace`/`-gcdump` all absent).

### 4.1 Already shipped — parallel Monte-Carlo batches

The batch loops in [`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) now run through `runParallelBatch` (`Array.Parallel.init` + per-iteration `Random(seeds[i])`). This is **scheduling-independent**: iteration `i` rolls identical numbers on any thread, so `--sim` and `--group` output is unchanged.

| n = 200 000, 8 cores | serial | parallel | `DOTNET_gcServer=1` |
| :--- | :---: | :---: | :---: |
| wall time | 2.62 s | 1.77 s (1.6×) | 1.29 s (**2.6×**) |

The parallel run burns **3.1× more CPU** than the simulation work requires — the gap is GC contention across allocating threads, not the progress lock. That is the direct motivation for §4.3.

Regression guard: [`ParallelDeterminismTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/ParallelDeterminismTests.fs) fails if a shared `Random` is reintroduced (verified to have teeth: reverting diverges 10.6% vs 19.2% on a matchup that should be ~63/37).

**Constraint — do not parallelise by calling these from multiple threads:** `runBatch` / `runGroupBatch` are **not safe to call concurrently**. Spectre.Console permits only one live progress display per process (`InvalidOperationException: Trying to run one or more interactive functions concurrently`). Parallelise *inside* them; test modules that invoke them share `[<Collection("SimulationBatch")>]`.

Thread-safety vs reproducibility (a `lock`-wrapped shared `Random` or `Random.Shared` is thread-safe but **not** reproducible — the multiset of rolls is stable while the per-matchup assignment drifts) was measured and rejected in favour of per-iteration seeding.

### 4.2 Measured profile — setup dominates, not the combat loop

A duel against Iron Recruit averages **1.00 rounds** (range 1–3), so the inner loop is fast and setup is effectively all of the work. Per duel: **20 KB allocated / 13.1 µs serial**; **20,155 B per match** end-to-end.

| Component | Time | Allocated |
| :--- | ---: | ---: |
| `StatBlock.Create` (per combatant) | 3,652 ns | **6,553 B** |
| `createIronRecruit` (archetype) | 3,892 ns | 6,136 B |
| `StatBlock.Baseline` (property) | 1,481 ns | **2,648 B** |
| `createGrandMasterBerserker` | 43 µs | 21,926 B |
| `Combatant.create` | 170 ns | 296 B |
| `ProgressionProfile.create` | 45 ns | 72 B |
| `CombatantId.New` | 354 ns | 16 B |

Two independent measurement lines agree that `StatBlock` is the bottleneck: **60% of duel time** (7.8 µs of 13.1 µs) and **61% of duel allocation** (12.3 KB of 20.2 KB).

### 4.3 Root cause — `StatBlock` is a `Map`

[`src/Fornach.Domain/Attributes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Attributes.fs) stores core stats as `Map<StatId, int>`. A `Map` is an immutable balanced tree: every read is a tree walk, and `Create` seeds twelve defaults through a twelve-step `Map.add` chain that allocates tree nodes on every combatant.

| Lookup route | ns/op | B/op |
| :--- | ---: | ---: |
| `bs.Get StatId.Might` (seeded) | 65.31 | 0 |
| `bs.Get StatId.Composure` (unseeded → default) | 88.14 | 0 |
| `c.Stats.Get StatId.Composure` | 65.31 | 0 |
| `c.GetStat StatId.Might` ([`Combatant.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs), `GetStat` line 97) | 68.47 | 0 |
| bare `Map.tryFind \|> Option.defaultValue` | 63.60 | 0 |
| **`array.[0]` control** | **1.75** | **0** |

≈**40× gap** on the base lookup, plus 6.5 KB per construction (2 M reads, median-of-5). `Attributes.fs` already documents the `Map` as an approximation, implying the representation was never meant to be load-bearing.

### 4.4 Ranked recommendations

| # | Change | Location | Expected win | Risk |
| :--- | :--- | :--- | :--- | :--- |
| 1 | `StatBlock` → array-backed (`int[]` indexed by `StatId`, explicit `Set` for unseeded stats) | [`Attributes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Attributes.fs) | `Get` 65 ns → ~2 ns; construction 3.9 µs → ~1 µs; duel 20 KB → under 8 KB | **Low** — the only read seam is [`Combatant.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs) `GetStat` (line 97); `ToMap()` and `.All` have no callers outside `Attributes.fs` |
| 2 | `StatBlock.Baseline` → cached static (it rebuilds a whole `Map` on every access today) | [`Attributes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Attributes.fs) | 1,481 ns / 2,648 B → 0 | Free once #1 lands; no callers outside `Attributes.fs` |
| 3 | Sim-local `CombatantId` counter (`Interlocked.Increment`) instead of `Guid.NewGuid()` | [`Identifiers.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Identifiers.fs), [`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) | ~5% of per-duel allocation | Must stay **in the sim**, not the domain, or identity for save/load breaks |
| 4 | Parallelise the 96-matchup balance matrix via `runParallelBatch` | [`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) (`renderBalanceMatrix`, ~line 744) | ~4–6× on the sweep | `matrixTable.AddRow` must be restructured; observe the §4.1 constraint |

Cheapest safe sequence: #1 → #2 (near-free once #1 lands) → #3 → #4. Note that none of this changes balance numbers: #1 and #2 are representation-only and the determinism guard in §4.1 protects them.

### 4.5 Checked and *not* hot

- **Rendering** — [`Display.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Display.fs), [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs): interactive-only, absent from the measured CPU path.
- **`descriptorOf`** (allocates a 7-field record per call; used by `ByPlane`, `byVector`, `socialNameOf`, `displayNameFor`, `tryParse`): rendering and parsing only, never combat.
- **`ActionResolver.fs`** (2,312 lines, 93 `GetStat` call sites): all route through the single `Combatant.GetStat` seam, so they inherit recommendation #1 with no per-site edits.
- **Status maps on `Combatant`** (`Map<StatusId,StatusInstance>`, `Map<string,int>`): small and short-lived; not allocation leaders.

**Explicitly not recommended:** replacing `System.Random` with a hand-rolled `splitmix64` struct PRNG. It is correct and reproducible, but the benchmarks contradicted each other by ~3.6× on identical code paths (80 M draws: 212 ms vs 105 ms *slower*; 100 M draws: 58 ms vs 199 ms *faster*). `Array.Parallel.init` over tiny work items is dispatch/GC-bound and cannot resolve the question. A verdict needs BenchmarkDotNet, not `Stopwatch` around a parallel fan-out.

---

## 5. Next Session TODO List

- [ ] **1. Juggernaut & Ranger Evaluation**:
  - Benchmark and tune `Juggernaut` (Power / Discipline) and `Ranger` (Agility / Discipline) progression in [`MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs) and `--balance-matrix`.
- [ ] **2. Infinite Tower Boss Scaling**:
  - Verify Grief Aspect bosses in [`docs/STORY_AND_TOWER.md`](file:///home/omary/Dev/fornach/docs/STORY_AND_TOWER.md) against the revised preparations and meter interactions.
- [ ] **3. Spatial Grid Integration**:
  - Connect `Fornach.Spatial` FOV, pathfinding, and directional flanking to the turn resolution loop.

---

## 6. Suggested Skills

The next agent should consider using the following skills for future tasks:
- **`fsharp-testing`**: For writing or extending xUnit, FsUnit, and FsCheck property-based tests in F#.
- **`domain-modeling`**: For modifying or adding domain entities, preparations, or ADRs.
- **`tdd`**: When introducing new preparation mechanics, status meter interactions, or combat actions test-first.
- **`roguelike`**: When integrating spatial grid combat, line of sight, and dungeon crawls with the combat engine.
- **`unslop`**: Always active to ensure clean, direct writing and documentation.

Recommended specifically for the performance work in §4:
- **`domain-modeling`**: Changing `StatBlock`'s representation (§4.4 #1) is a domain-model decision — it may warrant an ADR alongside [`docs/adr/0001-decoupled-spatial-engine-and-dynamic-fov.md`](file:///home/omary/Dev/fornach/docs/adr/0001-decoupled-spatial-engine-and-dynamic-fov.md).
- **`diagnosing-bugs`**: Its performance-regression loop is the right structure for validating the §4.4 changes against the §4.1 determinism guard.
- **`fsharp-testing`**: For the guard and benchmark-support tests around the `StatBlock` representation change.
- **No skill covers F#/.NET performance or concurrency.** That gap was searched for and confirmed empty, so use the measured profile in §4 rather than reaching for a skill on this axis.
