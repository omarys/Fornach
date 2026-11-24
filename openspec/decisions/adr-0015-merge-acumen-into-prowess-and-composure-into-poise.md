# ADR-0015: Merge Acumen into Prowess and Composure into Poise; floor a physical combatant's Morale at a quarter

## Status

accepted (auto-accepted, unreviewed)

**Domains**:

## Context

Two discipline pairs meant a mage's discipline was a different stat from a fighter's, so the same contest read differently by plane, and a physical champion had no answer to a morale burst: the Bargaining Aspect beat a level-appropriate fighter in 12 of 20 seeds with Health at 99%.

## Decision

Merge Acumen into Prowess and Composure into Poise; floor a physical combatant's Morale at a quarter

## Consequences

StatId loses Acumen/Composure (retired names still parse). Prowess/Poise are one shared pair for both planes: guard is Poise alone, monsters no longer zero it, the atomic read is Intellect-vs-Resolve, and off-plane altar boons exclude the pair. A mental blow cannot empty the Morale of a Physical-plane combatant below a quarter of maximum in one hit. AI keeps a 10-point Recklessness headroom under the crit cap. Story ladder: fighter Bargaining 8/20 -> 20/20, Guilt 13 -> 16, Anger 17 -> 18; mage Guilt 20 -> 16.

> Recorded by openlore decisions on 2026-11-24
> Decision ID: 633d6923
