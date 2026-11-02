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

  /// Level a story stage's guardian hands the protagonist: a six-level ramp per stage that lands the
  /// protagonist on the Veteran threshold (level 40) at the end of the sixth grief stage, so the
  /// Infinite Tower (story floor 8) is entered as a Veteran rather than a GrandMaster. Floor 1 is the
  /// tutorial's landing (level 6) and the last stage hands over nothing further (40, not 45) — the
  /// gate tops up to this level and only carries a capped lead beyond it.
  /// The story's own enemies are re-levelled to the protagonist by `TowerSession.rescaleFloorEnemies`,
  /// so this ladder is the protagonist's level, not the bestiary's authored magnitude.
  let storyGuardianLevel (floorNumber: int) : int =
    match floorNumber with
    | 1 -> 6
    | 2 -> 11
    | 3 -> 17
    | 4 -> 23
    | 5 -> 29
    | 6 -> 35
    | _ -> 40

  /// Authored bestiary levels the *story* stages may field, as opposed to `storyGuardianLevel`,
  /// which is the protagonist's own level. Templates are authored against the old tier magnitudes
  /// (Quarry 15/35/75, Calderas 40/95/160, the Sanctuaries 185), and `towerSession` re-levels every
  /// placement to the protagonist anyway, so this ladder only decides which kits a stage may draw
  /// from. It must stay the old ladder: folding the two together collapsed the deep stages onto a
  /// single `List.minBy Level` template once the protagonist's ladder dropped below 40.
  let stageTemplateCeiling (floorNumber: int) : int =
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
  /// Floors at 5% from level 1, then 5% every 10 levels above 50, capping at 80% at level 200.
  let spellArmorBonus (level: int) : float =
    // A caster carries no physical stat percentage on its kit, so it leans on spell armour from level 1
    // rather than only from 50: the prologue is the one stretch where it has nothing else on the physical
    // plane. `steps` is negative below 40 and the clamp floors it at the 0.05 the ramp starts with, so the
    // curve above 50 is unchanged (0.05 at 50, 0.30 at 100, 0.80 at 200).
    let steps = (level - 40) / 10
    Math.Clamp(Math.Round(float steps * 0.05, 2), 0.05, 0.80)

  /// Computes (primary, secondary, tertiary, minor, offPoise) for mental/magic classes.
  /// One curve serves both planes up to level 50, so a Mage and a Fighter are mirrors of each other rather
  /// than the caster sitting a step behind (`prim` used to run 40 + 3.6/level against the physical 45 +
  /// 4/level).
  let magicStatsForLevel (level: int) : int * int * int * int * int =
    let l = Math.Max(1, level)
    let delta = l - 1
    let prim = 45 + delta * 4
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
    let health, morale, armor = ProgressionScale.poolsForLevel cls.Plane level

    let statMap =
      if cls.Plane = Physical then
        let prim, sec, tert, min, offDef, offOff = ProgressionScale.physicalStatsForLevel level
        match cls with
        | CharacterClass.Fighter ->
          // Own plane on the primary tier; off plane growing but a step behind — offence at half the primary,
          // defence on `offDef` (0.75). Both planes keep growing, so a Fighter never carries a frozen block
          // into a fight and never stops improving against a mind.
          //
          // `GetGuard` is (Poise + Composure) / 2 and ignores the plane, so the two stats it blends must sit
          // at the same tier on both classes or the guard favours a plane: the Fighter's Poise is its
          // physical own plane and its Composure the mental `offDef`, the Mage's Poise is its physical
          // `offDef` and its Composure the mental own plane. Both then read (prim + offDef) / 2.
          let halfPrim = prim / 2

          [ Force, prim; Fortitude, prim
            Finesse, prim; Reflex, prim
            Prowess, prim; Poise, prim
            Intellect, halfPrim; Resolve, offDef
            Acuity, halfPrim; Intuition, offDef
            Acumen, halfPrim; Composure, offDef ]
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
        | _ ->
          [ Force, prim; Fortitude, prim
            Finesse, sec; Reflex, sec
            Prowess, sec; Poise, sec
            Intellect, min; Resolve, min
            Acuity, min; Intuition, min
            Acumen, min; Composure, min ]
      else
        // A Mage's own six mental stats sit on the primary tier and its six physical stats a step behind —
        // offence at half the primary, defence on `offDef` — the mirror of a Fighter, whose physical block
        // leads the same way. Both planes keep growing, so a Master or GrandMaster caster never carries a
        // frozen block into the fight.
        let prim, sec, _, _, offDef, _ = ProgressionScale.physicalStatsForLevel level
        let halfPrim = prim / 2
        let spellArmorPct = ProgressionScale.spellArmorBonus level
        // Spell armour adds a share of the caster's own mental stat to the physical defences it is genuinely
        // weak in (Resolve->Fortitude, Intuition->Reflex), but never above the caster's own primary: 80% of
        // primary stacked on `offDef` reached 1.55x primary, so a GrandMaster Mage's 1304 defence turned
        // every GrandMaster Fighter strike aside and the ward underneath was never reached. Capped at `prim`
        // the share closes the gap instead of walling it off.
        // The guard stat takes no share: Poise is already the physical `offDef`, the same tier a Fighter's
        // Composure holds, so the guard pair stays plane-neutral.
        let armorShare = int (Math.Round(float prim * spellArmorPct))
        let physicalDefence = min prim (offDef + armorShare)

        let mentalStats =
          match cls with
          | CharacterClass.Mage ->
            // Normalized mental stats: every mental attribute on the primary tier, guard pair included.
            [ Intellect, prim; Resolve, prim
              Acuity, prim; Intuition, prim
              Acumen, prim; Composure, prim ]
          | _ ->
            [ Intellect, prim; Resolve, prim
              Acuity, halfPrim; Intuition, sec
              Acumen, halfPrim; Composure, sec ]

        // Mirror of the Fighter's off-plane block: physical offence at half the primary, physical defence on
        // `offDef` plus the spell-armour share.
        let physicalStats =
          match cls with
          | CharacterClass.Mage ->
            [ Force, halfPrim; Fortitude, physicalDefence
              Finesse, halfPrim; Reflex, physicalDefence
              Prowess, halfPrim; Poise, sec ]
          | _ ->
            [ Force, prim; Fortitude, prim
              Finesse, halfPrim; Reflex, physicalDefence
              Prowess, halfPrim; Poise, sec ]

        mentalStats @ physicalStats

    let stats = StatBlock.Create statMap
    let name = sprintf "Lv.%d %s" level cls.Name
    let stance =
      match cls with
      | CharacterClass.Fighter
      | CharacterClass.Warrior
      | CharacterClass.Mage -> CombatStance.PowerStance
      | CharacterClass.Rogue -> CombatStance.AgilityStance

    { Combatant.createWithClass id name health morale stats cls level with
        Armor = ArmorIntegrity.Create armor
        Stance = stance }

  /// Creates a fully equipped combatant for a designated class archetype and combat tier
  let createClassTier (cls: CharacterClass) (tier: CombatTier) : Combatant =
    let level = ProgressionScale.tierToLevel tier
    let fighter = createClassLevel cls level
    let tierName = sprintf "%s %s" (tier.ToString()) cls.Name
    let mobStance =
      match cls with
      | CharacterClass.Fighter
      | CharacterClass.Warrior
      | CharacterClass.Mage -> CombatStance.PowerStance
      | CharacterClass.Rogue -> CombatStance.AgilityStance
    { fighter with Name = tierName; Stance = mobStance }

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
  let private engageLevelAllowance = 10

  /// Uniformly rescales a combatant to a target level. The authored block keeps its shape (a
  /// Finesse-primary foe stays Finesse-primary) while its magnitude follows the level ratio.
  /// ProgressionScale.rescaleToLevel is deliberately additive because it preserves hand-authored and
  /// gear-modified base stats, which for a monster authored 150 levels up would leave Reflex at three
  /// times the player's offense (and drive armor negative) -- precisely the problem this removes.
  let scaleCombatantToLevel (targetLevel: int) (enemy: Combatant) : Combatant =
    let targetLevel = Math.Max(1, targetLevel)
    if targetLevel = enemy.Level then enemy
    else
      let factor = float targetLevel / float (max 1 enemy.Level)
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
          let prim, _, _, _, _, _ = ProgressionScale.physicalStatsForLevel targetLevel
          prim
        else
          let prim, _, _, _, _ = ProgressionScale.magicStatsForLevel targetLevel
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
                Level = targetLevel
                PrimaryStat = primaryStat } }

  /// Clamps an enemy to a level the player's current build can plausibly contest.
  /// Bestiary levels are floor-themed rather than progression-aware, so mid-floor spawns can field a
  /// level 185/190 foe against a player near level 40, whose contested defense sat far above the
  /// player's best attack stat. Floor-entry scaling (`TowerSession`) keeps authored levels in range;
  /// this is the backstop for everything summoned after the floor is laid out.
  /// Only ever scales down: an enemy at or below the allowance is returned unchanged.
  let clampEngagedToPlayerLevel (enemy: Combatant) (player: Combatant) : Combatant =
    let ceiling = player.Level + engageLevelAllowance
    if enemy.Level <= ceiling then enemy
    else scaleCombatantToLevel ceiling enemy
