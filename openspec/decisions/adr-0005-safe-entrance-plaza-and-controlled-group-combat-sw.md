# ADR-0005: Safe Entrance Plaza and Controlled Group Combat Swarm Density

## Status

accepted

**Domains**: unknown

## Context

Longer tactical combat allowed roaming enemies to swarm the player on Floor 2 entrance plaza immediately upon ascending the stairs, and group combat lacked active defense integration and frontline engagement caps.

## Decision

The system SHALL designate entrance chambers on Floor 2 and above as safe thresholds, scale Story Floor 2 enemy density to two to three mobs per chamber, and enforce frontline attacker engagement caps during group combat.

## Consequences

Entrance chambers on Floor 2+ are safe thresholds; Story Floor 2 density scales to 2-3 mobs per chamber; group combat caps frontline attackers at 4 (3 with BastionZoneControl), enables Active Defense counterplay in group combat, and throttles reinforcements when frontline is saturated.

> Recorded by openlore decisions on 2026-10-28
> Decision ID: 39ac2214


