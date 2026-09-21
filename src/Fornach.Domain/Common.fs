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

[<RequireQualifiedAccess>]
type CombatStance =
  | PowerStance
  | AgilityStance
  | DisciplineStance

[<RequireQualifiedAccess>]
type WeaponCondition =
  | Pristine
  | Notched
  | Damaged
  | Broken

module WeaponCondition =
  let degradation = function
    | WeaponCondition.Pristine -> WeaponCondition.Notched
    | WeaponCondition.Notched -> WeaponCondition.Damaged
    | WeaponCondition.Damaged -> WeaponCondition.Broken
    | WeaponCondition.Broken -> WeaponCondition.Broken

  let effectiveness = function
    | WeaponCondition.Pristine -> 1.0
    | WeaponCondition.Notched -> 0.90
    | WeaponCondition.Damaged -> 0.75
    | WeaponCondition.Broken -> 0.50

  let displayName = function
    | WeaponCondition.Pristine -> "Pristine"
    | WeaponCondition.Notched -> "Notched"
    | WeaponCondition.Damaged -> "Damaged"
    | WeaponCondition.Broken -> "Broken"
