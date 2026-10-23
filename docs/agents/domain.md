# Fornach Balance, Combat Mechanics & Class Progression Handoff

## Status: 499 Tests Passing (273 `Fornach.Tests` + 203 `Fornach.Domain.Tests` + 23 `Fornach.Spatial.Tests`, 0 Warnings)

This document provides a comprehensive handoff of the recent combat balance tuning, archetype progression overhauls, tactical preparation enhancements, swarm simulation refinements, and a solution-wide performance profile (see §4) completed in the `Fornach` codebase.

---

## 1. Executive Summary of Progress Made

**Class roster note:** the eight specialized player classes — Berserker, Duelist, Warden, Justicar, Mesmer, Abjurer, Strategist and Inquisitor — were removed in the class reduction and merged into the three patterns below (Fighter, Mage, and the generic NPC trio). Their mechanics were re-keyed, not deleted: everything described in this section is live code reachable through `Fighter`, `Mage`, `Warrior`, `Rogue` or `Soldier` ([`Classes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Classes.fs)).

### A. Fighter (Power / Physical — fluid stances, cleave, bastion)
- **Pain Suppression / Damage Shrug**: Under [`BerserkTincture`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs), adrenaline deadens pain receptors, shrugging off **35% to 50% of incoming physical damage** (scaling dynamically with Force: $\min(0.50,\ 0.35 + \frac{\text{Force}}{1000})$) plus a smaller mental buffer ($\min(0.30,\ 0.15 + \frac{\text{Force}}{2500})$). Emits [`CombatEvent.EnrageDamageShrugged`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs). Consuming the tincture costs $\min(15, \max(5, \text{Health}/10))$ HP and spikes Recklessness by +15.
- **Recklessness Tolerance**: [`Combatant.evaluateCollapse`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs) raises the Recklessness collapse cap from 100 to **120** while the tincture is active — the inherited fever-pitch ceiling.
- **Enraged Frenzy Bonus Attack**: With the tincture active, [`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) Phase 1 fires an immediate follow-up `ForceStrike false` (with its own cleaves) each round, resolved before the next regular action.
- **Dynamic Cleave Scaling**: In `CombatStance.PowerStance` a Fighter cleaves $\min(5, \max(2, \text{Force}/35))$ adjacent targets (Novice: 2, GrandMaster: 5). [`ShockwaveSlam`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs) adds +2 targets, [`BerserkTincture`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) adds +1 target and halves self-inflicted cleave Recklessness, and either kinetic buff lifts the cleave damage ratio from 60% to **75%**.
- **Force Dispersion**: A Fighter in `PowerStance` subtracts **15** from a Mage's decoy-swap chance in [`ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs) — brute force disperses phantasmal static.
- **Bastion Frontage & Buckler**: [`BastionZoneControl`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) plants a zone that limits simultaneous attackers to **3** (`Math.Min(2, priorDefenses)`), [`ParryingBuckler`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) lowers the Indes threshold by 1, and [`CaltropPouch`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) turns the 5 flanking spaces into puncture ground.
- **Discipline Read / Study Stacks**: Attacks of opportunity pick the vector the defender outclasses the flanker in — Discipline or Agility, Power is excluded — and a Discipline-vector defender reads the attacker's martial school for **+1 Study Stack** per triggered AoO. Accumulated stacks then drive reactive ripostes and disarms (counter chance `Math.Min(70, stacks * 8)`).
- **Preparations (6)**: `ShockwaveSlam`, `BerserkTincture`, `CaltropPouch`, `ConcealedBlade`, `BastionZoneControl`, `ParryingBuckler` ([`PreparationType.ForClass`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs)).

### B. Mage (Mental / Power vector baseline — Complex Forms, decoys, wards, mind-shock)
- **Complex Forms**: The mental counterpart to stances, threaded automatically by `applyFluidTransition` from the last attack issued. **Resonance Spike** multiplies damage by 1.25, adds **+20 Cognitive Fatigue** to the target, and inflicts Fading Drain (+10 Cognitive Fatigue, +15 Recklessness on the caster). **Phantasmal Diffusion** regenerates one mirror clone per upkeep (up to 4), costs 15% direct damage, and raises the decoy-swap base chance from 25% to 50%. **Aegis Lattice** restores **+15 Arcane Ward** per upkeep, grounds `ArcaneCataclysm: Overchannel` to a standard cast, and reflects **50% + 15 Frustration** whenever the ward absorbs.
- **Mirror Decoys**: Passive clone weaving on a primary engagement keeps at most `maxClones` decoys (**2 at level ≥ 100, otherwise 1**) while cognitive fatigue stays under tolerance; [`MirrorMirage`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) deploys one immediately. Swapping places with a decoy shatters it for $\max(15, 0.10 \times \text{Acuity})$ Morale damage, +15 Confusion, +5 Recklessness and a combo reset; `MirrorMirage`'s phantasm deception (Acuity vs. Intuition, base 55%, clamped 5–95) blasts for $\max(15, 0.08 \times \text{Acuity})$ with +20 Confusion and +10 Recklessness. Both side swaps are bounded by the first 5 flanking spaces.
- **Arcane Ward & Retribution**: A Mage whose `ArcaneWard` absorbs a hit reflects **25%** of the raw damage + **10 Frustration**; [`AegisOfRetribution`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) (self or ally, $\max(25, 0.40 \times \text{Acumen})$ ward) cuts incoming damage to **65%** and reflects **50% + 15 Frustration**, as does an active `AegisLattice`. Emits [`CombatEvent.RetributionReflected`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs).
- **Synaptic Mind-Shock**: Intellect vs. Fortitude ($\text{ratio} \times 2.5 - 1.5$ margin). A margin of ≥ 2.5 shatters the attacker's focus (**+30 Cognitive Fatigue**, +15 Frustration, combo reset, fatigue cost 0/2/5/10 by level disparity); a margin of ≥ 0 glances the strike for **−30%** damage.
- **Destabilizing Ground Ward**: $\text{acumenMargin} = \text{Acumen}/20 - \text{Poise}/20 - \text{fatigue penalty} + 1d6 - 1d6$. A margin ≥ 3 trips the attacker ($\max(25, 0.20 \times \text{Acumen})$ Morale impact, Frustration $\max(35, 40 + \text{disparity}/15)$, +25 Overwhelm, +15 Exhaustion, combo reset — [`CombatEvent.DestabilizingWardTripped`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs)); a margin ≥ 0 halves the incoming strike. Frustration reaching 100 is a `StanceFailure` collapse ([`Combatant.evaluateCollapse`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs)).
- **Disorienting Shockwave**: Hits up to 5 adjacent flankers for 75% damage, +25 Frustration, +15 Confusion and +15 Provoke, and clears combo momentum.
- **Preparations (7)**: `DreadWarhorn` ($\max(20, 0.35 \times \text{Intellect})$ Morale damage + 25 Cognitive Fatigue), `SynapticBrand` (critical strikes deal 2× Morale damage and inflict Rupture), `MirrorMirage`, `PrismaticFlare` (Morale shock + 20 Confusion on Recklessness), `AegisOfRetribution`, `HeraldicTreatise` (+2 Study Stacks), `SocraticDossier`.

### C. Generic NPC Classes (Warrior / Rogue / Soldier)
- **No Preparations**: [`PreparationType.ForClass`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs) returns `[]` for all three, so none of the kits above is available to them — only stance, stats and the shared AoO/meter rules.
- **Warrior** (Power / Physical, Force + Fortitude primary): front-line shock troop; beast brutes and swarmers are built as Warriors.
- **Rogue** (Agility / Physical, Finesse + Reflex primary): skirmisher and stalker stand-in; an Agility advantage is what buys it the preemptive AoO against flankers.
- **Soldier** (Discipline / Physical, Prowess + Poise primary): rank-and-file guard, the construct/colossus stand-in. It carries **+15% morale and armor** (`int (base * 1.15)` in [`TierFactory.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs)) and is where the discipline read lives for NPCs — a Prowess advantage earns it the preemptive AoO and its Study Stacks fuel ripostes and disarms.
- **Stat Shape**: each generic class sets its own vector to `prim` and the other two physical vectors to `sec` (0.75×), with off-plane mental attributes at the level floor. Novice pools are **750 HP / 600 Morale / 30 Armor** on the Physical plane and **600 / 750 / 20** on the Mental plane.

### D. Class Roster Reduction (historical record)
- **Current roster**: `CharacterClass.All = [ Fighter; Mage; Warrior; Rogue; Soldier ]`, with `PlayerClasses = [ Fighter; Mage ]` and `GenericClasses = [ Warrior; Rogue; Soldier ]`. `SpecializedClasses` and `RestrictedClasses` no longer exist.
- **Where each retired kit went**: Berserker → `Fighter` (PowerStance cleave cap 5, kinetic shrug, Frenzy, Recklessness cap 120, `BerserkTincture`); Mesmer → `Mage` (mirror clones, `MirrorMirage`, `PrismaticFlare`); Abjurer → `Mage` (`AegisOfRetribution`, ward reflect); Strategist → `Mage` (`AegisLattice`, destabilizing ground ward); Inquisitor → `Mage` (`SynapticBrand`, synaptic mind-shock, `DreadWarhorn`); Warden → `Fighter`/`Soldier` (bastion frontage, Study Stacks); Justicar → `Soldier` (`HeraldicTreatise`, Study Stacks); Duelist → `Fighter` (`CaltropPouch`, `ConcealedBlade`, `ParryingBuckler`).
- **Monsters follow the same re-keying**: [`Bestiary.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Bestiary.fs) maps constructs to `Soldier`, beast brutes/swarmers to `Warrior`, beast skirmishers/stalkers to `Rogue`, and wraiths, grief manifestations and aberration casters/colossi to `Mage`; [`Bosses.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Bosses.fs) only builds named bosses from the five surviving classes.
- **Historical measurements**: figures recorded in this document before the reduction — including §4's profile rows — were produced with constructors that no longer exist (`createGrandMasterBerserker`, `createIronRecruit`) and with the retired archetype names in the §2 matrix. Read them as historical record, not as numbers reproducible against today's roster.

### E. Archetype Roster & Tier Factories
- **Registry**: [`Archetypes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Archetypes.fs) exposes 11 entries in `allArchetypes`: `Veteran Fighter`, `Veteran Mage`, `Master Fighter`, `Master Mage`, `Grandmaster Fighter`, `Grandmaster Mage`, plus the unprefixed Novice names `Fighter`, `Mage`, `Warrior`, `Rogue`, `Soldier`. Nothing else is registered, so tiered archetypes exist for the two player classes only.
- **Construction**: each entry is one call — `TierFactory.createClassTier cls tier` for the tiered rows, `TierFactory.createClassLevel cls 1` for the Novice rows. The `createVeteranFighter` / `createGrandMasterMage` family wraps those two functions, and `createGrandmasterFighter` / `createGrandmasterMage` remain as aliases.
- **What is gone**: the per-class archetype families (Juggernaut, Ranger, and the eight specialized classes) are no longer in the registry — specialisation now happens inside a class via fluid stances, Complex Forms and preparations, not by picking a class.

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
- **Wave Scaling**: [`MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs) now drives every wave through the surviving archetypes — Novice mobs against a Grandmaster player archetype:
  - *Grandmaster press (Lv. 200)*: `Grandmaster Fighter` holds a 100× Warrior press (win rate ≥ 95%, ≥ 80 eliminations, ≥ 2 opportunity attacks per round) and beats 12 Soldiers and 10 Rogues at the same 95% threshold.
  - *Grandmaster arcane (Lv. 200)*: `Grandmaster Mage` defeats 15 Mages, repels 8 Warriors through its Complex Forms, and outlasts 8 Soldiers (95% threshold on all three).
  - *Mastery cliff*: the same 8-Warrior press that a `Grandmaster Fighter` holds (≥ 95%) still overruns a Novice `Fighter` (< 50%).
- **Retired squads**: the Veteran/Master multi-enemy squads named in earlier drafts mixed archetypes that no longer exist; those wave numbers are historical and were not re-measured.

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
  - **`Phantasmal Diffusion`** (Agility / Acuity Vector): Sensory static and perceptual jitter. Passively weaves +1 Mirror Clone per turn during upkeep up to 4, raises the decoy-swap base chance from 25% to 50% on incoming attacks, and allows emergency phasing even with 0 initial clones. Incurs a **-15% direct damage reduction**.
  - **`Aegis Lattice`** (Discipline / Acumen Vector): Sacred geometric abjuration web. Passively restores **+15 Arcane Ward per turn** during upkeep, locks `ArcaneCataclysm: Overchannel` (grounding it back to standard cast with 0 Recklessness penalty), and reflects **50% damage back to attacker + 15 Frustration** whenever the Arcane Ward absorbs incoming damage.
- **Cross-Discipline Boundaries**: Physical combatants can only shift physical `CombatStance` (`PowerStance`, `AgilityStance`, `DisciplineStance`); mental combatants can only thread mental `ComplexForm`. Attempting the wrong discipline blocks the action with a `ComboReset` event.
- **Autonomous AI & Duel Menus**: In [`AI.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/AI.fs), magic combatants evaluate their preferred form and panic-shift into `AegisLattice` to stabilize if reckless entropy or cognitive fatigue nears dangerous levels. Duel menus in [`Program.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Program.fs) and HUD panels in [`Display.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Display.fs) and [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs) render active Complex Forms for all mental characters.
- **Verification**: Verified with 26 unit tests in [`ClassBalanceTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Tests/ClassBalanceTests.fs) (352 tests passing, 0 warnings across the solution) and against the `--balance-matrix` sweep in §2.

---

### K. Attack Ordering by Estimated Effectiveness
- [`AttackEstimator.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/AttackEstimator.fs) scores every available strike/spell against the scouted enemy stats; `Program.buildActionChoices` then orders the encounter menu by expected damage. An `Obscured` enemy falls back to archetype order — the estimator never guesses stats it does not have.
- Stance and off-vector penalties are reflected in the estimate; `AttackEstimatorTests.fs` guards the ranking, including a monotonicity test (a bigger enemy pool must not raise a strike's rank) and an end-to-end menu-order test.
- This is TODO item 7's first sub-item and the factory for Quick-Play; see §7.

### L. Traversal Economy — Perceive, Repair, Hone
- **FOV dilation** lives in [`Vision.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Vision.fs): `LocomotionState.Walking` sees **120°**; each turn spent waiting widens the arc by **90°** (45 per side) up to **360°**; moving again narrows it. `TowerSession.updateFov` recomputes it per move and costs ~0.2 ms.
- **Safe steps** (the same gate armor repair uses) restore **5% of max armor** and, every **fourth** safe step, hone the weapon one condition step (`TowerSession.honeOnSafeStep`). `restHoneWeapon` deliberately stops **one step short of Pristine** — rest can pull a ruined weapon back, never to full.
- **Resting** restores **33% of max** Health, Morale and Armor, all clamped by `restCap` at **80% of maximum** (`TowerSession.restRecoveryFraction = 0.80`). The 100% ceiling belongs to shrines and to the consumables in §P.
- `TowerDisplay.fs:1713` (5% step armor), `:1747-1752` (33% rest) are the TUI call sites; the arithmetic itself lives in `TowerSession.recoverOutOfCombat`.

### M. Wounds — Bleed Decay and the Regen Gate
- Bleed stacks decay **1 per safe step** and **5 per rest** (`TowerSession.bleedClosedPerStep` / `bleedClosedPerRest`), floored at 0.
- Health and Morale regeneration is **gated on the player having zero bleed stacks** after that turn's decay; armour repair and weapon honing are *not* gated. A bleeding player therefore still maintains gear but cannot heal, and the status line says so.
- In combat the existing tick is unchanged: `ActionResolver.fs:835-839` applies `stacks × 1.2%` of max health and consumes one stack per turn.

### N. Altars — Costs Are Percentages of Max
- Altar costs were flat integers (`SacrificeHealth 25`, `SacrificeMorale 30`, `ShredArmor 30` in [`WorldEvents.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/WorldEvents.fs)). Against pools that scale into the thousands at high tier those became free, so an altar could never bite.
- `applyAltarSacrifice` (`TowerSession.fs:823+`) now reads every cost through `percentOf <pool.Maximum> <pct>`: **health 25%, morale 25–30%, armor 30%**. Health cost is clamped to leave **at least 1 HP**, so an altar can never end a run.
- Consequence to preserve: accepting a health-cost altar from full always lands strictly below the 80% rest cap, which is the point — the dip is now unavoidable rather than level-dependent.
- **The reward side went the same way in the same commit.** `applyAltarSacrifice` (`TowerSession.fs:844+`) resolves it through a local `pctOf <current> <pct>`: `StatBuff (stat, pct)` raises the current value, `VitalitySurge (hpPct, morPct)` adds a share of the pool maximum (`ApplyDelta` clamps, so a surge accepted at full health is not banked), and `RelicReward` scales the item's `StatModifiers` against the bearer at the moment of the bargain — the stored relic keeps the resolved points, not the percentages.

### O. Shrines — Repeatable Consolidation Point
- A shrine is no longer consumed: the `when not shrine.IsUsed` guards are gone from the engine step branch and from both TUI sites, and `IsUsed` is never written. Stepping onto it re-triggers every time.
- Each visit runs `TowerSession.shrineBlessing`: full restore (Health/Morale/Armor/weapon/meters/bleed) → reveal **every** carried unidentified consumable → consume and apply **all** carried rare materials. Re-application of an already-identified bag is a no-op, never a duplicate upgrade.
- **Known, deliberate balance hole:** a repeatable shrine is an unlimited full restore, so it also resets the 80% caps and the bleed gate. The user was told and asked for no cost or cooldown; a per-visit soul cost is the one-line brake if that changes.

### P. Loot, Consumables and Identification
- `MonsterLootEntry` gained a `Category: LootCategory` (`Humanoid | Beast | Plant | Construct | Spirit`) in [`Bestiary.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Bestiary.fs). It is a **separate axis from `MonsterFamily`** on purpose — family drives traits and balance, category drives drops. Bandits have no template at all (built by `TierFactory.createClassLevel`) and resolve to `Humanoid` on the non-bestiary fallback path in `TowerSession.resolveEnemyDefeat`.
- Six kinds in [`Consumables.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Consumables.fs), one per category entry: `ArmorRepairKit` (Humanoid 45%), `GrindingStone` (Humanoid 25%), `Ration` (Beast 50%), `SpiritSeed` (Plant 50%), `WeaponMaterial` (Construct 35%), `MentalMaterial` (Spirit 30%). Drops are additive to souls and trophies; a kill can still drop nothing.
- Effects are the **only** way past the rest caps: repair kit → armour to Max, grinding stone → weapon to Pristine, ration → Health to Max, spirit seed → Morale to Max, materials → **+8** permanently to the class's primary offensive (or mental) stat (`Consumables.materialUpgrade`).
- `Spirit`-category species yield **1.5×** souls (`Bestiary.SoulsFor`).
- Found kinds are marked unidentified; **unidentified items cannot be used** and render as `?` in the HUD. Only a shrine reveals them (see §O). Inventory carry state is `TowerRunState.Consumables` + `UnidentifiedConsumables`.
- **Inventory panel** (`I` → `TowerDisplay.showInventoryPanel`, rendering rows from `Consumables.describe`): every carried kind with count and description, sorted **by most recent** (default) or **by name** with `S`. A concealed row shows `Unidentified` / "A shrine can reveal what this is." — the panel never touches `kind.Name` or `kind.Description` for an unrevealed kind, which is why sorting by name clusters all unknown rows under `U` rather than leaking an alphabetical ordering. `U` picks a row and hands the kind back to the game loop, which stays the only place that mutates session state. `Consumables.add` now moves a kind to the front of the bag, so bag order *is* recency.

### Q. Story Density, Ambushes and Log Visibility
- Ambush waypoints in `WorldEvents.generateFloorEncounters` previously launched a **stale survivor** of the encounter rather than the member the player had not fought — roughly 72% / 42% / 21% of ambushes on the affected floors. Fixed; the ambusher is now drawn from the undefeated members.
- Floors 3 and 6 (`| 0 ->` branches) now also emit an ambush waypoint; optional fights went 43 → 63 and encounters per floor are 2–5 (test bound widened accordingly).
- Resolved actions print applied damage plus the vitals swing (`Display.logDamageSummary`); duel, boss, pack and quick-resolve outcomes append a chronicle entry to the map log, and `V` opens `TowerDisplay.showLogViewer`.
- Multi-enemy damage lines now name the struck combatant via `Display.formatDamageLogLine` (`Display.fs:669`) — an unattributed "42 Physical" is unreadable in a pack. Spectre reads a single-bracketed word as a style tag, so the `[[CRITICAL]]` / `[[ARMOR COMPROMISED]]` literals are doubled; keep that if you add tags here.

### R. Rendering Path and What Actually Costs Time
- Measured with a throwaway off-screen Spectre console (StringWriter, no TTY): engine `stepPlayer` **0.12–0.47 ms**, `updateFov` **0.17–0.24 ms**, whole frame render **8.2 ms**, and a per-frame `AnsiConsole.Live(...).AutoClear(false)` cycle **26.3 ms**. The map's 1485-cell markup building is only **0.2 ms** — it was never the problem.
- **Do not wrap the frame in `Live`.** It was tried, measured 17 ms dearer than writing the same frame directly, and reverted (`TowerDisplay.fs:1325`). A long-lived Live session is also unsafe: combat and dialog output is written inline inside the same loop and a Live region cannot track text written past its height.
- Split of the remaining render: HUD **4.9–7.2 ms** (the largest single item, because it built ~110-cell rows into a 58-column panel and had ~40% clipped away), viewport 2.2–3.1 ms, log 1.9–2.7 ms. Frame allocations are **3.93 MB**, which is what produces the long p95 tail.
- HUD rows were therefore trimmed to the **54-cell usable column** (58 minus border and padding): labels `%-10s`, bars 7 cells, static glyph/key legends removed from the panel and left only in the `?`/F1 overlay (`Display.showSymbolAndGlyphLegend`). Anything added to `renderHudWithDebug` must respect that budget — the worst remaining row is 42 cells, plus an 11-cell `⚠ CRITICAL` tag.
- Still on the table if the floor ever feels slow: hand-built ANSI for viewport + HUD, measured at **0.70 ms** for the same per-cell drawing that costs 2.16 ms through Spectre (and 0.125 ms for a whole styled frame). It requires splitting the public `renderHud`/`renderViewport` renderable API that tests assert on.

### S. Enemy Density and Stage-Boss Levelling
- **Roaming density climbs with depth.** `TowerGenerator.fs:703` places `Math.Min(mobOffsets.Length, 3 + floorNumber / 2 + rng.Next(0, 3))` mobs per plaza (was a flat `3 + rng.Next(0, 3)`), so shallow floors hold 4–6 and deep floors saturate all ten `mobOffsets`. Story floor 1 keeps its authored pair.
- **Story Aspects were never levelled.** They were authored at Veteran magnitude (level-40 curve — Guilt's Force 210 against the curve's 201) and arrived at **Level 1 beside level-185 mobs**. `StoryBosses.scaleToStage` (`Bosses.fs:329`, called from `TowerGenerator.fs:589`) multiplies the whole stat block by `primAt (storyGuardianLevel floor) / primAt 40`, applies a separate `toughness = 2.0` multiplier to **Health, Morale and Armor only** (a stage boss duels one protagonist, so it outlasts a same-tier mob without disturbing the authored stat ratios, stance, kit or starting meters), and writes `Progression.Level`.
- **The guard on that function is `boss.Level > 1`** — anything already levelled returns untouched. That is what keeps the tutorial Bandit Leader and the `TierFactory`-built archetypes stable. Do not "simplify" it away.
- Verified against `BalanceRegressionTests.fs`; deltas are documented there rather than here.

### T. Quest Completion Confirmation
- `TowerEvent.NpcInteracted(npc, questAccepted)` was fed `npc.Quest.IsSome` on the **ordinary** branch, so every quest-bearing NPC looked identical to a completion — and no caller read the flag at all. A finished trial therefore left one `MessageLog` line, which the NPC's own modal had just cleared off the screen. Renamed to `questCompleted`; `true` only on the completion branch (`TowerSession.fs:529`).
- `TowerDisplay.showQuestCompleteDialog` shows the card after the dialogue closes, and `showNpcDialog` switches to `TRIAL FULFILLED` / `Reward granted:` once `q.IsCompleted`, so a finished trial is not re-offered. The reward text is `quest.RewardDescription` verbatim — the card cannot drift from what was authored.
- `completeQuest` now logs the quest **title** (`Quest complete: Save Lyra — the Ascension Portal is unlocked.`) instead of the raw id.
- Guard: `StorySpatialTests.fs` asserts a still-gated quest emits no `NpcInteracted(_, true)`.
- **Known mismatch, not fixed:** the engine pays a flat **+50 Souls / +40 Morale** on every completion, while `TowerGenerator.fs:352-442` authored rewards promising 60/75/90/100 Souls, `+50 Health`, "full vitality restoration", and keys/keepsakes via `RewardKeyId` — which is never granted anywhere. That is why the card prints the authored text rather than computed deltas. See §5 item 8.

### U. Stance Growth — Kills Sharpen, Blows Harden
- `Combatant.StanceAttributes` (`Combatant.fs:332`) maps plane + stance to an `(offense, defense)` pair. Physical: `PowerStance` → Force/Fortitude, `AgilityStance` → Finesse/Reflex, `DisciplineStance` → Prowess/Poise. Mental reads the mental triad instead: Intellect/Resolve, Acuity/Intuition, Acumen/Composure.
- `Combatant.sharpenStance` (**+5** to the stance's offensive attribute) and `Combatant.fortifyStance` (**+2** to its defensive one) write into canonical `Stats`, so growth survives the stance shift that follows a strike.
- `ActionResolver.applyStanceGrowth` (`ActionResolver.fs:2392`) runs once at the `resolveEx` funnel and **infers** growth by diffing Health/Morale before vs after the turn — so ripostes, bleed ticks, opportunity attacks and cleave all count with no per-site instrumentation, and a turn where nothing lands changes nothing. Both sides grow; every resolution path (duel, boss, pack, parting blow, quick-resolve) reaches it because they all funnel through `resolveEx`.
- **Opportunity attacks scale on the stat margin, not only the trigger chance.** `aooDmg = max 10 (base + delta * 0.5)` at `ActionResolver.fs:1686`, where `base` is `defFinesse * 0.65` / `defProwess * 0.75` and `delta` is the interceptor's vector stat minus the flanker's matching guard — the same `delta` that already drove the trigger chance. Interceptor Finesse 400 against Reflex 300 goes 260 → 310; against Reflex 100, 260 → 410.
- Guards: `tests/Fornach.Domain.Tests/StanceGrowthTests.fs` (6 tests) — the attribute map on both planes, +5/+2 in isolation with neighbours untouched, a real `resolveEx` exchange crediting both sides, a no-contact turn leaving stats byte-identical, and AoO monotonicity (wide gap strictly harder, both non-zero).
- **Unbounded on purpose.** No cap was requested and none was added: twenty PowerStance kills is +100 Force stacking on the buff layer. A clamp inside `sharpenStance` is the one-line brake if a long run sags — see §5 item 9.

### V. Percentages Replace Flat Bonuses
- **The problem was scale, not generosity.** A flat `+30 Fortitude` is `+67%` on a level-1 primary of 45 and `+3%` at level 200 (primary 841 — `ProgressionScale.physicalStatsForLevel`), so every authored item silently stopped mattering as the character grew. The same held for the rare-material drip and Raise Shield's armor repair.
- **`StatBonus.percentOf` (`Attributes.fs`) is the one rule**: `max 1 (round value * pct / 100)`, rounded half away from zero, floored at 1 so a small bonus on a small value still lands. `StatBlock.ApplyPercentBonuses` applies a batch of them against each stat's *current* value — the same basis altars already used, so altars, equipment and materials now share one rule instead of three.
- **`EquipmentItem.StatModifiers` is now `StatPercentBonuses`** and holds percentages, not points. 33 authored sites were converted mechanically at `points * 100 / StatBonus.authoredBaseline` (baseline = 45, the level-1 primary), so a level-1 character feels the same as before and a level-200 one keeps the bonus relevant. Examples: `30 → 67`, `20 → 44`, `15 → 33`, `40 → 89`, `10 → 22`.
- **`Combatant.applyItemBonus` is the single equip step** and calls `ApplyPercentBonuses`. The altar's `RelicReward` branch no longer pre-resolves anything — it used to convert the relic's percentages into points and then apply them, which would now double-convert. It just hands the item to `applyItemBonus`.
- **`Consumables.materialUpgrade` is `18` (a percentage)**, the old `+8` on the level-1 scale; the shrine message prints `+18%` rather than `+18`.
- **Raise Shield's armor repair is a percentage of the pool**: `armorPct = clamp 1 100 (25 + fortitude / 4)` then `StatBonus.percentOf Armor.Max`. The old `min (Max - Current) (25 + fortitude / 4)` repaired 36 points on a level-1 armor pool of 30, i.e. a full repair; it now repairs ~36% of the pool at any tier. The CLI line was corrected from the long-stale `Fortify Armor +20`.
- **Deliberately still flat**, because these sit on 0–100 meters or are damage, not defense: `StartingRecklessnessDelta`, preparation effects (`+25 Cognitive Fatigue`, `+20 Confusion`), study stacks, the meter drains in `BeginRecovery`/`RaiseShield`, and stance growth's `+5`/`+2` (§1 U) which was specified as a per-kill drip.
- Guards: `tests/Fornach.Domain.Tests/StatBonusTests.fs` (3 tests) — same proportion at both tiers, never rounds away to nothing, and `applyItemBonus` granting more points to a stronger bearer. Updated in the same change: `StorySpatialTests` (prologue fold-in totals, now 70/45/85/65/62) and `LootConsumableTests` (materials compound, so the second use is a percentage of the first result).

### W. Working Notes for the Next Agent
- [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs) is ~1875 lines. Edit it with the `edit` tool, surgically. Do **not** rewrite it from a script: a scripted rewrite read the wrong path back and overwrote the file with `Display.fs` during this session. It was recovered from `HEAD` (`1857fd6`, which already contained the session's work) with only that turn's edits re-applied — but the recovery was luck, not process.
- Suite counts drift fast. Verify with `dotnet test tests/Fornach.Tests/Fornach.Tests.fsproj` and `dotnet test tests/Fornach.Domain.Tests/Fornach.Domain.Tests.fsproj` rather than trusting the number in this header.
- `git status` is the source of truth for what is committed: `HEAD` is ahead of several handoff sections, so check `git log --oneline -3` before assuming a described change is uncommitted.
- **At the time of this handoff, §1 U was uncommitted**: `git status` showed `src/Fornach.Domain/Combatant.fs`, `src/Fornach.Engine/ActionResolver.fs` and `tests/Fornach.Domain.Tests/Fornach.Domain.Tests.fsproj` modified, plus the new `tests/Fornach.Domain.Tests/StanceGrowthTests.fs`. Everything else described above is in `cecf51a` / `5308c71`.
- §1 S/T were committed without a balance pass of their own; they are structural (`Math.Min` bound, `Level > 1` guard) and guarded by tests, but the *magnitudes* (floor `+ floorNumber / 2`, boss `toughness = 2.0`) were chosen by reasoning, not measurement.
- **At the time of this handoff, §1 T (quest payouts), §1 V (percentages) and the whole working tree were uncommitted.** `git status` also shows staged-but-uncommitted stance-growth work (`Combatant.fs`, `ActionResolver.fs`, `StanceGrowthTests.fs`). Nothing has been committed since `cecf51a` / `5308c71`.
- **§1 V has had no balance pass.** The suite is green, but the balance guards that would notice are the ones that stayed *inside* their tolerances — no Monte-Carlo or matrix sweep was re-baselined, and the change makes equipment strictly stronger at high tier by design (`+30 Fortitude` becomes `+67%`, which is `+563` at level 200). Run `--balance-matrix` before trusting the tipping-point tables in §2.
- **`EquipmentItem.HealthBonus` / `MoraleBonus` are dead fields.** They are declared on every item (25 sites in `Bosses.fs` alone, values 100–600) and **read nowhere** — neither `applyItemBonus` nor anything else consumes them, so a boss's `HealthBonus = 500` grants nothing. Left in place rather than wired up (wiring would hand every boss +500 health mid-run) and rather than deleted (40-site churn nobody asked for). Delete or convert to percentages before anyone trusts them.

### X. Study Stacks — Gambits Burn the Whole Pile
> **Superseded by §1 Y.** Study Stacks no longer exist. Everything below documents the removed economy; the finisher thresholds it measures (`flawSpendBase`/`insightSpendBase` ladders, one-shot at 4–6 stacks) are historical and must not be re-tuned — the meters in §1 Y replace all of it.
- **One scale, in `ActionResolver.strikeMultiplier` (`ActionResolver.fs:14`)**, replacing the per-attack magic numbers (0.05, 0.15, 0.25, and the spend bases 1.2/0.44, 1.1/0.30): holding a read is worth `1.0 + 0.05 × stacks` on every attack, `1.0 + 0.10 × stacks` on the Discipline school's own vectors (`ProwessStrike`, `ShieldBash`) — twice the flat rate, so the study school earns most from what it studied.
- **Spending pays far more than holding**: `2.2 + 0.7 × stacksSpent`, only from the second stack on (`spendBase = 2.2`, `spendPerStack = 0.7`). Ten stacks is ×9.2 on a spender against ×1.5 for merely holding them.
- **The three spenders now burn every stack held**, and the payoff follows the pile: `ForceInsightStrike`, `FinesseInsightStrike`, `CalculatedFlawStrike`. Their `stacksToSpend: int` payload was removed from `AttackClassification` (`Actions.fs:32-36`) — it had become a lie once the count stopped mattering; `Program.parseActionChoice`, `AI.fs`, `Display.fs` and `ClassBalanceTests` were updated with it. `MasterfulDisarm` keeps its payload: that one is a *requirement* (target Poise vs. Prowess), not a spend.
- **Calculated Flaw no longer refunds half.** It used to retain `spent / 2` in Discipline Stance for a Fighter (same-school master): burning the whole pile is the point of a spender, and the `DisciplineGambitExecuted` event still reports what was actually spent.
- **`AttackEstimator` mirrors the spend** (`AttackEstimator.fs:66`) — spenders estimate with `actor.StudyStacks`, disarm with its own requirement — so the menu's percentage cannot drift from what resolution pays. Both call the same `strikeMultiplier`.
- Unchanged, applied *after* the study rate: Wild Blow 1.35, Invitational Bait 1.25 (×1.25 Discipline Stance), Heavy Slam 1.30 (×1.20 Discipline Stance), Imperious Demand 1.3, Mind Fracture 1.35, Anomalous Glyph 1.25, Staggering Pulse 1.35, Staff Siphon ×1.25 Discipline Stance. The old ×3 rarity was never in the resolver.
- Guards: `tests/Fornach.Domain.Tests/ProlongedBattleTests.fs` — the 0.10 free-read rate fitted from damage (10 stacks ×2.0), the 0.7 spend rate fitted from the spend curve, all three spenders burning the whole pile, and the zero-Recklessness cost. `damageTaken` now hits a `punchingBag ()` (`Pool.Create 400000`) so a 50-Force target stops saturating its health pool and hiding the multiplier.
- **Balance pass done — see §2.** `--balance-matrix` before/after (baseline = the `cecf51a` worktree, i.e. the whole uncommitted §1 U/V/X set): Fighter Veteran 12/13/14/11 → 14/14/15/11, Fighter Master 41/24/33/19 → 48/30/37/29, Mage Veteran 11/9/10/11 → 12/9/10/11, Mage Master 20/18/17/23 → 19/18/17/23. Novice and GrandMaster rows unmoved; no category flipped. The martial climb is the 10% read alone, i.e. the intended direction at a magnitude that does not reorder the tiers. The spend magnitude is **invisible to the sweep**: re-running it after reverting `2.2 + 0.7n` back to `1.2 + 0.44n` reproduces every row above unchanged, because the swarm AI rarely banks the two stacks a spender needs before it dies. That is why the finisher had to be measured by hand (`--filter StudyStackSpend`), and damage turns out to be exactly linear in the multiplier, capped by the pool — so the whole question is where the ladder crosses `pool / baseHit`. At a top roll against a same-tier Fighter champion that crossing is: one stack `0.27–0.35×` pool, two `0.56–0.74×`, and a one-shot at **six** stacks Veteran, **five** Master, **four** GrandMaster under `1.2 + 0.44n`. Under the shipped `2.2 + 0.7n` the Master row one-shot at **two** — two rounds of study for a guaranteed kill on an equal-tier opponent, which is precisely the duel-pacing break §1 G was written to prevent and which no sweep could have caught. Reverted: martial spender back to `1.2 + 0.44n`, insight pair to its pre-existing `1.1 + 0.30n`, now as separate constants (`flawSpend*` / `insightSpend*`) so tuning one school cannot move the other. `StudyStackSpendTests` pins the ladder *and* recomputes the threshold from live damage, so a future magnitude change fails the suite instead of quietly moving the finisher earlier.

### Y. Combat Ramp Meters — Flow / Momentum / Focus (replaces Study Stacks)
- **Three meters, one shape.** `Combatant` lost `StudyStacks: int` and gained `Flow`, `Momentum`, `Focus` (`Combatant.fs:60-64`), each `int`, each bounded `0..Combatant.StackCap` where `StackCap = 5`. `Combatant.addStacks delta` raises all three; `Combatant.decayStacks` is `addStacks -1`; `Combatant.stacksIn vector` / `Combatant.drainStacks vector count` read and spend the one meter a special is entitled to (Agility→Flow, Power→Momentum, Discipline→Focus).
- **Every attack feeds all three.** The single `|> Combatant.addStacks rampGain` sites in `ActionResolver.fs` cover martial strikes, gambits, enemy attacks and equipment procs (`GainStacks`), so a fight of any length ramps both sides — the meters reward duration, not a build.
- **Passive per-stack bonus, applied after the stance adjustment** in `Combatant.GetCombatStat`: Finesse/Reflex ×`(1 + 0.10·Flow)`, Force/Fortitude ×`(1 + 0.01·Momentum)`, Prowess/Poise ×`(1 + 0.01·Focus)`, rounded and floored at 1. Flow is deliberately ten times the other two: it is the Agility school's payoff and the fastest-felt ramp.
- **Cashing out is per-vector and total.** A special cashes only its own meter, empties it, and multiplies damage by `1 + 0.5 × stacks` — five stacks is **3.5×**. That replaces the whole per-school ladder (`flawSpendBase`/`flawSpendPerStack`, `insightSpendBase`/`insightSpendPerStack`) and the per-tier finisher thresholds measured in §1 X.
- **Decay** is `Combatant.decayStacks`: one stack off all three at the end of any turn with no live engaged enemy (exploration, rest, the turn a fight ends). Nothing decays inside a fight, which is what makes the meters a within-fight ramp rather than a bankable resource.
- **Focus also sharpens reading.** `TacticalAssessment.calculateEffectiveScrutiny` counts `observer.Focus * 15` on top of Acumen and Intuition — the old study→scrutiny read, now riding the Discipline meter (the `+15` per stack kept its original magnitude).
- **Post-encounter growth replaces the study economy.** `Combatant.growFromEncounter (foes) (c)`, weighted by the strongest foe's `Progression.Level` — `+1` under 100, `+2` at 100+, `+3` at 200+. The weighting rides the *primary* pair and the secondary pair is a flat `+1`, per class:
  - **Fighter** — `Prowess`/`Poise` at the weighted gain, plus `Resolve`/`Intuition`/`Composure` (the three mental defences).
  - **Mage** — `Acumen`/`Composure` at the weighted gain, plus `Resolve`/`Intuition`. **No physical stat and no `Prowess` at any tier**; the mental defences are what scale spell armour (see the `ProgressionScale` spell-armour pass in `TierFactory.createClassLevel`), so a caster grows defence through the mind.
  - **every other class** — `Prowess`/`Poise` at the weighted gain, plus `Force`/`Finesse`.

  Applied to `Stats` with `StatBlock.ApplyModifiers`. Wired at the two victory sites that carry a persistent player: the boss duel (`Program.fs`, which already returns `outcome, currentCombatant`) and the group duel (grows `currentPlayer`). The arena sparring loop `runInteractiveDuel` is deliberately *not* wired — its combatants are throwaway locals.
- **Shield Bash is one action, stance-keyed, and gated on a wind-up.** The player menu offers a single `Shield Bash` entry, and only when `isInterruptibleWindup` is true (`ForceStrike true | FinesseCadence true | ProwessStrike true | ArcaneCataclysm true | TraumaAttack _` — the same predicate that draws the interrupt badge). The menu sends `ShieldBash true` (committed slam), so a menu-reachable bash always registers as an interrupt.
- **A bash is stance-neutral by design**: `ShieldBash _` is deliberately absent from the fluid-transition table (`applyFluidTransition`), so the action never re-stances the attacker, the contest uses the stance's own offense attribute (`fst currentActor.StanceAttributes` — Force/Finesse/Prowess), and the damage multiplier is fixed at `1.20` (`1.56` for the heavy slam) regardless of stance. Contesting on Prowess in every stance made Power and Agility bashes miss same-tier targets outright — which is how the first cut failed its own tests.
- **What lands depends on the stance** (`ActionResolver.fs`): **Power** floors the target — `addLimbDebuff` 6 for a heavy slam, 4 otherwise, plus `CombatEvent.LimbDisabled` (Reflex penalty until recovery); **Agility** parries — the wind-up is defused and the *parrier* gains `ComboTracker.RegisterHit()` (vital opening), with no debuff on the target; **Discipline** breaks the weapon — `Combatant.degradeWeapon` plus `CombatEvent.WeaponDegraded`. All three keep the combo reset, the Frustration tick, and the `ActionInterrupted` event (whose note is stance-flavoured). This removed the second player Shield Bash entry (`Heavy Slam`), which is the first cut toward a shorter combat menu.
- **Deleted dead surface**: `CombatEvent.StudyStackGenerated` (nothing emitted it once the funders moved to `addStacks`) and its display arm; `HeraldicTreatiseStudied` renamed `HeraldicTreatiseRead`, whose payload is now a ramp gain. Narrative strings across `Display.fs`, `Program.fs`, `Simulation.fs`, `Preparations.fs`, `TacticalAssessment.fs`, `Actions.fs` and `CombatLogger.fs` say “Combat Ramp” instead of “Study Stacks”.
- **Open risk, not yet measured: 3.5× sits above the §1 G one-shot threshold.** §1 X measured the equal-tier one-shot crossing at a **3.14** multiplier following the revert. A full five-stack Flow cash-out is 3.5×, so a capped pile can one-shot a same-tier opponent again — the exact duel-pacing break §1 G exists to prevent. No `--balance-matrix` run or hand measurement has been done against the new ladder yet; if the sweep confirms it, the fix is a cap or diminishing returns on the spend multiplier, not a change to the passive per-stack rates.
- Guards: `tests/Fornach.Domain.Tests/StudyStackSpendTests.fs` (file name kept, contents rewritten) pins cap 5, all-three feeding, single-vector draining and the `1 + 0.5n` cash-out; `ProlongedBattleTests.fs` carries the meter passives and the decay rules; `EncounterGrowthTests.fs` covers `growFromEncounter` (Novice `+1`, Master `+2`, GrandMaster `+3`, strongest-of-pack wins, Fighter vs other-class off-type ticks); `CombatTacticsTests.fs` pins the three stance-keyed bash behaviours differentially (Power limb penalty vs Agility vital opening vs Discipline weapon notch) and asserts a bash leaves the attacker's stance alone.

---

## 2. Archetype Tipping-Point Matrix (Novice mobs vs. Fighter & Mage)

Results from `dotnet run --project src/Fornach.Cli -- --balance-matrix` (re-run after §1 X, magnitudes unchanged by it beyond the cells below): `CharacterClass.PlayerClasses` × 4 tiers swept against the 4 Novice base classes (8 champion rows, 32 matchups, 100 iterations per cell, cap 100 mobs, one seeded roller per cell — `seed = 1000 + classIndex * 400 + tierIndex * 100 + mobIndex * 10`, so a cell is reproducible on its own). Each cell is the largest number of simultaneous Novice mobs of that base class the champion survives; `100+` is the sweep cap, reported as `Impenetrable`.

| Champion Archetype | Tier | vs. Warrior (Power) | vs. Rogue (Finesse) | vs. Soldier (Discipline) | vs. Mage (Arcane) | Tactical Dynamics & Observations |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Fighter** | Novice | 2 | 2 | 1 | 1 | Bastion frontage and stance-shifted parries resist early encirclement; compounding flank penalties still erode it in prolonged swarms |
| | Veteran | 14 | 14 | 15 | 11 | Same profile, wider frontage |
| | Master | 48 | 30 | 37 | 29 | Same profile |
| | GrandMaster | **100+** | **100+** | **100+** | **100+** | Fluid stances keep every vector available; `BastionZoneControl` holds the frontline to 3 while Prowess disparity drives massed AoO counters |
| **Mage** | Novice | 2 | 2 | 2 | 2 | Complex Forms disrupt and shed attackers; an unspecialised form is still punished by physical swarms |
| | Veteran | 12 | 9 | 10 | 11 | Same profile |
| | Master | 19 | 18 | 17 | 23 | Same profile |
| | GrandMaster | **100+** | **100+** | **100+** | **100+** | Threaded Complex Forms: Resonance Spikes burn, Phantasmal Diffusion sheds decoys, Aegis Lattices ward and reflect the swarm |

- **A spender reading 0 damage is counterplay, not a bug.** Measured at Master with a 12-stack `CalculatedFlawStrike` against same-tier mobs: a **Soldier** mob whiffs it outright (`Contest.NetHits = -10`, `IsWhiff`, attacker 551 against a 662 Discipline guard — the class answer to a Prowess attacker), and a **Mage** mob sheds it into a decoy (`contest = None` with `MirrorCloneDecoyed` / `PhantasmalSwapExecuted` / `MirrorCloneShattered` in the event log). Only the Warrior and Rogue mobs take the hit. Anyone reading a flat `dealt = 0` off a probe should look at `ActionResult.Events` and `ActionResult.Contest` before hunting a damage bug.
- **The `26 → 1` Arcane collapse in §5 item 11 does not reproduce, and the harness is why.** Both columns above were measured on the current `--balance-matrix` (per-cell seeded roller, 100 iterations); the old `26` (Mage GM vs. Arcane) and its `1` successor were taken with the retired shared `Random(42)` / 10-iteration sweep, where each cell inherited however many dice earlier cells had burned and one unlucky low-count death flips the search. Re-measured: GM vs. Arcane is `100+` (sweep cap) **both** at `cecf51a` and with the uncommitted §1 U/V/X set, and Veteran/Master sit at 11–29. Item 11 is closed as measurement noise from the old sweep, not a regression — and the old §2 values (`5 / 4 / 4`, `9 / 20 / 26`) are superseded by this table.
- **§1 X moved the martial rows up and left the rest alone.** Fighter Veteran 12/13/14/**11** → 14/14/15/**11**, Fighter Master 41/24/33/**19** → 48/30/37/**29**, Mage Veteran 11/**9**/10/11 → 12/**9**/10/11, Mage Master 20/18/17/**23** → 19/18/17/**23**. No floor moved (Novice 2/2/1/1 and 2/2/2/2 unchanged), no cap unseated, no column reordered. The climb is the Discipline school reading Study Stacks at 10% instead of 5% plus the spenders paying `2.2 + 0.7n` — the direction the change was meant to push, at a magnitude that does not reorder the tiers. Arcane moved only where it was not already capped (+10 for Fighter Master) — Novice Mage mobs are still the weakest swarm in the sweep at Novice/Veteran, so the matrix does not confirm DreadWarhorn- or ward-style pressure at mob scale.

**Re-measured after §1 U + §1 V (uncommitted tree), same seed and iteration count.** Physical columns held or improved — Fighter Master `43 → 48` vs Warrior, `36 → 37` vs Soldier — but the Arcane column collapsed for both archetypes: Fighter `4 → 1` at GrandMaster, Master `4 → 2`, Mage `26 → 1` at GrandMaster, `20 → 2` at Master, `9 → 1` at Veteran; Fighter Veteran vs Rogue/Soldier fell `17/17 → 14/15`. Two changes in that tree are candidates and neither is separated out here: Raise Shield's armor repair went from a full repair at Novice to ~36% of the pool (§1 V), and stance growth is uncapped (§1 U), so a long swarm run feeds the champion's defence without limit while mental attackers ignore it. Do not treat either row set as authoritative until the two are measured apart — see §5 item 11.

---

## 3. Key Files & Code References

- **Where decision records live — two directories, numbered independently, do not "fix" this without reading first.** [`docs/adr/`](file:///home/omary/Dev/fornach/docs/adr) holds the curated, hand-authored ADRs `0001`–`0005`. [`openspec/decisions/`](file:///home/omary/Dev/fornach/openspec/decisions) holds records emitted by `openlore decisions record` and its commit gate; those start again at `adr-0001`, so the two sets collide on the number and not on the content (`docs/adr/0001` is the spatial-engine decoupling, `openspec/decisions/adr-0001` is the study-stack pile). The generated set is what the pre-commit gate reads and syncs, so treat it as canonical for *gating* and `docs/adr/` as canonical for *reading*. Renumbering either side breaks the tool — the open decision is whether to write hand-authored ADRs through `record_decision` too, not to move files.

- **Core Mechanics & Combat Engine**:
  - [`src/Fornach.Engine/ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs): `applyDamage`, `resolveContestEx`, `resolveGroupTurn`, cleave scaling, ground ward tripping, retribution reflection, and AoO checks.
- **Domain Models & Events**:
  - [`src/Fornach.Domain/CombatEvent.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/CombatEvent.fs): Added `EnrageDamageShrugged`, `FrenzyStrikeExecuted`, `RetributionReflected`, `DestabilizingWardTripped`.
  - [`src/Fornach.Domain/Preparations.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Preparations.fs): Added `AegisOfRetribution`, `PrismaticFlare`.
  - [`src/Fornach.Domain/Classes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Classes.fs): Class definitions, plane mappings, archetype vectors.
  - [`src/Fornach.Domain/TierFactory.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs): Stat scaling across Novice, Veteran, Master, GrandMaster tiers.
- **Simulation & CLI**:
  - [`src/Fornach.Cli/Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs): `runSingleGroupMatch`, Enraged Frenzy attacks, tile blocking on clone shatter, collapse elimination, and balance matrix search.
  - [`src/Fornach.Cli/AI.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/AI.fs): `chooseIntentWithContext`, swarm preparation deployment — a Fighter facing 2+ opponents drinks `BerserkTincture` first (while above 30 HP and below 75 Recklessness) to shrug damage and unlock Frenzy, then falls back to crowd-control preparations and `AegisOfRetribution`.
  - [`src/Fornach.Cli/Archetypes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Archetypes.fs): the `allArchetypes` registry — Fighter and Mage at the Veteran/Master/GrandMaster tiers, plus the Novice Fighter, Mage, Warrior, Rogue and Soldier rows.
  - [`src/Fornach.Cli/Display.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Display.fs): ANSI formatting for all new combat events.
- **Tests**:
  - [`tests/Fornach.Domain.Tests/PreparationTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/PreparationTests.fs): Unit tests for `AegisOfRetribution`, `BerserkTincture`, and cleave scaling.
  - [`tests/Fornach.Domain.Tests/OverallBalanceTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/OverallBalanceTests.fs): Macro balance suite verifying tier scaling and plane vulnerabilities.
  - [`tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs): Swarm Monte-Carlo tests.
  - [`tests/Fornach.Domain.Tests/StanceGrowthTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/StanceGrowthTests.fs): Stance attribute map, `sharpenStance` / `fortifyStance`, growth through a real `resolveEx` exchange, and opportunity-attack damage monotonicity (§1 U).

---

## 4. Performance Findings & Optimisation Opportunities

**Method:** Release binaries, purpose-built `#r` harness against the built DLLs, tiered compilation disabled for micro-benchmarks, median-of-5. No profiler was available on this machine (`dotnet-counters`/`-trace`/`-gcdump` all absent).

**Currency:** §4.1–§4.5 were measured *before* the `StatBlock` rework of [ADR 0002](file:///home/omary/Dev/fornach/docs/adr/0002-array-backed-statblock-and-static-baseline.md) landed. §4.2's `StatBlock` / `Baseline` / `createGrandMasterBerserker` rows and the §4.4 ranking are superseded by **§4.6**, which is the current profile.

**Scope:** this section is about the *simulation* harness (duels, batches, Monte-Carlo). It says nothing about the TUI render path — engine-step, FOV, frame, HUD and the vett…

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

**Class-reduction note:** the `createGrandMasterBerserker` row above was measured before the class reduction; that constructor no longer exists. The figure is historical (§4.6 explains it as the union `ToString()` cost, not level scaling).

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

**Dominant finding:** [`TierFactory.fs:227`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs#L227) built the archetype name with `sprintf "%s %s" (tier.ToString()) cls.Name`. The compiler-generated union `ToString()` goes through reflective structured formatting: **40,093 ns / 15,026 B per call**, against **3.4 ns / 0 B** for the hand-written `CharacterClass.Name` ([`Classes.fs:52`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Classes.fs#L52)) and 7.9 ns for the `int.ToString()` control. Every union in `Fornach.Domain` pays this printer (`CharacterClass` 63 µs, `CollapseReason` 46 µs, `WeaponCondition` 42 µs, `CombatMode` 41 µs, `Plane` 36 µs).

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
| 1 | `member CombatTier.Name` on the union, used at `TierFactory.fs:227` (ADR 0005) | ~4.3× on the matrix sweep; −3.3 GB | Proposed, validated; 8-line patch |
| 2 | Stop rebuilding the mob list per swing — `List.mapi` at [`Simulation.fs:446`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs#L446) rebuilds all n elements on *every* swing (O(n²) list rebuilds per round), and `:444` rebuilds again on elimination | 86% of post-fix runtime: 1,808 ms / 3.55 GB, ~24 µs and ~47 KB per round | **Not measured** — mechanism only |
| 3 | Sim-local `CombatantId` counter (was §4.4 #3) | ~3% of runtime now that setup is cheap | Still valid |
| 4 | Parallelise the matrix sweep (was §4.4 #4) | less attractive at a 2.3 s total | Still valid, subject to the §4.1 Spectre constraint |

**Recommendation #2 in detail:** make `mob` a match-local array, replace the per-swing rebuild with an index write (`mobArr.[mobIdx] <- newAttacker`), and compact once per round rather than once per swing. Keep the compaction **order-preserving** — `AI.chooseGroupTarget` and enchainment order depend on list order, so a swap-remove would move balance numbers even though the outcome distribution should not change.

**Now not worth doing:** anything on the creation path (`StatBlock.Create`, template/`statList` caching, the record copies inside `createClassTier`) has a 306 ms ceiling (14%). `StatBlock.Get` measures 9.2 ns and `StatBlock.Baseline` 4.5 ns / 0 B, confirming §4.4 #1 and #2 landed as intended (realised lookup cost is ~9–12 ns rather than the ~2 ns projected).

---

## 5. Next Session TODO List

- [x] **1. Fighter & Mage Archetype Evaluation**:
  - Both player archetypes are benchmarked across every tier in [`MonteCarloSwarmTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/MonteCarloSwarmTests.fs), [`OverallBalanceTests.fs`](file:///home/omary/Dev/fornach/tests/Fornach.Domain.Tests/OverallBalanceTests.fs), and `--balance-matrix` (8 champion rows × 4 base classes). The per-class archetypes they replaced are folded into the two classes instead — see §1 D.
- [ ] **2. Infinite Tower Boss Scaling**:
  - Verify Grief Aspect bosses in [`docs/STORY_AND_TOWER.md`](file:///home/omary/Dev/fornach/docs/STORY_AND_TOWER.md) against the revised preparations and meter interactions.
- [ ] **3. Spatial Grid Integration**:
  - FOV is **wired** — [`Vision.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Vision.fs) computes a dilated arc and `TowerSession.updateFov` runs it per move (§1 L). Directional flanking and grid pathfinding are still **not** connected to turn resolution.
- [ ] **4. Quick-Play Mode** — the remaining half of TODO §7; see §7 below.
- [ ] **5. Shrine balance brake**: repeatable shrines are an unlimited full restore (§1 O). If the caps are meant to bite, add a per-visit soul cost that scales with floor.
- [ ] **6. Shrine-gated identification risk**: unidentified consumables cannot be used until a shrine, so a long floor can strand the player's loot. Measure shrines-per-floor on floors 5+ before deciding whether that is a reward or a trap (§1 P).
- [ ] **7. Bestiary unlock on repeated kills** was raised by the user but never scoped or implemented — do not assume it exists.
- [ ] **8. Quest reward payout — implemented, awaiting a play-check.** `TowerQuest` now carries a `Reward: QuestReward` record (`Souls`, `Health`, `Morale`, `FullVitality`, `KeyId`), declared through `questReward souls morale keyId` (`TowerGenerator.fs`) with the two odd ones written out in full. `TowerSession.completeQuest` grants it and the per-visit hardcoded `+50 Souls / +40 Morale` is gone. Suite green; nobody has read the resulting numbers in play.
- [ ] **9. Stance growth is uncapped** (§1 U): +5 per kill, +2 per hit taken, with no ceiling, stacking on the buff layer. Play a long run before deciding whether it needs diminishing returns or a per-run cap.
- [ ] **10. `HealthBonus` / `MoraleBonus` on `EquipmentItem` are never read** (§1 W). Either delete them or convert them to percentages of the pool maximum and apply them in `applyItemBonus` — but know that wiring them up hands every boss in `Bosses.fs` several hundred extra health.
- [x] **11. The Arcane matchup in §2 does not reproduce — closed as harness noise.** An earlier note here claimed Mage-vs-Mage fell `26 → 1` at GrandMaster and `9 → 1` at Veteran on the §1 U + §1 V tree. Re-running the *current* `--balance-matrix` (per-cell seeded roller, 100 iterations) gives `100+` (sweep cap) for GM-vs-Arcane at both `cecf51a` and with §1 X applied, and 11–23 at Veteran/Master. The `26 → 1` figure came from the retired shared `Random(42)` / 10-iteration sweep, where each cell inherited the dice the earlier cells had burned and a single unlucky low-count death flips the search. Suspects named then (Raise Shield armor repair, uncapped stance growth) were never implicated by a run that could separate them. §2 is re-baselined; re-open only if a *fixed* harness reproduces it.
- [x] **12. The two-stack finisher (§1 X) — closed by reverting the spend magnitude.** Measured, not inferred: a Master Fighter's top-roll `CalculatedFlawStrike` deleted a same-tier Fighter champion's whole pool from **two** stacks under `2.2 + 0.7n`; `1.2 + 0.44n` needs **five** at Master, **four** at GrandMaster and **six** at Veteran (one stack is `0.27–0.35×` pool, two `0.56–0.74×`). Reverted in `src/Fornach.Engine/ActionResolver.fs` (`flawSpendBase 1.2` / `flawSpendPerStack 0.44`; insight pair kept separate at `1.1` / `0.30`). `--balance-matrix` returned every row unchanged across the revert — the sweep cannot see spender magnitude at all — so the finisher must be checked with `--filter StudyStackSpend`, which pins where the ladder turns lethal at every tier: **four** stacks GrandMaster, **five** Master, **six** Veteran, and two stacks explicitly not a finisher. GrandMaster turning lethal earliest is the wrong direction — §1 G was written because GM duels had become rocket tag — so if this is ever flattened, the dial is a per-tier rate, not one flat constant; fitting a single constant to three measured points is guesswork without a duel sim to check it against.

- [ ] **13. The ramp cash-out is above the one-shot threshold (§1 Y) — unmeasured.** §1 X measured the equal-tier crossing at a **3.14** multiplier. A full five-stack cash-out under §1 Y is `1 + 0.5 × 5 = 3.5×`, so a capped meter can one-shot a same-tier opponent — the §1 G failure mode, reintroduced by a different route. No sweep or hand measurement has been run against the new ladder. Measure with `--filter` on the meter tests the way §1 X did (the swarm sweep is blind to spender magnitude); if it confirms, taper the cash-out (cap it at, say, 3.0, or make the third-to-fifth stack pay half) and leave the passive per-stack rates alone.
- [ ] **14. §2's matrix numbers predate §1 Y and must be re-baselined.** Every row in §2 was taken with Study Stacks live. The three meters change both sides of a matchup (attackers ramp, defenders sharpen Finesse/Reflex/Prowess/Poise as they ramp) and add permanent growth every won encounter, which the sweep does not model at all — a `--balance-matrix` run now means something slightly different from the one that produced that table.

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

Recommended for the work described in §1 K–W:
- **`fsharp-testing`**: §1 U is the freshest example of the house shape — a pure `Combatant` transform tested in isolation, then the same behaviour re-asserted through a real `resolveEx` exchange so the wiring cannot silently rot. Balance magnitudes (§1 S) belong in `BalanceRegressionTests.fs`, not in a new suite.
- **`roguelike`**: FOV dilation, safe-step traversal economy, shrine/altar placement and the loot loop are roguelike systems; read it before touching [`Vision.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Vision.fs), `TowerSession` recovery helpers, or floor generation.
- **`diagnosing-bugs`**: The measure-first loop behind §1 R (and the reverted `Live` experiment) is exactly its structure. Measure with an off-screen `StringWriter` console before changing the render path.
- **`fsharp-testing`**: Every change here is guarded by xUnit tests in `tests/Fornach.Tests`; the balance-sensitive ones (`AttackEstimatorTests`, `RepairTests`, `BleedRecoveryTests`, `LootConsumableTests`, `EngageScalingTests`) show the expected shape.
- **`domain-modeling`**: `LootCategory` being a deliberate second axis beside `MonsterFamily` (§1 P) is a domain decision worth an ADR if it grows; the existing ADRs live in [`docs/adr/`](file:///home/omary/Dev/fornach/docs/adr).
- **`ponytail`**: This session ran in it. The reverted `Live` wrapper is its lesson in one line: the smallest change that works would have been no change at all.
- **`unslop`**: Still applies to everything written here.

**No skill covers F#/.NET TUI rendering or Spectre.Console performance.** Use the measured numbers in §1 R instead of guessing.

---

## 7. Quick-Play Mode — the Forward Plan

Origin: the user's request that the tactical encounter menu be skippable by automating attack selection and each attack/defence round when a player/enemy collision occurs on the map. Standing still should simulate **Deep Breath / Steady Stance**, advancing into an enemy should simulate an **attack**, and retreating should simulate a **withdrawal**. The detailed checklist lives in [`TODO.md`](file:///home/omary/Dev/fornach/TODO.md) §7 item 7 — do not duplicate it here.

What already exists and should be built on:
- **Attack selection is solved** — [`AttackEstimator.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/AttackEstimator.fs) already ranks options and the menu already consumes that ranking (§1 K). Auto-resolution only needs to pick the top-ranked entry rather than a player.
- **The round machinery already exists** — `resolveDuel` / `resolveGroupFight` drive attack, defence and damage without the TUI, and already log applied damage plus the vitals swing (§1 Q).
- **Recovery vocabulary exists** — `Steady Form` is the universal reset (all seven strain meters, Morale, Study Stacks); bleeding now decays with movement and waiting (§1 M). "Wait" on the map already maps to meaningful sim behaviour, so the movement semantics above are a mapping problem, not new mechanics.
- **What is missing**: driving map collisions into a full auto round without the tactical menu, and the three movement→action mappings. Both are open in TODO §7.

Design constraints worth keeping:
- Auto-resolution must emit the same `CombatEvent`s so the Chronicle Log and damage summaries keep working unchanged.
- An `Obscured` enemy still has no stats to rank; quick-play must degrade to archetype order (§1 K), not fail.
- Anything auto-resolved remains behind the ambush-correctness fix in §1 Q: never resolve against a stale survivor.
