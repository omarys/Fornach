namespace Fornach.Domain

open System

/// Categorization of tactical preparations
type PreparationCategory =
  /// Crowd-Control Asset (Anti-Swarm): Mitigates/negates multi-opponent penalties, controls crowd geometry, or induces mass disruption
  | CrowdControl
  /// Single-Target Asset (Duelist Finisher): Enhances 1v1 lethality, exploits high Recklessness, sets up Indes windows, or inflicts meter rupture
  | SingleTargetDuel

/// Class-specific tactical preparation edge assets
type PreparationType =
  // --- Berserker (Power / Physical) ---
  /// Crowd: Surplus NetHits spill over as flat damage to all engaged flankers
  | ShockwaveSlam
  /// Duel: Consumes own health to spike Recklessness into Fever Pitch (bonus dice + damage)
  | BerserkTincture

  // --- Inquisitor (Power / Mental) ---
  /// Crowd: Howl that inflicts +25 Cognitive Fatigue on all active attackers
  | DreadWarhorn
  /// Duel: Marks 1 target; any critical strike deals 2x Morale damage and inflicts Rupture
  | SynapticBrand

  // --- Duelist (Agility / Physical) ---
  /// Crowd: Blinds secondary flankers, stripping their multi-opponent penalty for 5 turns
  | CaltropPouch
  /// Duel: Quick-draw boot blade; usable from Nach to land an immediate un-telegraphed puncture
  | ConcealedBlade

  // --- Mesmer (Agility / Mental) ---
  /// Crowd: Creates phantasms; flankers hit illusions, building +20 Confusion and missing turn
  | MirrorMirage
  /// Duel: Psychic venom; target takes escalating Morale drain for every point of Recklessness gained
  | NeuroToxin

  // --- Justicar (Discipline / Physical) ---
  /// Crowd: Plants polearm/shield; limits simultaneous attackers strictly to 3 (front 3 tiles)
  | BastionZoneControl
  /// Duel: Dedicated off-hand reaction die pool, expands Indes trigger window by +1 hit
  | ParryingBuckler

  // --- Strategist (Discipline / Mental) ---
  /// Crowd: Pre-battle notes; grants +2 Study Stacks on all visible foes immediately
  | HeraldicTreatise
  /// Duel: Exposes contradictions; immediately converts opponent's current Recklessness to unmitigated Morale damage
  | SocraticDossier

  /// Category classification (CrowdControl or SingleTargetDuel)
  member this.Category : PreparationCategory =
    match this with
    | ShockwaveSlam
    | DreadWarhorn
    | CaltropPouch
    | MirrorMirage
    | BastionZoneControl
    | HeraldicTreatise -> CrowdControl
    | BerserkTincture
    | SynapticBrand
    | ConcealedBlade
    | NeuroToxin
    | ParryingBuckler
    | SocraticDossier -> SingleTargetDuel

  /// Class association for this preparation
  member this.Class : CharacterClass =
    match this with
    | ShockwaveSlam | BerserkTincture -> CharacterClass.Berserker
    | DreadWarhorn | SynapticBrand -> CharacterClass.Inquisitor
    | CaltropPouch | ConcealedBlade -> CharacterClass.Duelist
    | MirrorMirage | NeuroToxin -> CharacterClass.Mesmer
    | BastionZoneControl | ParryingBuckler -> CharacterClass.Justicar
    | HeraldicTreatise | SocraticDossier -> CharacterClass.Strategist

  /// Human-readable display name
  member this.Name : string =
    match this with
    | ShockwaveSlam -> "Shockwave Slam"
    | BerserkTincture -> "Berserk Tincture"
    | DreadWarhorn -> "Dread Warhorn"
    | SynapticBrand -> "Synaptic Brand"
    | CaltropPouch -> "Caltrop Pouch"
    | ConcealedBlade -> "Concealed Blade"
    | MirrorMirage -> "Mirror Mirage"
    | NeuroToxin -> "NeuroToxin"
    | BastionZoneControl -> "Bastion Zone Control"
    | ParryingBuckler -> "Parrying Buckler"
    | HeraldicTreatise -> "Heraldic Treatise"
    | SocraticDossier -> "Socratic Dossier"

  /// Tactical mechanical description
  member this.Description : string =
    match this with
    | ShockwaveSlam -> "Surplus NetHits (>= 3) spill over as flat damage to all engaged flankers for 5 turns."
    | BerserkTincture -> "Spikes Recklessness into Fever Pitch for bonus dice and damage for 5 turns."
    | DreadWarhorn -> "Dreadful psychic howl inflicts +25 Cognitive Fatigue across active attackers."
    | SynapticBrand -> "Marks target for 5 turns: any critical strike deals 2x Morale damage and inflicts Rupture."
    | CaltropPouch -> "Blinds secondary flankers, stripping their multi-opponent penalty for 5 turns."
    | ConcealedBlade -> "Quick-draw boot blade; readied for 5 turns to interrupt incoming attacks from the Nach."
    | MirrorMirage -> "Phantasmal decoy field for 5 turns; flankers hit illusions, building +20 Confusion and missing turn."
    | NeuroToxin -> "Psychic venom for 5 turns; target suffers escalating Morale drain per point of Recklessness gained."
    | BastionZoneControl -> "Plants polearm/shield for 5 turns; limits simultaneous attackers strictly to 3 (front three tiles)."
    | ParryingBuckler -> "Dedicated off-hand reaction shield for 5 turns; modifies Indes threshold by -1, widening Vor window."
    | HeraldicTreatise -> "Pre-battle tactical notes; immediately grants +2 Study Stacks across all foes."
    | SocraticDossier -> "Exposes contradictions; immediately converts opponent's Recklessness to unmitigated Morale damage."

  /// Retrieves the pair of preparations (1 CrowdControl, 1 SingleTargetDuel) belonging to a class.
  /// Generic NPC classes (Warrior, Assassin, Soldier, Mage) do not possess specialized preparation abilities.
  static member ForClass (cls: CharacterClass) : PreparationType list =
    match cls with
    | CharacterClass.Berserker -> [ ShockwaveSlam; BerserkTincture ]
    | CharacterClass.Inquisitor -> [ DreadWarhorn; SynapticBrand ]
    | CharacterClass.Duelist -> [ CaltropPouch; ConcealedBlade ]
    | CharacterClass.Mesmer -> [ MirrorMirage; NeuroToxin ]
    | CharacterClass.Justicar -> [ BastionZoneControl; ParryingBuckler ]
    | CharacterClass.Strategist -> [ HeraldicTreatise; SocraticDossier ]
    | CharacterClass.Warrior
    | CharacterClass.Assassin
    | CharacterClass.Soldier
    | CharacterClass.Mage -> []

/// Individual preparation slot tracking usage availability
type PreparationSlot = {
  Type: PreparationType
  Category: PreparationCategory
  MaxUses: int
  RemainingUses: int
} with
  static member create (prepType: PreparationType) (maxUses: int) : PreparationSlot =
    { Type = prepType
      Category = prepType.Category
      MaxUses = maxUses
      RemainingUses = maxUses }

/// Character progression profile tracking level and preparation capacity
type ProgressionProfile = {
  Level: int
  CurrentXP: int
  Class: CharacterClass
  PrimaryStat: int
  Preparations: PreparationSlot list
} with
  /// Preparation capacity: strictly limited to 2 uses per encounter
  static member CalculateMaxPrepUses (_primaryStat: int) : int = 2

  /// Creates a standard progression profile with replenished class preparations based on primary stat.
  /// Generic NPC classes do not receive preparation uses or slots.
  static member createWithStat (cls: CharacterClass) (level: int) (primaryStat: int) : ProgressionProfile =
    let maxUses =
      if cls.IsGeneric then 0
      else ProgressionProfile.CalculateMaxPrepUses primaryStat
    let slots =
      PreparationType.ForClass cls
      |> List.map (fun pt -> PreparationSlot.create pt maxUses)
    { Level = Math.Max(1, level)
      CurrentXP = 0
      Class = cls
      PrimaryStat = primaryStat
      Preparations = slots }

  /// Creates profile with estimated primary stat for level
  static member create (cls: CharacterClass) (level: int) : ProgressionProfile =
    let estimatedPrimaryStat = 45 + (Math.Max(1, level) - 1) * 4
    ProgressionProfile.createWithStat cls level estimatedPrimaryStat

  /// Total remaining uses across all equipped preparation slots
  member this.TotalRemainingPrepUses : int =
    this.Preparations |> List.sumBy (fun s -> s.RemainingUses)

  /// Checks if a preparation has available uses remaining
  member this.HasRemainingUses (prepType: PreparationType) : bool =
    this.Preparations
    |> List.tryFind (fun s -> s.Type = prepType)
    |> Option.map (fun s -> s.RemainingUses > 0)
    |> Option.defaultValue false

  /// Spends 1 use of the specified preparation, returning the updated profile if successful
  member this.SpendPreparation (prepType: PreparationType) : ProgressionProfile option =
    match this.Preparations |> List.tryFind (fun s -> s.Type = prepType) with
    | Some slot when slot.RemainingUses > 0 ->
      let updated =
        this.Preparations
        |> List.map (fun s ->
          if s.Type = prepType then
            { s with RemainingUses = Math.Max(0, s.RemainingUses - 1) }
          else s)
      Some { this with Preparations = updated }
    | _ -> None

  /// Replenishes all preparation uses to maximum capacity (e.g. upon complete rest)
  member this.ReplenishAll () : ProgressionProfile =
    let maxUses =
      if this.Class.IsGeneric then 0
      else ProgressionProfile.CalculateMaxPrepUses this.PrimaryStat
    let restored =
      this.Preparations
      |> List.map (fun s -> { s with MaxUses = maxUses; RemainingUses = maxUses })
    { this with Preparations = restored }

/// Represents an ongoing or stance-based preparation active in combat
type ActivePreparation = {
  Type: PreparationType
  TargetId: CombatantId option
  DurationTurns: int
  Parameter: int
} with
  static member create (prepType: PreparationType) (targetIdOpt: CombatantId option) (duration: int) : ActivePreparation =
    { Type = prepType
      TargetId = targetIdOpt
      DurationTurns = duration
      Parameter = 0 }

  member this.IsExpired : bool =
    this.DurationTurns <= 0

  member this.DecrementTurn () : ActivePreparation =
    { this with DurationTurns = Math.Max(0, this.DurationTurns - 1) }
