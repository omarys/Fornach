namespace Fornach.Domain

open System

/// Standard progression and combat mastery tiers
type CombatTier =
  /// Level 1: Base starting stats and entry-level abilities
  | Novice
  /// Level 40: Seasoned veteran representing roughly 40 levels of progression
  | Veteran
  /// Level 100: Paragon master representing roughly 100 levels of progression
  | Master
  /// Level 200: Ascendant grandmaster representing roughly 200 levels of progression
  | GrandMaster

module ProgressionScale =

  /// Resolves the canonical character level for a combat tier
  let tierToLevel (tier: CombatTier) : int =
    match tier with
    | Novice -> 1
    | Veteran -> 40
    | Master -> 100
    | GrandMaster -> 200

  /// Resolves the mastery tier for an arbitrary (uncapped) character level.
  let levelToTier (level: int) : CombatTier =
    if level >= 200 then GrandMaster
    elif level >= 100 then Master
    elif level >= 40 then Veteran
    else Novice

  /// Computes (primary, secondary, tertiary, minor, offDefensive, offOffensive) for physical classes based on 1.0 : 0.75 : 0.75 ratio
  let physicalStatsForLevel (level: int) : int * int * int * int * int * int =
    let l = Math.Max(1, level)
    let delta = l - 1
    let prim = 45 + delta * 4
    let sec = int (Math.Round(float prim * 0.75))
    let tert = int (Math.Round(float prim * 0.50))
    let min = 15 + int (Math.Round(float delta * 1.0))
    // Defensive off-plane stat gets solid boost (tert) to prevent severe roll blowouts
    let offDef = tert
    // Offensive off-plane stat gets modest growth (halfway between min and tert)
    let offOff = min + int (Math.Round(float (tert - min) * 0.50))
    (prim, sec, tert, min, offDef, offOff)

  /// Computes (primary, secondary, tertiary, minor, offPoise) for mental/magic classes based on 1.0 : 0.75 : 0.75 utility ratio
  let magicStatsForLevel (level: int) : int * int * int * int * int =
    let l = Math.Max(1, level)
    let delta = l - 1
    // Magic utility ratio (756 at GM) balanced across all 3 mental vectors
    let prim = 40 + int (Math.Round(float delta * 3.6))
    let sec = int (Math.Round(float prim * 0.75))
    let tert = int (Math.Round(float prim * 0.50))
    let min = 15 + int (Math.Round(float delta * 1.0))
    // Poise boost (physical defensive discipline) to maintain stance/casting concentration
    let offPoise = min + int (Math.Round(float delta * 1.5))
    (prim, sec, tert, min, offPoise)

  /// Legacy helper for general stat lookups
  let statsForLevel (level: int) : int * int * int =
    let p, s, _, m, _, _ = physicalStatsForLevel level
    (p, s, m)

  /// Computes (health, morale, armor) for a given plane and uncapped character level.
  let poolsForLevel (plane: Plane) (level: int) : int * int * int =
    let l = Math.Max(1, level)
    let delta = l - 1
    if plane = Physical then
      let hp = 750 + delta * 75
      let morale = 600 + delta * 50
      let armor = 30 + int (Math.Round(float delta * 1.5))
      (hp, morale, armor)
    else
      // Magic users have slightly less physical HP but higher Morale and balanced wards
      let hp = 600 + delta * 45
      let morale = 750 + delta * 80
      let armor = 20 + int (Math.Round(float delta * 1.0))
      (hp, morale, armor)

module TierFactory =

  /// Creates a fully equipped combatant for a designated class archetype and exact level
  let createClassLevel (cls: CharacterClass) (level: int) : Combatant =
    let id = CombatantId.New()
    let health, baseMorale, baseArmor = ProgressionScale.poolsForLevel cls.Plane level
    let morale, armor =
      match cls with
      | CharacterClass.Warden
      | CharacterClass.Justicar ->
        int (Math.Round(float baseMorale * 1.15)),
        int (Math.Round(float baseArmor * 1.15))
      | _ -> baseMorale, baseArmor

    let statMap =
      if cls.Plane = Physical then
        let prim, sec, tert, min, offDef, offOff = ProgressionScale.physicalStatsForLevel level
        match cls with
        | CharacterClass.Berserker ->
          // Power Primary (1.0 : 0.75 : 0.75): Power 1.0, Agility 0.75, Discipline 0.75
          // Power Archetype: Composure (defensive discipline), Acumen (tactical reading), and Resolve (mental grit) boosted
          [ Force, prim; Fortitude, prim
            Finesse, sec; Reflex, sec
            Prowess, sec; Poise, sec
            Intellect, min; Resolve, offDef
            Acuity, min; Intuition, min
            Acumen, offOff; Composure, offDef ]
        | CharacterClass.Duelist ->
          // Agility Primary (1.0 : 0.75 : 0.75): Agility 1.0, Discipline 0.75, Power 0.75
          // Agility Archetype: Intuition (defensive awareness), Acuity (offensive precision), and Resolve boosted
          [ Finesse, prim; Reflex, prim
            Prowess, sec; Poise, sec
            Force, sec; Fortitude, sec
            Intellect, min; Resolve, offDef
            Acuity, offOff; Intuition, offDef
            Acumen, min; Composure, min ]
        | CharacterClass.Warden
        | CharacterClass.Justicar ->
          // Discipline Primary (1.0 : 0.75 : 0.75): Discipline 1.0, Power 0.75, Agility 0.75
          // Discipline Archetype: Intuition (defensive awareness), Resolve (mental fortitude), and Composure boosted
          [ Prowess, prim; Poise, prim
            Force, sec; Fortitude, sec
            Finesse, sec; Reflex, sec
            Intellect, min; Resolve, offDef
            Acuity, min; Intuition, offDef
            Acumen, min; Composure, offDef ]
        | CharacterClass.Rogue ->
          // Generic Agility NPC
          [ Finesse, prim; Reflex, prim
            Prowess, sec; Poise, sec
            Force, sec; Fortitude, sec
            Intellect, min; Resolve, offDef
            Acuity, offOff; Intuition, offDef
            Acumen, min; Composure, min ]
        | CharacterClass.Warrior ->
          // Generic Power NPC
          [ Force, prim; Fortitude, prim
            Prowess, sec; Poise, sec
            Finesse, sec; Reflex, sec
            Intellect, min; Resolve, offDef
            Acuity, min; Intuition, min
            Acumen, offOff; Composure, offDef ]
        | CharacterClass.Soldier ->
          // Generic Discipline NPC
          [ Prowess, prim; Poise, prim
            Force, sec; Fortitude, sec
            Finesse, sec; Reflex, sec
            Intellect, min; Resolve, offDef
            Acuity, min; Intuition, offDef
            Acumen, min; Composure, offDef ]
        | _ ->
          [ Force, prim; Fortitude, prim
            Finesse, sec; Reflex, sec
            Prowess, sec; Poise, sec
            Intellect, min; Resolve, min
            Acuity, min; Intuition, min
            Acumen, min; Composure, min ]
      else
        // Magic / Mental Plane: 1.0 : 0.75 : 0.75 across Mental Vectors with Poise grounding and tertiary physical defense
        let prim, sec, tert, min, offPoise = ProgressionScale.magicStatsForLevel level
        let offPhys = tert
        match cls with
        | CharacterClass.Inquisitor
        | CharacterClass.Mage ->
          // Mental Power Primary (Intellect/Resolve), Agility & Discipline Secondary
          // Physical: Fortitude boosted to tert for physical armor soak
          [ Intellect, prim; Resolve, prim
            Acuity, sec; Intuition, sec
            Acumen, sec; Composure, sec
            Force, min; Fortitude, offPhys
            Finesse, min; Reflex, min
            Prowess, min; Poise, offPoise ]
        | CharacterClass.Mesmer ->
          // Mental Agility Primary (Acuity/Intuition), Power & Discipline Secondary
          // Physical: Reflex boosted to tert for evasive agility
          [ Acuity, prim; Intuition, prim
            Intellect, sec; Resolve, sec
            Acumen, sec; Composure, sec
            Force, min; Fortitude, min
            Finesse, min; Reflex, offPhys
            Prowess, min; Poise, offPoise ]
        | CharacterClass.Abjurer
        | CharacterClass.Strategist ->
          // Mental Discipline Primary (Acumen/Composure), Power & Agility Secondary
          // Physical: Fortitude & Reflex boosted to tert for fortified arcane tanking
          [ Acumen, prim; Composure, prim
            Intellect, sec; Resolve, sec
            Acuity, sec; Intuition, sec
            Force, min; Fortitude, offPhys
            Finesse, min; Reflex, offPhys
            Prowess, min; Poise, offPoise ]
        | _ ->
          [ Intellect, prim; Resolve, prim
            Acuity, sec; Intuition, sec
            Acumen, sec; Composure, sec
            Force, min; Fortitude, offPhys
            Finesse, min; Reflex, min
            Prowess, min; Poise, offPoise ]

    let stats = StatBlock.Create statMap
    let name = sprintf "Lv.%d %s" level cls.Name
    let stance =
      match cls with
      | CharacterClass.Berserker
      | CharacterClass.Warrior
      | CharacterClass.Inquisitor
      | CharacterClass.Mage -> CombatStance.PowerStance
      | CharacterClass.Duelist
      | CharacterClass.Rogue
      | CharacterClass.Mesmer -> CombatStance.AgilityStance
      | CharacterClass.Warden
      | CharacterClass.Justicar
      | CharacterClass.Soldier
      | CharacterClass.Abjurer
      | CharacterClass.Strategist -> CombatStance.DisciplineStance

    { Combatant.createWithClass id name health morale stats cls level with
        Armor = ArmorIntegrity.Create armor
        Stance = stance }

  /// Creates a fully equipped combatant for a designated class archetype and combat tier
  let createClassTier (cls: CharacterClass) (tier: CombatTier) : Combatant =
    let level = ProgressionScale.tierToLevel tier
    let fighter = createClassLevel cls level
    let tierName = sprintf "%s %s" (tier.ToString()) cls.Name
    { fighter with Name = tierName }

  /// Rescales an existing combatant to a new level, preserving identity, gear, stance, and
  /// current resource ratios. Reuses the canonical ProgressionScale formulas so level-ups
  /// stay consistent with combatants generated directly at that level.
  let rescaleToLevel (c: Combatant) (level: int) : Combatant =
    let template = createClassLevel c.Class level

    let scaledRatio (current: int) (max: int) =
      if max <= 0 then 1.0 else float current / float max

    let scaledValue (current: int) (oldMax: int) (newMax: int) =
      int (Math.Round(float newMax * scaledRatio current oldMax))

    let primaryStat =
      if c.Plane = Physical then
        let prim, _, _, _, _, _ = ProgressionScale.physicalStatsForLevel level
        prim
      else
        let prim, _, _, _, _ = ProgressionScale.magicStatsForLevel level
        prim

    { c with
        Stats = template.Stats
        Health =
          { Current = scaledValue c.Health.Current c.Health.Maximum template.Health.Maximum
            Maximum = template.Health.Maximum }
        Morale =
          { Current = scaledValue c.Morale.Current c.Morale.Maximum template.Morale.Maximum
            Maximum = template.Morale.Maximum }
        Armor =
          { Current = scaledValue c.Armor.Current c.Armor.Max template.Armor.Max
            Max = template.Armor.Max }
        Progression =
          { c.Progression with
              Level = level
              PrimaryStat = primaryStat } }
