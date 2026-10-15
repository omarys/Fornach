---
type: concept
title: Fluid Stances and Complex Forms
description: Detailed mechanics of Fornach's automatic stance shifting, stat modifiers, off-vector penalties, and arcane complex forms.
tags: [combat, stances, fluid-stances, complex-forms, action-resolver]
verified:
  - by: openwiki/0.7.2
    at: 2026-10-15T19:23:52.290Z
sources:
  - id: openwiki-source-4bd3b3640ea43be1c0ef914f
    resource: repo://src/Fornach.Domain/Combatant.fs
  - id: openwiki-source-874fea1c7bf4856ccdbb9e09
    resource: repo://src/Fornach.Engine/ActionResolver.fs
generated: { by: "antigravity", at: "2026-10-15T19:23:52.290Z" }
---

# Fluid Stances and Complex Forms

Combat in Fornach revolves around fluid transitions rather than rigid, turn-wasting stance toggles. Every strike, cadence, and arcane incantation embodies a tactical posture. Adopting a stance shifts offensive and defensive efficiency along the chosen vector triad.

## Martial Stances (Physical Plane)

Physical fighters shift dynamically between three martial postures based on the attack executed:

| Stance | Vector Focus | Trigger Actions | Stat Effect |
| :--- | :--- | :--- | :--- |
| **PowerStance** | `Power` (Might, Fortitude) | `ForceStrike`, `TraumaAttack BasaltEruption` | +10% Power stats, -10% Agility/Discipline offensive stats |
| **AgilityStance** | `Agility` (Agility, Reflex) | `FinesseCadence` | +10% Agility stats, -10% Power/Discipline offensive stats |
| **DisciplineStance** | `Discipline` (Prowess, Poise) | `ProwessStrike`, `CalculatedFlawStrike`, `MasterfulDisarm` | +10% Discipline stats, -10% Power/Agility offensive stats |

### Combat Stat Trade-Offs

When resolving opposed physical contests, `Combatant.GetCombatStat` applies situational vector bonuses:
- **Vector Affinity (+10%)**: All attributes matching the active stance's vector receive a 10% boost (`Math.Round(baseValue * 1.10)`).
- **Off-Vector Offensive Penalty (-10%)**: Offensive stats belonging to differing vectors in the same plane suffer a 10% reduction (`Math.Round(baseValue * 0.90)`).
- **Preserved Guard**: Defensive off-vector attributes (such as `Fortitude`, `Reflex`, and `Poise`) do not suffer penalties, ensuring defensive integrity is maintained regardless of stance.

## Arcane Complex Forms (Mental Plane)

Arcane practitioners channel their mental vectors through three interlocking psychic structures known as Complex Forms:

### 1. Resonance Spike (`Power` Vector)
- **Trigger**: `ArcaneCataclysm`
- **Characteristics**: Volatile psychic overclocking granting +25% spell damage and cognitive fatigue amplification with splash potential.
- **Cost**: Generates self-fatigue (+10) and increases Recklessness (+15) on fading drain.

### 2. Phantasmal Diffusion (`Agility` Vector)
- **Trigger**: `SynapticGlamour`, `MirrorIllusion`, `TraumaAttack DenialPhaseShift`
- **Characteristics**: Sensory static and perceptual jitter. Weaves passive mirror clones and grants a 40% decoy evasion swap when struck by melee attacks.
- **Cost**: Reduces direct strike damage by 15%.

### 3. Aegis Lattice (`Discipline` Vector)
- **Trigger**: `RunicWardTrap`, `DisorientingShockwave`, `TraumaAttack CoerciveBargain`
- **Characteristics**: Interlocking geometric abjuration web generating +15 Arcane Ward per turn. Reflects 50% damage taken and inflicts 15 Frustration on the attacker when the ward absorbs a blow.
- **Restriction**: Locks out Overchanneling, grounding `ArcaneCataclysm true` back to a standard stable cast.

## Fluid Transitions in ActionResolver

Stances and complex forms transition automatically during offensive action resolution in `ActionResolver.fs`:
1. When an offensive action is declared, `applyFluidTransition` evaluates whether the chosen attack commands a different stance or form.
2. If a new stance is indicated, the combatant adopts it seamlessly, emitting a `CombatEvent.StanceShifted` or `CombatEvent.ComplexFormThreaded` event.
3. Attack calculation immediately applies the updated stance multipliers to the resolving strike.
