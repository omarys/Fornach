# TODO List

**Status:** 656 tests green (373 `Fornach.Tests` + 256 `Fornach.Domain.Tests` + 27 `Fornach.Spatial.Tests`), 0 warnings.

This file tracks **only open work**. Completed items — the archetype evaluation, the six §1 iteration priorities, the combat pacing work, the bestiary subsystem, the encounter engine, the tempo system and everything through `d4dbf47` — are recorded in [`docs/agents/domain.md`](file:///home/omary/Dev/fornach/docs/agents/domain.md) §1 (what was built) and §5 (the numbered ledger). The numbers in parentheses below are that ledger's item numbers; this file must not invent a second numbering.

---

## Open Work

### Feature Work

- [ ] **Infinite Tower Boss Scaling** (§5 item 2) — scaling, tuning and gear pools are all done; what remains open is the `Combatant.Plane` flip these aspects exposed.
  - The scaling works. `scaleToStage` ([`Bosses.fs:329`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Bosses.fs)) normalises the level-40-authored block down to `ProgressionScale.storyGuardianLevel floor`, then `TowerSession.rescaleFloorEnemiesWith` lifts the boss to `player.Level + 10` (`floorScalingFor` returns `bossOffset = 10`). Measured: a level-6 player on story floor 2 meets Aspect of Guilt at **Lv.16, HP 3002, Force 113** — not the authored Lv.1/HP 2800/Force 210 block.
  - `StoryBosses.createEnemy` ([`StoryRunner.fs:92`](file:///home/omary/Dev/fornach/src/Fornach.Story/StoryRunner.fs)) returns the unscaled authored block at `Level = 1`, and `StoryEvent.CombatInitiated` has **no consumer anywhere in the CLI** — combat is driven by `TowerEvent.GroupCombatTriggered` from the rescaled floor. So that factory is test-only; `StoryBridgeTests` asserting the authored constants is correct, not a bug.
  - **Resolved in `d828607`** — the spread was never the boss pools. Per-action Exhaustion (`12 + disparity/4`, five sites in `ActionResolver.fs`) plus `ApathyDoldrums` (`+25`) broke the player's *own* meter, and Exhaustion 100 forces the next defense into a Collapse, which is an ExecutionFinisher — so any fight past a few rounds executed the player whatever the boss's stats were. Three successive boss stat cuts produced byte-identical results, which is what identified the meter rather than the block. Halving the per-action cost and `ApathyDoldrums` 25 → 8 put all six Aspects in band.
  - **Boss gear: pools fixed in `c9e2ee1`, percentages withheld deliberately.** Bosses assigned `EquippedItems` directly, so no gear bonus ever landed. Folding the *whole* kit is wrong the other way: with percentages applied, Denial, Bargaining and Depression killed the player in **one round at 0%** across 24 seeds, because an aspect's authored block is already its final stat line and +67% Force roughly doubles a tuned gap. With pools only, all six sit at 91-100% wins in 2-4 rounds. `equip` therefore folds `HealthBonus`/`MoraleBonus` and withholds `StatPercentBonuses` and `StartingRecklessnessDelta` - the latter because Anger's authored `Recklessness = 45` became 75 and silently changed which gambit the AI picks.
  - **The "2-4 round burst" was not a tuning problem: Guilt could not attack at all.** `AI.chooseOffensiveAttack`'s `GriefManifestation` profile handed it `AcumenInterrogation`, a **mental** attack, and `AttackClassification.IsAllowedFor` refuses that for a physical actor — the resolver then burned the turn on *"cannot execute magic attacks (Physical martial discipline only)"*. Measured against a 100 000 HP player: nine rounds, finished at hp 100.00% / morale 100.00%. Guilt's only win path was the player's own Exhaustion/Frustration reaching 100, which is why its potency dial was a cliff (1.62 → 75%, 1.58 → 98%) rather than a curve.
  - **The same defect class also hit Denial and the Storm Drake.** `Combatant.Plane` flips any Warrior whose mental totals exceed its physical ones to `Mental`, and it fires far more widely than intended: **13 of 21 bestiary monsters resolve mental**, and three aspects flip on margins under 90 points (Depression by 6 — 193 vs 199). Denial's press (`FinesseCadence`) and the Beast profile's attacks (`FinesseCadence`/`ForceStrike`) are all physical, so a flipped combatant lost its entire turn. Fixed by asking `IsAllowedFor` in the `GriefManifestation` and `Beast` profiles, giving Denial a mental press (`SynapticGlamour` rather than `MirrorIllusion`, which stacks Recklessness onto a target already drowning in it), and adding two sweeps to `AiRecoveryLockTests` that fail if any aspect or bestiary family is ever handed an attack its own plane refuses.
  - **Re-measured at 40 seeds (8-seed gate in brackets):** Guilt 72% / 6.5 rounds [6/8], Denial 77% / 4.7 [6/8], Anger 62% / 4.9 [6/8], Bargaining 80% / 4.7 [6/8], Depression 92% / 5.0 [8/8], Acceptance 77% / 7.7 [7/8]. Every aspect now attacks, and the band is 4.7-7.7 rounds instead of the 2-4 burst. Guilt's tuning moved from `PoolFactor 1.5 / PotencyFactor 1.7` to `2.0 / 1.36`, Denial's from `2.0 / 1.45` to `2.5 / 1.0`.
  - **Still open: the `Combatant.Plane` flip itself.** Gating it on `PlayerStyle` would return monsters and bosses to their class plane, but `Plane` also selects the resolver's damage vector, so it is a balance-wide change needing its own measurement pass. Denial's plane looks accidental — its block reads as an agility duelist (Finesse 235, Reflex 240, *"elusive, fluid misdirection"*) yet resolves mental by 42 points.
  - Non-humanoid stat zeroing ([`Combatant.fs:186-188`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs), ADR 0009) discards Guilt's authored `Prowess 160` / `Poise 185`; `GetGuard` falls back to raw Fortitude and `Frontage` stays 8. Intended, but worth knowing when reading the blocks.
  - Bosses never accrue guard stacks or ramp meters: `addStacks`/`addGuardStacks` no-op when `not c.PlayerStyle` ([`Combatant.fs:420-431`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs)) and every boss is `PlayerStyle = false`. Their Nach-based guard accrual is structurally impossible, not merely slow.

- [ ] **Quick-Play Mode — automatic encounters** (§5 item 4, forward plan in [`domain.md`](file:///home/omary/Dev/fornach/docs/agents/domain.md) §7):
  - Attack selection and combat-log visibility are done: [`AttackEstimator.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/AttackEstimator.fs) ranks options and `Display.logDamageSummary` / the Chronicle Log viewer (`TowerDisplay.showLogViewer`) report results.
  - [ ] **Auto-Resolution**: drive map collisions into a full auto round (attack pick + defense + damage application) using the top-ranked option, so an encounter resolves without the tactical menu. Must emit the same `CombatEvent`s so the Chronicle Log keeps working, and must degrade to archetype order when the enemy is `Obscured`.
  - [ ] **Movement Semantics**: standing still maps to Deep Breath / Steady Stance, advancing into an enemy maps to an attack, retreating maps to a withdrawal round.

### ADR 0004 — Main Story World Encounters (Phase 4)

Reference: [`docs/adr/0004-monster-bestiary-and-world-encounters.md`](file:///home/omary/Dev/fornach/docs/adr/0004-monster-bestiary-and-world-encounters.md). Phases 1–3 are complete; only Phase 4 is open. This is the one open item with no §5 ledger entry.

- [ ] **Main Story World Encounters & Roadside Memories (ADR 0004 Phase 4)**:
  - [ ] **Sensory Flashbacks**: scatter interactive roadside memory fragments across story chapters in [`StoryRunner.fs`](file:///home/omary/Dev/fornach/src/Fornach.Story/StoryRunner.fs), unlocking narrative clues about the protagonist's lost twin (Lyra).
  - [ ] **Psychological Anomaly Events**: atmospheric "Truck-kun" reality breaks (phantom headlights in fog, tire skid marks on cobblestones, radio static). Note the death replay already ships a full Truck-kun animation in [`DeathAnimation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/DeathAnimation.fs); these are in-world anomalies, not that sequence.
  - [ ] **Alternative Resolutions**: moral/peaceful encounter resolutions (e.g. comforting a weeping specter to Morale equilibrium without physical bloodshed).

### Balance & Economy

- [ ] **Shrine balance brake** (§5 item 5): repeatable shrines are an unlimited, free full restore — `TowerSession.shrineBlessing` restores Health, Morale, Armor and every meter on *every* visit; only the stat boon is one-time. If the caps are meant to bite, add a per-visit soul cost scaling with floor.

- [ ] **Shrine-gated identification risk** (§5 item 6): `TowerSession.useConsumable` refuses unidentified kinds until a shrine, so a long floor can strand the pack. Measure shrines-per-floor on floors 5+ before deciding whether that is a reward or a trap.

- [ ] **Quest reward payout — implemented, awaiting a play-check** (§5 item 8): `TowerQuest` carries a `Reward: QuestReward` record (`Souls`, `Health`, `Morale`, `FullVitality`, `KeyId`), declared through `questReward souls morale keyId` in [`TowerGenerator.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/TowerGenerator.fs); `TowerSession.completeQuest` grants it and the old per-visit hardcoded `+50 Souls / +40 Morale` is gone. Suite green; nobody has read the resulting numbers in play.

### Tempo System

- [x] **The balance sweeps cannot see tempo — the group harness is fixed, the 1v1 sweep already was.** `runPreparedGroupMatchWith` resolved every round solo-first regardless of initiative, discarding the `Combatant.Tempo` the resolver was already flipping and the `TempoShifted` events it was already emitting. It now resolves in Vor order (a living pack in the Vor against a solo in the Nach strikes first, and the ambiguous both-or-neither state falls back to solo-first for determinism), counts `TempoShifted` into `GroupSingleResult.TotalTempoShifts`, and reports it in the telemetry table as *Vor / Nach Tempo Shifts*. Guarded by `tests/Fornach.Tests/TempoSweepTests.fs` (3 tests; the ordering test fails if the phase order is reverted). The 1v1 duel sweep never had the bug — `Simulation.fs:138` already keys the attacker on `combA.IsInVor || not combB.IsInVor`.
  - **Still not asserted**: guard-stack accumulation. It is exercised through `priorDefenses` on every swarm swing, but nothing asserts the stack count itself, so a guard-economy regression would still slip through. That is the remaining hole in the "sweep unchanged is no evidence" rule, together with §5 item 12's spender magnitude.

- [ ] **Holding more than one Vor at once is still not implemented** (§5 item 29, ADR 0009 *Multi-Combatant Engagements*). Gated on `Prowess` against the foes' `Prowess`/`Instinct`, deliberately left out; there is no code path for it and no test asserting the single-Vor limit is intentional. The sharp edge: `Combatant.Tempo` is one global field on the player, but `Program.fs:1497-1506` deliberately lets every non-target foe regain the Vor against the player's *other* engagements, so the state "player holds the Vor **and** a foe holds the Vor" is reachable and meaningful. The menu reads only the player's field, so it renders correctly today — but anything that starts reasoning about "who holds the Vor" globally will read that state wrong. Per-engagement tempo is the model; the field is not.

- [ ] **`cedesInitiative` cost grows with pack size** (§5 item 30): six call sites across [`Program.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Program.fs), [`TowerDisplay.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/TowerDisplay.fs) and [`TowerSession.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/TowerSession.fs) — one per unseen foe inside the ambush loop and again in the end-of-round reclaim, each reading a `GetCombatStat` pair per tempo vector. Fine at today's pack sizes; hoist the player's stat pair out of the loop if a floor ever fields dozens of foes.

### Awaiting a Play-Check

- [ ] **Frontage 1 against a GrandMaster** (§5 item 24): rank pays for the ring — Poise sets frontage (one lane per 100 points, floored at 1) and Prowess buys interceptions. A single body fighting a GrandMaster is now recorded by the swarm cells as saturation rather than a tipping point. Confirm that is the intended feel and not a silent immunity.

- [ ] **Floors 1–2 under the pessimistic model** (§5 item 17): `rescaleFloorEnemies` at `min (salt % 6) (2 + floor.FloorNumber / 2)` lifted the floor-1 clear from 55% → 98%. If the opening now reads as a corridor rather than a tutorial, the dial is the leading `2` (the measured alternative `1 + floor/2` gives 100/100/85/76/77 at average depth 4.88 — softer and flatter).

---

## Recently Closed — Do Not Re-Report

- **Spatial Grid Integration** (§5 item 3) — complete. FOV was already wired; directional flanking landed in `721376a`; and grid pathfinding was **already** wired through `TowerSession.stepEnemies` calling `Pathfinding.aStar`, covered by `EnemyMovementTests`, `PursuitPaceTests`, `ChokepointTests` and `AmbushTempoTests`. The "pathfinding is not connected" claim was stale — do not re-open it.

The initiative deadlocks from the first-floor Blight Sprite ambush are fixed: a declared guard is no longer discarded when the opponent braces instead of striking (`bb811ce`), a pack that collapsed or died surrenders the uncontested Vor (`d4dbf47`), and two earlier deadlocks in the same exchange are closed by `c4850e0` and `f4ed026`. Guards: `tests/Fornach.Tests/TempoDeadlockTests.fs`, `TempoReclaimTests.fs`.
