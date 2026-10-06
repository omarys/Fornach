namespace Fornach.Engine

open System
open Fornach.Domain
open Fornach.Spatial

/// World and Tower Dynamic Encounters Subsystem (ADR 0004 Phase 3)
module WorldEvents =

  /// Creates a monster pack ambush tailored to floor theme and progression
  let createAmbush (theme: FloorTheme) (floorNum: int) : AmbushData =
    let themeMonsters = Bestiary.byBiome theme
    let leader, minions, packName, triggerDesc =
      match themeMonsters with
      | [] ->
        Bestiary.slagHound, [ Bestiary.slagHound; Bestiary.slagHound ],
        "Ambush: Pack of Slag Hounds",
        "The shadows twist violently! A feral pack of Slag Hounds lunges from the colonnade!"
      | [ single ] ->
        single, [ single; single ],
        sprintf "Ambush: Pack of %ss" single.Name,
        sprintf "The shadows twist violently! A pack of %ss lunges from the colonnade!" single.Name
      | first :: second :: rest ->
        if floorNum <= 1 then
          // Floor 1 (Prologue): Pack of Novice swarmers matching early player progression (no instant one-shots!)
          let leader = first
          let minions = [ first; first ]
          leader, minions,
          sprintf "Ambush: Pack of %ss" first.Name,
          sprintf "The shadows twist violently! A ravenous pack of %ss lunges from the colonnade!" first.Name
        elif floorNum <= 3 then
          // Floors 2-3: Veteran skirmisher accompanied by Novice swarmers
          let leader = second
          let minions = [ first; first ]
          leader, minions,
          sprintf "Ambush: %s & %s Pack" second.Name first.Name,
          sprintf "The shadows twist violently! A %s accompanied by %ss lunges from the colonnade!" second.Name first.Name
        else
          // Floors 4+: Elite Master brute leading seasoned skirmishers
          let leader = if rest.IsEmpty then second else rest.Head
          let minion1 = second
          let minion2 = first
          leader, [ minion1; minion2 ],
          sprintf "Ambush: %s Vanguard" leader.Name,
          sprintf "The shadows twist violently! A colossal %s emerges at the head of a deadly strike pack!" leader.Name

    { Id = sprintf "ambush_floor_%d" floorNum
      Name = packName
      Pack = { Leader = leader; Minions = minions }
      TriggerDescription = triggerDesc
      IsTriggered = false }

  /// Creates an ancient sacrificial altar presenting risk vs reward dilemmas
  let createAltar (theme: FloorTheme) (floorNum: int) : AltarChoice =
    match theme with
    | QuarryPlazas ->
      { Id = sprintf "altar_quarry_%d" floorNum
        Name = "Altar of the Iron Quarryman"
        Description = "A dark granite crucible bearing the scorched handprints of broken miners."
        Cost = AltarCost.SacrificeHealth 25
        Reward = AltarReward.StatBuff(StatId.Force, 15)
        IsUsed = false }

    | PineCloisters ->
      { Id = sprintf "altar_pine_%d" floorNum
        Name = "Shrine of the Weeping Willow"
        Description = "An ancient, moss-hung pine stump weeping sap that smells of sorrow."
        Cost = AltarCost.SacrificeMorale 30
        Reward = AltarReward.StatBuff(StatId.Resolve, 15)
        IsUsed = false }

    | BasaltCalderas ->
      { Id = sprintf "altar_basalt_%d" floorNum
        Name = "Crucible of the Slag Martyr"
        Description = "A glowing basalt forge demanding the sacrificial tempering of armor."
        Cost = AltarCost.ShredArmor 30
        Reward =
          let relic : EquipmentItem =
            { Name = "Cinder-Forged Sigil"
              Slot = EquipmentSlot.MentalRelic
              Description = "A scorching medallion carved from living basalt magma."
              StatModifiers = [ StatId.Prowess, 15; StatId.Poise, 10 ]
              HealthBonus = 0
              MoraleBonus = 0
              StartingRecklessnessDelta = 10
              Triggers = [] }
          AltarReward.RelicReward relic
        IsUsed = false }

    | TempestTerraces ->
      { Id = sprintf "altar_tempest_%d" floorNum
        Name = "Gale-Worn Monolith of the Abyss"
        Description = "A wind-carved spire poised above the roaring ocean spray."
        Cost = AltarCost.SacrificeHealth 20
        Reward = AltarReward.VitalitySurge(35, 35)
        IsUsed = false }

    | SunkenBoulevards ->
      { Id = sprintf "altar_sunken_%d" floorNum
        Name = "Font of the Drowned King"
        Description = "A flooded marble baptismal font reflecting undisturbed abyssal waters."
        Cost = AltarCost.SacrificeMorale 25
        Reward = AltarReward.StatBuff(StatId.Intuition, 15)
        IsUsed = false }

    | ElysianSanctuaries ->
      { Id = sprintf "altar_elysian_%d" floorNum
        Name = "Seraphic Mirror of White Lilies"
        Description = "A crystal mirror surrounded by blossoming white lilies that hum with serene quiet."
        Cost = AltarCost.SacrificeHealth 15
        Reward = AltarReward.StatBuff(StatId.Composure, 15)
        IsUsed = false }

    | CelestialSpires ->
      { Id = sprintf "altar_celestial_%d" floorNum
        Name = "Astral Pyre of Transcendence"
        Description = "A pillar of quiet starlight suspended over the cosmic abyss."
        Cost = AltarCost.SacrificeHealth 30
        Reward = AltarReward.StatBuff(StatId.Intellect, 20)
        IsUsed = false }

  let private themeMonstersForTrophy (theme: FloorTheme) : AlchemicalTrophy option =
    match theme with
    | BasaltCalderas -> Some Bestiary.slagHoundCore
    | PineCloisters -> Some Bestiary.weaverSilk
    | QuarryPlazas -> Some Bestiary.gargoyleTalisman
    | TempestTerraces -> Some Bestiary.tempestFeather
    | SunkenBoulevards -> Some Bestiary.specterDust
    | ElysianSanctuaries -> Some Bestiary.dawnBellFragment
    | CelestialSpires -> Some Bestiary.starlightShard

  /// Creates a spectral wandering trader with alchemical trophies and soul transactions
  let createTrader (theme: FloorTheme) (floorNum: int) : SpectralMerchant =
    let merchantName, title, greeting =
      match theme with
      | QuarryPlazas -> "The Slagbound Mason", "Spectral Craftsman", "I trade in the heavy iron left behind by those who broke."
      | PineCloisters -> "The Mist Weaver", "Cloistered Wanderer", "The fog parts only for those who carry the scent of beasts."
      | BasaltCalderas -> "The Cinder Hermit", "Forgemaster of Ashes", "Bring me the glowing hearts of caldera hounds, traveler."
      | TempestTerraces -> "The Gale Navigator", "Wreckage Surveyor", "The winds tore away my crew. Only their relics remain."
      | SunkenBoulevards -> "Priest of the Sunken Crypt", "Abyssal Antiquarian", "Drowned treasures for souls that still retain their flame."
      | ElysianSanctuaries -> "The Silent Chronicler", "Keeper of Petals", "Even in paradise, remnants of earthly struggle hold quiet power."
      | CelestialSpires -> "The Chrono-Astrologer", "Voice of the Spire", "Time is fluid here. Trade your essence for relics of the stars."

    let wares =
      [ { Item =
            { Name = sprintf "%s Talisman" theme.Name
              Slot = EquipmentSlot.MentalRelic
              Description = sprintf "An antique charm saturated with the essence of %s." theme.Name
              StatModifiers = [ StatId.Intuition, 12; StatId.Acumen, 12 ]
              HealthBonus = 0
              MoraleBonus = 25
              StartingRecklessnessDelta = 0
              Triggers = [] }
          CostSouls = 50 + floorNum * 10
          RequiredTrophy = None
          IsPurchased = false }
        { Item =
            { Name = sprintf "%s Vanguard Cuirass" theme.Name
              Slot = EquipmentSlot.Armor
              Description = "Heavy ceremonial plate reinforced against elemental hazards."
              StatModifiers = [ StatId.Fortitude, 15; StatId.Poise, 15 ]
              HealthBonus = 25
              MoraleBonus = 0
              StartingRecklessnessDelta = 0
              Triggers = [] }
          CostSouls = 80 + floorNum * 15
          RequiredTrophy =
            match themeMonstersForTrophy theme with
            | Some trophy -> Some (trophy, 1)
            | None -> None
          IsPurchased = false } ]

    { Id = sprintf "trader_floor_%d" floorNum
      Name = merchantName
      Title = title
      Dialogue = [ greeting; "Everything has a price in the Tower. Do you seek to trade?" ]
      Wares = wares
      HasTraded = false }

  /// Creates a sealed treasure vault guarding relics behind puzzles or stat checks
  let createVault (theme: FloorTheme) (floorNum: int) : VaultData =
    let puzzle =
      match floorNum % 3 with
      | 0 ->
        VaultPuzzle.StatCheck(StatId.Finesse, 30 + floorNum * 2, "A complex multi-dial clockwork tumbler requiring deft mechanical dexterity to bypass.")
      | 1 ->
        VaultPuzzle.StatCheck(StatId.Intellect, 30 + floorNum * 2, "An intricate geometric arcane matrix requiring rigorous analytical deduction to balance.")
      | _ ->
        VaultPuzzle.KeyholeLock(sprintf "key_floor_%d" floorNum, sprintf "Floor %d Keystone" floorNum, "Carried by the hostile floor guardian.")

    let relic : EquipmentItem =
      { Name = sprintf "Crown of %s" theme.Name
        Slot = EquipmentSlot.MentalRelic
        Description = sprintf "An ancient crown recovered from the locked vaults of %s." theme.Name
        StatModifiers = [ StatId.Resolve, 18; StatId.Intuition, 14; StatId.Acumen, 14 ]
        HealthBonus = 15
        MoraleBonus = 40
        StartingRecklessnessDelta = 0
        Triggers = [] }

    { Id = sprintf "vault_floor_%d" floorNum
      Name = sprintf "Vault of %s" theme.Name
      Description = "A sealed chamber of reinforced obsidian and brass, housing forgotten treasures."
      Puzzle = puzzle
      Relics = [ relic ]
      BonusSouls = 75 + floorNum * 15
      IsOpen = false }

  /// Creates a mechanical trap corridor or gauntlet
  let createTrap (theme: FloorTheme) (floorNum: int) : TrapGauntletData =
    match theme with
    | QuarryPlazas ->
      { Id = sprintf "trap_floor_%d" floorNum
        Name = "Pneumatic Floor Spikes"
        TrapType = TrapType.FloorSpikes 20
        DisarmStat = StatId.Reflex
        DisarmThreshold = 28 + floorNum
        IsDisarmed = false
        IsTriggered = false }

    | PineCloisters ->
      { Id = sprintf "trap_floor_%d" floorNum
        Name = "Hallucinogenic Pine Spore Vent"
        TrapType = TrapType.HallucinogenicGas(25, 15)
        DisarmStat = StatId.Intuition
        DisarmThreshold = 28 + floorNum
        IsDisarmed = false
        IsTriggered = false }

    | BasaltCalderas ->
      { Id = sprintf "trap_floor_%d" floorNum
        Name = "Pressurized Magma Geyser"
        TrapType = TrapType.ArcaneDischarge 25
        DisarmStat = StatId.Reflex
        DisarmThreshold = 30 + floorNum
        IsDisarmed = false
        IsTriggered = false }

    | TempestTerraces ->
      { Id = sprintf "trap_floor_%d" floorNum
        Name = "Spring-Loaded Gale Dart Volley"
        TrapType = TrapType.DartVolley(20, 10)
        DisarmStat = StatId.Finesse
        DisarmThreshold = 30 + floorNum
        IsDisarmed = false
        IsTriggered = false }

    | SunkenBoulevards ->
      { Id = sprintf "trap_floor_%d" floorNum
        Name = "Murky Siphon Whirlpool"
        TrapType = TrapType.HallucinogenicGas(15, 25)
        DisarmStat = StatId.Fortitude
        DisarmThreshold = 28 + floorNum
        IsDisarmed = false
        IsTriggered = false }

    | ElysianSanctuaries ->
      { Id = sprintf "trap_floor_%d" floorNum
        Name = "Blinding Solar Prism"
        TrapType = TrapType.ArcaneDischarge 20
        DisarmStat = StatId.Composure
        DisarmThreshold = 28 + floorNum
        IsDisarmed = false
        IsTriggered = false }

    | CelestialSpires ->
      { Id = sprintf "trap_floor_%d" floorNum
        Name = "Temporal Distortion Rift"
        TrapType = TrapType.ArcaneDischarge 30
        DisarmStat = StatId.Acumen
        DisarmThreshold = 32 + floorNum
        IsDisarmed = false
        IsTriggered = false }

  /// Creates narrative roadside memory fragments connecting to amnesia and the lost twin
  let createMemoryEcho (theme: FloorTheme) (floorNum: int) : MemoryEchoData =
    match theme with
    | QuarryPlazas ->
      { Id = "echo_wet_asphalt"
        Title = "The Scent of Wet Asphalt"
        SensoryDetail = "A sudden chill. The smell of cold autumn rain and burning rubber on slick asphalt."
        MemoryTranscript =
          [ "\"Are you ready to head home? It's pouring outside...\""
            "Lyra's laugh. The windshield wipers rhythmic thumping: back and forth, back and forth."
            "A sudden blinding glare of twin high-beams piercing through the dark rain." ]
        MoraleRecovery = 30
        IsCommuned = false }

    | PineCloisters ->
      { Id = "echo_unsent_message"
        Title = "The Unsent Message"
        SensoryDetail = "A glowing phone screen vibrating on the passenger seat, reflecting in the rain."
        MemoryTranscript =
          [ "A notification glowing in the dark: 'Mom - Where are you two? Dinner is getting cold.'"
            "\"Look out—!\""
            "A scream choked by the sound of tearing metal and shattering safety glass." ]
        MoraleRecovery = 30
        IsCommuned = false }

    | BasaltCalderas ->
      { Id = "echo_screeching_brakes"
        Title = "The Screech of Locked Brakes"
        SensoryDetail = "The acrid smell of smoking friction pads, crushed iron, and boiling radiator fluid."
        MemoryTranscript =
          [ "No horn. Just the violent, screeching skid of twelve tons of steel out of control."
            "Why didn't they stop? Why was the truck on our side of the divider?"
            "The world flips upside down in an instant of crushing thunder." ]
        MoraleRecovery = 35
        IsCommuned = false }

    | TempestTerraces ->
      { Id = "echo_paramedic_siren"
        Title = "The Paramedic's Siren"
        SensoryDetail = "Wailing Doppler sirens echoing across the dark highway, flashing red and blue on rain."
        MemoryTranscript =
          [ "\"Driver is conscious, but pulse is thready. What about the passenger?!\""
            "\"Get the hydraulic cutters! She's pinned—we're losing her rhythm!\""
            "Please... anyone... take me instead. Don't let her go." ]
        MoraleRecovery = 35
        IsCommuned = false }

    | SunkenBoulevards ->
      { Id = "echo_silent_ward"
        Title = "The Silent Intensive Care Ward"
        SensoryDetail = "The rhythmic, cold chirp of a heart monitor, the smell of sterile antiseptic, and silence."
        MemoryTranscript =
          [ "A hand limp and cold between trembling fingers."
            "The flatline tone that never ends."
            "You closed your eyes, wishing you had never woken up." ]
        MoraleRecovery = 40
        IsCommuned = false }

    | ElysianSanctuaries ->
      { Id = "echo_meadow_lake"
        Title = "The Meadow by the Lake"
        SensoryDetail = "Warm summer sunlight, the scent of blooming white lilies, and laughter in the grass."
        MemoryTranscript =
          [ "\"Promise me you won't get lost in the dark, okay?\""
            "\"You still have so much to live for. You have to climb back up.\""
            "A warm smile fading into the golden mist." ]
        MoraleRecovery = 50
        IsCommuned = false }

    | CelestialSpires ->
      { Id = "echo_ascent_beyond"
        Title = "The Ascent Beyond Regret"
        SensoryDetail = "Boundless starlight piercing through the obsidian clouds. The rain has finally stopped."
        MemoryTranscript =
          [ "\"I was never trapped in the Tower... I was waiting for you to forgive yourself.\""
            "\"Take the final step. Awaken.\"" ]
        MoraleRecovery = 60
        IsCommuned = false }

  /// Procedurally generates 2 to 4 diverse dynamic encounters for a floor based on biome and layout
  let generateFloorEncounters (theme: FloorTheme) (floorNum: int) (plazaCenters: Point list) : (Point * FloorEncounter) list =
    match plazaCenters with
    | p0 :: p1 :: p2 :: p3 :: p4 :: p5 :: _ ->
      [ // 1. Memory Echo on every floor (narrative core of the grief ascent)
        let echoPos = { X = p0.X - 3; Y = p0.Y + 2 }
        echoPos, FloorEncounter.MemoryEchoFragment (createMemoryEcho theme floorNum)

        // 2. Sacrificial Altar
        let altarPos = { X = p2.X + 3; Y = p2.Y + 2 }
        altarPos, FloorEncounter.SacrificialAltar (createAltar theme floorNum)

        // 3. Biome/Floor varied dynamic encounters (1 to 2 additional, guaranteeing 3 to 4 total)
        match floorNum % 3 with
        | 0 ->
          let traderPos = { X = p1.X - 3; Y = p1.Y - 2 }
          let trapPos = { X = p4.X - 3; Y = p4.Y + 2 }
          traderPos, FloorEncounter.WanderingTrader (createTrader theme floorNum)
          trapPos, FloorEncounter.MechanicalTrapGauntlet (createTrap theme floorNum)
        | 1 ->
          let ambushPos = { X = p3.X + 3; Y = p3.Y - 2 }
          let vaultPos = { X = p3.X - 3; Y = p3.Y + 2 }
          ambushPos, FloorEncounter.AmbushLair (createAmbush theme floorNum)
          vaultPos, FloorEncounter.TreasureVault (createVault theme floorNum)
        | _ ->
          let ambushPos = { X = p3.X + 3; Y = p3.Y - 2 }
          let traderPos = { X = p1.X - 3; Y = p1.Y - 2 }
          ambushPos, FloorEncounter.AmbushLair (createAmbush theme floorNum)
          traderPos, FloorEncounter.WanderingTrader (createTrader theme floorNum)
      ]
    | _ -> []
