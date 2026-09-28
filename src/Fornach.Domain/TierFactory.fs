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

  /// Computes (primaryStat, secondaryStat, minorStat) for an uncapped character level.
  /// Designed for endless progression with consistent, linear rate of attribute growth:
  /// - Primary attributes:   +4.0 stat points per level (+156 at lv 40, +396 at lv 100, +796 at lv 200)
  /// - Secondary attributes: +2.5 stat points per level (+98 at lv 40,  +248 at lv 100, +498 at lv 200)
  /// - Minor attributes:     +1.5 stat points per level (+59 at lv 40,  +149 at lv 100, +299 at lv 200)
  let statsForLevel (level: int) : int * int * int =
    let l = Math.Max(1, level)
    let delta = l - 1
    let prim = 45 + delta * 4
    let sec = 25 + int (Math.Round(float delta * 2.5))
    let min = 15 + int (Math.Round(float delta * 1.5))
    (prim, sec, min)

  /// Computes (health, morale, armor) for a given plane and uncapped character level.
  /// - Primary pool:   +75 per level (+2,925 at lv 40, +7,425 at lv 100, +14,925 at lv 200)
  /// - Secondary pool: +50 per level (+1,950 at lv 40, +4,950 at lv 100,  +9,950 at lv 200)
  /// - Armor rating:   +1.5 (physical) / +1.0 (mental) per level
  let poolsForLevel (plane: Plane) (level: int) : int * int * int =
    let l = Math.Max(1, level)
    let delta = l - 1
    if plane = Physical then
      let hp = 650 + delta * 75
      let morale = 450 + delta * 50
      let armor = 25 + int (Math.Round(float delta * 1.5))
      (hp, morale, armor)
    else
      let hp = 450 + delta * 50
      let morale = 650 + delta * 75
      let armor = 15 + int (Math.Round(float delta * 1.0))
      (hp, morale, armor)

module TierFactory =

  /// Creates a fully equipped combatant for a designated class archetype and exact level
  let createClassLevel (cls: CharacterClass) (level: int) : Combatant =
    let id = CombatantId.New()
    let primStat, secStat, minStat = ProgressionScale.statsForLevel level
    let health, morale, armor = ProgressionScale.poolsForLevel cls.Plane level

    let statMap =
      match cls with
      | CharacterClass.Berserker
      | CharacterClass.Warrior ->
        // Power / Physical: Primary Force, Fortitude; Secondary Prowess, Poise; Minor rest
        [ Force, primStat; Fortitude, primStat
          Prowess, secStat; Poise, secStat
          Finesse, minStat; Reflex, minStat
          Intellect, minStat; Resolve, minStat
          Acuity, minStat; Intuition, minStat
          Acumen, minStat; Composure, minStat ]
      | CharacterClass.Inquisitor
      | CharacterClass.Mage ->
        // Power / Mental: Primary Intellect, Resolve; Secondary Acumen, Composure; Minor rest
        [ Intellect, primStat; Resolve, primStat
          Acumen, secStat; Composure, secStat
          Force, minStat; Fortitude, minStat
          Finesse, minStat; Reflex, minStat
          Prowess, minStat; Poise, minStat
          Acuity, minStat; Intuition, minStat ]
      | CharacterClass.Duelist
      | CharacterClass.Assassin ->
        // Agility / Physical: Primary Finesse, Reflex; Secondary Prowess, Poise; Minor rest
        [ Finesse, primStat; Reflex, primStat
          Prowess, secStat; Poise, secStat
          Force, minStat; Fortitude, minStat
          Intellect, minStat; Resolve, minStat
          Acuity, minStat; Intuition, minStat
          Acumen, minStat; Composure, minStat ]
      | CharacterClass.Mesmer ->
        // Agility / Mental: Primary Acuity, Intuition; Secondary Intellect, Resolve; Minor rest
        [ Acuity, primStat; Intuition, primStat
          Intellect, secStat; Resolve, secStat
          Force, minStat; Fortitude, minStat
          Finesse, minStat; Reflex, minStat
          Prowess, minStat; Poise, minStat
          Acumen, minStat; Composure, minStat ]
      | CharacterClass.Justicar
      | CharacterClass.Soldier ->
        // Discipline / Physical: Primary Prowess, Poise; Secondary Fortitude, Force; Minor rest
        [ Prowess, primStat; Poise, primStat
          Fortitude, primStat; Force, secStat
          Finesse, minStat; Reflex, minStat
          Intellect, minStat; Resolve, minStat
          Acuity, minStat; Intuition, minStat
          Acumen, minStat; Composure, minStat ]
      | CharacterClass.Strategist ->
        // Discipline / Mental: Primary Acumen, Composure; Secondary Intellect, Intuition; Minor rest
        [ Acumen, primStat; Composure, primStat
          Intellect, secStat; Intuition, secStat
          Force, minStat; Fortitude, minStat
          Finesse, minStat; Reflex, minStat
          Prowess, minStat; Poise, minStat
          Acuity, minStat; Resolve, minStat ]

    let stats = StatBlock.Create statMap
    let name = sprintf "Lv.%d %s" level cls.Name
    let stance =
      match cls with
      | CharacterClass.Berserker
      | CharacterClass.Warrior -> CombatStance.PowerStance
      | CharacterClass.Inquisitor
      | CharacterClass.Mage -> CombatStance.PowerStance
      | CharacterClass.Duelist
      | CharacterClass.Assassin -> CombatStance.AgilityStance
      | CharacterClass.Mesmer -> CombatStance.AgilityStance
      | CharacterClass.Justicar
      | CharacterClass.Soldier -> CombatStance.DisciplineStance
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
