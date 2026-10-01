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
type ComplexForm =
  /// Power / Intellect: Volatile psychic overclocking (+25% spell damage & cognitive fatigue; splash damage; Fading drain: +10 self-fatigue, +15 self-recklessness)
  | ResonanceSpike
  /// Agility / Acuity: Sensory static and perceptual jitter (passive clone weaving, 40% decoy evasion swap on melee strike; -15% direct damage)
  | PhantasmalDiffusion
  /// Discipline / Acumen: Interlocking geometric abjuration web (+15 Arcane Ward per turn, 50% damage reflection + 15 Frustration when ward struck; locks Overchannel)
  | AegisLattice

  member this.Name : string =
    match this with
    | ResonanceSpike -> "Resonance Spike"
    | PhantasmalDiffusion -> "Phantasmal Diffusion"
    | AegisLattice -> "Aegis Lattice"

  member this.Vector : Vector =
    match this with
    | ResonanceSpike -> Vector.Power
    | PhantasmalDiffusion -> Vector.Agility
    | AegisLattice -> Vector.Discipline

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
