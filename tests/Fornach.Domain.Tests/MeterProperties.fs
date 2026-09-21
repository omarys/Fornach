namespace Fornach.Domain.Tests

open FsCheck
open FsCheck.Xunit
open Fornach.Domain

[<Properties(Arbitrary = [| typeof<DomainArbitraries> |])>]
module MeterProperties =

  [<Property>]
  let ``Meter.Create always clamps value between 0 and 100`` (raw: int) =
    let m = Meter.Create raw
    m.Value >= 0 && m.Value <= 100

  [<Property>]
  let ``Meter addition never exceeds 100 nor drops below 0`` (m: Meter) (delta: int) =
    let result = m + delta
    result.Value >= 0 && result.Value <= 100

  [<Property>]
  let ``Meter subtraction never drops below 0 nor exceeds 100`` (m: Meter) (delta: int) =
    let result = m - delta
    result.Value >= 0 && result.Value <= 100

  [<Property>]
  let ``Meter addition is monotonic for non-negative deltas`` (m: Meter) (PositiveInt delta) =
    let result = m + delta
    result.Value >= m.Value

  [<Property>]
  let ``Meter subtraction is monotonic for non-negative deltas`` (m: Meter) (PositiveInt delta) =
    let result = m - delta
    result.Value <= m.Value

  [<Property>]
  let ``Active pattern matches exact boundary semantics`` (m: Meter) =
    match m with
    | Collapsed -> m.Value = 100
    | Critical -> m.Value >= 75 && m.Value < 100
    | Strained -> m.Value >= 40 && m.Value < 75
    | Normal -> m.Value >= 0 && m.Value < 40

  [<Property>]
  let ``Pool deltas never allow Current to exceed Maximum or drop below 0`` (maxHp: int) (delta: int) =
    let pool = Pool.Create maxHp
    let updated = pool.ApplyDelta delta
    updated.Current >= 0 && updated.Current <= pool.Maximum
