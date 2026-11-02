# Poise Sets Frontage, Prowess Pays for Interception

## Status

Accepted and Implemented.

## Context

[ADR 0009](file:///home/omary/Dev/fornach/docs/adr/0009-prowess-poise-and-asymmetric-tempo-redesign.md) removed the dedicated `Prowess` and `Poise` attacks and made both passive: `Prowess` reads intent and adds an offensive bonus, `Poise` stabilises defence and resists shock. Neither was left with a job that decides how a fight *shapes* — and with the Prowess-based class gone, `Prowess` had no consumer that cared about it as a stat rather than a modifier.

Two swarm mechanics were keyed on the wrong thing as a result:

- **How many enemies could reach a defender** was a hard `Math.Min(8, mob.Length)` in the swarm loop — the eight surrounding tiles, identical for a raw Novice and a GrandMaster. Nothing the player invested in changed it.
- **Attacks of opportunity** were keyed on **level** (`(Level - 20) / 10`, capped at 8), so every class punished an approach equally well regardless of the stat that throws a reactive strike. `Poise` in particular had no say in whether a defender was flanked at all.

That left both stats decorative in the one situation where they should be decisive: being surrounded.

## Decision

`Poise` becomes the stat that decides **how many bodies can bring a strike to bear at once**, and `Prowess` becomes the stat that pays for **answering those approaches**.

In [`src/Fornach.Domain/Combatant.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs):

```fsharp
member this.Frontage = Math.Clamp(8 - (this.Stats.Get Poise / 100), 1, 8)

member this.AttackOfOpportunityCapacity = Math.Clamp((this.Stats.Get Prowess - 40) / 20, 0, 8)
```

`Frontage` is spent by [`src/Fornach.Cli/Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) in place of the flat eight-tile limit; `BastionZoneControl` still restricts the ring to its three frontline tiles, now bounded by the frontage as well. `AttackOfOpportunityCapacity` gates the preemptive interception in [`src/Fornach.Engine/ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs), and the interception's damage now reads `Prowess` on either vector — the vector decides whether the defender can react at all, `Prowess` decides how hard the reaction lands.

The encirclement penalty was already live in `DicePool.computeEncirclementPenalty` (`priorDefenses * (priorDefenses + 1) / 2 * pressureRatio * 3.5`) and is unchanged; what was missing was `PeakEncirclementPenalty` telemetry, which the swarm loop never assigned and now records.

## Consequences

- **Frontage spans eight approaches to one.** A raw Novice (Poise 45) is surrounded by every tile; a Veteran (201) meets six; a Master Fighter (441) four; a GrandMaster (841) one. The floor of 1 is deliberate: frontage decides the crowd, never immunity.
- **A champion that has bought the ring cannot be buried by numbers.** Once frontage falls to one, a weaker swarm arrives single file and is answered piecemeal. `VeteranSwarmTests` records GrandMaster saturation instead of failing on it, and `BestiarySwarmTests` accepts an impenetrable Master champion where frontage is doing the work. The colossus premise still holds wherever the champion has not yet bought that space control.
- **Interception needs a flanker to punish.** The AoO gate stays `priorDefenses >= 1`, so at frontage 1 there is never a second body and the count is legitimately zero — the mechanic belongs to defenders who are outnumbered but not outclassed, which is why `MonteCarloSwarmTests` now asserts the absence rather than a rate.
- **`Prowess` starts answering at 60 and fills the ring at 200**, so a Novice answers nothing — the same shape the old level curve gave — while a Veteran lands the full eight.
