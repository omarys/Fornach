# TODO List

## Priority Architecture & Feature Roadmap

### Current Iteration Priorities

- [ ] **1. Juggernaut & Ranger Evaluation**:
  - Benchmark and tune `Juggernaut` (Power / Discipline) and `Ranger` (Agility / Discipline) progression in [`tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs) and `--balance-matrix`.

- [ ] **2. Infinite Tower Boss Scaling**:
  - Verify Grief Aspect bosses in [`docs/STORY_AND_TOWER.md`](file:///home/omary/Dev/fornach/docs/STORY_AND_TOWER.md) against the revised preparations and meter interactions.

- [ ] **3. Spatial Grid Integration**:
  - Connect `Fornach.Spatial` FOV, pathfinding, and directional flanking to the turn resolution loop.

---

### ADR 0004: Monster Bestiary & World Encounters

Reference: [`docs/adr/0004-monster-bestiary-and-world-encounters.md`](file:///home/omary/Dev/fornach/docs/adr/0004-monster-bestiary-and-world-encounters.md)

- [ ] **4. Monster Bestiary Subsystem (ADR 0004 Phases 1 & 2)**:
  - [ ] **Taxonomy & Primitives**: Define `MonsterFamily` (Beast, Construct, UndeadWraith, Aberration, GriefManifestation), `MonsterRole`, `MonsterTrait`, and `MonsterTemplate` in `src/Fornach.Domain/Bestiary.fs`.
  - [ ] **Biome Catalog**: Author specialized non-humanoid species across all 7 Tower biomes:
    - *Quarry Plazas*: Slag Hounds, Stone Gargoyles, Quarry Overseers (Force / Armor crush).
    - *Pine Cloisters*: Thorn Weavers, Mist Stalkers, Blight Sprites (Finesse / Venom / Phantoms).
    - *Basalt Calderas*: Magma Crawlers, Basalt Golems, Obsidian Fiends (Recklessness / Lava Rifts).
    - *Tempest Terraces*: Gale Harpies, Storm Drakes, Cloud Mantises (Knockbacks / Disorient).
    - *Sunken Boulevards*: Drowned Husks, Mire Crawlers, Siren Specters (Cognitive Doldrums / Morale drain).
    - *Elysian Sanctuaries*: Seraphic Wardens, Solar Sphinxes, Dawn Heralds (Reflect / Calming aura).
    - *Celestial Spires*: Starlit Eidolons, Void Reavers, Chrono-Anomalies (Teleport / Turn distortion).
  - [ ] **Tactical Action Intents**: Hook specialized monster attack patterns (Pounce, Acidic Spit, Seismic Quake, Soul Wail, Engulf) into [`ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs).
  - [ ] **Verification**: Add unit tests in `tests/Fornach.Domain.Tests/BestiaryTests.fs` and verify balance matrix compatibility.

- [ ] **5. Dynamic World & Tower Encounters Engine (ADR 0004 Phase 3)**:
  - [ ] **Encounter Domain Models**: Define `FloorEncounter` (AmbushLair, SacrificialAltar, WanderingTrader, TreasureVault, MechanicalTrapGauntlet, MemoryEchoFragment) in `src/Fornach.Domain/TowerModel.fs`.
  - [ ] **Procedural Placement**: Update [`TowerGenerator.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/TowerGenerator.fs) to distribute 2–4 diverse encounters per floor (guarding bottleneck archways, secret vault chambers, central plaza shrines).
  - [ ] **Interactive TUI Modals**: Build interactive encounter dialogs and decision prompts in [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs) with full Nerd Font integration and full-width layout expansion.

- [ ] **6. Main Story World Encounters & Roadside Memories (ADR 0004 Phase 4)**:
  - [ ] **Sensory Flashbacks**: Scatter interactive roadside memory fragments across story chapters in [`StoryRunner.fs`](file:///home/omary/Dev/fornach/src/Fornach.Story/StoryRunner.fs), unlocking narrative clues about the protagonist's lost twin (Lyra).
  - [ ] **Psychological Anomaly Events**: Implement atmospheric "Truck-kun" reality breaks (phantom headlights in fog, tire skid marks on cobblestones, radio static).
  - [ ] **Alternative Resolutions**: Support moral/peaceful encounter resolutions (e.g. comforting a weeping specter to achieve Morale equilibrium without physical bloodshed).
