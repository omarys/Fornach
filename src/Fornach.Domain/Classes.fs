namespace Fornach.Domain

/// Character classes in Fornach, encompassing specialized player archetypes
/// and generic non-player character (NPC) classes.
type CharacterClass =
  // --- Specialized Player Archetypes (with Tactical Preparations) ---
  // Power Vector (Force & Intellect)
  /// Physical: Power Primary (1.0 : 0.75 : 0.75), relentless kinetic momentum, cleaves
  | Berserker
  /// Mental: Dread Arcanist/Psychic, raw cognitive dominance, mind-shock
  | Inquisitor

  // Agility Vector (Finesse & Acuity)
  /// Physical: Agility Primary (1.0 : 0.75 : 0.75), technical precision, parry/riposte
  | Duelist
  /// Mental: Phantasmist/Illusionist, sensory static, feedback traps, mirror decoy swaps
  | Mesmer

  // Discipline Vector (Prowess & Acumen)
  /// Physical: Discipline Primary (1.0 : 0.75 : 0.75), heavy bastion polearm, zone control
  | Warden
  /// Physical: Discipline Primary (legacy alias for Warden)
  | Justicar
  /// Mental: Abjuration/Wards, composure-woven barriers, destabilizing ground runes
  | Abjurer
  /// Mental: Dialectician/Judge (legacy alias for Abjurer)
  | Strategist

  // --- Generic NPC Classes (without specialized player preparations) ---
  /// Generic Power NPC class: Kinetic brute, front-line shock troop
  | Warrior
  /// Generic Agility NPC class: Agile skirmisher, silent puncture specialist
  | Rogue
  /// Generic Discipline NPC class: Rank-and-file martial guard, tight line formation
  | Soldier
  /// Generic Magic NPC class: Arcane channeler across elemental/psychic vectors
  | Mage

  /// True if this is a generic non-player character class without specialized preparations
  member this.IsGeneric : bool =
    match this with
    | Warrior | Rogue | Soldier | Mage -> true
    | Berserker | Inquisitor | Duelist | Mesmer | Warden | Justicar | Abjurer | Strategist -> false

  /// True if this is a specialized player archetype equipped with tactical preparations
  member this.IsPlayerClass : bool = not this.IsGeneric

  /// The underlying stat Vector (Power, Agility, or Discipline)
  member this.Vector : Vector =
    match this with
    | Berserker | Inquisitor | Warrior -> Power
    | Duelist | Rogue | Mesmer -> Agility
    | Warden | Justicar | Abjurer | Strategist | Soldier -> Discipline
    | Mage -> Power // Baseline vector; arcane casting dynamically specializes based on stats

  /// The primary governing attribute of this class archetype
  member this.PrimaryStat : StatId =
    match this with
    | Berserker | Warrior -> Force
    | Inquisitor | Mage -> Intellect
    | Duelist | Rogue -> Finesse
    | Mesmer -> Acuity
    | Warden | Justicar | Soldier -> Prowess
    | Abjurer | Strategist -> Acumen

  /// The operative combat Plane (Physical or Mental)
  member this.Plane : Plane =
    match this with
    | Berserker | Duelist | Rogue | Warden | Justicar | Warrior | Soldier -> Physical
    | Inquisitor | Mesmer | Abjurer | Strategist | Mage -> Mental

  /// Display name of the character class
  member this.Name : string =
    match this with
    | Berserker -> "Berserker"
    | Inquisitor -> "Inquisitor"
    | Duelist -> "Duelist"
    | Mesmer -> "Mesmer"
    | Warden -> "Warden"
    | Justicar -> "Justicar"
    | Abjurer -> "Abjurer"
    | Strategist -> "Abjurer"
    | Warrior -> "Warrior"
    | Rogue -> "Rogue"
    | Soldier -> "Soldier"
    | Mage -> "Mage"

  /// Full descriptive title including archetype flavor
  member this.Title : string =
    match this with
    | Berserker -> "Berserker (Kinetic Slayer)"
    | Inquisitor -> "Inquisitor (Dread Arcanist)"
    | Duelist -> "Duelist (Master Fencer)"
    | Mesmer -> "Mesmer (Phantasmist)"
    | Warden -> "Warden (Bastion Knight)"
    | Justicar -> "Justicar (Knight Warder)"
    | Abjurer
    | Strategist -> "Abjurer (Runic Warder)"
    | Warrior -> "Warrior (Generic Power NPC)"
    | Rogue -> "Rogue (Generic Finesse NPC)"
    | Soldier -> "Soldier (Generic Discipline NPC)"
    | Mage -> "Mage (Generic Arcane NPC)"

  /// Narrative description of combat specialization
  member this.Description : string =
    match this with
    | Berserker -> "Massive kinetic momentum, devastating cleaves, and reckless wild follow-through."
    | Inquisitor -> "Raw cognitive dominance, imposing psychic presence, and synaptic mind-shocks."
    | Duelist -> "Rapid probing cadences, technical parries, and lethal precision punctures."
    | Mesmer -> "Intuitive mirror clones, sensory static, and acuteness-based phantasmal decoy swaps."
    | Warden -> "Impenetrable bastion guard, zone control polearms, and unyielding line defense."
    | Justicar -> "Defensive bastion tactics and precision ripostes."
    | Abjurer
    | Strategist -> "Composure-woven wards, destabilizing tactical ground runes, and protective abjuration barriers."
    | Warrior -> "Standard brute relying on kinetic Force and physical Fortitude without specialized preparations."
    | Rogue -> "Skirmisher relying on Finesse and Reflex without specialized dueling preparations."
    | Soldier -> "Disciplined rank-and-file fighter maintaining guard and Prowess without specialized preparations."
    | Mage -> "Generic arcane practitioner channeling mental energies without specialized preparation assets."

  /// All specialized player class archetypes
  static member PlayerClasses = [ Berserker; Inquisitor; Duelist; Mesmer; Warden; Justicar; Abjurer ]

  /// All generic NPC classes
  static member GenericClasses = [ Warrior; Rogue; Soldier; Mage ]

  /// All character classes
  static member All = CharacterClass.PlayerClasses @ CharacterClass.GenericClasses
