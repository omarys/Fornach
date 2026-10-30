namespace Fornach.Domain

open System

[<Struct>]
type Pool =
  { Current: int
    Maximum: int }

  static member Create max =
    let m = Math.Max(1, max)
    { Current = m; Maximum = m }

  member this.ApplyDelta delta =
    { Current = Math.Clamp(this.Current + delta, 0, this.Maximum)
      Maximum = this.Maximum }

  /// Grows the pool by an item's flat bonus, carrying the gain into `Current` so equipping it heals by the
  /// same amount rather than leaving the bearer at the old ceiling.
  member this.RaiseMaximum (bonus: int) =
    let maximum = Math.Max(this.Maximum, this.Maximum + bonus)
    { Current = Math.Min(this.Current + maximum - this.Maximum, maximum)
      Maximum = maximum }

  member this.IsDepleted = this.Current <= 0
