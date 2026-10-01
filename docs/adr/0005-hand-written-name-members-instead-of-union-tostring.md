# Hand-Written Name Members Instead of Union `ToString()` in Hot Paths

## Status

Proposed — measured and validated on a scratch copy; the patch below is **not yet applied**.

## Context

A second-pass profile of the balance-matrix workload (`--balance-matrix`), taken after the array-backed `StatBlock` change shipped ([ADR 0002](file:///home/omary/Dev/fornach/docs/adr/0002-array-backed-statblock-and-static-baseline.md)), found that the dominant remaining cost in match setup is not arithmetic at all: it is a single `sprintf` on a discriminated union.

[`src/Fornach.Domain/TierFactory.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs#L238):

```fsharp
let tierName = sprintf "%s %s" (tier.ToString()) cls.Name
```

F# compiles a union with no custom `ToString()` to a **reflective structured-formatting printer**. It allocates a formatting buffer and reflection state on every call. Measured on Release DLLs with tiered compilation disabled, median-of-5:

| Expression | ns/op | B/op |
| :--- | ---: | ---: |
| `CombatTier.Novice.ToString()` | 40,093 | 15,026 |
| `CombatTier.GrandMaster.ToString()` | 41,942 | 15,026 |
| `CharacterClass.Berserker.ToString()` | 63,327 | 28,756 |
| `CollapseReason.SomaticAnoxia.ToString()` | 45,983 | 18,402 |
| `WeaponCondition.Broken.ToString()` | 42,032 | 15,506 |
| `CombatMode.Arcane.ToString()` | 40,820 | 14,674 |
| `Plane.Physical.ToString()` | 35,717 | 12,602 |
| `CharacterClass.Berserker.Name` (hand-written member, [`Classes.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Classes.fs#L79)) | **3.4** | **0** |
| `WeaponCondition.displayName` (hand-written module function, [`Common.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Common.fs#L56)) | **3.4** | **0** |
| `int.ToString()` (control — a primitive, compiler-intrinsic) | 7.9 | 0 |

This is a domain-wide footgun, not a `CombatTier` quirk: every union in `Fornach.Domain` pays it. It was also hiding in plain sight in the first profile pass — the `createGrandMasterBerserker` row of [HANDOFF §4.2](file:///home/omary/Dev/fornach/docs/HANDOFF.md) (43 µs / 21,926 B) was attributed to level scaling, but it is entirely this call:

| | ns/op | B/op |
| :--- | ---: | ---: |
| `createClassLevel Berserker 1` | 1,073 | 1,920 |
| `createClassLevel` + a literal name | 1,137 | 2,096 |
| `createClassTier Berserker Novice` | **43,139** | **17,353** |

End-to-end on `Fornach.Cli --balance-matrix` (96 matchups, 9,580 matches / 75,736 rounds), with identical instrumentation applied to both trees:

| | baseline | name member |
| :--- | ---: | ---: |
| wall clock (2 runs) | 9.39 s / 10.05 s | **2.28 s / 2.30 s** |
| total (instrumented) | 9,530 ms | 2,114 ms |
| of which match setup | 7,604 ms (**80%**) | 306 ms (14%) |
| allocated | 7.27 GB | **3.94 GB** |
| matches / rounds | — identical — | |
| balance matrix table | — byte-identical after ANSI strip — | |
| `dotnet test -c Release` | — 179 / 179 pass, 0 warnings — | |

## Decision

**Unions that are converted to display text on a hot path get a hand-written name member** — `member this.Name : string` on the type, or `displayName` in the type's companion module — and hot paths use that member instead of `ToString()` / `%s` / string interpolation.

This follows the two precedents already in the domain: `CharacterClass.Name` ([`Classes.fs:79`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Classes.fs#L79), a member on the union) and `WeaponCondition.displayName` ([`Common.fs:56`](file:///home/omary/Dev/fornach/src/Fornach.Domain/Common.fs#L56), a module function), alongside `ProgressionScale.tierToLevel` ([`TierFactory.fs:19`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs#L19)) as the same idea for the tier → level direction.

### Patch

Add the member to `type CombatTier` in [`TierFactory.fs`](file:///home/omary/Dev/fornach/src/Fornach.Domain/TierFactory.fs#L6-L14), after the `GrandMaster` case:

```fsharp
  /// Display name for this tier.
  /// Hand-written deliberately: the compiler-generated union ToString() goes
  /// through reflective structured formatting (~40 us / 15 KB per call).
  member this.Name : string =
    match this with
    | Novice -> "Novice"
    | Veteran -> "Veteran"
    | Master -> "Master"
    | GrandMaster -> "GrandMaster"
```

Then replace the caller's `tier.ToString()` with `tier.Name`:

```fsharp
let tierName = sprintf "%s %s" tier.Name cls.Name
```

The strings are identical, so names, `%A` output, test expectations, and balance numbers are unchanged. This is the whole production change: one member, one call site. The tier string is also built reflectively in `tests/Fornach.Domain.Tests/OverallBalanceTests.fs:112`, so a member is preferred over inlining the match in the caller.

## Considered Options

- **Hand-written `member this.Name` on the union** — *selected*. Matches `CharacterClass.Name`; explicit at the call site; compiles to a jump table returning interned literals (0 B); no change to `ToString()` semantics for debugging or `%A`.
- **Override `this.ToString()` on the union to return the literal** — rejected. It would fix every existing call site with zero caller churn, but it silently changes `ToString()`/`%A`-adjacent behaviour across the domain, hides the cost at the call site, and diverges from the `CharacterClass.Name` precedent already in place.
- **Inline the match in `TierFactory.createClassTier`** — rejected as the general rule. The smallest diff for this one caller, but it invites a second copy of the tier → string table and does not help the other unions that hit the same printer.
- **Memoise names in a `Dictionary<CombatTier, string>`** — rejected. The hand-written match is already ~3 ns and allocation-free, so a cache adds a hash lookup, a mutable static, and thread-safety surface for no gain.
- **Keep `ToString()`** — rejected. It is ~80% of balance-matrix wall time.

## Consequences

- **Balance matrix**: 9.39–10.05 s → 2.28–2.30 s (**~4.3×**); match setup 7,604 ms (80%) → 306 ms (14%); allocation 7.27 GB → 3.94 GB.
- **Behaviour**: none. Output is byte-identical and all 179 tests pass, because the produced strings are unchanged.
- **New convention**: never `%s`, `$"..."`, or `.ToString()` on a union inside a measured hot path. Add a name member instead. The unions in `Fornach.Domain` that are currently formatted reflectively in *cold* paths are not urgent — `tests/Fornach.Domain.Tests/OverallBalanceTests.fs:112,134-138,169` and [`src/Fornach.Story/StoryRunner.fs:50`](file:///home/omary/Dev/fornach/src/Fornach.Story/StoryRunner.fs#L50) — but they are the same trap and should follow if they ever move into a loop.
- **Residual bottleneck moves**: with setup down to 306 ms, the round loop in [`src/Fornach.Cli/Simulation.fs`](file:///home/omary/Dev/fornach/src/Fornach.Cli/Simulation.fs) is now 86% of the run (1,808 ms / 3.55 GB of the 3.94 GB) — see the follow-up in [HANDOFF §4.6](file:///home/omary/Dev/fornach/docs/HANDOFF.md). Further creation-side micro-optimisation has a 306 ms ceiling and is not worth pursuing first.
- **Documentation**: [HANDOFF §4.2 and the new §4.6](file:///home/omary/Dev/fornach/docs/HANDOFF.md) are updated to record that the §4.2 `createGrandMasterBerserker` row is this call, not level scaling, and to re-rank the remaining work.
