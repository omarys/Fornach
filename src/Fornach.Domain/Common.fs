namespace Fornach.Domain

open System

/// Ids only have to be unique and to sort in creation order. They used to come from `Guid.NewGuid`, and the
/// combat ordering sorts by id (`TowerSession` breaks distance ties with `e.Id`), so the outcome of a tie
/// depended on a random Guid — the same seed could resolve a fight differently on every run. A counter keeps
/// ids unique within a process and makes that ordering reproducible, which is what the optimal-play harness
/// needs before any balance number it prints can be trusted.
module private CombatantIdSource =
  let mutable next = 0

[<Struct>]
type CombatantId =
  private
  | CombatantId of Guid

  static member New() =
    let n = System.Threading.Interlocked.Increment(&CombatantIdSource.next)
    CombatantId(Guid(n, 0s, 0s, Array.zeroCreate 8))

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

/// Tactical timing in the historical German fencing (Vor / Nach / Indes) paradigm
type TacticalTiming =
  /// Vor (Before): Possessing initiative, attacking first, driving the tempo
  | Vor
  /// Nach (After): Reacting, parrying, defending against an incoming strike
  | Nach
  /// Indes (Meanwhile / In the midst): Intercepting at the tempo midpoint; simultaneous counter-action
  | Indes

  member this.Name : string =
    match this with
    | Vor -> "Vor"
    | Nach -> "Nach"
    | Indes -> "Indes"

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
  /// Damps the status-meter pressure a target takes from incoming strikes. Grief Aspects carry this
  /// so the Exhaustion clock cannot collapse an authored multi-round fight in two rounds and bypass
  /// its pools entirely (collapse makes the target executable, and execution ignores pools).
  | StoicResistance of meterReductionPct: int

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
    | StoicResistance _ -> "Stoic Resistance"

/// Names a combatant behaviour the ruleset owns. The engine never interprets an id: it looks it up in
/// the table its caller hands it and keeps its own generic discipline when the table names nothing. The
/// Grief Aspects are the first users -- each names a signature press -- but a new setting can name its
/// own presses without the engine learning what any of them mean.
[<Struct>]
type PolicyId =
  | PolicyId of string
  member this.Value =
    match this with
    | PolicyId id -> id

/// The policy ids the shipped story ruleset uses. Declared once here because both sides of the
/// contract live in different projects -- the Aspect that carries the id and the table that resolves it
/// -- and a typo on either side would silently drop the combatant back to the generic discipline.
module PolicyIds =
  let griefGuilt = PolicyId "grief.guilt"
  let griefDenial = PolicyId "grief.denial"
  let griefAnger = PolicyId "grief.anger"
  let griefBargaining = PolicyId "grief.bargaining"
  let griefDepression = PolicyId "grief.depression"
  let griefAcceptance = PolicyId "grief.acceptance"
