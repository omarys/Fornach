# Seize Indes: Answer a Strike With a Strike

## Status

Accepted and Implemented.

## Context

[ADR 0007](file:///home/omary/Dev/fornach/docs/adr/0007-tempo-turns-on-decisive-answers.md) gave the Nach two rewards: one extra defensive hit against every incoming strike, and the tempo back when the strike is read to nothing. Both are *passive* — they pay the defender for standing still and letting the dice decide. Nothing in the defensive menu let a player **take** the initiative from the Nach on their own terms, and the word *Indes* (the moment between Vor and Nach) only ever appeared as something the engine granted after a whiff, never as something a combatant could commit to.

The three active defences (`FortitudeGuard`, `ReflexSlip`, `ResolveAnchor`, `IntuitionPerception`) all resolve the same way: mitigate a strike you did not stop. Against a strike that simply outclasses you — a Warrior twenty levels above you, a GrandMaster caster on your weak plane — no mitigation multiplier is enough, because the contest was lost before the guard was chosen.

## Decision

`DefenseChoice` in [`src/Fornach.Engine/Actions.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/Actions.fs) widens `DefenseClassification` to a choice of two kinds:

```fsharp
| Guard of DefenseClassification
| SeizeIndes of AttackClassification
```

The distinction is the point: a guard answers a strike, an Indes counter **is** a strike. `ActionResolver.resolveExWithDefense` therefore takes `DefenseChoice option` and resolves a declared counter as its own contest (Step C.2) — the defender's offensive stat and banked stacks against the incoming attacker's defence on the counter's vector, rolled with the same `DicePool.resolveContestEx` every other exchange uses.

The wager is symmetric and both halves are enforced:

1. **Win the exchange** — the counter's total hits exceed the incoming strike's. The strike is stopped outright (no damage, no crit, no strain), the defender seizes the Vor, and the attacker falls into the Nach. A strike stopped by a strike is the Indes made real.
2. **Lose the exchange** — the counter answered nothing, so the strike it invited lands **1.5x**, every banked stack is wiped (`Combatant.clearStacks`), and the attempt frustrates (+25). The gamble was made of those stacks; a defender who spent them on a failed counter does not keep the ramp it built.

A counter carries no guard affinity (`isAffinityCounter = false`, mitigation 1.0) — there is nothing to mitigate with, because the counter either beats the roll or it does not. It is offered in the tower's defensive menu only (`allowIndes`), and always as a strike of the player's own vector, so the offer is never one the build cannot make. The two duels pass `false`: a duel has no defence slot, so a menu entry promising a counter would be a lie.

## Consequences

- The Nach gains an active third option beside "mitigate" and "recover": trade the guard for a strike. It is the only defensive choice whose value does not degrade as the incoming strike outclasses you, because it contests a different stat pair.
- Losing costs more than a plain guard: 1.5x damage *and* the entire ramp. A player who counters a Master's strike on a coin-flip is worse off than one who simply braced, which is what makes it a wager rather than a free reroll.
- The counter is deliberately not free of the tempo economy: it is declared from the Nach and spends the defence slot, but it does not consume the player's action, so the player still swings that round. Two exchanges for one round is the price of the risk, not a bug.
- `ActiveDefenseTests` gained two dice-independent facts: a counter that out-rolls stops the strike and takes the Vor, and one that loses lands exactly 1.5x an unopposed strike with every stack spent.
- `Program.buildActionChoicesWithTelegraph` grew `allowIndes` and appends the entry after the core menu; the core builder is now `buildActionChoicesCore`. The entry is matched back by name in the tower loop (`choice.Contains("Seize Indes")`), which sets `playerActiveDefenseOpt <- Some(SeizeIndes atk)`.
- Enemy AI does not use it. `AI.chooseActiveDefense` still returns a `DefenseClassification`, so the balance of every simulated climb is unchanged by this ADR — it adds a player option, not a difficulty term.
