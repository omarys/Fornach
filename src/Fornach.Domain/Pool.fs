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

  member this.IsDepleted = this.Current <= 0
