# ADR-0013: Mana Integrity is the mental-plane vocabulary of one wear ladder, not a second resource

## Status

accepted (auto-accepted, unreviewed)

**Domains**:

## Context

A mage's implement condition was exempted from damage (weaponEff = 1.0 for the mental plane), so physical classes carried a -50% tail risk mages never faced while the client still printed that penalty to mages. Rather than a parallel ManaCapacity pool, the same four-state field is renamed per plane and now taxes both planes alike.

## Decision

Mana Integrity is the mental-plane vocabulary of one wear ladder, not a second resource

## Consequences

WeaponCondition.displayNameFor gives Centered/Clouded/Tainted/Corrupted on the mental plane; a Grinding Stone reads as a Mana Stone via ConsumableKind.NameFor. Arcane damage now pays WeaponCondition.effectiveness. Engine 0.1.27; trucked clients use the plane-aware readout.

> Recorded by openlore decisions on 2026-11-24
> Decision ID: ae909f04
