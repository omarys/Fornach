namespace Fornach.Story

open Fornach.Domain

module StoryBosses =
  /// Creates the manifestation of Guilt and Hesitation encountered in the quarry pit.
  let createGuiltAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 120 // Heavy kinetic output, crushing hammer
          StatId.Fortitude, 100 // High structural mass, absorbs punishment
          StatId.Finesse, 35 // Slow, deliberate
          StatId.Reflex, 30 // Low evasion, relies on poise and mass
          StatId.Prowess, 40 // Crude but destructive
          StatId.Poise, 65 // Anchored by heavy guilt
          StatId.Intellect, 40 // Dull psychic hum
          StatId.Resolve, 85 // Stubborn psychological weight
          StatId.Acuity, 20
          StatId.Intuition, 30
          StatId.Acumen, 25
          StatId.Composure, 50 ]

    let id = CombatantId.New()
    let guilt = Combatant.create id "Aspect of Guilt" 350 250 stats

    let hammer =
      { Name = "Quarry Sledgehammer"
        Slot = EquipmentSlot.Weapon
        Description = "Heavy, brutal mining hammer forged to crack solid iron ore."
        StatModifiers = [ StatId.Force, 15 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 10
        Triggers = [] }

    let plate =
      { Name = "Rusted Heavy Plate"
        Slot = EquipmentSlot.Armor
        Description = "Scrap plate salvaged from quarry machinery."
        StatModifiers = [ StatId.Fortitude, 20 ]
        HealthBonus = 50
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [] }

    { guilt with
        Armor = ArmorIntegrity.Create 80
        EquippedItems = [ hammer; plate ]
        Stance = CombatStance.PowerStance
        Meters =
          { guilt.Meters with
              CognitiveFatigue = Meter.Create 25
              Recklessness = Meter.Create 15 } }

  /// Creates the manifestation of Denial encountered in the Shrouded Grove.
  let createDenialAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 45
          StatId.Fortitude, 35
          StatId.Finesse, 110 // Elusive, fluid misdirection
          StatId.Reflex, 115 // Razor-sharp evasion
          StatId.Prowess, 70
          StatId.Poise, 40
          StatId.Intellect, 75
          StatId.Resolve, 60
          StatId.Acuity, 90
          StatId.Intuition, 85
          StatId.Acumen, 65
          StatId.Composure, 55 ]

    let id = CombatantId.New()
    let denial = Combatant.create id "Aspect of Denial" 240 300 stats

    let daggers =
      { Name = "Twin Mirage Daggers"
        Slot = EquipmentSlot.Weapon
        Description = "Spectral daggers that flicker in and out of sight, inflicting doubt."
        StatModifiers = [ StatId.Finesse, 15; StatId.Reflex, 10 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [ OnHitLanded(fun _ -> [ InflictDebuff(false, "Confusion", 20) ]) ] }

    let veil =
      { Name = "Veil of Distortions"
        Slot = EquipmentSlot.Armor
        Description = "A gossamer cloak that bends light and distorts perspective."
        StatModifiers = [ StatId.Reflex, 15 ]
        HealthBonus = 0
        MoraleBonus = 30
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ InflictDebuff(false, "Confusion", 10) ]) ] }

    let relic =
      { Name = "Specter of Avoidance"
        Slot = EquipmentSlot.MentalRelic
        Description = "A cracked pocket watch stuck at the moment before the accident."
        StatModifiers = [ StatId.Intuition, 10 ]
        HealthBonus = 0
        MoraleBonus = 40
        StartingRecklessnessDelta = 0
        Triggers = [ OnCriticalStrike(fun _ -> [ RestorePool(false, 30) ]) ] }

    { denial with
        Armor = ArmorIntegrity.Create 30
        MirrorClones = 2
        EquippedItems = [ daggers; veil; relic ]
        Stance = CombatStance.AgilityStance
        Meters =
          { denial.Meters with
              Confusion = Meter.Create 10
              Recklessness = Meter.Create 5 } }

  /// Creates the manifestation of Anger encountered in the Basalt Caldera.
  let createAngerAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 140 // Destructive kinetic fury
          StatId.Fortitude, 90 // Hardened volcanic stone
          StatId.Finesse, 50
          StatId.Reflex, 40
          StatId.Prowess, 95 // Relentless aggressive assault
          StatId.Poise, 30
          StatId.Intellect, 30
          StatId.Resolve, 90
          StatId.Acuity, 35
          StatId.Intuition, 25
          StatId.Acumen, 30
          StatId.Composure, 0 ] // Completely unchecked wrath

    let id = CombatantId.New()
    let anger = Combatant.create id "Aspect of Anger" 420 180 stats

    let sword =
      { Name = "Slag-Forged Greatsword"
        Slot = EquipmentSlot.Weapon
        Description = "A massive slab of red-hot iron shedding embers with every swing."
        StatModifiers = [ StatId.Force, 25 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 25
        Triggers = [ OnHitLanded(fun _ -> [ ShredTargetArmor 15; InflictDebuff(false, "Overwhelm", 20) ]) ] }

    let carapace =
      { Name = "Molten Carapace"
        Slot = EquipmentSlot.Armor
        Description = "Cooling volcanic basalt fused directly over scorched flesh."
        StatModifiers = [ StatId.Fortitude, 20 ]
        HealthBonus = 60
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ FreeCounterStrike(Plane.Physical, 10) ]) ] }

    let relic =
      { Name = "Fury Furnace"
        Slot = EquipmentSlot.MentalRelic
        Description = "An unquenchable ember burning with self-destructive rage."
        StatModifiers = [ StatId.Prowess, 15 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 15
        Triggers = [ OnAttackDeclared(fun _ -> [ BonusDamage(Plane.Physical, 8) ]) ] }

    { anger with
        Armor = ArmorIntegrity.Create 60
        EquippedItems = [ sword; carapace; relic ]
        Stance = CombatStance.PowerStance
        Meters =
          { anger.Meters with
              Recklessness = Meter.Create 45
              Frustration = Meter.Create 40 } }

  /// Creates the manifestation of Bargaining encountered at the Shattered Promontory.
  let createBargainingAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 65
          StatId.Fortitude, 70
          StatId.Finesse, 85
          StatId.Reflex, 75
          StatId.Prowess, 70
          StatId.Poise, 85
          StatId.Intellect, 95
          StatId.Resolve, 105
          StatId.Acuity, 80
          StatId.Intuition, 75
          StatId.Acumen, 105 // Calculating transactions
          StatId.Composure, 80 ]

    let id = CombatantId.New()
    let bargaining = Combatant.create id "Aspect of Bargaining" 310 340 stats

    let rapier =
      { Name = "Gale-Wind Rapier"
        Slot = EquipmentSlot.Weapon
        Description = "A slender foil that seeks tactical leverage and calculated concessions."
        StatModifiers = [ StatId.Finesse, 15; StatId.Acumen, 10 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [ OnHitLanded(fun _ -> [ GainStudyStacks 2 ]) ] }

    let mantle =
      { Name = "Tide-Worn Mantle"
        Slot = EquipmentSlot.Armor
        Description = "A drenched oilskin coat resisting the sea spray and gale force."
        StatModifiers = [ StatId.Fortitude, 10; StatId.Composure, 15 ]
        HealthBonus = 30
        MoraleBonus = 20
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ RestorePool(false, 15) ]) ] }

    let relic =
      { Name = "Scales of Desperation"
        Slot = EquipmentSlot.MentalRelic
        Description = "A brass balance scale tipping wildly between survival and regret."
        StatModifiers = [ StatId.Acumen, 15 ]
        HealthBonus = 0
        MoraleBonus = 50
        StartingRecklessnessDelta = 0
        Triggers =
          [ OnCriticalStrike(fun _ ->
              [ InflictDebuff(false, "Frustration", 25)
                RestorePool(true, 20) ]) ] }

    { bargaining with
        Armor = ArmorIntegrity.Create 50
        EquippedItems = [ rapier; mantle; relic ]
        Stance = CombatStance.DisciplineStance
        Meters =
          { bargaining.Meters with
              Provoke = Meter.Create 20
              CognitiveFatigue = Meter.Create 15 } }

  /// Creates the manifestation of Depression encountered in the Sunken Metropolis.
  let createDepressionAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 30
          StatId.Fortitude, 135 // Immovable, crushing weight
          StatId.Finesse, 25
          StatId.Reflex, 20
          StatId.Prowess, 35
          StatId.Poise, 95
          StatId.Intellect, 50
          StatId.Resolve, 100
          StatId.Acuity, 30
          StatId.Intuition, 40
          StatId.Acumen, 40
          StatId.Composure, 110 ] // Numb, emotionless silence

    let id = CombatantId.New()
    let depression = Combatant.create id "Aspect of Depression" 480 400 stats

    let anchor =
      { Name = "Anchor of Apathy"
        Slot = EquipmentSlot.Weapon
        Description = "A waterlogged stone mooring iron that drags down body and spirit."
        StatModifiers = [ StatId.Force, 10; StatId.Fortitude, 15 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers =
          [ OnHitLanded(fun _ ->
              [ InflictDebuff(false, "CognitiveFatigue", 30)
                InflictDebuff(false, "Exhaustion", 20) ]) ] }

    let shroud =
      { Name = "Shroud of Still Waters"
        Slot = EquipmentSlot.Armor
        Description = "Heavy wet rags soaked in murky, stagnant floodwater."
        StatModifiers = [ StatId.Fortitude, 25; StatId.Composure, 20 ]
        HealthBonus = 80
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ InflictDebuff(false, "CognitiveFatigue", 15) ]) ] }

    let relic =
      { Name = "Weight of Absences"
        Slot = EquipmentSlot.MentalRelic
        Description = "An empty picture frame containing only cold grey glass."
        StatModifiers = [ StatId.Resolve, 15 ]
        HealthBonus = 0
        MoraleBonus = 100
        StartingRecklessnessDelta = 0
        Triggers = [ OnAttackDeclared(fun _ -> [ InflictDebuff(false, "CognitiveFatigue", 10) ]) ] }

    { depression with
        Armor = ArmorIntegrity.Create 120
        EquippedItems = [ anchor; shroud; relic ]
        Stance = CombatStance.DisciplineStance
        Meters =
          { depression.Meters with
              CognitiveFatigue = Meter.Create 40
              Exhaustion = Meter.Create 30 } }

  /// Creates the manifestation of Acceptance encountered in the White Meadow.
  let createAcceptanceAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 75
          StatId.Fortitude, 80
          StatId.Finesse, 80
          StatId.Reflex, 80
          StatId.Prowess, 75
          StatId.Poise, 90
          StatId.Intellect, 80
          StatId.Resolve, 95
          StatId.Acuity, 80
          StatId.Intuition, 85
          StatId.Acumen, 80
          StatId.Composure, 95 ] // Peaceful harmony

    let id = CombatantId.New()
    let acceptance = Combatant.create id "Aspect of Acceptance" 350 500 stats

    let staff =
      { Name = "Unfinished Crossing Staff"
        Slot = EquipmentSlot.Weapon
        Description = "A walking staff carved from birch, smooth to the touch."
        StatModifiers = [ StatId.Poise, 15; StatId.Composure, 15 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers =
          [ OnHitLanded(fun _ ->
              [ RestorePool(true, 15)
                RestorePool(false, 15) ]) ] }

    let linen =
      { Name = "Linen of White Lilies"
        Slot = EquipmentSlot.Armor
        Description = "Clean, unblemished robes fragrant with meadow blossoms."
        StatModifiers = [ StatId.Composure, 20 ]
        HealthBonus = 40
        MoraleBonus = 60
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ RestorePool(false, 20) ]) ] }

    let relic =
      { Name = "Keepsake Pendant"
        Slot = EquipmentSlot.MentalRelic
        Description = "A silver locket holding a lock of twin hair and an unclouded smile."
        StatModifiers = [ StatId.Resolve, 20 ]
        HealthBonus = 0
        MoraleBonus = 80
        StartingRecklessnessDelta = 0
        Triggers = [ OnCriticalStrike(fun _ -> [ RestorePool(false, 40) ]) ] }

    { acceptance with
        Armor = ArmorIntegrity.Create 70
        EquippedItems = [ staff; linen; relic ]
        Stance = CombatStance.DisciplineStance
        Meters = StatusMeters.Zero }

  /// Factory to resolve any story encounter by id
  let createEnemy (enemyId: string) : Combatant =
    match enemyId.ToLowerInvariant() with
    | "guilt_aspect"
    | "guiltaspect"
    | "bandit_guilt_aspect" -> createGuiltAspect ()
    | "denial_aspect"
    | "denialaspect"
    | "denial" -> createDenialAspect ()
    | "anger_aspect"
    | "angeraspect"
    | "anger" -> createAngerAspect ()
    | "bargaining_aspect"
    | "bargainingaspect"
    | "bargaining" -> createBargainingAspect ()
    | "depression_aspect"
    | "depressionaspect"
    | "depression" -> createDepressionAspect ()
    | "acceptance_aspect"
    | "acceptanceaspect"
    | "acceptance" -> createAcceptanceAspect ()
    | _ -> createGuiltAspect ()
