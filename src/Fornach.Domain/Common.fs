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

  let repair = function
    | WeaponCondition.Broken -> WeaponCondition.Damaged
    | WeaponCondition.Damaged -> WeaponCondition.Notched
    | WeaponCondition.Notched -> WeaponCondition.Pristine
    | WeaponCondition.Pristine -> WeaponCondition.Pristine

  let restorePristine (_: WeaponCondition) = WeaponCondition.Pristine

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

/// High-level ecological classification of non-humanoid adversaries
[<RequireQualifiedAccess>]
type MonsterFamily =
  | Beast
  | Construct
  | UndeadWraith
  | Aberration
  | GriefManifestation

  member this.Name : string =
    match this with
    | Beast -> "Beast"
    | Construct -> "Construct"
    | UndeadWraith -> "Undead Wraith"
    | Aberration -> "Aberration"
    | GriefManifestation -> "Grief Manifestation"

  member this.Description : string =
    match this with
    | Beast -> "Predatory fauna shaped by harsh ecological biomes; relies on packs, venom, and feral speed."
    | Construct -> "Ancient animated stone and clockwork sentinels; possesses heavy armor and unyielding stability."
    | UndeadWraith -> "Ethereal phantoms born of grief and trauma; bypasses physical plate to erode Morale."
    | Aberration -> "Anomalous horrors from the void beneath the Tower; disrupts spacetime and sanity."
    | GriefManifestation -> "Psychological embodiments of repressed trauma, amnesia, and unresolved loss."

/// Tactical combat role defining stat distributions and AI behavior
[<RequireQualifiedAccess>]
type MonsterRole =
  | Swarmer
  | Brute
  | Skirmisher
  | Stalker
  | Caster
  | Colossus

  member this.Name : string =
    match this with
    | Swarmer -> "Swarmer"
    | Brute -> "Brute"
    | Skirmisher -> "Skirmisher"
    | Stalker -> "Stalker"
    | Caster -> "Caster"
    | Colossus -> "Colossus"

/// Passive traits and biological/mechanical properties for monsters
type MonsterTrait =
  | PackTactics of hitBonusPerAlly: int
  | VenomousSting of bleedPerHit: int
  | AcidicBlood of armorCorrosion: int
  | EtherealCarapace of physicalSoakBonus: float
  | RelentlessFerocity of lowHealthDamageBonusPct: int
  | PetrifyingGaze of reflexDebuff: int
  | PsychicDoldrums of passiveCognitiveFatigue: int
  | MoltenAura of physicalBurn: int
  | ChillingPresence of exhaustionDrain: int

  member this.Name : string =
    match this with
    | PackTactics _ -> "Pack Tactics"
    | VenomousSting _ -> "Venomous Sting"
    | AcidicBlood _ -> "Acidic Blood"
    | EtherealCarapace _ -> "Ethereal Carapace"
    | RelentlessFerocity _ -> "Relentless Ferocity"
    | PetrifyingGaze _ -> "Petrifying Gaze"
    | PsychicDoldrums _ -> "Psychic Doldrums"
    | MoltenAura _ -> "Molten Aura"
    | ChillingPresence _ -> "Chilling Presence"
