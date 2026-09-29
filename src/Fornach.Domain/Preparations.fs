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
  /// Duel: Blinding burst of prismatic fireworks; target takes acute Morale shock and +20 Confusion for every point of Recklessness gained
  | PrismaticFlare

  // --- Justicar (Discipline / Physical) ---
  /// Crowd: Plants polearm/shield; limits simultaneous attackers strictly to 3 (front 3 tiles)
  | BastionZoneControl
  /// Duel: Dedicated off-hand reaction die pool, expands Indes trigger window by +1 hit
  | ParryingBuckler

  // --- Abjurer (Discipline / Mental) ---
  /// Crowd/Buff: Runic sanctuary barrier applied to self or ally; reduces all damage taken by 35% and reflects 50% back to attackers as radiant retribution damage and +15 Frustration
  | AegisOfRetribution
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
    | AegisOfRetribution
    | HeraldicTreatise -> CrowdControl
    | BerserkTincture
    | SynapticBrand
    | ConcealedBlade
    | PrismaticFlare
    | ParryingBuckler
    | SocraticDossier -> SingleTargetDuel

  /// Class association for this preparation
  member this.Class : CharacterClass =
    match this with
    | ShockwaveSlam | BerserkTincture -> CharacterClass.Berserker
    | DreadWarhorn | SynapticBrand -> CharacterClass.Inquisitor
    | CaltropPouch | ConcealedBlade -> CharacterClass.Duelist
    | MirrorMirage | PrismaticFlare -> CharacterClass.Mesmer
    | BastionZoneControl | ParryingBuckler -> CharacterClass.Warden
    | AegisOfRetribution | HeraldicTreatise | SocraticDossier -> CharacterClass.Abjurer

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
    | PrismaticFlare -> "Prismatic Flare"
    | BastionZoneControl -> "Bastion Zone Control"
    | ParryingBuckler -> "Parrying Buckler"
    | AegisOfRetribution -> "Aegis of Retribution"
    | HeraldicTreatise -> "Heraldic Treatise"
    | SocraticDossier -> "Socratic Dossier"

  /// Tactical mechanical description
  member this.Description : string =
    match this with
    | ShockwaveSlam -> "Surplus NetHits (>= 3) spill over as flat damage to all engaged flankers (duration scales with level/primary stat)."
    | BerserkTincture -> "Spikes Recklessness into Fever Pitch for bonus dice and damage (duration scales with level/primary stat)."
    | DreadWarhorn -> "Dreadful psychic howl inflicts +25 Cognitive Fatigue across active attackers."
    | SynapticBrand -> "Marks target: any critical strike deals 2x Morale damage and inflicts Rupture (duration scales with level/primary stat)."
    | CaltropPouch -> "Blinds secondary flankers, stripping their multi-opponent penalty (duration scales with level/primary stat; base 2 turns)."
    | ConcealedBlade -> "Quick-draw boot blade; readied to interrupt incoming attacks from the Nach (duration scales with level/primary stat)."
    | MirrorMirage -> "Phantasmal decoy field; flankers hit illusions, building +20 Confusion and missing turn (duration scales with level/primary stat)."
    | PrismaticFlare -> "Blinding burst of prismatic fireworks; target suffers acute Morale shock and +20 Confusion per point of Recklessness gained (duration scales with level/primary stat)."
    | BastionZoneControl -> "Plants polearm/shield; limits simultaneous attackers strictly to 3 (front three tiles; duration scales with level/primary stat)."
    | ParryingBuckler -> "Dedicated off-hand reaction shield; modifies Indes threshold by -1, widening Vor window (duration scales with level/primary stat)."
    | AegisOfRetribution -> "Runic sanctuary barrier applied to self or an ally: reduces incoming damage taken by 35% and reflects 50% back to attackers as radiant retribution damage and +15 Frustration (duration scales with level/primary stat)."
    | HeraldicTreatise -> "Pre-battle tactical notes; immediately grants +2 Study Stacks across all foes."
    | SocraticDossier -> "Exposes contradictions; immediately converts opponent's Recklessness to unmitigated Morale damage."

  /// Base duration in turns for tactical preparations at Level 1 (Novice).
  /// Instantaneous preparations return 0.
  member this.BaseDuration : int =
    match this with
    | CaltropPouch -> 2
    | BastionZoneControl -> 3
    | ShockwaveSlam
    | BerserkTincture
    | SynapticBrand
    | ConcealedBlade
    | MirrorMirage
    | PrismaticFlare
    | ParryingBuckler
    | AegisOfRetribution -> 3
    | DreadWarhorn
    | HeraldicTreatise
    | SocraticDossier -> 0

  /// Calculates scaled duration in turns based on character level and/or primary stat points.
  /// Operational window extends with character level and primary stat investment.
  member this.CalculateDuration (level: int) (primaryStat: int) : int =
    if this.BaseDuration <= 0 then 0
    else
      let lvl = Math.Max(1, level)
      match this with
      | CaltropPouch ->
        // Caltrops scale steadily: +1 turn per 40 levels / 160 primary stat points
        let bonus = Math.Max((lvl - 1) / 40, Math.Max(0, (primaryStat - 40) / 160))
        Math.Max(2, this.BaseDuration + bonus)
      | BastionZoneControl ->
        // Bastion zone control scales strongly: disciplined defenders hold chokepoints against swarms
        let bonus = Math.Max((lvl - 1) / 13, Math.Max(0, (primaryStat - 40) / 53))
        Math.Max(3, this.BaseDuration + bonus)
      | _ ->
        // Standard combat preparations:
        let bonus = Math.Max((lvl - 1) / 20, Math.Max(0, (primaryStat - 40) / 80))
        Math.Max(this.BaseDuration, this.BaseDuration + bonus)

  /// Retrieves the pair of preparations (1 CrowdControl, 1 SingleTargetDuel) belonging to a class.
  /// Generic NPC classes (Warrior, Assassin, Soldier, Mage) do not possess specialized preparation abilities.
  static member ForClass (cls: CharacterClass) : PreparationType list =
    match cls with
    | CharacterClass.Berserker -> [ ShockwaveSlam; BerserkTincture ]
    | CharacterClass.Juggernaut -> [ ShockwaveSlam; ParryingBuckler ]
    | CharacterClass.Inquisitor -> [ DreadWarhorn; SynapticBrand ]
    | CharacterClass.Duelist -> [ CaltropPouch; ConcealedBlade ]
    | CharacterClass.Assassin -> [ CaltropPouch; ConcealedBlade ]
    | CharacterClass.Mesmer -> [ MirrorMirage; PrismaticFlare ]
    | CharacterClass.Warden
    | CharacterClass.Justicar -> [ BastionZoneControl; ParryingBuckler ]
    | CharacterClass.Ranger -> [ CaltropPouch; ParryingBuckler ]
    | CharacterClass.Abjurer -> [ AegisOfRetribution; SocraticDossier ]
    | CharacterClass.Strategist -> [ HeraldicTreatise; SocraticDossier ]
    | CharacterClass.Warrior
    | CharacterClass.Rogue
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

  static member createScaled (prepType: PreparationType) (targetIdOpt: CombatantId option) (level: int) (primaryStat: int) : ActivePreparation =
    let duration = prepType.CalculateDuration level primaryStat
    { Type = prepType
      TargetId = targetIdOpt
      DurationTurns = duration
      Parameter = 0 }

  member this.IsExpired : bool =
    this.DurationTurns <= 0

  member this.DecrementTurn () : ActivePreparation =
    { this with DurationTurns = Math.Max(0, this.DurationTurns - 1) }
