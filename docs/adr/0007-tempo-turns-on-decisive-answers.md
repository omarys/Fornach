# The Vor Turns on a Decisive Answer, Not on Contact

## Status

Accepted and Implemented.

## Context

[ADR 0006](file:///home/omary/Dev/fornach/docs/adr/0006-vor-nach-indes-tactical-tempo-integration.md) put `TacticalTiming` on `Combatant` and made the Vor decide who strikes first in a round. It also gave the Vor away for free in both directions: **any landed strike** seized it, and it was only ever *lost* through an Indes seizure. Between two fighters of comparable skill both rules fire almost every round, so the tempo became a tautology — whoever swung last held the Vor, and in the honest case it flipped every single round in strict alternation. A player could not lose the initiative by playing badly, could not hold it by playing well, and the `[VOR]` badge was a round-parity indicator with extra steps.

A second gap sat on the defensive side. `IndesResolver.evaluateIndesWithAttacker` reads `Defender.TotalHits` to decide whether a whiff can be counter-seized, and the Nach was purely a *label*: it granted no defensive substance, so "yield the initiative and answer" was never actually cheaper than racing the strike.

## Decision

Tempo ownership is now resolved by three pure functions in [`src/Fornach.Engine/ActionResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/ActionResolver.fs) (section "1b. Tempo ownership"), not by `netHits > 0`:

1. **`resolveLandedTempo attacker defender netHits`** — a strike landing from the Nach seizes the Vor only at **two hits of margin**. One hit is contact without control, and the striker holding the Vor keeps it against a scratch. This is the rule that removes the alternation: the tempo now moves when someone wins an exchange cleanly, not whenever dice touch armour.
2. **`resolveWhiffTempo attacker defender`** — a Vor holder whose strike whiffs has spent its commitment, and the defender takes the Vor. Without this the opening holder kept the first strike for the whole fight, because the only way to lose the Vor was an Indes seizure that required the *defender* to whiff too.
3. **`applyNachPosture defender contest`** — the Nach answers with **one extra defensive hit** against every strike it takes, recomputing `NetHits`, `IsCritical` and `IsWhiff`. The same `ContestResult` feeds `IndesResolver`, so the price of holding the Vor (a blunted strike, a wider Indes window) and the reward for yielding it are one number instead of two systems.

`resolveEx` applies the posture before the damage pipeline and routes the whiff branch through `resolveWhiffTempo` after the Indes check declines; both emit `CombatEvent.TempoShifted`, so the log and HUD keep telling one story.

## Consequences

- Yielding the initiative is a real posture: the Nach blunts each incoming strike by a hit and hands the answer to the defender, who takes the tempo back by landing two hits of margin or by making the holder whiff.
- **Answering the strike is the Nach's reward.** `resolveWhiffTempo` fires when the attacker holds the Vor *or* the defender is standing in the Nach, so a defender that reads the attack and reduces it to nothing takes the initiative even though it never held it. Without this half, standing in the Nach was strictly worse than racing the strike: the posture blunted damage but bought no path back to the Vor.
- The Vor is now lost by playing badly (whiffing while holding it) and kept by playing well (insisting on contact while holding it), which is what makes the badge worth reading.
- `CombatTacticsTests` gained six dice-independent facts on the three helpers (scratch does not steal, two-hit answer does, whiff hands over, whiff from the Nach does nothing to a Vor holder, a Nach defender answering a strike takes the Vor, Nach posture blunts by exactly one and can convert a hit into a whiff).
- A new `--run-sim <runs> --floors <n>` sweep ([`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs), `runProgressionSim`) carries one player through generated floors with the tower's own enemy scaling, kill growth and XP. Its first readings said the tempo change is not the difficulty term: the tower spawns enemies at `player.Level + (salt % 6)`, so a level-1 player on floor 1 met a pack of four enemies up to level 6, cleared it 55% of the time, and the run died on floor 3 two times out of three. Growth tuning (Prowess/Poise per kill) and tempo rules are second-order next to that spread; it is now capped by depth in [ADR 0008](file:///home/omary/Dev/fornach/docs/adr/0008-cap-enemy-level-spread-by-depth.md).
