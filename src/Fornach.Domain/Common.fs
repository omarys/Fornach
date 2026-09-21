namespace Fornach.Domain

open System

[<Struct>]
type CombatantId =
  private
  | CombatantId of Guid

  static member New() = CombatantId(Guid.NewGuid())
  static member OfGuid(g: Guid) = CombatantId g

  member this.Value =
    match this with
    | CombatantId g -> g

type Plane =
  | Physical
  | Mental

type Vector =
  | Power
  | Agility
  | Discipline

[<RequireQualifiedAccess>]
type CombatMode =
  | Physical
  | Social
  | Arcane
