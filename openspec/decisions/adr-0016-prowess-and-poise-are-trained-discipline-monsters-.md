# ADR-0016: Prowess and Poise are trained discipline: monsters have neither, and only a combatant that learns grows them

## Status

accepted (auto-accepted, unreviewed)

**Domains**:

## Context

The merged pair is discipline, not instinct. Giving it to beasts, constructs, wraiths, aberrations and grief manifestations made every monster a martial artist: the Acceptance Aspect went from a 52-round attrition fight to a 4-round walkover once its guard vanished, and a manifestation's own signature press had no stat behind it. Humanoid opposition (a Knight, a Wizard) is authored without a MonsterFamily and therefore reads the pair like the player.

## Decision

Prowess and Poise are trained discipline: monsters have neither, and only a combatant that learns grows them

## Consequences

GetStat returns 0 for Prowess/Poise on any combatant with a MonsterFamily, whatever the sheet holds (the values left there came from Acumen/Composure and are now inert). The GriefManifestation species instinct presses the Power vector instead of an interrogation it cannot throw. Only the player grows the pair, out of a stressed victory or a defeat. The Bargaining press applies its drain, heal and provocation whatever the guard does, since a manifestation has no Prowess to land it.

> Recorded by openlore decisions on 2026-11-24
> Decision ID: d5b310ab
