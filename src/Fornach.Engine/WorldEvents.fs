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

  /// Creates an ancient sacrificial altar presenting risk vs reward dilemmas.
  /// Every number here is a percentage, not a flat sum: costs take a slice of the protagonist's maximum
  /// and rewards are read back off their own attributes at commune time, so neither side fades as they
  /// climb. The per-theme numbers keep their relative severity.
  /// Enlightenment points each defensive attribute of the bearer's *other* plane gains. Small on
  /// purpose: it is a temper, not a tier, and it lands on defences rather than on the pair the
  /// altar's relic already sharpens.
  let altarEnlightenment = 5

  /// A theme relic. Percentages, never flat points — the resolution onto the bearer's pair happens
  /// in `AltarReward.ResolvedFor`, so the authored list here is always empty.
  let private altarRelic (name: string) (description: string) : EquipmentItem =
    { Name = name
      Slot = EquipmentSlot.MentalRelic
      Description = description
      StatPercentBonuses = []
      HealthBonus = 0
      MoraleBonus = 0
      StartingRecklessnessDelta = 0
      Triggers = [] }

  /// The five themed altars hand a relic and an enlightenment. They used to hand a flat `+40` on
  /// the pair itself, which was a fifth of a level-40 attribute and a rounding error at 185.
  let private tempered (relic: EquipmentItem) (boon: StatBoon) (pairPercent: int) =
    AltarReward.TemperedRelic(relic, boon, pairPercent, altarEnlightenment)

  let createAltar (theme: FloorTheme) (floorNum: int) (boon: StatBoon) : AltarChoice =
    let boonIdSuffix =
      match boon with
      | PrimaryPair -> ""
      | SecondaryPair -> "_prime"

    match theme with
    | QuarryPlazas ->
      { Id = sprintf "altar_quarry_%d%s" floorNum boonIdSuffix
        Name = "Altar of the Iron Quarryman"
        Description = "A dark granite crucible bearing the scorched handprints of broken miners."
        Cost = AltarCost.SacrificeHealth 25
        Reward =
          tempered
            (altarRelic "Miner's Last Ember" "A palm-sized lump of quarry iron that never quite cools.")
            boon
            15
        IsUsed = false }

    | PineCloisters ->
      { Id = sprintf "altar_pine_%d%s" floorNum boonIdSuffix
        Name = "Shrine of the Weeping Willow"
        Description = "An ancient, moss-hung pine stump weeping sap that smells of sorrow."
        Cost = AltarCost.SacrificeMorale 30
        Reward =
          tempered
            (altarRelic "Weeping-Willow Locket" "Its sap was sealed in silver, and it still remembers every grief.")
            boon
            18
        IsUsed = false }

    | BasaltCalderas ->
      { Id = sprintf "altar_basalt_%d%s" floorNum boonIdSuffix
        Name = "Crucible of the Slag Martyr"
        Description = "A glowing basalt forge demanding the sacrificial tempering of armor."
        Cost = AltarCost.ShredArmor 30
        Reward =
          let relic : EquipmentItem =
            { Name = "Cinder-Forged Sigil"
              Slot = EquipmentSlot.MentalRelic
              Description = "A scorching medallion carved from living basalt magma."
              StatPercentBonuses = [ StatId.Force, 33; StatId.Finesse, 22 ]
              HealthBonus = 0
              MoraleBonus = 0
              StartingRecklessnessDelta = 10
              Triggers = [] }
          AltarReward.RelicReward relic
        IsUsed = false }

    | TempestTerraces ->
      { Id = sprintf "altar_tempest_%d%s" floorNum boonIdSuffix
        Name = "Gale-Worn Monolith of the Abyss"
        Description = "A wind-carved spire poised above the roaring ocean spray."
        Cost = AltarCost.SacrificeHealth 20
        // Resolve only, never flesh: it charges Health, so paying Health back would make the toll a
        // formality that is most generous at the point of death (see `AltarReward.RefundsItsOwnToll`).
        Reward = AltarReward.VitalitySurge(0, 35)
        IsUsed = false }

    | SunkenBoulevards ->
      { Id = sprintf "altar_sunken_%d%s" floorNum boonIdSuffix
        Name = "Font of the Drowned King"
        Description = "A flooded marble baptismal font reflecting undisturbed abyssal waters."
        Cost = AltarCost.SacrificeMorale 25
        Reward =
          tempered
            (altarRelic "Drowned King's Signet" "A tarnished ring whose crest the tides wore featureless.")
            boon
            15
        IsUsed = false }

    | ElysianSanctuaries ->
      { Id = sprintf "altar_elysian_%d%s" floorNum boonIdSuffix
        Name = "Seraphic Mirror of White Lilies"
        Description = "A crystal mirror surrounded by blossoming white lilies that hum with serene quiet."
        Cost = AltarCost.SacrificeHealth 15
        Reward =
          tempered
            (altarRelic "Lily-Bound Circlet" "White lilies wound into a circlet that will not stop humming.")
            boon
            10
        IsUsed = false }

    | CelestialSpires ->
      { Id = sprintf "altar_celestial_%d%s" floorNum boonIdSuffix
        Name = "Astral Pyre of Transcendence"
        Description = "A pillar of quiet starlight suspended over the cosmic abyss."
        Cost = AltarCost.SacrificeHealth 30
        Reward =
          tempered
            (altarRelic "Starfall Reliquary" "A shard of quiet starlight, cold enough to burn.")
            boon
            20
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
              StatPercentBonuses = [ StatId.Intellect, 27; StatId.Resolve, 27 ]
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
              StatPercentBonuses = [ StatId.Fortitude, 33; StatId.Finesse, 33 ]
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
  let createVault (theme: FloorTheme) (floorNum: int) (playerPlane: Plane) : VaultData =
    // The vault asks for a stat at a share of the tier its floor's level carries, not at a flat
    // `30 + floorNum * 2`. That flat number was mis-scaled at both ends: on floor 1 it demanded 32
    // Intellect while a Fighter carries 22 and a Warrior 15, so every physical archetype walked past the
    // first vault in the game; by floor 20 it asked for 70 while the tier it was gating had reached 201,
    // so it had stopped gating anything. Half the primary is the tier an archetype can reach with its
    // off-plane stat and the one it misses without it, which is the gate the vault means to be.
    let _, _, offTier, _, _, _ = ProgressionScale.physicalStatsForLevel (max 1 floorNum)
    let requirement = offTier

    // The vault asks the stat the archetype is *not* built around. `requirement` was already set to the
    // off-plane tier, but the stat itself still alternated by floor, so a Fighter was handed its own
    // primary on one floor and gated on its off-plane on another. Reading the plane keeps the gate honest
    // for every class: physical archetypes are asked to deduce, casters to work with their hands.
    let puzzleStat, statTest =
      match playerPlane with
      | Physical ->
        StatId.Intellect, "An intricate geometric arcane matrix requiring rigorous analytical deduction to balance."
      | Mental ->
        StatId.Finesse, "A complex multi-dial clockwork tumbler requiring deft mechanical dexterity to bypass."

    let puzzle =
      match floorNum % 3 with
      | 2 ->
        VaultPuzzle.KeyholeLock(sprintf "key_floor_%d" floorNum, sprintf "Floor %d Keystone" floorNum, "Carried by the hostile floor guardian.")
      | _ -> VaultPuzzle.StatCheck(puzzleStat, requirement, statTest)

    let relic : EquipmentItem =
      { Name = sprintf "Crown of %s" theme.Name
        Slot = EquipmentSlot.MentalRelic
        Description = sprintf "An ancient crown recovered from the locked vaults of %s." theme.Name
        StatPercentBonuses = [ StatId.Resolve, 40; StatId.Intuition, 31 ]
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
        DisarmStat = StatId.Poise
        DisarmThreshold = 28 + floorNum
        IsDisarmed = false
        IsTriggered = false }

    | CelestialSpires ->
      { Id = sprintf "trap_floor_%d" floorNum
        Name = "Temporal Distortion Rift"
        TrapType = TrapType.ArcaneDischarge 30
        DisarmStat = StatId.Prowess
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
        MoraleGrowth = 5
        IsCommuned = false }

    | PineCloisters ->
      { Id = "echo_unsent_message"
        Title = "The Unsent Message"
        SensoryDetail = "A glowing phone screen vibrating on the passenger seat, reflecting in the rain."
        MemoryTranscript =
          [ "A notification glowing in the dark: 'Mom - Where are you two? Dinner is getting cold.'"
            "\"Look out—!\""
            "A scream choked by the sound of tearing metal and shattering safety glass." ]
        MoraleGrowth = 5
        IsCommuned = false }

    | BasaltCalderas ->
      { Id = "echo_screeching_brakes"
        Title = "The Screech of Locked Brakes"
        SensoryDetail = "The acrid smell of smoking friction pads, crushed iron, and boiling radiator fluid."
        MemoryTranscript =
          [ "No horn. Just the violent, screeching skid of twelve tons of steel out of control."
            "Why didn't they stop? Why was the truck on our side of the divider?"
            "The world flips upside down in an instant of crushing thunder." ]
        MoraleGrowth = 6
        IsCommuned = false }

    | TempestTerraces ->
      { Id = "echo_paramedic_siren"
        Title = "The Paramedic's Siren"
        SensoryDetail = "Wailing Doppler sirens echoing across the dark highway, flashing red and blue on rain."
        MemoryTranscript =
          [ "\"Driver is conscious, but pulse is thready. What about the passenger?!\""
            "\"Get the hydraulic cutters! She's pinned—we're losing her rhythm!\""
            "Please... anyone... take me instead. Don't let her go." ]
        MoraleGrowth = 6
        IsCommuned = false }

    | SunkenBoulevards ->
      { Id = "echo_silent_ward"
        Title = "The Silent Intensive Care Ward"
        SensoryDetail = "The rhythmic, cold chirp of a heart monitor, the smell of sterile antiseptic, and silence."
        MemoryTranscript =
          [ "A hand limp and cold between trembling fingers."
            "The flatline tone that never ends."
            "You closed your eyes, wishing you had never woken up." ]
        MoraleGrowth = 7
        IsCommuned = false }

    | ElysianSanctuaries ->
      { Id = "echo_meadow_lake"
        Title = "The Meadow by the Lake"
        SensoryDetail = "Warm summer sunlight, the scent of blooming white lilies, and laughter in the grass."
        MemoryTranscript =
          [ "\"Promise me you won't get lost in the dark, okay?\""
            "\"You still have so much to live for. You have to climb back up.\""
            "A warm smile fading into the golden mist." ]
        MoraleGrowth = 8
        IsCommuned = false }

    | CelestialSpires ->
      { Id = "echo_ascent_beyond"
        Title = "The Ascent Beyond Regret"
        SensoryDetail = "Boundless starlight piercing through the obsidian clouds. The rain has finally stopped."
        MemoryTranscript =
          [ "\"I was never trapped in the Tower... I was waiting for you to forgive yourself.\""
            "\"Take the final step. Awaken.\"" ]
        MoraleGrowth = 10
        IsCommuned = false }

  let private euclid (a: Point) (b: Point) : float =
    let dx = float (a.X - b.X)
    let dy = float (a.Y - b.Y)
    sqrt (dx * dx + dy * dy)

  let private preferredRoomOffsets =
    [ (0, 0)
      (2, 1); (-2, 1); (2, -1); (-2, -1)
      (3, 2); (-3, 2); (3, -2); (-3, -2)
      (1, 2); (-1, 2); (1, -2); (-1, -2)
      (2, 0); (-2, 0); (0, 2); (0, -2)
      (1, 1); (-1, 1); (1, -1); (-1, -1)
      (1, 0); (-1, 0); (0, 1); (0, -1)
      (3, 0); (-3, 0); (0, 3); (0, -3)
      (3, 1); (-3, 1); (3, -1); (-3, -1) ]

  /// Picks a walkable, non-Pillar, non-Hazard, unoccupied `Floor` tile near `center`.
  let private pickFloorTile
    (center: Point)
    (spawn: Point)
    (stairway: Point)
    (tiles: Map<Point, TowerTile>)
    (entities: Map<Point, TowerEntity>)
    (occupied: Set<Point>)
    (extraFilter: Point -> bool)
    : Point option =
    preferredRoomOffsets
    |> List.tryPick (fun (dx, dy) ->
      let pt = { X = center.X + dx; Y = center.Y + dy }
      if pt = spawn
         || pt = stairway
         || Set.contains pt occupied
         || Map.containsKey pt entities
         || not (extraFilter pt) then
        None
      else
        match Map.tryFind pt tiles with
        | Some (Floor _) -> Some pt
        | _ -> None)

  /// Minimum Euclidean distance between any Sacrificial Altar and any Shrine (strictly above
  /// `EnemyDensityTests.sameRoomDistance = 6.0` so an altar never shares a chamber with a shrine).
  let private minAltarShrineDistance = 8.0

  /// Minimum Euclidean distance from spawn to count a room/tile as outside the entrance chamber.
  let private minNonSpawnDistance = 6.0

  /// Distributes dynamic floor encounters across all non-spawn rooms (and optional alcove tips)
  /// scored by distance from spawn/stairway, selecting walkable, non-Pillar, non-Hazard,
  /// unoccupied `Floor` tiles and guaranteeing Altars never share a room with a Shrine or spawn.
  let generateFloorEncountersInLayout
    (theme: FloorTheme)
    (floorNum: int)
    (spawn: Point)
    (stairway: Point)
    (plazaCenters: Point list)
    (alcoveCenters: Point list)
    (tiles: Map<Point, TowerTile>)
    (entities: Map<Point, TowerEntity>)
    (playerPlane: Plane)
    : (Point * FloorEncounter) list =
    if plazaCenters.IsEmpty then []
    else
      let shrinePositions =
        entities
        |> Map.toList
        |> List.choose (fun (pt, ent) ->
          match ent with
          | EntityShrine _ -> Some pt
          | _ -> None)

      let isSafeForAltar (pt: Point) =
        euclid pt spawn >= minNonSpawnDistance
        && shrinePositions |> List.forall (fun s -> euclid pt s >= minAltarShrineDistance)

      // Score all non-spawn rooms by distance from spawn (ascending: early -> mid -> far/outer)
      let nonSpawnRooms =
        plazaCenters
        |> List.filter (fun c -> euclid c spawn >= minNonSpawnDistance)
        |> List.sortBy (fun c -> euclid c spawn, c.X, c.Y)

      if nonSpawnRooms.IsEmpty then []
      else
        let n = nonSpawnRooms.Length
        let mutable occupied = Set.empty
        let mutable usedAltarRooms = Set.empty
        let mutable results = []

        let tryPlaceInCandidates (candidates: Point list) (extraFilter: Point -> bool) (encounter: FloorEncounter) : Point option =
          candidates
          |> List.tryPick (fun roomCenter ->
            match pickFloorTile roomCenter spawn stairway tiles entities occupied extraFilter with
            | Some pt ->
              occupied <- Set.add pt occupied
              results <- (pt, encounter) :: results
              Some roomCenter
            | None -> None)

        // 1. Sacrificial Altars: early-to-mid floor, never in spawn or sharing a room with a Shrine.
        let altarRooms =
          nonSpawnRooms
          |> List.filter (fun c -> shrinePositions |> List.forall (fun s -> euclid c s >= minAltarShrineDistance))

        let altar1 = FloorEncounter.SacrificialAltar (createAltar theme floorNum PrimaryPair)
        match tryPlaceInCandidates altarRooms isSafeForAltar altar1 with
        | Some usedRoom -> usedAltarRooms <- Set.add usedRoom usedAltarRooms
        | None -> ()

        let secondAltarData = createAltar theme floorNum SecondaryPair
        let altar2 =
          FloorEncounter.SacrificialAltar
            { secondAltarData with
                Id = secondAltarData.Id + "_b"
                Name = secondAltarData.Name + " (Weathered)" }
        let altar2Rooms =
          let midStart = max 0 (altarRooms.Length / 2)
          let rotated = (altarRooms |> List.skip midStart) @ (altarRooms |> List.take midStart)
          rotated |> List.filter (fun c -> not (Set.contains c usedAltarRooms))
        match tryPlaceInCandidates (altar2Rooms @ altarRooms) isSafeForAltar altar2 with
        | Some usedRoom -> usedAltarRooms <- Set.add usedRoom usedAltarRooms
        | None -> ()

        // Build mid-to-far room ordering (explicitly reaching outer rooms 6..11) plus alcoves.
        let midToFarRooms =
          let half = n / 2
          let outer = nonSpawnRooms |> List.skip half |> List.rev
          let inner = nonSpawnRooms |> List.take half |> List.rev
          outer @ inner

        let rotateBy (offset: int) (xs: 'a list) =
          if xs.IsEmpty then []
          else
            let k = ((offset % xs.Length) + xs.Length) % xs.Length
            (xs |> List.skip k) @ (xs |> List.take k)

        let farRotated shift = rotateBy (floorNum + shift) midToFarRooms
        let alcovesRotated shift = rotateBy (floorNum + shift) alcoveCenters
        let anyWalkable (_: Point) = true

        // 2. Memory Echo: prefer an alcove tip, otherwise a mid-to-far outer room
        let echoEnc = FloorEncounter.MemoryEchoFragment (createMemoryEcho theme floorNum)
        tryPlaceInCandidates (alcovesRotated 0 @ farRotated 0) anyWalkable echoEnc |> ignore

        // 3. Biome/Floor varied dynamic encounters (guaranteeing 1 Ambush + 1-2 additional POIs)
        match floorNum % 3 with
        | 0 ->
          tryPlaceInCandidates (alcovesRotated 1 @ farRotated 1) anyWalkable (FloorEncounter.WanderingTrader (createTrader theme floorNum)) |> ignore
          tryPlaceInCandidates (farRotated 2) anyWalkable (FloorEncounter.MechanicalTrapGauntlet (createTrap theme floorNum)) |> ignore
          tryPlaceInCandidates (farRotated 3) anyWalkable (FloorEncounter.AmbushLair (createAmbush theme floorNum)) |> ignore
        | 1 ->
          tryPlaceInCandidates (alcovesRotated 1 @ farRotated 1) anyWalkable (FloorEncounter.TreasureVault (createVault theme floorNum playerPlane)) |> ignore
          tryPlaceInCandidates (farRotated 2) anyWalkable (FloorEncounter.AmbushLair (createAmbush theme floorNum)) |> ignore
        | _ ->
          tryPlaceInCandidates (alcovesRotated 1 @ farRotated 1) anyWalkable (FloorEncounter.WanderingTrader (createTrader theme floorNum)) |> ignore
          tryPlaceInCandidates (farRotated 2) anyWalkable (FloorEncounter.AmbushLair (createAmbush theme floorNum)) |> ignore

        List.rev results

  /// Procedurally generates 3 to 5 diverse dynamic encounters for a floor based on biome and layout.
  /// Distributes encounters across all non-spawn rooms (including outer rooms 6..11) ordered by
  /// distance from spawn while keeping Altars out of spawn and traditional shrine rooms (indices 2, 5, 8).
  let generateFloorEncounters (theme: FloorTheme) (floorNum: int) (plazaCenters: Point list) (playerPlane: Plane) : (Point * FloorEncounter) list =
    match plazaCenters with
    | spawnCenter :: rest when rest.Length >= 5 ->
      // Exclude traditional shrine plaza indices (2, 5, 8) for Altar placement when tile/entity maps are not supplied.
      let indexedNonSpawn =
        plazaCenters
        |> List.indexed
        |> List.skip 1
        |> List.sortBy (fun (_, c) -> euclid c spawnCenter, c.X, c.Y)

      let altarSafe =
        indexedNonSpawn
        |> List.filter (fun (idx, _) -> idx <> 2 && idx <> 5 && idx <> 8)
        |> List.map snd

      let allNonSpawn = indexedNonSpawn |> List.map snd
      let outerFirst =
        let half = allNonSpawn.Length / 2
        (allNonSpawn |> List.skip half |> List.rev) @ (allNonSpawn |> List.take half |> List.rev)

      let pick (xs: Point list) (idx: int) =
        xs.[((idx % xs.Length) + xs.Length) % xs.Length]

      let altar1Center = pick altarSafe 0
      let altar2Center = pick altarSafe (max 1 (altarSafe.Length / 2))
      let echoCenter = pick outerFirst floorNum
      let primaryPoiCenter = pick outerFirst (floorNum + 1)
      let ambushCenter = pick outerFirst (floorNum + 2)
      let trapCenter = pick outerFirst (floorNum + 3)

      let secondAltar = createAltar theme floorNum SecondaryPair
      [ { X = echoCenter.X - 2; Y = echoCenter.Y + 1 }, FloorEncounter.MemoryEchoFragment (createMemoryEcho theme floorNum)
        { X = altar1Center.X + 2; Y = altar1Center.Y + 1 }, FloorEncounter.SacrificialAltar (createAltar theme floorNum PrimaryPair)
        { X = altar2Center.X + 2; Y = altar2Center.Y - 1 },
        FloorEncounter.SacrificialAltar
          { secondAltar with
              Id = secondAltar.Id + "_b"
              Name = secondAltar.Name + " (Weathered)" }
        match floorNum % 3 with
        | 0 ->
          { X = primaryPoiCenter.X - 2; Y = primaryPoiCenter.Y - 1 }, FloorEncounter.WanderingTrader (createTrader theme floorNum)
          { X = trapCenter.X - 2; Y = trapCenter.Y + 1 }, FloorEncounter.MechanicalTrapGauntlet (createTrap theme floorNum)
          { X = ambushCenter.X + 2; Y = ambushCenter.Y - 1 }, FloorEncounter.AmbushLair (createAmbush theme floorNum)
        | 1 ->
          { X = ambushCenter.X + 2; Y = ambushCenter.Y - 1 }, FloorEncounter.AmbushLair (createAmbush theme floorNum)
          { X = primaryPoiCenter.X - 2; Y = primaryPoiCenter.Y + 1 }, FloorEncounter.TreasureVault (createVault theme floorNum playerPlane)
        | _ ->
          { X = ambushCenter.X + 2; Y = ambushCenter.Y - 1 }, FloorEncounter.AmbushLair (createAmbush theme floorNum)
          { X = primaryPoiCenter.X - 2; Y = primaryPoiCenter.Y - 1 }, FloorEncounter.WanderingTrader (createTrader theme floorNum) ]
    | _ -> []
