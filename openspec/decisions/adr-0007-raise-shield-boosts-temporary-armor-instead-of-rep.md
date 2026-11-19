# ADR-0007: Raise Shield boosts temporary armor instead of repairing durability

## Status

accepted (auto-accepted, unreviewed)

**Domains**:

## Context

Raise Shield is a shield held up, not a repair action: repairing armor mid-fight made armor durability a self-service resource and overlapped with repair kits and shrines. A temporary Boost pool (eaten by armor shred before real durability, cleared when the player next acts) simulates holding a shield that takes the beating and is then lowered.

## Decision

Raise Shield boosts temporary armor instead of repairing durability

## Consequences

ArmorIntegrity gains a Boost field (old saves deserialize with Boost=0); Raise Shield no longer restores durability; soak ratio can exceed 50% while the shield is up; Raise Shield now pays Inertia/Focus guard stacks instead of Flow/Momentum ramp

> Recorded by openlore decisions on 2026-11-19
> Decision ID: 093ce21d
