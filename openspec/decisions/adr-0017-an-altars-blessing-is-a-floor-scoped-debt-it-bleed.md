# ADR-0017: An altar's blessing is a floor-scoped debt: it bleeds both pools per step, ends at the stairs, and a shrine lifts it

## Status

accepted (auto-accepted, unreviewed)

**Domains**:

## Context

A permanent altar boon was either enormous or free depending on when it was found, and it turned pacts into pure upgrades. Pricing the blessing as a debt makes the pact a decision about the floor you are on: the points are real now, and the body pays for them.

## Decision

An altar's blessing is a floor-scoped debt: it bleeds both pools per step, ends at the stairs, and a shrine lifts it

## Consequences

TowerRunState gains AltarDebt (the granted point deltas plus a 1% of maximum Health and Morale drain per step, floored at 1 so it cannot kill). ascendFloor lifts the boon and its debt; a shrine cleanses both early; walking into a wall pays nothing. The altar's menu text names the pact.

> Recorded by openlore decisions on 2026-11-24
> Decision ID: b30e2cfb
