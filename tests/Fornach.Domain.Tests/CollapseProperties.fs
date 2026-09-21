namespace Fornach.Domain.Tests

open System
open FsCheck
open FsCheck.Xunit
open Fornach.Domain

[<Properties(Arbitrary = [| typeof<DomainArbitraries> |])>]
module CollapseProperties =

  [<Property>]
  let ``Combatant with all meters below 100 remains Stable`` (c: Combatant) =
    let subMaxMeters =
      { Recklessness = Meter.Create(min 99 c.Meters.Recklessness.Value)
        Exhaustion = Meter.Create(min 99 c.Meters.Exhaustion.Value)
        Overwhelm = Meter.Create(min 99 c.Meters.Overwhelm.Value)
        Frustration = Meter.Create(min 99 c.Meters.Frustration.Value)
        CognitiveFatigue = Meter.Create(min 99 c.Meters.CognitiveFatigue.Value)
        Confusion = Meter.Create(min 99 c.Meters.Confusion.Value)
        Provoke = Meter.Create(min 99 c.Meters.Provoke.Value) }

    let evaluated =
      { c with
          Meters = subMaxMeters
          Collapse = CollapseState.Stable }
      |> Combatant.evaluateCollapse

    match evaluated.Collapse with
    | CollapseState.Stable -> true
    | CollapseState.Collapsed _ -> false

  [<Property>]
  let ``Exhaustion at 100 triggers SomaticAnoxia`` (c: Combatant) =
    let subMaxMeters =
      { Recklessness = Meter.Create 50
        Exhaustion = Meter.Max
        Overwhelm = Meter.Create 50
        Frustration = Meter.Create 50
        CognitiveFatigue = Meter.Create 50
        Confusion = Meter.Create 50
        Provoke = Meter.Create 50 }

    let updated =
      { c with
          Meters = subMaxMeters
          Collapse = CollapseState.Stable }
      |> Combatant.evaluateCollapse

    updated.Collapse = CollapseState.Collapsed SomaticAnoxia
    && updated.IsExecuteEligible

  [<Property>]
  let ``ExecuteEligible targets always suffer exact 75% defense reduction`` (c: Combatant) =
    let collapsed = { c with Collapse = CollapseState.Collapsed SomaticAnoxia }

    Attributes.all
    |> List.forall (fun stat ->
      let baseVal = collapsed.Stats.Get stat
      let effectiveVal = collapsed.GetStat stat
      let expected = int (Math.Round(float baseVal * 0.25))
      effectiveVal = expected)

  [<Property>]
  let ``Stable targets suffer 0% defense penalty`` (c: Combatant) =
    let stable = { c with Collapse = CollapseState.Stable }

    Attributes.all
    |> List.forall (fun stat ->
      stable.GetStat stat = stable.Stats.Get stat)

  [<Property>]
  let ``Once collapsed, subsequent meter changes do not clear collapse state`` (c: Combatant) (newMeters: StatusMeters) =
    let collapsed = { c with Collapse = CollapseState.Collapsed SomaticAnoxia }
    let evaluated =
      { collapsed with Meters = newMeters }
      |> Combatant.evaluateCollapse
    CollapseState.isCollapsed evaluated.Collapse
