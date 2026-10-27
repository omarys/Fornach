# Prowess, Poise and Asymmetric Tempo Redesign

## Status

Accepted. Decision draft `c5838dca` recorded with OpenLore.

## Context

Fornach's combat model was historically rooted in a symmetrical 12-stat matrix across Physical and Mental planes (Power, Agility, Discipline vectors; Offense and Defense orientations). In this model:
- `Prowess` and `Poise` served as the physical Discipline vector, with explicit actions like `ProwessStrike`, `CalculatedFlawStrike`, and `MasterfulDisarm` spending a dedicated `Focus` resource.
- Enemies carried a flat percentage modifier based on kill count (`+2%` damage dealt, `-1%` damage taken per kill up to 10 kills in `ActionResolver.masteryMultiplier`).
- Non-humanoid monsters (beasts, constructs) were instantiated with the same human-like stat blocks, diluting the thematic distinction between beasts fighting on raw predatory instinct and humanoids fighting with martial discipline.
- Status meters (Exhaustion, Overwhelm, Frustration, etc.) existed alongside high raw HP damage, often allowing burst damage to end fights before tactical strain could accumulate and trigger deep collapses.
- In round initiative, turns did not distinguish between attacking with initiative (*Vor*) versus reacting/intercepting (*Nach*).

## Decision

1. **Passive Discipline Model (Prowess & Poise)**:
   - Remove dedicated `Prowess` and `Poise` attacks from the active combat menu.
   - `Prowess` is a passive martial reading skill providing:
     - Intent reading on opponents (via a deterministic 3-tier information ladder).
     - Passive offensive bonus applied to `Force` and `Finesse` strikes.
   - `Poise` is a passive defensive stability skill providing:
     - Passive defensive bonus applied to `Fortitude` and `Reflex` defenses.
     - Resilience against `Exhaustion`, `Overwhelm`, and `Frustration`.
     - Damage-spike shock resistance against physical `Pain`.
   - The Mental plane mirrors this symmetrically: `Acumen` provides intent reading and bonuses to `Intellect` / `Acuity`, while `Composure` provides mental shock resistance to `Resolve` / `Intuition`.
   - The `Focus` resource is permanently removed from `CombatantState`. `Momentum` (Power) and `Flow` (Agility) remain (0 to 5). At 5/5 stacks + Prowess advantage, gambits (`Flaw Exploit` and `Masterful Disarm`) execute automatically as passive finishers.

2. **Asymmetric Tempo Roles (`Vor` / `Nach` / `Indes`)**:
   - Combat choices are streamlined to Power and Agility vectors dynamically bound to tempo roles:
     - **In `Vor` (Attacker)**: Choose offensive vector: `Force` (Power Strike) or `Finesse` (Agility Cadence), plus Preparations / Recovery.
     - **In `Nach` (Defender)**: The incoming attack is telegraphed based on Prowess; choose active defense vector: `Fortitude` (Power Defense) or `Reflex` (Agility Defense).
     - **In `Indes` (Mutual Clash / Bind)**: Both combatants commit to an offensive strike (`Force` vs. `Finesse`). The strike with the superior margin or speed wins the bind, deals damage, and seizes the `Vor`.
   - **Affinity Matching**:
     - `Fortitude` counters `Force` (+25% mitigation, seizes `Vor` on success); vulnerable to `Finesse`.
     - `Reflex` counters `Finesse` (+25% mitigation, seizes `Vor` on success); vulnerable to `Force`.
   - **Unattacked Stance Benefits**: When the opponent recovers or prepares, active defenses grant recovery:
     - `Fortitude`: Restores Armor durability, vents self-Recklessness, grants `Braced` buff (+bonus to next Force strike).
     - `Reflex`: Vents Overwhelm/Exhaustion, regains tempo, grants `Flow/Riposte` buff (+bonus to next Finesse strike).

3. **Determining Factor & Frustration**:
   - Prowess/Poise is the "determining factor" if and only if base stat alone would have failed, but the Prowess/Poise bonus flipped failure to success.
   - When met, emits `CombatEvent.TacticalOutmaneuver` and inflicts heavy `Frustration` on humanoid targets.

4. **Transient Pain Shock System**:
   - Single-turn HP damage $\ge 15\%$ Max HP triggers a transient Pain Shock test: $\text{Poise} \ge \text{Damage\%} \times 2$.
   - Failure inflicts **Stagger** (loss of 1 action/turn) and a meter surge (**+25 Overwhelm**, **+15 Exhaustion**).

5. **Status Meter Primacy & Strain Debuffs**:
   - Direct HP damage scaled down by -30% across the board.
   - Reaching $\ge 75$ on any strain meter applies progressive stat debuffs:
     - `Exhaustion >= 75`: -25% Force & Fortitude.
     - `Overwhelm >= 75`: -25% Finesse & Reflex.
     - `Frustration >= 75`: -50% Poise / Intent reading masked.
     - `Recklessness >= 75`: +25% damage dealt, -25% defense, all incoming hits crit.
     - `Cognitive Fatigue >= 75`: -25% Intellect & Acuity.
     - `Confusion >= 75`: Inverted affinities / misdirection.
     - `Provoke >= 75`: Forced attack on provoker, cannot defend.
   - Reaching 100 on any meter triggers immediate decisive victory / collapse.

6. **Non-Humanoid Bestiary & Instinct**:
   - Only humanoids possess `Prowess` and `Poise`.
   - Beasts have `0` Prowess/Poise, compensated by higher base `Force` and `Finesse`.
   - Beasts use `Fortitude` as fallback for Pain Shock checks, are immune to `Frustration` (suffering `Overwhelm` when outplayed), and their read DC is based on their `Finesse`.
   - **Limp Away / Fleeing Behavior**: When a lone beast has HP $< 30\%$ and Player HP $> 70\%$, it attempts to flee. The player can:
     - **Spare (Mercy)**: Beast scrambles into the shadows and despawns. Clears player Recklessness, restores Morale, and awards Bestiary encounter XP.
     - **Execute (Coup de Grâce)**: Automatic execution. Awards corpse loot/pelts, awards Bestiary encounter XP, and inflicts +15 Recklessness.

7. **Bestiary Knowledge & Discipline Progression**:
   - Replaces the legacy `masteryMultiplier` percentage bonuses.
   - Character tracks `BestiaryKnowledge: Map<string, int>`.
   - Prowess gains per species encounter: **+25** (1st), **+5** (2nd), **+3** (3rd), **+2** (4th), **+1** (5th+ uncapped), awarded upon encounter completion (including spare/flee).
   - Poise gains: **+1** per encounter where player passed a Pain Shock check or executed a successful active defense counter; **+3** on Floor Boss clears.
   - Prowess and Poise are removed from manual level-up stat point allocation.

## Consequences

- Active combat menus are uncluttered and directly reflect the Liechtenauer fencing roles: Attacker drives in *Vor*, Defender intercepts in *Nach*, and binds clash in *Indes*.
- Bestiary encounters provide meaningful, diegetic knowledge progression that directly impacts player awareness and tactical reading.
- Encounters become true psychological and martial duels where positioning, posture, and status thresholds decide victory rather than simple HP race attrition.
