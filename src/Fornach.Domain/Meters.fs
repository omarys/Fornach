namespace Fornach.Domain

open System

[<Struct>]
type Meter =
  private
  | Meter of int

  member this.Value =
    match this with
    | Meter v -> v

  static member Zero = Meter 0
  static member Max = Meter 100

  static member Create v = Meter(Math.Clamp(v, 0, 100))

  static member (+)(Meter a, delta: int) = Meter.Create(a + delta)
  static member (-)(Meter a, delta: int) = Meter.Create(a - delta)

  override this.ToString() = sprintf "%d%%" this.Value

[<AutoOpen>]
module MeterPatterns =
  /// Categorizes meter strain into operational bands
  let (|Normal|Strained|Critical|Collapsed|) (m: Meter) =
    match m.Value with
    | 100 -> Collapsed
    | v when v >= 75 -> Critical
    | v when v >= 40 -> Strained
    | _ -> Normal

/// Aggregate record holding all 7 dynamic combat gauges
type StatusMeters =
  {
    // Shared combat entropy
    Recklessness: Meter
    // Physical status meters
    Exhaustion: Meter
    Overwhelm: Meter
    Frustration: Meter
    // Mental status meters
    CognitiveFatigue: Meter
    Confusion: Meter
    Provoke: Meter }

  static member Zero =
    { Recklessness = Meter.Zero
      Exhaustion = Meter.Zero
      Overwhelm = Meter.Zero
      Frustration = Meter.Zero
      CognitiveFatigue = Meter.Zero
      Confusion = Meter.Zero
      Provoke = Meter.Zero }
