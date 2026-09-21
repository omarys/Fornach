namespace Fornach.Engine

open Fornach.Domain

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
  /// Arcane Discipline (Acumen vs. Composure): Reactive abjuration wards and runic glyphs inflicting Provoke and stance traps
  | RunicWardTrap of isAnomalousGlyph: bool

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
    | RunicWardTrap _ -> CombatMode.Arcane

  /// Identifies the underlying stat Vector (Power, Agility, or Discipline)
  member this.Vector : Vector =
    match this with
    | ForceStrike _
    | AuthorityDecree _
    | ArcaneCataclysm _ -> Power
    | FinesseCadence _
    | GuileDeception _
    | SynapticGlamour _ -> Agility
    | ProwessStrike _
    | CalculatedFlawStrike _
    | MasterfulDisarm _
    | AcumenInterrogation _
    | RunicWardTrap _ -> Discipline

  /// Identifies the target Plane (Physical or Mental)
  member this.Plane : Plane =
    match this with
    | ForceStrike _
    | FinesseCadence _
    | ProwessStrike _
    | CalculatedFlawStrike _
    | MasterfulDisarm _ -> Physical
    | AuthorityDecree _
    | GuileDeception _
    | AcumenInterrogation _
    | ArcaneCataclysm _
    | SynapticGlamour _
    | RunicWardTrap _ -> Mental

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
