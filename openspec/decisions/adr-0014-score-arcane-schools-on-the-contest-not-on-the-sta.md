# ADR-0014: Score arcane schools on the contest, not on the stat spread; the player AI takes the hardest hit that stays under the crit cap

## Status

accepted (auto-accepted, unreviewed)

**Domains**:

## Context

The off-school proficiency ratio compared each vector's stat against the actor's own highest mental stat, which punished specialisation: the staff and both materials sharpened Intellect while stressed victories sharpened Acumen, so a developing mage lost potency in the schools they actually cast (0.56 ratio = x0.74 damage, +7 Cognitive Fatigue per cast). Adaptive play is about the contest against the foe, not the shape of your own sheet.

## Decision

Score arcane schools on the contest, not on the stat spread; the player AI takes the hardest hit that stays under the crit cap

## Consequences

GetArcaneProficiency, profMult, the per-cast strain and ArcaneStrainIncurred are gone; clones and wards no longer scale by proficiency. chooseEstimatedAttack now ranks every legal strike in plain and gambit form, drops the ones whose spike reaches the 75 crit cap, and takes the highest estimated damage — with the plain strike winning ties and outright when it already finishes the foe. AttackEstimator folds in gambitDamageMultiplier so a gamble outranks the strike it gambles.

> Recorded by openlore decisions on 2026-11-24
> Decision ID: 69de0df8
