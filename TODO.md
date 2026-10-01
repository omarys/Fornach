# TODO List

## Priority Architecture & Feature Roadmap

### Current Iteration Priorities

- [x] **1. Juggernaut & Ranger Evaluation**:
  - Benchmark and tune `Juggernaut` (Power / Discipline) and `Ranger` (Agility / Discipline) progression in [`tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs) and `--balance-matrix` (expanded to 128 matchups).

- [x] **1.1. Tower Inspect / Look Mode**:
  - Implemented full tile inspection reticle (`x` / `X` / `;`) in [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs) with Vim/arrow navigation, environmental hazard alerts (Corrosive Acid Slag, Molten Lava Rift, Deep Current, Calming Spores), and chest/shrine detection. Verified in [`TowerTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Tests/TowerTests.fs).

- [x] **1.2. Combat Pacing & One-Shot Elimination (All Tiers)**:
  - Eliminated instant turn-1 kills across both Novice and Grandmaster duels. Soft-capped `computeTierMultiplier` with diminishing returns (capped at 5.0x), normalized mental base damage, tuned Agility crit multiplier, capped per-strike armor shredding to 25% of max durability, and raised baseline pool floors in `TierFactory.fs`. Both Novice and Grandmaster non-mirror duels now sustain 3–6 tactical rounds.

- [x] **1.3. Higher-Level Enemy Swarm Waves**:
  - Integrated Veteran (Lv. 40), Master (Lv. 100), and Grandmaster (Lv. 200) multi-enemy swarm waves in [`MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs) (expanded to 12 tests, 190 tests overall).

- [x] **1.4. Discipline Attack Separation & Duel Menu Decluttering**:
  - Enforced strict discipline boundaries in [`ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs) and [`Actions.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/Actions.fs): physical characters cannot execute magic attacks, and magic characters cannot execute physical martial strikes.
  - Streamlined duel menu in [`Program.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Program.fs): filtered choices so physical characters see only physical martial strikes, stances, and `Steady Form` (~9–10 options), while magic characters see only arcane spellcraft with proficiency indicators and `Center Mind` (~11 options). Filtered boss Trauma Gambits to contextual encounters.
  - Verified with 4 new unit tests in [`ClassBalanceTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Tests/ClassBalanceTests.fs) (194 tests passing across solution).

- [x] **1.5. Magic Complex Forms System (Occult Mental Stances)**:
  - Modeled Shadowrun Technomancer-inspired *"Weaving Complex Forms"* for arcane combatants: `ResonanceSpike` (Power/Intellect overclocking, +25% spell damage & cognitive fatigue, splash, Fading drain: +10 self-fatigue, +15 self-recklessness), `PhantasmalDiffusion` (Agility/Acuity sensory static, passive clone weaving up to 4, +40% decoy evasion swap bonus, -15% direct damage), and `AegisLattice` (Discipline/Acumen abjuration lattice, +15 Arcane Ward per turn, locks Overchannel, 50% damage reflection + 15 Frustration when ward struck).
  - Enforced cross-discipline stance separation: physical characters shift physical martial stances; mental characters thread mental complex forms.
  - Added autonomous AI form evaluation and panic switching under cognitive strain.
  - Updated duel menus and HUD rendering in [`Display.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Display.fs), [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs), and [`Program.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Program.fs).
  - Verified with 8 unit tests in [`ClassBalanceTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Tests/ClassBalanceTests.fs) (202 tests passing, 0 warnings) and verified against 128-matchup `--balance-matrix`.

- [ ] **2. Infinite Tower Boss Scaling**:
  - Verify Grief Aspect bosses in [`docs/STORY_AND_TOWER.md`](file:///home/omary/Dev/fornach/docs/STORY_AND_TOWER.md) against the revised preparations and meter interactions.

- [ ] **3. Spatial Grid Integration**:
  - Connect `Fornach.Spatial` FOV, pathfinding, and directional flanking to the turn resolution loop.

---

### ADR 0004: Monster Bestiary & World Encounters

Reference: [`docs/adr/0004-monster-bestiary-and-world-encounters.md`](file:///home/omary/Dev/fornach/docs/adr/0004-monster-bestiary-and-world-encounters.md)

- [x] **4. Monster Bestiary Subsystem (ADR 0004 Phases 1 & 2)**:
  - [x] **Taxonomy & Primitives**: Define `MonsterFamily` (Beast, Construct, UndeadWraith, Aberration, GriefManifestation), `MonsterRole`, `MonsterTrait`, and `MonsterTemplate` in `src/Fornach.Domain/Bestiary.fs`.
  - [x] **Biome Catalog**: Author specialized non-humanoid species across all 7 Tower biomes:
    - *Quarry Plazas*: Slag Hounds, Stone Gargoyles, Quarry Overseers (Force / Armor crush).
    - *Pine Cloisters*: Thorn Weavers, Mist Stalkers, Blight Sprites (Finesse / Venom / Phantoms).
    - *Basalt Calderas*: Magma Crawlers, Basalt Golems, Obsidian Fiends (Recklessness / Lava Rifts).
    - *Tempest Terraces*: Gale Harpies, Storm Drakes, Cloud Mantises (Knockbacks / Disorient).
    - *Sunken Boulevards*: Drowned Husks, Mire Crawlers, Siren Specters (Cognitive Doldrums / Morale drain).
    - *Elysian Sanctuaries*: Seraphic Wardens, Solar Sphinxes, Dawn Heralds (Reflect / Calming aura).
    - *Celestial Spires*: Starlit Eidolons, Void Reavers, Chrono-Anomalies (Teleport / Turn distortion).
  - [x] **Tactical Action Intents**: Hook specialized monster attack patterns and reactive/offensive traits (Venomous Sting, Acidic Blood, Molten Aura, Ethereal Carapace, Psychic Doldrums, Relentless Ferocity) into [`ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs) and species-instinct profiles into [`AI.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/AI.fs).
  - [x] **Verification**: Add unit tests in `tests/Fornach.Domain.Tests/BestiaryTests.fs` (212 tests passing, 0 warnings across solution).

- [x] **5. Dynamic World & Tower Encounters Engine (ADR 0004 Phase 3)**:
  - [x] **Encounter Domain Models**: Defined `FloorEncounter` (`AmbushLair`, `SacrificialAltar`, `WanderingTrader`, `TreasureVault`, `MechanicalTrapGauntlet`, `MemoryEchoFragment`) in [`TowerModel.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TowerModel.fs).
  - [x] **Procedural Placement**: Updated [`TowerGenerator.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/TowerGenerator.fs) to distribute 2–4 diverse encounters per floor via [`WorldEvents.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/WorldEvents.fs) (ambushes along colonnades, secret vaults in alcoves, sacrificial altars in courtyards).
  - [x] **Interactive TUI Modals**: Built interactive Spectre.Console encounter modals and decision prompts in [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs) with full Nerd Font glyphs, trading economy (Souls & alchemical trophies), sacrifice pacts, puzzle vaults, disarm mechanics, and roadside memory echo fragments.
  - [x] **Verification**: Added 8 comprehensive unit tests in [`EncounterTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Tests/EncounterTests.fs) (all 221 tests passing cleanly with 0 warnings).

- [ ] **6. Main Story World Encounters & Roadside Memories (ADR 0004 Phase 4)**:
  - [ ] **Sensory Flashbacks**: Scatter interactive roadside memory fragments across story chapters in [`StoryRunner.fs`](file:///home/omary/Dev/fornach/src/Fornach.Story/StoryRunner.fs), unlocking narrative clues about the protagonist's lost twin (Lyra).
  - [ ] **Psychological Anomaly Events**: Implement atmospheric "Truck-kun" reality breaks (phantom headlights in fog, tire skid marks on cobblestones, radio static).
  - [ ] **Alternative Resolutions**: Support moral/peaceful encounter resolutions (e.g. comforting a weeping specter to achieve Morale equilibrium without physical bloodshed).
