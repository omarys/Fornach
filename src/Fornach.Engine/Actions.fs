namespace Fornach.Engine

open Fornach.Domain

/// Psychological Trauma Gambits and specialized boss manifestations
type TraumaGambit =
  /// Denial (Agility/Mental): Asserts reality never happened; conjures a mirror clone, evades, and inflicts Confusion
  | DenialPhaseShift
  /// Anger (Power/Physical): Volcanic fury that smashes molten basalt into the target, shredding armor and inflicting Overwhelm
  | BasaltEruption
  /// Bargaining (Discipline/Mental): A predatory transaction stealing Morale to restore vitality, inflicting Provoke and Frustration
  | CoerciveBargain
  /// Depression (Discipline/Mental): The crushing weight of apathy dragging the spirit under; inflicts deep Cognitive Fatigue and Exhaustion
  | ApathyDoldrums
  /// Acceptance (Discipline/Mental): Peaceful surrender of hostility; calms Recklessness to 0 and restores Morale to both combatants
  | SereneResolution

/// Classifies offensive actions across Physical, Social, and Arcane disciplines
type AttackClassification =
  // -------------------------------------------------------------------------
  // 1. Physical Attacks (Power, Agility, Martial Discipline)
  // -------------------------------------------------------------------------
  /// Power (Force vs. Fortitude): Heavy cleave or wild smash causing Exhaustion & Stagger
  | ForceStrike of isWildBlow: bool
  /// Agility (Finesse vs. Reflex): Rapid cadence seeking openings to inflict Overwhelm & Hemorrhage
  | FinesseCadence of isRelentlessCadence: bool
  /// Martial Discipline (Prowess vs. Poise): Stance pressure and tactical disarms causing Frustration
  | ProwessStrike of isInvitationalBait: bool
  /// Dedicated Discipline Gambit: Precision flaw exploitation consuming Study Stacks (0 Recklessness)
  | CalculatedFlawStrike of stacksToSpend: int
  /// Dedicated Discipline Gambit: Tactical disarm requiring Study Stacks scaled by target Poise vs. Prowess
  | MasterfulDisarm of stacksToSpend: int

  // -------------------------------------------------------------------------
  // 2. Social Attacks (Mental Plane - Interpersonal / Courtroom)
  // -------------------------------------------------------------------------
  /// Power (Presence/Intellect vs. Resolve): Intimidation, commands, and dominance causing Cognitive Fatigue
  | AuthorityDecree of isImperiousDemand: bool
  /// Agility (Guile/Acuity vs. Intuition): Rhetorical misdirection, bluffs, and gaslighting causing Confusion
  | GuileDeception of isConfidenceTrap: bool
  /// Social Discipline (Leverage/Acumen vs. Composure): Procedural traps, hypocrisy exposure, and mockery causing Provoke
  | AcumenInterrogation of isSocraticCheckmate: bool

  // -------------------------------------------------------------------------
  // 3. Arcane & Psionic Attacks (Mental Plane - Spellcraft)
  // -------------------------------------------------------------------------
  /// Power (Intellect vs. Resolve): Overchanneling raw psychic/elemental energy causing deep Fatigue and somatic bleed
  | ArcaneCataclysm of isOverchannel: bool
  /// Agility (Acuity vs. Intuition): Blinding sensory glamours and neural static causing Confusion and Paralysis
  | SynapticGlamour of isMindFracture: bool
  /// Agility (Acuity vs. Intuition): Mirror illusions and decoy trickery creating clone decoys and misdirection
  | MirrorIllusion of isDecoySwarm: bool
  /// Arcane Discipline (Acumen vs. Composure): Reactive abjuration wards and runic glyphs inflicting Provoke and stance traps
  | RunicWardTrap of isAnomalousGlyph: bool
  /// Arcane Discipline (Acumen vs. Composure): Disorienting shockwave that disrupts enemy balance and stance tempo
  | DisorientingShockwave of isStaggeringPulse: bool

  // -------------------------------------------------------------------------
  // 4. Psychological Trauma Gambits (Boss / Manifestation Mechanics)
  // -------------------------------------------------------------------------
  /// Specialized psychological trauma mechanics mapping the 5 stages of grief
  | TraumaAttack of TraumaGambit

  /// Identifies the CombatMode (Physical, Social, or Arcane)
  member this.Mode : CombatMode =
    match this with
    | ForceStrike _
    | FinesseCadence _
    | ProwessStrike _
    | CalculatedFlawStrike _
    | MasterfulDisarm _ -> CombatMode.Physical
    | AuthorityDecree _
    | GuileDeception _
    | AcumenInterrogation _ -> CombatMode.Social
    | ArcaneCataclysm _
    | SynapticGlamour _
    | MirrorIllusion _
    | RunicWardTrap _
    | DisorientingShockwave _ -> CombatMode.Arcane
    | TraumaAttack BasaltEruption -> CombatMode.Physical
    | TraumaAttack (CoerciveBargain | ApathyDoldrums | SereneResolution) -> CombatMode.Social
    | TraumaAttack DenialPhaseShift -> CombatMode.Arcane

  /// Identifies the underlying stat Vector (Power, Agility, or Discipline)
  member this.Vector : Vector =
    match this with
    | ForceStrike _
    | AuthorityDecree _
    | ArcaneCataclysm _
    | TraumaAttack BasaltEruption -> Power
    | FinesseCadence _
    | GuileDeception _
    | SynapticGlamour _
    | MirrorIllusion _
    | TraumaAttack DenialPhaseShift -> Agility
    | ProwessStrike _
    | CalculatedFlawStrike _
    | MasterfulDisarm _
    | AcumenInterrogation _
    | RunicWardTrap _
    | DisorientingShockwave _
    | TraumaAttack (CoerciveBargain | ApathyDoldrums | SereneResolution) -> Discipline

  /// Identifies the target Plane (Physical or Mental)
  member this.Plane : Plane =
    match this with
    | ForceStrike _
    | FinesseCadence _
    | ProwessStrike _
    | CalculatedFlawStrike _
    | MasterfulDisarm _
    | TraumaAttack BasaltEruption -> Physical
    | AuthorityDecree _
    | GuileDeception _
    | AcumenInterrogation _
    | ArcaneCataclysm _
    | SynapticGlamour _
    | MirrorIllusion _
    | RunicWardTrap _
    | DisorientingShockwave _
    | TraumaAttack (DenialPhaseShift | CoerciveBargain | ApathyDoldrums | SereneResolution) -> Mental

  /// Verifies if this attack classification is permitted for the combatant's operative plane
  member this.IsAllowedFor (actor: Combatant) : bool =
    match this with
    | TraumaAttack _ -> true
    | _ ->
      match actor.Plane with
      | Physical -> this.Plane = Physical
      | Mental -> this.Plane = Mental

/// Defensive recoveries used to bleed accumulated Recklessness and re-center posture
type DefensiveReset =
  /// Physical reset: uses Poise to bleed Recklessness and generates Study Stacks
  | SteadyForm
  /// Mental/Social reset: uses Composure to bleed Recklessness, clears Confusion, and generates Insight
  | CenterMind

/// Top-level intent dispatched to the resolution engine each turn
type ActionIntent =
  /// An offensive strike, gambit, or incantation
  | StandardAttack of AttackClassification
  /// A defensive stabilization or posture recovery
  | RecoveryAction of DefensiveReset
  /// An execution attempt against an opponent currently in a Collapsed state
  | ExecuteStrike of Plane
  /// Shifting active tactical stance (Power, Agility, Discipline)
  | ShiftStance of CombatStance
  /// Threading or shifting active mental Complex Form (Resonance Spike, Phantasmal Diffusion, Aegis Lattice)
  | ThreadComplexForm of ComplexForm
  /// Deploying a class-specific tactical preparation asset
  | DeployPreparation of preparation: PreparationType * targetId: CombatantId option

/// Result payload emitted after an ActionIntent is fully evaluated
type ActionResult =
  { Actor: Combatant
    Target: Combatant
    Events: CombatEvent list
    Contest: ContestResult option }

/// Result payload emitted after a group-aware ActionIntent is evaluated across multiple opponents
type GroupActionResult =
  { Actor: Combatant
    PrimaryTarget: Combatant
    SecondaryTargets: Combatant list
    Events: CombatEvent list }
