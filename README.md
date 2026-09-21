# Fornach

> **A functional, multi-plane turn-based combat simulation engine and balance workbench written in F# (.NET 10).**

**Fornach** models multi-domain tactical combat across **Physical (Kinetic/Martial)**, **Mental (Social/Courtroom)**, and **Arcane (Psionic/Spellcraft)** disciplines. Breaking away from traditional 1–20 D&D ceilings, Fornach implements an **uncapped stat progression model** (stats ranging from 30 to 500+, pools from 500 to 10,000+) driven by a **bounded ratio dice-pool engine (4 to 20 dice)** with **deterministic floor hits**, dynamic entropy meters, and threshold-based execution finishers.

---

## Key Features

- **Dual-Plane, 3-Vector, 12-Stat Coordinate Matrix**:
  - Two parallel conflict planes: **Physical** and **Mental**.
  - Three tactical vectors: **Power**, **Agility**, and **Discipline**, each split into **Offensive** and **Defensive** attributes.
  - *Martial* denotes physical discipline mastery (**Prowess** / **Poise**), operating alongside brute kinetic Force and Reflex evasion.
  - Full domain mapping for Social/Courtroom combat (Presence, Will, Guile, Insight, Leverage, Composure).
- **Bounded Ratio Opposed Dice Pool (4–20 Dice)**:
  - Scales dice count dynamically based on the attacker/defender stat ratio (`clamp (round(6 + 4 * (atk / def)), 4, 20)`).
  - Eliminates roll explosion and memory overhead while maintaining probability curves.
- **Deterministic Floor Hits**:
  - High-tier combatants receive guaranteed hits (`stat / 15`), ensuring masters never catastrophically whiff against novices.
- **Vector Resolution Rules**:
  - **Power Vector**: Target Number 5+ (rolls of 5 score 1 hit; rolls of 6 score 2 hits).
  - **Agility Vector**: Target Number 4+ with high cadence.
  - **Discipline Vector**: Target Number 4+ with tactical rerolls funded by accumulated **Study Stacks**.
- **Tiered Multiplier & Lethality Scaling**:
  - **Whiff (≤ 0 Net Hits)**: 0.0x damage, offensive combo resets to zero.
  - **Glancing Superiority (1–2 Net Hits)**: 1.15x – 1.30x multiplier.
  - **Clear Dominance (3–5 Net Hits)**: 1.50x – 2.20x multiplier.
  - **Blowout Catastrophe (6+ Net Hits)**: 2.50x – 4.00x+ multiplier.
  - High-impact hits automatically shred physical armor durability (`max 15 (damage / 3)`).
- **Dynamic Entropy & Threshold Collapses**:
  - High-commitment power strikes and gambits generate **Recklessness** (0–100%).
  - Six status debuff meters (**Exhaustion**, **Overwhelm**, **Frustration**, **Cognitive Fatigue**, **Confusion**, **Provoke**).
  - Crossing 100% on any meter triggers a **Collapse** state (75% defense penalty), exposing the combatant to an immediate lethal **Execution Finisher**.
- **Spectre.Console Frontend (`Fornach.Cli`)**:
  - **Interactive Duel Arena**: Dual ANSI character panels, colorized resource bars, categorized action selection, and roll inspection callouts.
  - **Monte-Carlo Balance Simulator**: Headless batch simulator running 50–5000 duels with live progress tracking and statistical balance dashboards.
  - **Custom Combatant Workbench**: Interactive tool to craft and balance custom stat profiles.

---

## The 12-Attribute Matrix

| Plane | Vector | Orientation | Canonical Stat | Social Alias | Role & Mechanics |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Physical** | Power | Offense | **Force** | *Might* | Kinetic cleave, shield-breaking impact, commits Recklessness |
| **Physical** | Power | Defense | **Fortitude** | *Vigor* | Mass, structural absorption, resists stagger & blunt trauma |
| **Physical** | Agility | Offense | **Finesse** | *Precision* | Velocity, tempo exploitation, amplifies damage vs. reckless foes |
| **Physical** | Agility | Defense | **Reflex** | *Evasion* | Active deflection windows, spatial evasion, avoids bursts |
| **Physical** | Discipline | Offense | **Prowess** | *Martial Craft* | Stance dissection, converts Study Stacks into vital openings |
| **Physical** | Discipline | Defense | **Poise** | *Balance* | Stance equilibrium, primary brake to bleed off Recklessness |
| **Mental** | Power | Offense | **Intellect** | **Presence / Command** | Raw cognitive force, intimidation, inflicts Cognitive Fatigue |
| **Mental** | Power | Defense | **Resolve** | **Will / Defiance** | Psychological resilience, moral conviction, psychic warding |
| **Mental** | Agility | Offense | **Acuity** | **Guile / Charm** | Speed of thought, gaslighting, bluffs, inflicts Confusion |
| **Mental** | Agility | Defense | **Intuition** | **Insight / Scrutiny** | Lie detection, micro-expression reading, spots deceptive tells |
| **Mental** | Discipline | Offense | **Acumen** | **Leverage / Wit** | Procedural deconstruction, mockery, legal traps, inflicts Provoke |
| **Mental** | Discipline | Defense | **Composure** | **Poise / Composure** | Emotional poker face, stoicism, bleeds mental Recklessness |

---

## Project Structure

```
Dev/combat/
├── Fornach.slnx                 # .NET 10 Solution file
├── mise.toml                    # Task runner and environment definitions
├── .contextive/                 # Ubiquitous language definition mappings
├── src/
│   ├── Fornach.Domain/          # 100% functionally pure domain models
│   │   ├── Common.fs            # Planes, Vectors, Modes, Coordinates
│   │   ├── Identifiers.fs       # Strongly-typed IDs (CombatantId, StatId)
│   │   ├── Attributes.fs        # 12-stat matrix, StatBlock, aliases
│   │   ├── Meters.fs            # Bounded value objects (0-100 StatusMeters)
│   │   ├── Pool.fs              # Current/Max Health and Morale pools
│   │   ├── Collapse.fs          # Collapse states and threshold conditions
│   │   ├── Equipment.fs         # Armor integrity and equipment triggers
│   │   ├── CombatEvent.fs       # Pure domain events emitted during turns
│   │   └── Combatant.fs         # Aggregate root and collapse evaluators
│   │
│   ├── Fornach.Engine/          # Functional combat resolution engine
│   │   ├── DicePool.fs          # Bounded opposed pool resolver (4-20 dice)
│   │   ├── Actions.fs           # Action classifications, intents, results
│   │   └── ActionResolver.fs    # Tiered multipliers, passives, hooks, whiffs
│   │
│   └── Fornach.Cli/             # Spectre.Console terminal UI & test harness
│       ├── Types.fs             # Simulation types and CLI models
│       ├── Archetypes.fs        # Preset tiers (Novice, Adept, Master) & custom factory
│       ├── AI.fs                # Tactical heuristic AI evaluator
│       ├── Display.fs           # ANSI HUD, resource bars, roll breakdown, event logger
│       ├── Simulation.fs        # Headless Monte-Carlo runner & dashboard tables
│       └── Program.fs           # Entry point, CLI args (--sim), interactive menu loop
│
└── tests/
    └── Fornach.Domain.Tests/    # Unit & property-based test suite (FsCheck + xUnit)
        ├── Generators.fs        # Domain generators for property-based tests
        ├── MeterProperties.fs   # Invariant tests for 0-100 bounded meters
        ├── CollapseProperties.fs# Invariant tests for collapse thresholds
        ├── AttributeMappingTests.fs # Canonical and social stat mapping tests
        └── DicePoolTests.fs     # Pool bounds, floor hits, vector rules, whiffs
```

---

## Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- Optional: [mise-en-place](https://mise.jdx.dev/) for task runner automation

### Building & Testing

```bash
# Build the entire solution (zero warnings)
dotnet build Fornach.slnx

# Run all 25 unit and property-based tests
dotnet test Fornach.slnx
```

If using `mise`:
```bash
mise run build
mise run test
```

---

## Running the CLI Frontend (`Fornach.Cli`)

### 1. Interactive Menu Mode
Launch the interactive terminal interface:
```bash
dotnet run --project src/Fornach.Cli
# or:
mise run run
```

The menu provides:
1. ⚔️ **Interactive Duel Arena**: Playable turn-by-turn combat with categorized menus (Physical, Social, Arcane, Recovery, Finisher), dynamic ANSI health/morale meters, tactical roll breakdown, and combat event logs.
2. 📊 **Monte-Carlo Balance Simulator**: Select any two combatants and simulate 50 to 1000 iterations to evaluate statistical balance and round pacing.
3. 🛠️ **Custom Combatant Builder**: Design combatants with custom stat allotments and immediately test them in combat.
4. 📜 **View Archetype Roster**: Inspect preset character sheets across Novice, Adept, and Master tiers.

### 2. Headless Simulation Mode (Scriptable / CI)
Run batch simulations directly from the command line:

```bash
# Balanced tactical duel (Adept vs. Adept)
dotnet run --project src/Fornach.Cli -- --sim -a1 "High Magistrate" -a2 "Thought-Weaver" -n 100

# Skewed matchup verification (Master vs. Novice)
dotnet run --project src/Fornach.Cli -- --sim -a1 "Grand Warmaster" -a2 "Iron Recruit" -n 50

# Mirror match balance check
dotnet run --project src/Fornach.Cli -- --sim -a1 "Iron Vanguard" -a2 "Iron Vanguard" -n 100
```

#### CLI Simulation Flags:
- `--sim` / `-s`: Run in headless simulation mode (skips interactive menu).
- `-a1` / `--archetype1 <name>`: First combatant name (default: `"Iron Vanguard"`).
- `-a2` / `--archetype2 <name>`: Second combatant name (default: `"Thought-Weaver"`).
- `-n` / `--iterations <count>`: Number of simulated duels (default: `100`).

---

## Balance & Pacing Targets

The engine is tuned to satisfy strict design pacing guarantees:

1. **Skewed Matchups** (≥ 2:1 stat ratio or +100 stat disparity):
   - Resolve decisively in **1–2 rounds** with massive blows and devastating criticals.
2. **Even Matchups** (Comparable stat tiers):
   - Settle into a tactical pacing of **4–6 rounds**, allowing defensive resets, study accumulation, and status threshold pressure to play out.
3. **No High-Level Whiffing**:
   - High-stat combatants rely on deterministic floor hits (`stat / 15`), ensuring experienced warriors never roll catastrophic zeros against weaker opponents.

---

## License

This project is licensed under the MIT License.
