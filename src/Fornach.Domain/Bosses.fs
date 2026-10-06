namespace Fornach.Domain

module StoryBosses =
  /// Creates the manifestation of Guilt and Hesitation encountered in the quarry pit.
  let createGuiltAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 210 // Heavy kinetic output, crushing hammer
          StatId.Fortitude, 195 // High structural mass, absorbs punishment
          StatId.Finesse, 100 // Slow, deliberate
          StatId.Reflex, 95 // Low evasion, relies on poise and mass
          StatId.Prowess, 160 // Crude but destructive
          StatId.Poise, 185 // Anchored by heavy guilt
          StatId.Intellect, 80 // Dull psychic hum
          StatId.Resolve, 200 // Stubborn psychological weight
          StatId.Acuity, 75
          StatId.Intuition, 110
          StatId.Acumen, 105
          StatId.Composure, 150 ]

    let id = CombatantId.New()
    let guilt = Combatant.create id "Aspect of Guilt" 2800 2400 stats

    let hammer =
      { Name = "Quarry Sledgehammer"
        Slot = EquipmentSlot.Weapon
        Description = "Heavy, brutal mining hammer forged to crack solid iron ore."
        StatModifiers = [ StatId.Force, 30; StatId.Prowess, 15 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 10
        Triggers = [] }

    let plate =
      { Name = "Rusted Heavy Plate"
        Slot = EquipmentSlot.Armor
        Description = "Scrap plate salvaged from quarry machinery."
        StatModifiers = [ StatId.Fortitude, 30; StatId.Poise, 20 ]
        HealthBonus = 400
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [] }

    { guilt with
        Armor = ArmorIntegrity.Create 150
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
        [ StatId.Force, 120
          StatId.Fortitude, 110
          StatId.Finesse, 235 // Elusive, fluid misdirection
          StatId.Reflex, 240 // Razor-sharp evasion
          StatId.Prowess, 175
          StatId.Poise, 140
          StatId.Intellect, 180
          StatId.Resolve, 180
          StatId.Acuity, 210
          StatId.Intuition, 210
          StatId.Acumen, 170
          StatId.Composure, 155 ]

    let id = CombatantId.New()
    let denial = Combatant.create id "Aspect of Denial" 2600 2600 stats

    let daggers =
      { Name = "Twin Mirage Daggers"
        Slot = EquipmentSlot.Weapon
        Description = "Spectral daggers that flicker in and out of sight, inflicting doubt."
        StatModifiers = [ StatId.Finesse, 30; StatId.Reflex, 20 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [ OnHitLanded(fun _ -> [ InflictDebuff(false, "Confusion", 20) ]) ] }

    let veil =
      { Name = "Veil of Distortions"
        Slot = EquipmentSlot.Armor
        Description = "A gossamer cloak that bends light and distorts perspective."
        StatModifiers = [ StatId.Reflex, 25 ]
        HealthBonus = 0
        MoraleBonus = 300
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ InflictDebuff(false, "Confusion", 10) ]) ] }

    let relic =
      { Name = "Specter of Avoidance"
        Slot = EquipmentSlot.MentalRelic
        Description = "A cracked pocket watch stuck at the moment before the accident."
        StatModifiers = [ StatId.Intuition, 25 ]
        HealthBonus = 0
        MoraleBonus = 300
        StartingRecklessnessDelta = 0
        Triggers = [ OnCriticalStrike(fun _ -> [ RestorePool(false, 30) ]) ] }

    { denial with
        Armor = ArmorIntegrity.Create 120
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
        [ StatId.Force, 270 // Destructive kinetic fury
          StatId.Fortitude, 210 // Hardened volcanic stone
          StatId.Finesse, 130
          StatId.Reflex, 120
          StatId.Prowess, 240
          StatId.Poise, 210
          StatId.Intellect, 90
          StatId.Resolve, 210
          StatId.Acuity, 80
          StatId.Intuition, 90
          StatId.Acumen, 100
          StatId.Composure, 0 ] // Consumed by blind rage

    let id = CombatantId.New()
    let anger = Combatant.create id "Aspect of Anger" 3300 2200 stats

    let greatsword =
      { Name = "Slag-Forged Greatsword"
        Slot = EquipmentSlot.Weapon
        Description = "A massive slab of red-hot iron oozing molten basalt."
        StatModifiers = [ StatId.Force, 40; StatId.Prowess, 20 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 20
        Triggers = [ OnHitLanded(fun _ -> [ InflictDebuff(false, "Bleed", 25) ]) ] }

    let carapace =
      { Name = "Molten Carapace"
        Slot = EquipmentSlot.Armor
        Description = "Blackened basalt armor plates bound with bubbling magma veins."
        StatModifiers = [ StatId.Fortitude, 30; StatId.Poise, 20 ]
        HealthBonus = 500
        MoraleBonus = 0
        StartingRecklessnessDelta = 10
        Triggers = [ OnDamageReceived(fun _ -> [ RestorePool(true, 10) ]) ] }

    { anger with
        Armor = ArmorIntegrity.Create 170
        EquippedItems = [ greatsword; carapace ]
        Stance = CombatStance.PowerStance
        Meters =
          { anger.Meters with
              Recklessness = Meter.Create 45
              Frustration = Meter.Create 40 } }

  /// Creates the manifestation of Bargaining encountered at the Shattered Promontory.
  let createBargainingAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 150
          StatId.Fortitude, 150
          StatId.Finesse, 200
          StatId.Reflex, 190
          StatId.Prowess, 180
          StatId.Poise, 180
          StatId.Intellect, 260 // Rationalizing the trauma
          StatId.Resolve, 240
          StatId.Acuity, 250
          StatId.Intuition, 240
          StatId.Acumen, 280 // Transactional, looking for any trade
          StatId.Composure, 170 ]

    let id = CombatantId.New()
    let bargaining = Combatant.create id "Aspect of Bargaining" 3100 3200 stats

    let rapier =
      { Name = "Gale-Wind Rapier"
        Slot = EquipmentSlot.Weapon
        Description = "A needle-thin blade whistling with ocean gale gusts."
        StatModifiers = [ StatId.Finesse, 25; StatId.Acumen, 20 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [ OnHitLanded(fun _ -> [ RestorePool(false, 20) ]) ] }

    let cloak =
      { Name = "Scales of Unequal Trade"
        Slot = EquipmentSlot.Armor
        Description = "A cloak lined with weighing scales, balancing life for time."
        StatModifiers = [ StatId.Acumen, 30 ]
        HealthBonus = 300
        MoraleBonus = 400
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ RestorePool(false, 15) ]) ] }

    { bargaining with
        Armor = ArmorIntegrity.Create 150
        EquippedItems = [ rapier; cloak ]
        Stance = CombatStance.DisciplineStance
        Meters =
          { bargaining.Meters with
              Exhaustion = Meter.Create 20 } }

  /// Creates the manifestation of Depression encountered in the Sunken Metropolis.
  let createDepressionAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 120
          StatId.Fortitude, 310 // Heavy, unmoving waterlogged weight
          StatId.Finesse, 90
          StatId.Reflex, 95
          StatId.Prowess, 140
          StatId.Poise, 290 // Grounded in despair
          StatId.Intellect, 170
          StatId.Resolve, 320 // Impenetrable numbness
          StatId.Acuity, 100
          StatId.Intuition, 130
          StatId.Acumen, 130
          StatId.Composure, 280 ] // Cold, still waters

    let id = CombatantId.New()
    let depression = Combatant.create id "Aspect of Depression" 3800 3800 stats

    let anchor =
      { Name = "Anchor of Apathy"
        Slot = EquipmentSlot.Weapon
        Description = "A barnacle-encrusted ship anchor, impossible to lift without weeping."
        StatModifiers = [ StatId.Poise, 35; StatId.Fortitude, 25 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = -10
        Triggers = [ OnHitLanded(fun _ -> [ InflictDebuff(false, "Exhaustion", 30) ]) ] }

    let shroud =
      { Name = "Shroud of Still Waters"
        Slot = EquipmentSlot.Armor
        Description = "Heavy waterlogged burial shroud radiating numbing chill."
        StatModifiers = [ StatId.Fortitude, 40; StatId.Composure, 35 ]
        HealthBonus = 600
        MoraleBonus = 400
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ InflictDebuff(false, "Exhaustion", 15) ]) ] }

    { depression with
        Armor = ArmorIntegrity.Create 200
        EquippedItems = [ anchor; shroud ]
        Stance = CombatStance.DisciplineStance
        Meters =
          { depression.Meters with
              CognitiveFatigue = Meter.Create 40
              Exhaustion = Meter.Create 30 } }

  /// Creates the manifestation of Acceptance encountered in the White Meadow.
  let createAcceptanceAspect () : Combatant =
    let stats =
      StatBlock.Create
        [ StatId.Force, 200
          StatId.Fortitude, 220
          StatId.Finesse, 220
          StatId.Reflex, 220
          StatId.Prowess, 210
          StatId.Poise, 240
          StatId.Intellect, 230
          StatId.Resolve, 310
          StatId.Acuity, 230
          StatId.Intuition, 300
          StatId.Acumen, 230
          StatId.Composure, 300 ] // Peaceful harmony

    let id = CombatantId.New()
    let acceptance = Combatant.create id "Aspect of Acceptance" 4400 4100 stats

    let staff =
      { Name = "Unfinished Crossing Staff"
        Slot = EquipmentSlot.Weapon
        Description = "A walking staff carved from birch, smooth to the touch."
        StatModifiers = [ StatId.Poise, 30; StatId.Composure, 30 ]
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
        StatModifiers = [ StatId.Composure, 30 ]
        HealthBonus = 400
        MoraleBonus = 400
        StartingRecklessnessDelta = 0
        Triggers = [ OnDamageReceived(fun _ -> [ RestorePool(false, 20) ]) ] }

    let relic =
      { Name = "Keepsake Pendant"
        Slot = EquipmentSlot.MentalRelic
        Description = "A silver locket holding a lock of twin hair and an unclouded smile."
        StatModifiers = [ StatId.Resolve, 35 ]
        HealthBonus = 0
        MoraleBonus = 500
        StartingRecklessnessDelta = 0
        Triggers = [ OnCriticalStrike(fun _ -> [ RestorePool(false, 40) ]) ] }

    { acceptance with
        Armor = ArmorIntegrity.Create 180
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

  /// Creates the player protagonist archetype for story mode with initial stats, weapon, and caltrops
  let createProloguePlayer (className: string) : Combatant =
    let id = CombatantId.New()
    let caltrops =
      { Name = "Sharpened Caltrops"
        Slot = EquipmentSlot.MentalRelic
        Description = "A pouch of jagged iron spikes scavenged from the battered chest."
        StatModifiers = [ StatId.Reflex, 15; StatId.Finesse, 10 ]
        HealthBonus = 0
        MoraleBonus = 0
        StartingRecklessnessDelta = 0
        Triggers = [] }

    match className.ToLowerInvariant() with
    | "berserker" ->
      let stats =
        StatBlock.Create
          [ StatId.Force, 210
            StatId.Fortitude, 190
            StatId.Finesse, 140
            StatId.Reflex, 135
            StatId.Prowess, 165
            StatId.Poise, 160
            StatId.Intellect, 70
            StatId.Resolve, 150
            StatId.Acuity, 90
            StatId.Intuition, 100
            StatId.Acumen, 120
            StatId.Composure, 140 ]
      let sword =
        { Name = "Heavy Iron Greatsword"
          Slot = EquipmentSlot.Weapon
          Description = "A two-handed cleaver with brutal momentum and jagged edges."
          StatModifiers = [ StatId.Force, 25; StatId.Prowess, 10 ]
          HealthBonus = 0
          MoraleBonus = 0
          StartingRecklessnessDelta = 10
          Triggers = [] }
      let c = Combatant.createWithClass id "Protagonist" 2600 1800 stats CharacterClass.Berserker 35
      { c with
          Armor = ArmorIntegrity.Create 120
          EquippedItems = [ sword; caltrops ]
          Stance = CombatStance.PowerStance }

    | "duelist" ->
      let stats =
        StatBlock.Create
          [ StatId.Force, 140
            StatId.Fortitude, 135
            StatId.Finesse, 220
            StatId.Reflex, 210
            StatId.Prowess, 165
            StatId.Poise, 150
            StatId.Intellect, 80
            StatId.Resolve, 140
            StatId.Acuity, 150
            StatId.Intuition, 160
            StatId.Acumen, 110
            StatId.Composure, 130 ]
      let rapier =
        { Name = "Twin Stiletto & Rapier"
          Slot = EquipmentSlot.Weapon
          Description = "Finely balanced blades forged for fluid, relentless fencing cadences."
          StatModifiers = [ StatId.Finesse, 25; StatId.Reflex, 10 ]
          HealthBonus = 0
          MoraleBonus = 0
          StartingRecklessnessDelta = 0
          Triggers = [] }
      let c = Combatant.createWithClass id "Protagonist" 2300 2100 stats CharacterClass.Duelist 35
      { c with
          Armor = ArmorIntegrity.Create 100
          EquippedItems = [ rapier; caltrops ]
          Stance = CombatStance.AgilityStance }

    | "warden" ->
      let stats =
        StatBlock.Create
          [ StatId.Force, 155
            StatId.Fortitude, 200
            StatId.Finesse, 135
            StatId.Reflex, 140
            StatId.Prowess, 205
            StatId.Poise, 215
            StatId.Intellect, 75
            StatId.Resolve, 170
            StatId.Acuity, 95
            StatId.Intuition, 140
            StatId.Acumen, 130
            StatId.Composure, 175 ]
      let shieldSet =
        { Name = "Iron Arming Sword & Tower Buckler"
          Slot = EquipmentSlot.Weapon
          Description = "A notched steel blade paired with an iron-banded buckler for unyielding defense."
          StatModifiers = [ StatId.Poise, 20; StatId.Prowess, 15; StatId.Fortitude, 20 ]
          HealthBonus = 200
          MoraleBonus = 100
          StartingRecklessnessDelta = -10
          Triggers = [] }
      let c = Combatant.createWithClass id "Protagonist" 2800 2000 stats CharacterClass.Warden 35
      { c with
          Armor = ArmorIntegrity.Create 160
          EquippedItems = [ shieldSet; caltrops ]
          Stance = CombatStance.DisciplineStance }

    | _ -> // Inquisitor / Arcanist
      let stats =
        StatBlock.Create
          [ StatId.Force, 90
            StatId.Fortitude, 140
            StatId.Finesse, 120
            StatId.Reflex, 130
            StatId.Prowess, 130
            StatId.Poise, 145
            StatId.Intellect, 225
            StatId.Resolve, 210
            StatId.Acuity, 165
            StatId.Intuition, 160
            StatId.Acumen, 160
            StatId.Composure, 155 ]
      let staff =
        { Name = "Carved Ash Staff"
          Slot = EquipmentSlot.Weapon
          Description = "A runic focus humming with psychic resonance and mental clarity."
          StatModifiers = [ StatId.Intellect, 25; StatId.Resolve, 15 ]
          HealthBonus = 0
          MoraleBonus = 250
          StartingRecklessnessDelta = 0
          Triggers = [] }
      let c = Combatant.createWithClass id "Protagonist" 2200 2600 stats CharacterClass.Inquisitor 35
      { c with
          Armor = ArmorIntegrity.Create 90
          EquippedItems = [ staff; caltrops ]
          Stance = CombatStance.PowerStance }
