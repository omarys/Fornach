# ADR-0001: Study-stack gambits burn the whole pile and scale off it

## Status

accepted

**Domains**: unknown

## Context

Holding a read was worth a flat 5% on a handful of attacks and the three spenders capped spend at the requested 2-6, so a 12-stack buildup paid like a 2-stack one. One scale (hold 5%, Discipline vectors 10%, spend 2.2+0.7n) makes the spender the payoff for the buildup; consuming every stack removes the meaningless payload that let callers guess a count.

## Decision

The system SHALL consume all accumulated study stacks when executing a spender gambit and scale its potency based on the total number of consumed stacks.

## Consequences

A spender converts an entire buildup into one strike, so pacing shifts for anyone who banks stacks; Calculated Flaw loses its Discipline-stance half-retention; measured on --balance-matrix the martial rows rise (Fighter Master 41/24/33/19 to 48/30/37/29) with no floor, cap or column order changing.

> Recorded by openlore decisions on 2026-10-23
> Decision ID: 89d606ce


