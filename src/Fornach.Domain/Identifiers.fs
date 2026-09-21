namespace Fornach.Domain

open System

[<Struct>]
type CombatantId =
  private
  | CombatantId of Guid

  member this.Value =
    match this with
    | CombatantId g -> g

  static member New() = CombatantId(Guid.NewGuid())
  static member OfGuid(g: Guid) = CombatantId g
  override this.ToString() = this.Value.ToString("D")

[<Struct>]
type ActionId =
  private
  | ActionId of string

  member this.Value =
    match this with
    | ActionId s -> s

  static member Create(s: string) =
    if String.IsNullOrWhiteSpace s then
      invalidArg (nameof s) "Action identifier cannot be empty or whitespace."

    ActionId(s.Trim().ToLowerInvariant())

  override this.ToString() = this.Value
