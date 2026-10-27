# Vor / Nach / Indes Tactical Tempo Integration in Combat

## Status

Accepted and Implemented.

## Context

*Fornach* takes its name directly from the German Liechtenauer fencing tradition (*Kunst des Fechtens*):
- **Vor** (Before): Possessing initiative, attacking first, driving the tempo.
- **Nach** (After): Reacting, parrying, defending against an incoming committed strike.
- **Indes** (In the instant / Meanwhile / In the bind): Intercepting at the midpoint of tempo; simultaneous counter-action that seizes the Vor.

Historically, the domain model declared `type TacticalTiming = Vor | Nach | Indes` inside [`src/Fornach.Engine/IndesResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/IndesResolver.fs), but the type was unreferenced across the codebase. Duels in [`Program.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Program.fs) and simulations in [`Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) hardcoded turn order so Player / Combatant A always attacked first every single round. While `IndesResolver.resolveIndesOpportunity` evaluated net defense hits on an attack whiff, it only granted a ramp stack and added Frustration without altering round initiative, tactical options, or combatant state.

## Decision

1. **First-Class Domain Modeling**:
   - Promoted `TacticalTiming` (`Vor | Nach | Indes`) into [`src/Fornach.Domain/Common.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Common.fs).
   - Added `Tempo: TacticalTiming` to [`Combatant`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Combatant.fs) alongside helper methods (`Combatant.seizeVor`, `Combatant.yieldVor`, `IsInVor`, `IsInNach`).
   - Added `CombatEvent.TempoShifted` to trace dynamic tempo transfers in combat logs.

2. **Dynamic Indes Thresholds**:
   - In [`src/Fornach.Engine/IndesResolver.fs`](file:///home/omary/Dev/fornach/src/Fornach.Engine/IndesResolver.fs), enhanced threshold calculation to factor in attacker commitment and overextension:
     $$\text{Threshold} = \max\Big(1,\ 3 - \text{BucklerBonus} - \text{WindupBonus} - \text{RecklessnessBonus}\Big)$$
     - $\text{BucklerBonus} = 1$ when defender has active `ParryingBuckler`.
     - $\text{WindupBonus} = 1$ when attacker executes a heavy windup (`isInterruptibleWindup`).
     - $\text{RecklessnessBonus} = 1$ when attacker's Recklessness is $\ge 60$.

3. **Dynamic Round Initiative**:
   - In [`src/Fornach.Cli/Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) (`runSingleMatch`), round turn order is dictated by `combA.IsInVor || not combB.IsInVor`.
   - In [`src/Fornach.Cli/Program.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Program.fs) (`runInteractiveDuel`, `runStoryDuel`), whichever combatant holds the Vor strikes first in the round. If an enemy seizes the Vor, the enemy executes their strike first in the following round, placing the player in the Nach.
   - When in the Vor, taking a recovery action (`SteadyForm`, `RaiseShield`, `CenterMind`) voluntarily yields the Vor to the opponent.
   - Landing a successful offensive strike while in the Nach seizes the Vor for the attacker.
   - Interrupting windups via `ShieldBash` or disrupting strikes via `ConcealedBlade` seizes the Vor in Indes.

4. **HUD & Presentation**:
   - Combatant panels in [`src/Fornach.Cli/Display.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Display.fs) now render `[VOR]` (Gold) and `[NACH]` (Steel Blue) badges in their header titles.
   - `Display.logEvent` renders `󰒋 TEMPO SHIFTED` entries whenever the Vor transfers.

## Consequences

- Combat is now an active contest for tempo: whiffing against high defense margins or overextending with heavy windups directly surrenders initiative to the defender.
- Defensive preparations like `ParryingBuckler` and `ConcealedBlade` gain heightened tactical value by enabling defenders to seize the Vor against aggressive foes.
- Deterministic simulations remain stable and reproducible across parallel batches (`ParallelDeterminismTests` 100% passing).
