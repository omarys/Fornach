# ADR 0004: Monster Bestiary Subsystem and World Encounters Architecture

## Status
Accepted

## Context
In *Fornach*, combat resolution, spatial positioning, and progression have matured significantly:
- Character attributes utilize a zero-allocation, array-backed [`StatBlock`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Attributes.fs).
- The combat engine supports a 12-stat dual-plane matrix (Physical vs. Mental), three tactical vectors (Power, Agility, Discipline), and 24 mastery player archetypes.
- The procedural spatial engine generates expansive plazas and colonnades across 7 thematic floor biomes in [`TowerGenerator.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/TowerGenerator.fs).
- The narrative engine in [`STORY_AND_TOWER.md`](file:///home/omary/Dev/fornach/docs/STORY_AND_TOWER.md) frames the protagonist's descent into combat as an externalization of repressed grief following a fatal vehicle collision.

However, two major design gaps currently limit gameplay depth and atmospheric immersion:

1. **Lack of Non-Humanoid Ecological Bestiary**:
   - Currently, all enemies in the Tower and combat arena are constructed as humanoid player classes/archetypes (e.g., Novice Warrior, Veteran Assassin, Master Mage) via `TierFactory.createClassTier`.
   - Combat encounters feel like humanoid mirror duels rather than encounters with wild beasts, ancient stone constructs, chasm wraiths, and grotesque psychological grief-manifestations.
   - Enemies lack monstrous capabilities—such as pack ambushes, venomous bites, acidic blood, petrifying gazes, seismic stomps, and ethereal phasing.

2. **Static, Sparse World Encounters**:
   - Tower floors currently generate a minimal, static entity set (1 enemy, 1 NPC, 1 chest, 1 shrine per floor) placed at fixed quadrant centers.
   - There are no dynamic world encounters: no roaming elite packs, no trapped puzzle vaults, no risk/reward sacrificial altars, no wandering spectral merchants, and no environmental hazard surges.
   - In the main story, narrative chapters transition abruptly between text and boss duels without intermediate roadside encounters, memory echo fragments, or atmospheric world events.

## Decision

We establish an extensible **Monster Bestiary Subsystem** and **World Encounters Engine** across four architectural phases.

---

### Phase 1: Monster Taxonomy & Domain Model (`Fornach.Domain.Bestiary`)

We introduce dedicated domain primitives for non-humanoid adversaries while reusing the robust [`Combatant`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs) foundation for combat resolution:

1. **Monster Classification**:
   ```fsharp
   type MonsterFamily =
     | Beast              // Predators, hounds, serpents, drakes
     | Construct          // Gargoyles, basalt golems, clockwork sentinels
     | UndeadWraith       // Phantoms, drowned husks, specters of remorse
     | Aberration         // Void leeches, chasm horrors, mind-flayers
     | GriefManifestation // Grotesque embodiments of Denial, Anger, Bargaining, Depression

   type MonsterRole =
     | Swarmer    // Low HP, high numbers, Pack Tactics bonus
     | Brute      // Heavy HP, high Force/Fortitude, armor-shredding attacks
     | Skirmisher // High Agility/Reflex, evasive disengagement, bleed/poison
     | Stalker    // Stealth/invisibility, lethal initial strike from darkness
     | Caster     // Ranged psychic/arcane artillery, cognitive fatigue auras
     | Colossus   // Multi-phase floor mini-boss with heavy armor soak
   ```

2. **Monster Traits & Passive Behaviors**:
   ```fsharp
   type MonsterTrait =
     | PackTactics of hitBonusPerAlly: int
     | VenomousSting of bleedPerHit: int
     | AcidicBlood of armorCorrosion: int
     | EtherealCarapace of physicalSoakBonus: float
     | RelentlessFerocity of lowHealthRecklessnessBonus: int
     | PetrifyingGaze of reflexDebuff: int
     | PsychicDoldrums of passiveCognitiveFatigue: int
   ```

3. **Monster Action Intents**:
   Extend [`ActionIntent`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs) or provide a dedicated monster intent resolver mapping to custom attack patterns:
   - *Rend & Tear* (Dual physical bleed strike)
   - *Seismic Quake* (Area posture knockdown)
   - *Venom Spit* (Ranged attrition)
   - *Soul Wail* (Mental Morale drain & Confusion)
   - *Engulf* (Grapple pinning action)

4. **Data-Driven Monster Templates**:
   A declarative bestiary catalog (`Bestiary.allMonsters`) defining base attributes, scaling curves, traits, and unique loot drop tables.

---

### Phase 2: Biome-Themed Bestiary Catalog

Populate the 7 Tower biomes and 5 Story stages with distinctive monster rosters:

| Biome / Stage | Monster Species | Primary Vector | Tactical Dynamics |
|---|---|---|---|
| **Quarry Plazas / Prologue** | Slag Hound, Stone Gargoyle, Quarry Overseer | Power (Force) | High Armor soak, crushing bites, stun strikes |
| **Pine Cloisters / Stage 1 (Denial)** | Thorn Weaver, Mist Stalker, Blight Sprite | Agility (Finesse) | Mirage clones, evasive retreat, poison darts |
| **Basalt Calderas / Stage 2 (Anger)** | Magma Crawler, Basalt Golem, Obsidian Fiend | Power (Force) | High Recklessness, lava rifts, explosive death throes |
| **Tempest Terraces / Stage 3 (Bargaining)** | Gale Harpy, Storm Drake, Cloud Mantis | Agility & Discipline | Positional knockbacks off chasm edges, bait strikes |
| **Sunken Boulevards / Stage 4 (Depression)** | Drowned Husk, Mire Leech, Siren Specter | Discipline (Mental) | High Fortitude, Cognitive Fatigue aura, Morale siphon |
| **Elysian Sanctuaries / Stage 5 (Acceptance)** | Seraphic Warden, Solar Sphinx, Dawn Herald | Balanced | Reflected damage, calming aura reducing Recklessness |
| **Celestial Spires (Floor 7+)** | Starlit Eidolon, Void Reaver, Chrono-Anomaly | Transcendent | Spatial teleportation, temporal turn distortion |

---

### Phase 3: World & Tower Dynamic Encounters Subsystem (`Fornach.Engine.WorldEvents`)

Elevate Tower floor exploration from static placement to an event-driven expedition:

1. **Encounter Types**:
   ```fsharp
   type FloorEncounter =
     | AmbushLair of monsters: (MonsterTemplate * Point) list
     | SacrificialAltar of AltarChoice
     | WanderingTrader of SpectralMerchant
     | TreasureVault of VaultPuzzle * Relic list
     | MechanicalTrapGauntlet of TrapType * Point list
     | MemoryEchoFragment of MemoryId * string

   type AltarChoice =
     { Name: string
       Description: string
       Cost: AltarCost       // e.g., Sacrifice 25% max HP, lose 30 Morale, shred Armor
       Reward: AltarReward } // e.g., Guaranteed Critical Hit pool, permanent +15 Prowess, Vault Key
   ```

2. **Dynamic Generation & Distribution**:
   - Update [`TowerGenerator.generateFloor`](file:///home/omary/Dev/fornach/src/Fornach.Engine/TowerGenerator.fs#L48-L120) to place 2–4 diverse encounters per floor based on biome tables.
   - Enforce strategic positioning: ambush encounters guarding bottleneck colonnades, secret vaults hidden behind chasm archways, and shrines situated in central plazas.

3. **Interactive Resolution**:
   - Render themed Spectre.Console interaction modals in [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs) with full Nerd Font integration and choice prompts.

---

### Phase 4: Main Story World Encounters & Roadside Memories

Integrate the psychological narrative of amnesia and loss directly into world exploration:

1. **Roadside Memory Fragments**:
   - In [`StoryRunner.fs`](file:///home/omary/Dev/fornach/src/Fornach.Story/StoryRunner.fs) and environment scenes, scatter interactive roadside memory fragments between narrative knots.
   - Inspecting these reveals sensory fragments of the real world: the smell of ozone before rain, an abandoned school bag, a shattered headlight on wet asphalt, an unfinished voice message.

2. **Psychological Anomaly Events**:
   - Atmospheric foreshadowing of "Truck-kun": sudden tire screeching reverberating through quiet cloisters, phantom brake lights illuminating foggy archways, and distorted radio static.

3. **Non-Violent Encounter Resolution**:
   - Provide dialogue/moral choices to resolve certain encounters without violence (e.g., comforting a weeping Drowned Husk to release its Morale collapse peacefully).

---

## Consequences

### Positive
- **Vastly Enhanced Replayability**: Tower runs transition from repetitive humanoid duels into dynamic dungeon expeditions with varied threats and tactical dilemmas.
- **Deepened Narrative Cohesion**: Monsters and encounters directly reinforce the 5 stages of grief and the fatal car collision metaphor established in [`STORY_AND_TOWER.md`](file:///home/omary/Dev/fornach/docs/STORY_AND_TOWER.md).
- **Tactical Diversity**: Monsters with unique traits (venom, ethereal soak, pack flanking) force players to switch stances and utilize preparations rather than spamming a single optimal attack.

### Considerations & Guardrails
- **Performance & Zero-Allocation**: Monster templates must instantiate lightweight array-backed [`StatBlock`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Attributes.fs) combatants to maintain Monte-Carlo simulation throughput.
- **Simulation Compatibility**: New monster types and swarm behaviors must be covered in [`MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs) to verify balance curve stability.
- **Backwards Compatibility**: The existing 24 player archetypes remain untouched and continue to serve as benchmarks in `--balance-matrix`.
