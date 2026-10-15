---
type: concept
title: Classes, Progression, and Spell Armor
description: Specification of Fornach's class archetypes, progression tiers (1-200), Level 49 physical stat caps for magic users, and Spell Armor scaling formulas.
tags: [progression, classes, spell-armor, stat-scaling, archetypes]
verified:
  - by: openwiki/0.7.2
    at: 2026-10-15T19:23:52.290Z
sources:
  - id: openwiki-source-234d7d2c3abdcbeba22e2c52
    resource: repo://src/Fornach.Domain/Classes.fs
  - id: openwiki-source-ca0dddfb2158f0b64c9ad5be
    resource: repo://src/Fornach.Domain/TierFactory.fs
generated: { by: "antigravity", at: "2026-10-15T19:23:52.290Z" }
---

# Classes, Progression, and Spell Armor

Progression in Fornach balances martial mastery against arcane transcendence across a 200-level spectrum. The system establishes distinct archetypes, rigorous stat-growth curves, defensive caps, and a specialized Spell Armor mechanism to prevent late-game stat inflation.

## Character Classes and Archetypes

Classes (`Classes.fs`) are divided into **Specialized Player Archetypes** (equipped with progression profiles, preparatory cooldowns, and tactical abilities) and **Generic NPC Classes**:

| Class | Plane | Dominant Vector | Primary Stat | Archetype Role |
| :--- | :--- | :--- | :--- | :--- |
| **Berserker** | Physical | Power | Force | Kinetic powerhouse; high risk gambits and frenzy momentum |
| **Duelist** | Physical | Agility | Finesse | Technical fencer; rapid cadences, parry/riposte, vital strikes |
| **Warden** / *Justicar* | Physical | Discipline | Prowess | Stalwart polearm defender; Bastion zone control, flaw exploitation |
| **Inquisitor** | Mental | Power | Intellect | Dread psychic arcanist; cognitive mind-shocks, direct morale shock |
| **Mesmer** | Mental | Agility | Acuity | Phantasmist; sensory static, mirror clones, perceptual disruption |
| **Abjurer** / *Strategist*| Mental | Discipline | Acumen | Warded dialectician; abjuration lattices, interrogative pressure |
| **Warrior** (NPC) | Physical | Power | Force | Generic shock troop and frontline brute |
| **Rogue** (NPC) | Physical | Agility | Finesse | Generic agile skirmisher and backstab stalker |
| **Soldier** (NPC) | Physical | Discipline | Prowess | Generic line infantry with coordinated defenses |
| **Mage** (NPC) | Mental | Power | Intellect | Generic arcane caster across elemental disciplines |

## Progression Tiers (Levels 1 – 200)

Progression is organized into four distinct mastery tiers (`ProgressionScale.tierForLevel`):
- **Novice** (Levels `1 – 39`): Grounded baseline stats, learning vector counters and fluid stance transitions.
- **Veteran** (Levels `40 – 79`): Enhanced dice pools; unlock of Level 50 Spell Armor for magic classes.
- **Master** (Levels `80 – 159`): Multi-die floor hits; specialized tactical preparation enhancements.
- **Grandmaster** (Levels `160 – 200`): Apex tier; maximum dice pool ceilings and up to 80% spell armor mitigation.

```mermaid
graph LR
    N[Novice: Lv 1-39] --> V[Veteran: Lv 40-79]
    V --> M[Master: Lv 80-159]
    M --> GM[Grandmaster: Lv 160-200]
```

## Level 49 Physical Growth Cap for Magic Classes

To counter over-inflation of physical defense among high-level arcane practitioners, magic classes (`Inquisitor`, `Mesmer`, `Abjurer`, `Mage`) experience a hard cap on natural physical attribute development:
$$\text{physDelta} = \text{clamp}(1, 49, \text{Level}) - 1$$

After Level 49, a caster's base physical stats (`Force`, `Fortitude`, `Finesse`, `Reflex`, `Prowess`, `Poise`) cease growing naturally (`TierFactory.fs`).

## Spell Armor Scaling

To compensate for the Level 49 physical stat freeze and simulate protective arcane barriers, mental defense stats are projected into physical defenses via **Spell Armor**:
- **Resolve** transfers to **Fortitude** (Power defense).
- **Intuition** transfers to **Reflex** (Agility defense).
- **Composure** transfers to **Poise** (Discipline defense).

$$\text{FinalFortitude} = \text{BaseFortitude} + \left\lfloor\text{Resolve} \times \text{SpellArmorBonus}\right\rceil$$

### Spell Armor Progression Table

Spell Armor activates at Level 50 with a 5% transfer bonus, increasing by +5% every 10 levels, capping at 80% at Level 200 (`ProgressionScale.spellArmorBonus`):

| Level | Spell Armor Bonus |
| :--- | :--- |
| **1 – 49** | 0% (Physical stats develop normally) |
| **50** | 5% |
| **60** | 10% |
| **70** | 15% |
| **80** | 20% |
| **90** | 25% |
| **100** | 30% |
| **110** | 35% |
| **120** | 40% |
| **130** | 45% |
| **140** | 50% |
| **150** | 55% |
| **160** | 60% |
| **170** | 65% |
| **180** | 70% |
| **190** | 75% |
| **200** | 80% (Cap) |

This creates a balanced endgame dynamic: physical characters dominate raw kinetic endurance, while high-level mages weave mental poise into impervious spell wards.
