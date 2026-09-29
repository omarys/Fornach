# Fornach Balance, Combat Mechanics & Class Progression Handoff

## Status: 176 Tests Passing (0 Warnings across 3 Projects)

This document provides a comprehensive handoff of the recent combat balance tuning, archetype progression overhauls, tactical preparation enhancements, and swarm simulation refinements completed in the `Fornach` codebase.

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

## 4. Next Session TODO List

- [ ] **1. Juggernaut & Ranger Evaluation**:
  - Benchmark and tune `Juggernaut` (Power / Discipline) and `Ranger` (Agility / Discipline) progression in [`MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs) and `--balance-matrix`.
- [ ] **2. Infinite Tower Boss Scaling**:
  - Verify Grief Aspect bosses in [`docs/STORY_AND_TOWER.md`](file:///home/omary/Dev/fornach/docs/STORY_AND_TOWER.md) against the revised preparations and meter interactions.
- [ ] **3. Spatial Grid Integration**:
  - Connect `Fornach.Spatial` FOV, pathfinding, and directional flanking to the turn resolution loop.

---

## 5. Suggested Skills

The next agent should consider using the following skills for future tasks:
- **`fsharp-testing`**: For writing or extending xUnit, FsUnit, and FsCheck property-based tests in F#.
- **`domain-modeling`**: For modifying or adding domain entities, preparations, or ADRs.
- **`tdd`**: When introducing new preparation mechanics, status meter interactions, or combat actions test-first.
- **`roguelike`**: When integrating spatial grid combat, line of sight, and dungeon crawls with the combat engine.
- **`unslop`**: Always active to ensure clean, direct writing and documentation.
