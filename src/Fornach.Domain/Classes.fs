namespace Fornach.Domain

/// Character classes in Fornach, encompassing specialized player archetypes
/// and generic non-player character (NPC) classes.
type CharacterClass =
  // --- Specialized Player Archetypes (with Tactical Preparations) ---
  // Power Vector (Force & Intellect)
  /// Physical: Juggernaut/Golem, massive kinetic momentum, cleaves
  | Titan
  /// Mental: Dread Arcanist/Psychic, raw cognitive dominance, mind-shock
  | Inquisitor

  // Agility Vector (Finesse & Acuity)
  /// Physical: Skirmisher/Rapier specialist, vital punctures, evasion
  | Duelist
  /// Mental: Phantasmist/Illusionist, sensory static, feedback traps
  | Mesmer

  // Discipline Vector (Prowess & Acumen)
  /// Physical: Knight/Warder, polearm mastery, tight guard, bastion
  | Justicar
  /// Mental: Dialectician/Judge, Socratic traps, composure breakdowns
  | Strategist

  // --- Generic NPC Classes (without specialized player preparations) ---
  /// Generic Power NPC class: Kinetic brute, front-line shock troop
  | Warrior
  /// Generic Finesse NPC class: Agile skirmisher, silent puncture specialist
  | Assassin
  /// Generic Discipline NPC class: Rank-and-file martial guard, tight line formation
  | Soldier
  /// Generic Magic NPC class: Arcane channeler across elemental/psychic vectors
  | Mage

  /// True if this is a generic non-player character class without specialized preparations
  member this.IsGeneric : bool =
    match this with
    | Warrior | Assassin | Soldier | Mage -> true
    | Titan | Inquisitor | Duelist | Mesmer | Justicar | Strategist -> false

  /// True if this is a specialized player archetype equipped with tactical preparations
  member this.IsPlayerClass : bool = not this.IsGeneric

  /// The underlying stat Vector (Power, Agility, or Discipline)
  member this.Vector : Vector =
    match this with
    | Titan | Inquisitor | Warrior -> Power
    | Duelist | Mesmer | Assassin -> Agility
    | Justicar | Strategist | Soldier -> Discipline
    | Mage -> Power // Baseline vector; arcane casting dynamically specializes based on stats

  /// The operative combat Plane (Physical or Mental)
  member this.Plane : Plane =
    match this with
    | Titan | Duelist | Justicar | Warrior | Assassin | Soldier -> Physical
    | Inquisitor | Mesmer | Strategist | Mage -> Mental

  /// Display name of the character class
  member this.Name : string =
    match this with
    | Titan -> "Titan"
    | Inquisitor -> "Inquisitor"
    | Duelist -> "Duelist"
    | Mesmer -> "Mesmer"
    | Justicar -> "Justicar"
    | Strategist -> "Strategist"
    | Warrior -> "Warrior"
    | Assassin -> "Assassin"
    | Soldier -> "Soldier"
    | Mage -> "Mage"

  /// Full descriptive title including archetype flavor
  member this.Title : string =
    match this with
    | Titan -> "Titan (Kinetic Juggernaut)"
    | Inquisitor -> "Inquisitor (Dread Arcanist)"
    | Duelist -> "Duelist (Vital Skirmisher)"
    | Mesmer -> "Mesmer (Phantasmist)"
    | Justicar -> "Justicar (Knight Warder)"
    | Strategist -> "Strategist (Dialectician)"
    | Warrior -> "Warrior (Generic Power NPC)"
    | Assassin -> "Assassin (Generic Finesse NPC)"
    | Soldier -> "Soldier (Generic Discipline NPC)"
    | Mage -> "Mage (Generic Arcane NPC)"

  /// Narrative description of combat specialization
  member this.Description : string =
    match this with
    | Titan -> "Massive kinetic momentum, devastating cleaves, and reckless fever pitch."
    | Inquisitor -> "Raw cognitive dominance, psychic warhorns, and synaptic branding."
    | Duelist -> "Rapid probing cadences, vital punctures, and quick-draw concealed counters."
    | Mesmer -> "Sensory static, feedback traps, phantasmal mirages, and psychic neurotoxins."
    | Justicar -> "Impenetrable bastion guard, zone control polearms, and precision parrying."
    | Strategist -> "Dialectical traps, heraldic treatises, and Socratic exposure of hypocrisy."
    | Warrior -> "Standard brute relying on kinetic Force and physical Fortitude without specialized preparations."
    | Assassin -> "Skirmisher relying on Finesse and Reflex without specialized dueling preparations."
    | Soldier -> "Disciplined rank-and-file fighter maintaining guard and Prowess without specialized preparations."
    | Mage -> "Generic arcane practitioner channeling mental energies without specialized preparation assets."

  /// All 6 specialized player class archetypes
  static member PlayerClasses = [ Titan; Inquisitor; Duelist; Mesmer; Justicar; Strategist ]

  /// All 4 generic NPC classes
  static member GenericClasses = [ Warrior; Assassin; Soldier; Mage ]

  /// All 10 character classes
  static member All = CharacterClass.PlayerClasses @ CharacterClass.GenericClasses
