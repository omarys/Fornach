# Fornach Balance, Combat Mechanics & Class Progression Handoff

## Status: 202 Tests Passing (0 Warnings across 3 Projects)

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

### E. Juggernaut & Ranger (Power & Discipline Physical Specialists)
- **Juggernaut**: Evaluated across all tiers. Employs heavy armor absorption and [`ShockwaveSlam`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) for area disruption. GrandMaster reaches 100+ vs Warriors and 81 vs Soldiers; Mages (29) serve as an intended mental check.
- **Ranger**: Evaluated across all tiers. Skirmishes with [`CaltropPouch`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs), turning flank zones into puncture fields while capitalizing on high Prowess and Finesse for opportunist AoO strikes, reaching 100+ against all physical mobs at GrandMaster.

### F. Tower Tile & Hazard Inspection Mode
- **Interactive Inspect / Look Reticle**: Toggled via `x` / `X` / `;` in [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs). Allows free-panning the viewport with Vim (`h/j/k/l/y/u/b/n`) and arrow keys to examine tiles across visible and explored memory.
- **Environmental Hazard Diagnostics**: Highlights tactical warnings for environmental terrain:
  - *Corrosive Acid Slag*: Dissolves -15 Armor durability upon stepping.
  - *Molten Lava Rift*: Scorches player for -15 direct HP.
  - *Deep Floodwater Current*: Inflicts +15 Exhaustion drag.
  - *Calming Spore Blossom*: Resets Recklessness to 0.
- **Entity Intelligence**: Displays occupant summaries for player `@ YOU`, active guardians with Lv/HP/Morale/Stance, dialogue NPCs, chests (unopened vs looted), and runic shrines. Explored memory retains remembered chest/shrine locations. Verified with unit tests in [`TowerTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Tests/TowerTests.fs).

### G. Combat Pacing & One-Shot Elimination (All Tiers)
- **Problem Diagnosed**: High-tier (Grandmaster) duels previously devolved into "rocket tag" where the first character to land a hit dealt 20,000–65,000 damage against 9,000–15,500 HP/Morale pools, ending duels in Round 1. Concurrently, Novice duels across mismatched classes had paper-thin off-plane defensive stats (15) and shallow pools (420–450), also collapsing in Round 1.
- **Structural Normalization**:
  - `computeTierMultiplier` soft-capped at 5.0x with diminishing returns (previously 12.0x).
  - Scaled base damage: Physical to `int (float offStat * 1.50)` and Mental to `int (float offStat * 1.15)` (eliminating the quadratic `offStat^2 / 180` term that caused mental damage blowouts).
  - Tuned Agility crit multiplier from 3.2x to 1.85x.
  - Capped per-strike armor shredding to at most 25% of max armor durability (`Math.Max(15, target.Armor.Max / 4)`), allowing armor to protect over 3–5 tactical rounds rather than disintegrating on Turn 1.
  - Raised baseline Level 1 pool floors in [`TierFactory.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs) (Physical: 750 HP / 600 Morale / 30 Armor; Magic: 600 HP / 750 Morale / 20 Armor) and unified generic NPC archetypes in [`Archetypes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Archetypes.fs) with canonical `TierFactory` stat curves.
- **Results**: Both Novice and Grandmaster non-mirror duels now reliably last **3 to 6 tactical rounds**, turning duels into true strategic contests.

### H. Higher-Level Enemy Swarm Waves
- **Wave Scaling**: Expanded [`MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs) with higher-level multi-enemy encounters:
  - *Veteran Squads (Lv. 40)*: Justicar vs. 3× Veteran Juggernauts; Berserker vs. 3× Veteran Duelists.
  - *Master Pairs (Lv. 100)*: Berserker vs. 2× Master Juggernauts.
  - *Grandmaster Multi-Enemy Encounters (Lv. 200)*: Justicar vs. 2× Grandmaster Berserkers (sustaining 4.4 rounds of tactical combat).

### I. Discipline Attack Separation & Duel Menu Decluttering
- **Engine-Level Validation**: In [`Actions.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/Actions.fs) and [`ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs), characters are strictly bound to their operative combat plane (`actor.Plane`):
  - Physical combatants cannot execute magic/arcane attacks (`ArcaneCataclysm`, `SynapticGlamour`, `MirrorIllusion`, `RunicWardTrap`, `DisorientingShockwave`).
  - Magic combatants cannot execute physical martial strikes (`ForceStrike`, `FinesseCadence`, `ProwessStrike`, `CalculatedFlawStrike`, `MasterfulDisarm`).
  - Attempting an off-discipline attack safely halts the action and emits a explanatory `CombatEvent.ComboReset` without producing an invalid contest.
  - Boss `TraumaAttack` gambits remain permissible for boss manifestations.
- **Duel Menu Decluttering**: In [`Program.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Program.fs) (`buildActionChoices`), menu choices are cleanly segmented by player plane:
  - Physical combatants see exclusively physical martial strikes, stance shifts, and `Steady Form` (~9–10 choices).
  - Magic combatants see exclusively arcane spellcraft with proficiency tags and `Center Mind` (~11 choices).
  - Grief boss trauma gambits only appear when facing or controlling Grief manifestations.
- **AI Decision Alignment**: In [`AI.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/AI.fs), autonomous combatants select offensive attacks, recovery resets, and finishers matching their discipline.

### J. Magic Complex Forms System (Occult Mental Stances)
- **Domain Modeling**: Designed Shadowrun Technomancer-inspired *"Weaving Complex Forms"* for mental/arcane combatants, decoupling mental magic from physical martial stances:
  - **`Resonance Spike`** (Power / Intellect Vector): Volatile psychic overclocking. Grants **+25% spell damage**, inflicts **+20 extra Cognitive Fatigue** on targets, and expands group splash radius up to 4 targets at 65% damage. Incurs somatic **Fading Drain** on the caster (+10 Cognitive Fatigue and +15 Recklessness per cast), emitting [`CombatEvent.FadingDrainSuffered`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs). If fatigue/recklessness hits 100%, existing threshold collapses fire naturally.
  - **`Phantasmal Diffusion`** (Agility / Acuity Vector): Sensory static and perceptual jitter. Passively weaves +1 Mirror Clone per turn during upkeep up to 4, grants a **+40% decoy evasion swap bonus** on incoming attacks, and allows emergency phasing even with 0 initial clones. Incurs a **-15% direct damage reduction**.
  - **`Aegis Lattice`** (Discipline / Acumen Vector): Sacred geometric abjuration web. Passively restores **+15 Arcane Ward per turn** during upkeep, locks `ArcaneCataclysm: Overchannel` (grounding it back to standard cast with 0 Recklessness penalty), and reflects **50% damage back to attacker + 15 Frustration** whenever the Arcane Ward absorbs incoming damage.
- **Cross-Discipline Boundaries**: Physical combatants can only shift physical `CombatStance` (`PowerStance`, `AgilityStance`, `DisciplineStance`); mental combatants can only thread mental `ComplexForm`. Attempting the wrong discipline blocks the action with a `ComboReset` event.
- **Autonomous AI & Duel Menus**: In [`AI.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/AI.fs), magic combatants evaluate their preferred form and panic-shift into `AegisLattice` to stabilize if reckless entropy or cognitive fatigue nears dangerous levels. Duel menus in [`Program.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Program.fs) and HUD panels in [`Display.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Display.fs) and [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs) render active Complex Forms for all mental characters.
- **Verification**: Verified with 8 dedicated unit tests in [`ClassBalanceTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Tests/ClassBalanceTests.fs) (202 tests passing, 0 warnings across solution) and verified against 128-matchup `--balance-matrix`.

---

## 2. 128-Matchup Balance Benchmark Snapshot

Results from `rtk dotnet run --project src/Fornach.Cli -- --balance-matrix`:

| Archetype | Tier | vs. Warrior (Power) | vs. Assassin (Finesse) | vs. Soldier (Discipline) | vs. Mage (Arcane) | Status |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Berserker** | Novice | 1 | 1 | 1 | 1 | Balanced Novice baseline |
| | Veteran | 21 | 28 | 16 | 12 | Kinetic slugger |
| | Master | 56 | 50 | 28 | 20 | High-power crowd clear |
| | GrandMaster | **73** | **86** | **49** | **36** | Huge physical endurance; Mages remain primary threat |
| **Juggernaut** | Novice | 1 | 1 | 1 | 1 | Iron Colossus baseline |
| | Veteran | 16 | 8 | 14 | 4 | Heavy armor soak |
| | Master | 46 | 25 | 34 | 12 | Shockwave Slam formation disrupt |
| | GrandMaster | **100+** | **53** | **81** | **29** | Heavy physical endurance; Mages check lower mental resolve |
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
| **Ranger** | Novice | 1 | 1 | 1 | 1 | Skirmisher baseline |
| | Veteran | 27 | 32 | 32 | 12 | Caltrop Pouch crowd control |
| | Master | 100+ | 97 | 100+ | 36 | Opportunist agility & reactive counters |
| | GrandMaster | **100+** | **100+** | **100+** | **100+** | Fluid zone control & reactive counters |

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

**Currency:** §4.1–§4.5 were measured *before* the `StatBlock` rework of [ADR 0002](file:///home/omary/Dev/fornach/docs/adr/0002-array-backed-statblock-and-static-baseline.md) landed. §4.2's `StatBlock` / `Baseline` / `createGrandMasterBerserker` rows and the §4.4 ranking are superseded by **§4.6**, which is the current profile.

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

A duel against Iron Recruit (a Novice soldier archetype, retired from the roster in the archetype cleanup) averages **1.00 rounds** (range 1–3), so the inner loop is fast and setup is effectively all of the work. Per duel: **20 KB allocated / 13.1 µs serial**; **20,155 B per match** end-to-end.

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

### 4.6 Second-pass review — union `ToString()` in match setup

Taken after the ADR 0002 work landed, on scratch copies (the working tree was not modified). Method as in §4, plus before/after instrumentation of `Fornach.Cli --balance-matrix` (96 matchups, 9,580 matches / 75,736 rounds) with identical instrumentation on both sides. Full evidence and the decision: [ADR 0005](file:///home/omary/Dev/fornach/docs/adr/0005-hand-written-name-members-instead-of-union-tostring.md).

**Dominant finding:** [`TierFactory.fs:238`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs#L238) built the archetype name with `sprintf "%s %s" (tier.ToString()) cls.Name`. The compiler-generated union `ToString()` goes through reflective structured formatting: **40,093 ns / 15,026 B per call**, against **3.4 ns / 0 B** for the hand-written `CharacterClass.Name` ([`Classes.fs:79`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Classes.fs#L79)) and 7.9 ns for the `int.ToString()` control. Every union in `Fornach.Domain` pays this printer (`CharacterClass` 63 µs, `CollapseReason` 46 µs, `WeaponCondition` 42 µs, `CombatMode` 41 µs, `Plane` 36 µs).

| `--balance-matrix` | baseline | with a `CombatTier.Name` member |
| :--- | ---: | ---: |
| wall clock (2 runs) | 9.39 s / 10.05 s | **2.28 s / 2.30 s** (~4.3×) |
| match setup | 7,604 ms (80%) | 306 ms (14%) |
| allocated | 7.27 GB | 3.94 GB |
| balance table | — byte-identical after ANSI strip — | |
| `dotnet test -c Release` | — 179 / 179 pass, 0 warnings — | |

This also fully explains the `createGrandMasterBerserker` row of §4.2 (43 µs / 21,926 B) — it is the `ToString()` call, not level scaling: `createClassLevel` alone is 1,073 ns / 1,920 B.

**Ranking after this pass:**

| # | Change | Expected win | Status |
| :--- | :--- | :--- | :--- |
| 1 | `member CombatTier.Name` on the union, used at `TierFactory.fs:238` (ADR 0005) | ~4.3× on the matrix sweep; −3.3 GB | Proposed, validated; 8-line patch |
| 2 | Stop rebuilding the mob list per swing — `List.mapi` at [`Simulation.fs:446`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs#L446) rebuilds all n elements on *every* swing (O(n²) list rebuilds per round), and `:444` rebuilds again on elimination | 86% of post-fix runtime: 1,808 ms / 3.55 GB, ~24 µs and ~47 KB per round | **Not measured** — mechanism only |
| 3 | Sim-local `CombatantId` counter (was §4.4 #3) | ~3% of runtime now that setup is cheap | Still valid |
| 4 | Parallelise the matrix sweep (was §4.4 #4) | less attractive at a 2.3 s total | Still valid, subject to the §4.1 Spectre constraint |

**Recommendation #2 in detail:** make `mob` a match-local array, replace the per-swing rebuild with an index write (`mobArr.[mobIdx] <- newAttacker`), and compact once per round rather than once per swing. Keep the compaction **order-preserving** — `AI.chooseGroupTarget` and enchainment order depend on list order, so a swap-remove would move balance numbers even though the outcome distribution should not change.

**Now not worth doing:** anything on the creation path (`StatBlock.Create`, template/`statList` caching, the record copies inside `createClassTier`) has a 306 ms ceiling (14%). `StatBlock.Get` measures 9.2 ns and `StatBlock.Baseline` 4.5 ns / 0 B, confirming §4.4 #1 and #2 landed as intended (realised lookup cost is ~9–12 ns rather than the ~2 ns projected).

---

## 5. Next Session TODO List

- [x] **1. Juggernaut & Ranger Evaluation**:
  - Benchmark and tune `Juggernaut` (Power / Discipline) and `Ranger` (Agility / Discipline) progression in [`MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs), [`OverallBalanceTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/OverallBalanceTests.fs), and `--balance-matrix` (expanded to 128 matchups).
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
- **`domain-modeling`**: The architecture and empirical benchmarks for changing `StatBlock`'s representation (§4.4 #1 & #2) and multi-threading simulation sweeps (§4.4 #4) are recorded in [`docs/adr/0002-array-backed-statblock-and-static-baseline.md`](file:///home/omary/Dev/fornach/docs/adr/0002-array-backed-statblock-and-static-baseline.md) and [`docs/adr/0003-deterministic-parallel-simulation-and-balance-matrix.md`](file:///home/omary/Dev/fornach/docs/adr/0003-deterministic-parallel-simulation-and-balance-matrix.md).
- **`domain-modeling`**: The second-pass measurements (§4.6) and the decision to hand-write name members instead of formatting unions reflectively are recorded in [`docs/adr/0005-hand-written-name-members-instead-of-union-tostring.md`](file:///home/omary/Dev/fornach/docs/adr/0005-hand-written-name-members-instead-of-union-tostring.md).
- **`diagnosing-bugs`**: Its performance-regression loop is the right structure for validating the §4.4 changes against the §4.1 determinism guard.
- **`fsharp-testing`**: For the guard and benchmark-support tests around the `StatBlock` representation change.
- **No skill covers F#/.NET performance or concurrency.** That gap was searched for and confirmed empty, so use the measured profile in §4 rather than reaching for a skill on this axis.

Recommended for content expansion:
- **`domain-modeling` & `roguelike`**: The architecture for the Monster Bestiary, ecological traits, dynamic Tower encounters, and main story memory fragments is specified in [`docs/adr/0004-monster-bestiary-and-world-encounters.md`](file:///home/omary/Dev/fornach/docs/adr/0004-monster-bestiary-and-world-encounters.md) and tracked in [`TODO.md`](file:///home/omary/Dev/fornach/TODO.md).
