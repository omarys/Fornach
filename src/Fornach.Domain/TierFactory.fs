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

  /// Level a story stage's guardian hands the protagonist, chosen to match what the *next* stage's
  /// biome monsters assume: per-kill experience alone (~0.3 levels per floor after the tutorial)
  /// would leave the protagonist at level 33 walking into the Basalt Calderas' level-95 Master golem
  /// and the Elysian Sanctuaries' level-185 GrandMasters. Floor 1 is the tutorial's authored
  /// landing (level 32) and the last stage hands over nothing further.
  /// Also the enemy-level ceiling for a floor's guardian, so placement cannot outrun progression.
  let storyGuardianLevel (floorNumber: int) : int =
    match floorNumber with
    | 1 -> 32
    | 2 -> 45
    | 3 -> 95
    | 4 -> 105
    | 5 -> 110
    | 6 -> 185
    | _ -> 185

  /// Computes (primary, secondary, tertiary, minor, offDefensive, offOffensive) for physical classes based on 1.0 : 0.75 : 0.75 ratio
  let physicalStatsForLevel (level: int) : int * int * int * int * int * int =
    let l = Math.Max(1, level)
    let delta = l - 1
    let prim = 45 + delta * 4
    let sec = int (Math.Round(float prim * 0.75))
    let tert = int (Math.Round(float prim * 0.50))
    let min = 15 + int (Math.Round(float delta * 1.0))
    // Off-plane mental defence is no longer a trade-off stat: the low-intelligence physical classes
    // (Berserker and kin) are gone, so a Fighter is not assumed to be dull. Sits at the secondary
    // tier rather than the tertiary one it used to share with mental offence.
    let offDef = sec
    // Offensive off-plane stat gets modest growth (halfway between min and tert)
    let offOff = min + int (Math.Round(float (tert - min) * 0.50))
    (prim, sec, tert, min, offDef, offOff)

  /// Resolves the spell armor percentage for magic users based on level.
  /// Starts at level 50 (5%), increasing by 5% every 10 levels, capping at 80% at level 200.
  let spellArmorBonus (level: int) : float =
    if level < 50 then 0.0
    else
      let steps = (level - 40) / 10
      Math.Clamp(Math.Round(float steps * 0.05, 2), 0.05, 0.80)

  /// Computes (primary, secondary, tertiary, minor, offPoise) for mental/magic classes based on 1.0 : 0.75 : 0.75 utility ratio.
  /// Physical stat growth stops developing after level 49 to counter defense over-inflation.
  let magicStatsForLevel (level: int) : int * int * int * int * int =
    let l = Math.Max(1, level)
    let delta = l - 1
    // Magic utility ratio (756 at GM) balanced across all 3 mental vectors
    let prim = 40 + int (Math.Round(float delta * 3.6))
    let sec = int (Math.Round(float prim * 0.75))
    let tert = int (Math.Round(float prim * 0.50))
    // Physical stat growth stops developing after level 49
    let physDelta = Math.Clamp(level, 1, 49) - 1
    let min = 15 + int (Math.Round(float physDelta * 1.0))
    // Poise boost (physical defensive discipline) to maintain stance/casting concentration
    let offPoise = min + int (Math.Round(float physDelta * 1.5))
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
      | CharacterClass.Soldier ->
        int (Math.Round(float baseMorale * 1.15)),
        int (Math.Round(float baseArmor * 1.15))
      | _ -> baseMorale, baseArmor

    let statMap =
      if cls.Plane = Physical then
        let prim, sec, tert, min, offDef, offOff = ProgressionScale.physicalStatsForLevel level
        match cls with
        | CharacterClass.Fighter ->
          // Normalized physical stats: all 6 physical attributes set to prim
          // Off-plane mental attributes: Resolve, Intuition, Composure get offDef; mental offenses get min
          [ Force, prim; Fortitude, prim
            Finesse, prim; Reflex, prim
            Prowess, prim; Poise, prim
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
            Acumen, min; Composure, offDef ]
        | CharacterClass.Warrior ->
          // Generic Power NPC
          [ Force, prim; Fortitude, prim
            Prowess, sec; Poise, sec
            Finesse, sec; Reflex, sec
            Intellect, min; Resolve, offDef
            Acuity, min; Intuition, offDef
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
        // Magic / Mental Plane: 1.0 : 0.75 : 0.75 across Mental Vectors (normalized 1.0 for Mage)
        // Physical stat growth stops developing after level 49 to counter over-inflation.
        let physLevel = Math.Clamp(level, 1, 49)
        let prim, sec, _, _, _ = ProgressionScale.magicStatsForLevel level
        let physPrim = 40 + int (Math.Round(float (physLevel - 1) * 3.6))
        let physTert = int (Math.Round(float physPrim * 0.50))
        let min = 15 + int (Math.Round(float (physLevel - 1) * 1.0))
        let offPoise = min + int (Math.Round(float (physLevel - 1) * 1.5))
        let offPhys = physTert

        let spellArmorPct = ProgressionScale.spellArmorBonus level

        let baseFortitude, baseReflex, basePoise, resolveVal, intuitionVal, composureVal =
          match cls with
          | CharacterClass.Mage ->
            // Normalized mental stats: all mental defenses are prim, so spell armor scales from prim across all 3 defenses
            offPhys, min, offPoise, prim, prim, prim
          | _ ->
            offPhys, min, offPoise, prim, sec, sec

        let finalFortitude = baseFortitude + int (Math.Round(float resolveVal * spellArmorPct))
        let finalReflex = baseReflex + int (Math.Round(float intuitionVal * spellArmorPct))
        let finalPoise = basePoise + int (Math.Round(float composureVal * spellArmorPct))

        let mentalStats =
          match cls with
          | CharacterClass.Mage ->
            // Normalized mental stats: all 6 mental attributes set to prim
            [ Intellect, prim; Resolve, prim
              Acuity, prim; Intuition, prim
              Acumen, prim; Composure, prim ]
          | _ ->
            [ Intellect, prim; Resolve, prim
              Acuity, sec; Intuition, sec
              Acumen, sec; Composure, sec ]

        let physicalStats =
          [ Force, min; Fortitude, finalFortitude
            Finesse, min; Reflex, finalReflex
            Prowess, min; Poise, finalPoise ]

        mentalStats @ physicalStats

    let stats = StatBlock.Create statMap
    let name = sprintf "Lv.%d %s" level cls.Name
    let stance =
      match cls with
      | CharacterClass.Fighter
      | CharacterClass.Warrior
      | CharacterClass.Mage -> CombatStance.PowerStance
      | CharacterClass.Rogue -> CombatStance.AgilityStance
      | CharacterClass.Soldier -> CombatStance.DisciplineStance

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
  /// current resource ratios. Applies only the per-level growth between the canonical
  /// templates for the old and new level, so hand-authored or equipment-modified base
  /// stats are preserved rather than reset to the generic class formula.
  let rescaleToLevel (c: Combatant) (level: int) : Combatant =
    let template = createClassLevel c.Class level
    let previousTemplate = createClassLevel c.Class c.Level

    let growth (oldValue: int) (newValue: int) = newValue - oldValue

    let scaledStats =
      Attributes.all
      |> List.fold
        (fun (stats: StatBlock) stat ->
          stats.Modify(stat, growth (previousTemplate.Stats.Get stat) (template.Stats.Get stat)))
        c.Stats

    let newHealthMax = c.Health.Maximum + growth previousTemplate.Health.Maximum template.Health.Maximum
    let newMoraleMax = c.Morale.Maximum + growth previousTemplate.Morale.Maximum template.Morale.Maximum
    let newArmorMax = c.Armor.Max + growth previousTemplate.Armor.Max template.Armor.Max

    let scaledValue (current: int) (oldMax: int) (newMax: int) =
      if oldMax <= 0 then newMax
      else int (Math.Round(float newMax * (float current / float oldMax)))

    let primaryStat =
      if c.Plane = Physical then
        let prim, _, _, _, _, _ = ProgressionScale.physicalStatsForLevel level
        prim
      else
        let prim, _, _, _, _ = ProgressionScale.magicStatsForLevel level
        prim

    { c with
        Stats = scaledStats
        Health =
          { Current = scaledValue c.Health.Current c.Health.Maximum newHealthMax
            Maximum = newHealthMax }
        Morale =
          { Current = scaledValue c.Morale.Current c.Morale.Maximum newMoraleMax
            Maximum = newMoraleMax }
        Armor =
          { Current = scaledValue c.Armor.Current c.Armor.Max newArmorMax
            Max = newArmorMax }
        Progression =
          { c.Progression with
              Level = level
              PrimaryStat = primaryStat } }

  /// How far above the player a fight is still allowed to be, expressed in levels.
  let private engageLevelAllowance = 2

  /// Clamps an enemy to a level the player's current build can plausibly contest.
  /// Bestiary levels are floor-themed rather than progression-aware, so tower floors 6 and 7
  /// fielded a level 185/190 guardian against a player near level 36, whose contested defense
  /// sat far above the player's best attack stat. Scaling here, at engage time, keeps the
  /// authored roster and floor generation untouched.
  /// Only ever scales down: an enemy at or below the allowance is returned unchanged.
  let clampEngagedToPlayerLevel (enemy: Combatant) (player: Combatant) : Combatant =
    let ceiling = player.Level + engageLevelAllowance
    if enemy.Level <= ceiling then enemy
    else
      // Uniform rescale. The authored block keeps its shape (a Finesse-primary foe stays
      // Finesse-primary) while its magnitude follows the level ratio. ProgressionScale.rescaleToLevel
      // is deliberately additive because it preserves hand-authored and gear-modified base stats,
      // which for a monster authored 150 levels up would leave Reflex at three times the player's
      // offense (and drive armor negative) -- precisely the problem this exists to remove.
      let factor = float ceiling / float enemy.Level
      let scale (v: int) = max 1 (int (Math.Round(float v * factor)))

      let scaledStats =
        Attributes.all
        |> List.fold
          (fun (stats: StatBlock) stat ->
            let current = enemy.Stats.Get stat
            stats.Modify(stat, scale current - current))
          enemy.Stats

      let scalePool (p: Pool) =
        let maximum = scale p.Maximum
        { Current = max 1 (int (Math.Round(float p.Current * float maximum / float (max 1 p.Maximum))))
          Maximum = maximum }

      let armorMax = scale enemy.Armor.Max

      let primaryStat =
        if enemy.Plane = Physical then
          let prim, _, _, _, _, _ = ProgressionScale.physicalStatsForLevel ceiling
          prim
        else
          let prim, _, _, _, _ = ProgressionScale.magicStatsForLevel ceiling
          prim

      { enemy with
          Stats = scaledStats
          Health = scalePool enemy.Health
          Morale = scalePool enemy.Morale
          Armor =
            { Current = max 1 (int (Math.Round(float enemy.Armor.Current * float armorMax / float (max 1 enemy.Armor.Max))))
              Max = armorMax }
          Progression =
            { enemy.Progression with
                Level = ceiling
                PrimaryStat = primaryStat } }
