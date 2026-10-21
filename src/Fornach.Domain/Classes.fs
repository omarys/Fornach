namespace Fornach.Domain

/// Character classes in Fornach: the two core player archetypes, plus generic NPC classes.
type CharacterClass =
  // --- Core Player Archetypes ---
  /// Physical: Fighter with a sword, mastering kinetic momentum, precision parrying, and bastion guard
  | Fighter
  /// Mental: Mage with staff, channeling cognitive resonance, phantasmal static, and protective wards
  | Mage

  // --- Generic NPC Classes (without specialized player preparations) ---
  /// Generic Power NPC class: Kinetic brute, front-line shock troop
  | Warrior
  /// Generic Agility NPC class: Agile skirmisher, silent puncture specialist
  | Rogue
  /// Generic Discipline NPC class: Rank-and-file martial guard, tight line formation
  | Soldier

  /// True if this is a generic non-player character class without specialized preparations
  member this.IsGeneric : bool =
    match this with
    | Warrior | Rogue | Soldier -> true
    | Fighter | Mage -> false

  /// True if this is a playable archetype (see PlayerClasses). The kit may come from preparations,
  /// Complex Forms, or fluid stance transitions rather than preparations alone.
  member this.IsPlayerClass : bool = not this.IsGeneric

  /// The underlying stat Vector (Power, Agility, or Discipline)
  member this.Vector : Vector =
    match this with
    | Fighter | Warrior -> Power
    | Rogue -> Agility
    | Soldier -> Discipline
    | Mage -> Power // Baseline vector; arcane casting dynamically specializes based on stats / fluid forms

  /// The primary governing attribute of this class archetype
  member this.PrimaryStat : StatId =
    match this with
    | Fighter | Warrior -> Force
    | Mage -> Intellect
    | Rogue -> Finesse
    | Soldier -> Prowess

  /// The operative combat Plane (Physical or Mental)
  member this.Plane : Plane =
    match this with
    | Fighter | Rogue | Warrior | Soldier -> Physical
    | Mage -> Mental

  /// Display name of the character class
  member this.Name : string =
    match this with
    | Fighter -> "Fighter"
    | Mage -> "Mage"
    | Warrior -> "Warrior"
    | Rogue -> "Rogue"
    | Soldier -> "Soldier"

  /// Full descriptive title including archetype flavor
  member this.Title : string =
    match this with
    | Fighter -> "Fighter (Martial Master)"
    | Mage -> "Mage (Arcane Practitioner)"
    | Warrior -> "Warrior (Generic Power NPC)"
    | Rogue -> "Rogue (Generic Finesse NPC)"
    | Soldier -> "Soldier (Generic Discipline NPC)"

  /// Narrative description of combat specialization
  member this.Description : string =
    match this with
    | Fighter -> "Master of physical martial combat with sword and shield, seamlessly flowing between kinetic cleaves, fencing cadences, and bastion defense."
    | Mage -> "Master of arcane disciplines with runic staff, seamlessly threading resonance spikes, phantasmal static, and protective abjuration lattices."
    | Warrior -> "Standard brute relying on kinetic Force and physical Fortitude without specialized preparations."
    | Rogue -> "Skirmisher relying on Finesse and Reflex without specialized dueling preparations."
    | Soldier -> "Disciplined rank-and-file fighter maintaining guard and Prowess without specialized preparations."

  /// Core simplified player class archetypes
  static member PlayerClasses = [ Fighter; Mage ]

  /// All generic NPC classes
  static member GenericClasses = [ Warrior; Rogue; Soldier ]

  /// All character classes
  static member All = [ Fighter; Mage; Warrior; Rogue; Soldier ]
