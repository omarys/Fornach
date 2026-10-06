namespace Fornach.Engine

open System
open Fornach.Domain
open Fornach.Spatial

module TowerGenerator =

  /// Resolves the thematic surface type for a given floor theme
  let private surfaceForTheme (theme: FloorTheme) : SurfaceType =
    match theme with
    | QuarryPlazas -> PavedStone
    | PineCloisters -> ForestMoss
    | BasaltCalderas -> BasaltRock
    | TempestTerraces -> PavedStone
    | SunkenBoulevards -> ShallowWater
    | ElysianSanctuaries -> LilyPetals
    | CelestialSpires -> StarlitGlass

  /// Resolves the hazard type for a given floor theme
  let private hazardForTheme (theme: FloorTheme) : HazardType =
    match theme with
    | BasaltCalderas -> LavaRift
    | QuarryPlazas -> AcidSlag
    | TempestTerraces
    | SunkenBoulevards -> DeepCurrent
    | PineCloisters
    | ElysianSanctuaries
    | CelestialSpires -> CalmingSpores

  /// Generates an expansive architectural plaza
  type private Plaza =
    { Id: int
      X: int
      Y: int
      Width: int
      Height: int }

    member this.Center : Point =
      { X = this.X + this.Width / 2
        Y = this.Y + this.Height / 2 }

    member this.Contains (p: Point) : bool =
      p.X >= this.X && p.X < this.X + this.Width &&
      p.Y >= this.Y && p.Y < this.Y + this.Height

  /// Generates a complete Tower floor layout with open architectural plazas and no blocky caves
  let generateFloorWithMode (seed: int) (floorNumber: int) (isStory: bool) : TowerFloor =
    let rng = Random(seed + floorNumber * 1009)

    // 1. Determine Floor Theme
    let theme =
      match (floorNumber - 1) % 7 with
      | 0 -> QuarryPlazas
      | 1 -> PineCloisters
      | 2 -> BasaltCalderas
      | 3 -> TempestTerraces
      | 4 -> SunkenBoulevards
      | 5 -> ElysianSanctuaries
      | _ -> CelestialSpires

    let surface = surfaceForTheme theme
    let hazard = hazardForTheme theme

    let mapWidth = 64
    let mapHeight = 44

    // Default: Entire floor begins as boundless open Chasm/Void
    let mutable tiles = Map.empty<Point, TowerTile>
    for y in 0 .. mapHeight - 1 do
      for x in 0 .. mapWidth - 1 do
        tiles <- Map.add { X = x; Y = y } Chasm tiles

    // 2. Generate 4 to 6 Expansive Plazas distributed across quadrants
    let plazaPositions =
      [ (4, 4, 14, 12)
        (26, 4, 14, 12)
        (44, 4, 16, 12)
        (6, 24, 14, 14)
        (26, 24, 14, 14)
        (44, 24, 16, 14) ]

    let plazas =
      plazaPositions
      |> List.mapi (fun i (px, py, pw, ph) ->
        let jitterX = rng.Next(-2, 3)
        let jitterY = rng.Next(-2, 3)
        { Id = i
          X = Math.Clamp(px + jitterX, 2, mapWidth - 18)
          Y = Math.Clamp(py + jitterY, 2, mapHeight - 18)
          Width = pw
          Height = ph })

    // 3. Carve Plaza Floors and Colonnade Pillars
    for plaza in plazas do
      for py in plaza.Y .. plaza.Y + plaza.Height - 1 do
        for px in plaza.X .. plaza.X + plaza.Width - 1 do
          let pt = { X = px; Y = py }
          let isBorder =
            px = plaza.X || px = plaza.X + plaza.Width - 1 ||
            py = plaza.Y || py = plaza.Y + plaza.Height - 1

          // Architectural Colonnades: pillars placed at rhythmic intervals along border edges
          if isBorder && (px % 3 = 0 || py % 3 = 0) then
            tiles <- Map.add pt Pillar tiles
          elif (px = plaza.X + 3 || px = plaza.X + plaza.Width - 4) &&
               (py = plaza.Y + 3 || py = plaza.Y + plaza.Height - 4) then
            // Internal monument pillars
            tiles <- Map.add pt Pillar tiles
          else
            // Check for environmental hazards (e.g. small lava rifts or shallow water pools)
            if rng.Next(100) < 5 then
              tiles <- Map.add pt (Hazard hazard) tiles
            else
              tiles <- Map.add pt (Floor surface) tiles

    // 4. Carve Wide Colonnade Causeways (2 to 3 tiles wide) connecting plazas
    let carveCauseway (p1: Point) (p2: Point) =
      let minX, maxX = Math.Min(p1.X, p2.X), Math.Max(p1.X, p2.X)
      let minY, maxY = Math.Min(p1.Y, p2.Y), Math.Max(p1.Y, p2.Y)

      // Horizontal leg (width 2)
      for x in minX .. maxX do
        for dy in 0 .. 1 do
          let pt = { X = x; Y = p1.Y + dy }
          tiles <- Map.add pt (Floor surface) tiles

      // Vertical leg (width 2)
      for y in minY .. maxY do
        for dx in 0 .. 1 do
          let pt = { X = p2.X + dx; Y = y }
          tiles <- Map.add pt (Floor surface) tiles

      // Place open Archways at entrance and exit of the causeway
      tiles <- Map.add p1 Archway tiles
      tiles <- Map.add p2 Archway tiles

    // Connect sequential plazas to form a connected transit network
    for i in 0 .. plazas.Length - 2 do
      carveCauseway plazas.[i].Center plazas.[i + 1].Center

    // Cross-connect for loop exploration
    carveCauseway plazas.[0].Center plazas.[3].Center
    carveCauseway plazas.[2].Center plazas.[5].Center

    // 5. Establish Spawn and Stairway Portal
    let spawnPlaza = plazas.[0]
    let stairwayPlaza = plazas.[plazas.Length - 1]

    let spawn = spawnPlaza.Center
    let stairway = stairwayPlaza.Center

    // Make spawn and stairway tiles guaranteed walkable
    tiles <- Map.add spawn (Floor surface) tiles

    // Door State logic based on floor progression
    let keyIdForFloor = sprintf "key_floor_%d" floorNumber
    let questIdForFloor = sprintf "quest_floor_%d" floorNumber

    let doorState, requiredKey, requiredQuest =
      if isStory && floorNumber <= 6 then
        let kId = sprintf "keystone_stage_%d" floorNumber
        let bossName =
          match floorNumber with
          | 1 -> "Aspect of Guilt"
          | 2 -> "Aspect of Denial"
          | 3 -> "Aspect of Anger"
          | 4 -> "Aspect of Bargaining"
          | 5 -> "Aspect of Depression"
          | _ -> "Aspect of Acceptance"
        let hint = sprintf "Held by the %s in the central arena" bossName
        DoorState.LockedByKey(kId, sprintf "%s's Keystone" bossName, hint), Some kId, None
      else
        match floorNumber % 3 with
        | 1 ->
          DoorState.Open, None, None
        | 2 ->
          let hint = sprintf "Held by the Floor %d Guardian in the far terrace" floorNumber
          DoorState.LockedByKey(keyIdForFloor, sprintf "Floor %d Keystone" floorNumber, hint), Some keyIdForFloor, None
        | _ ->
          let req = sprintf "Speak with the Floor %d Scholar and recover the lost ancient archive" floorNumber
          DoorState.LockedByQuest(questIdForFloor, sprintf "The Ascension Trial of Floor %d" floorNumber, req), None, Some questIdForFloor

    tiles <- Map.add stairway (StairwayPortal doorState) tiles

    // 6. Populate Interactive Entities
    let mutable entities = Map.empty<Point, TowerEntity>

    // A. Battered Scavenger Chest (at spawn for Story Mode Stage 1)
    if isStory && floorNumber = 1 then
      let batteredChestPos = { X = spawn.X + 1; Y = spawn.Y }
      tiles <- Map.add batteredChestPos (Floor surface) tiles
      let caltropsItem : EquipmentItem =
        { Name = "Sharpened Caltrops"
          Slot = EquipmentSlot.MentalRelic
          Description = "A pouch of jagged iron spikes scavenged from the battered chest."
          StatModifiers = [ StatId.Reflex, 5 ]
          HealthBonus = 0
          MoraleBonus = 0
          StartingRecklessnessDelta = 0
          Triggers = [] }
      let batteredChest =
        { Id = "battered_chest"
          Description = "A battered iron chest resting in the mud, its lock broken and hanging loose."
          LootKeyId = None
          ItemReward = Some caltropsItem
          IsOpen = false }
      entities <- Map.add batteredChestPos (EntityChest batteredChest) entities

    // B. Floor NPC & Story Quests
    let npcPlaza = plazas.[1]
    let npcPos = { X = npcPlaza.Center.X + 2; Y = npcPlaza.Center.Y }
    tiles <- Map.add npcPos (Floor surface) tiles

    let npc =
      if isStory then
        match floorNumber with
        | 1 ->
          { Id = "npc_shivering_boy"
            Name = "The Shivering Boy"
            Role = "Lost Child"
            Dialogue =
              [ "M-mister... please! They dragged Lyra toward the iron pit! My twin sister... they said she was a tithe!"
                "Feral slag hounds and quarry deserters prowl the colonnades. Please, clear them and save her!"
                "The leader carries a heavy quarry hammer from the deep forge. He doesn't speak. He just crushes everything!" ]
            Quest =
              Some {
                Id = "quest_quarry_hounds"
                Title = "The Boy's Plea: Cull the Feral Slag Hounds"
                Description = "Defeat the feral beasts prowling the quarry colonnades."
                IsCompleted = false
                RewardKeyId = Some "quarry_talisman"
                RewardDescription = "Awards +40 Morale, 50 Souls, and the Talisman of Poise."
              }
            HasGivenReward = false }
        | 2 ->
          { Id = "npc_hermit_mist"
            Name = "Hermit of the Mist"
            Role = "Keeper of Memories"
            Dialogue =
              [ "Dense fog swallows every footstep into unnerving silence... Whispers chorus: 'She's at home waiting for you.'"
                "Beware the blight weavers and mist stalkers weaving webs of denial." ]
            Quest =
              Some {
                Id = "quest_denial_weavers"
                Title = "Trial of Denial: Dispel the Mist Weavers"
                Description = "Defeat the arachnid blight weavers infesting the sacred cloister."
                IsCompleted = false
                RewardKeyId = Some "cloak_clarity"
                RewardDescription = "Awards +40 Morale, 60 Souls, and the Cloak of Clarity."
              }
            HasGivenReward = false }
        | 3 ->
          { Id = "npc_slag_alchemist"
            Name = "The Slag Alchemist"
            Role = "Volcanic Hermit"
            Dialogue =
              [ "The caldera shakes with self-consuming fury! 'Whose fault was this? Why didn't anyone stop it?!'"
                "The molten beasts feed on burning rage." ]
            Quest =
              Some {
                Id = "quest_anger_magma"
                Title = "Trial of Anger: Subdue the Magma Wurms"
                Description = "Quell the burning beasts feeding the caldera's rage."
                IsCompleted = false
                RewardKeyId = Some "cinder_sigil"
                RewardDescription = "Awards +50 Health, 75 Souls, and the Cinder-Forged Sigil."
              }
            HasGivenReward = false }
        | 4 ->
          { Id = "npc_storm_navigator"
            Name = "The Storm Navigator"
            Role = "Promontory Lookout"
            Dialogue =
              [ "Gale-force rain lashes the cliff edge. 'If only I had left earlier... if only I turned the wheel...'"
                "The Arbiter demands unequal trades and desperate pacts." ]
            Quest =
              Some {
                Id = "quest_bargain_harpies"
                Title = "Trial of Bargaining: Silence the Gale Harpies"
                Description = "Overcome the howling wind harpies shrieking false promises above the abyss."
                IsCompleted = false
                RewardKeyId = Some "gale_pendant"
                RewardDescription = "Awards +40 Morale, 90 Souls, and the Gale-Wind Pendant."
              }
            HasGivenReward = false }
        | 5 ->
          { Id = "npc_priestess_waters"
            Name = "Priestess of Still Waters"
            Role = "Oracle of the Drowned"
            Dialogue =
              [ "Crushing silence drowns the flooded ruins. Total numbness and cognitive doldrums sap all will to fight."
                "The Drowned Sovereign feeds on the silence of the empty bedroom." ]
            Quest =
              Some {
                Id = "quest_depression_leeches"
                Title = "Trial of Depression: Dredge the Abyssal Leeches"
                Description = "Clear the abyssal leeches draining the light from the flooded causeway."
                IsCompleted = false
                RewardKeyId = Some "anchor_resolve"
                RewardDescription = "Awards +50 Morale, 100 Souls, and the Anchor of Resolve."
              }
            HasGivenReward = false }
        | 6 ->
          { Id = "npc_keeper_blossoms"
            Name = "Keeper of White Blossoms"
            Role = "Guardian of Acceptance"
            Dialogue =
              [ "White lilies bloom in serene, fragrant peace. 'The journey ends not with forgetting, but with peace.'"
                "Step into the meadow and face the final manifestation." ]
            Quest =
              Some {
                Id = "quest_acceptance_harmony"
                Title = "Trial of Acceptance: Harmony with the Meadow Seraphs"
                Description = "Commune with the serene guardians of the white meadow."
                IsCompleted = false
                RewardKeyId = Some "keepsake_pendant"
                RewardDescription = "Awards full vitality restoration and the Keepsake Pendant."
              }
            HasGivenReward = false }
        | _ ->
          { Id = sprintf "npc_floor_%d" floorNumber
            Name = "The Cosmic Astrologer"
            Role = "Tower Inhabitant"
            Dialogue = [ "You stand upon Celestial Spires."; "Beyond the colonnade lies the infinite ascent." ]
            Quest = None
            HasGivenReward = false }
      else
        let npcQuestOpt =
          match requiredQuest with
          | Some qId ->
            Some {
              Id = qId
              Title = sprintf "Trial of Floor %d" floorNumber
              Description = "The ascension gates remain barred by ancient decree. Prove your resolve."
              IsCompleted = false
              RewardKeyId = None
              RewardDescription = "Unlocks the stairway portal to the next floor."
            }
          | None -> None

        { Id = sprintf "npc_floor_%d" floorNumber
          Name =
            match theme with
            | QuarryPlazas -> "The Iron Foreman"
            | PineCloisters -> "Hermit of the Mist"
            | BasaltCalderas -> "The Slag Alchemist"
            | TempestTerraces -> "The Storm Navigator"
            | SunkenBoulevards -> "Priestess of Still Waters"
            | ElysianSanctuaries -> "Keeper of White Blossoms"
            | CelestialSpires -> "The Cosmic Astrologer"
          Role = "Tower Inhabitant"
          Dialogue =
            [ sprintf "Greetings, traveler. You stand upon %s." theme.Name
              theme.Description
              "Beyond the grand colonnade lies the stairway to higher tiers." ]
          Quest = npcQuestOpt
          HasGivenReward = false }

    entities <- Map.add npcPos (EntityNpc npc) entities

    // C. Floor Shrines
    let shrinePlaza = plazas.[2]
    let shrinePos = { X = shrinePlaza.Center.X - 2; Y = shrinePlaza.Center.Y }
    tiles <- Map.add shrinePos (Floor surface) tiles

    let shrine =
      { Id = sprintf "shrine_floor_%d" floorNumber
        Name = sprintf "Monolith of %s" theme.Name
        BlessingDescription = "Restores 40 Morale and calms accumulated Recklessness."
        IsUsed = false }

    entities <- Map.add shrinePos (EntityShrine shrine) entities

    // D. Treasure Chests
    let chestPlaza = plazas.[3]
    let chestPos = { X = chestPlaza.Center.X; Y = chestPlaza.Center.Y + 3 }
    tiles <- Map.add chestPos (Floor surface) tiles

    let relicItem : EquipmentItem =
      { Name = sprintf "Relic of %s" theme.Name
        Slot = EquipmentSlot.MentalRelic
        Description = sprintf "An ancient heirloom resonant with %s." theme.Name
        StatModifiers = [ StatId.Resolve, 15; StatId.Poise, 10 ]
        HealthBonus = 0
        MoraleBonus = 30
        StartingRecklessnessDelta = 0
        Triggers = [] }

    let chest =
      { Id = sprintf "chest_floor_%d" floorNumber
        Description = "An ornate iron-bound chest resting beneath an archway."
        LootKeyId = None
        ItemReward = Some relicItem
        IsOpen = false }

    entities <- Map.add chestPos (EntityChest chest) entities

    // E. Floor Enemies & Guardians (or Story Bosses)
    let guardianPlaza = plazas.[4]
    let guardianPos = guardianPlaza.Center
    tiles <- Map.add guardianPos (Floor surface) tiles

    let biomeMonsters = Bestiary.byBiome theme

    let guardianEnemy =
      if isStory && floorNumber <= 6 then
        let bossCombatant =
          match floorNumber with
          | 1 -> StoryBosses.createGuiltAspect ()
          | 2 -> StoryBosses.createDenialAspect ()
          | 3 -> StoryBosses.createAngerAspect ()
          | 4 -> StoryBosses.createBargainingAspect ()
          | 5 -> StoryBosses.createDepressionAspect ()
          | _ -> StoryBosses.createAcceptanceAspect ()
        let bossName =
          match floorNumber with
          | 1 -> "Aspect of Guilt"
          | 2 -> "Aspect of Denial"
          | 3 -> "Aspect of Anger"
          | 4 -> "Aspect of Bargaining"
          | 5 -> "Aspect of Depression"
          | _ -> "Aspect of Acceptance"
        { Id = sprintf "boss_stage_%d" floorNumber
          Name = bossName
          Combatant = bossCombatant
          DropsKeyId = requiredKey
          IsDefeated = false }
      else
        let monsterTemplate =
          match biomeMonsters with
          | [] -> Bestiary.slagHound
          | list ->
            let idx = Math.Min(list.Length - 1, (floorNumber - 1) % list.Length)
            list.[idx]
        let guardianCombatant = Bestiary.createMonster monsterTemplate
        { Id = sprintf "enemy_guardian_%d" floorNumber
          Name = monsterTemplate.Name
          Combatant = guardianCombatant
          DropsKeyId = requiredKey
          IsDefeated = false }

    entities <- Map.add guardianPos (EntityEnemy guardianEnemy) entities

    // F. Grinding Mob Encounters across other plazas
    let grindPlazaIndices = [ 1; 2; 3; 5 ]
    for i, pIdx in List.indexed grindPlazaIndices do
      if pIdx < plazas.Length then
        let p = plazas.[pIdx]
        let mobPos = { X = p.Center.X - 2; Y = p.Center.Y + 2 }
        tiles <- Map.add mobPos (Floor surface) tiles
        if not (Map.containsKey mobPos entities) && mobPos <> spawn && mobPos <> stairway then
          let mobTemplate =
            if biomeMonsters.IsEmpty then Bestiary.slagHound
            else
              let available =
                if floorNumber <= 1 then
                  // Floor 1: First plazas have Novice mobs (e.g. Slag Hound), later plazas have Veteran mobs (e.g. Stone Gargoyle)
                  if i < 2 then [ biomeMonsters.[0] ]
                  else [ biomeMonsters.[Math.Min(1, biomeMonsters.Length - 1)] ]
                elif floorNumber <= 3 then
                  // Floors 2-3: Novice and Veteran mobs
                  biomeMonsters |> List.truncate 2
                else
                  // Floors 4+: Full spectrum of biome mobs
                  biomeMonsters
              let mIdx = (floorNumber + i) % available.Length
              available.[mIdx]
          let mobCombatant = Bestiary.createMonster mobTemplate
          let mobEnemy =
            { Id = sprintf "grind_mob_%d_%d" floorNumber i
              Name = mobTemplate.Name
              Combatant = mobCombatant
              DropsKeyId = None
              IsDefeated = false }
          entities <- Map.add mobPos (EntityEnemy mobEnemy) entities

    // G. Dynamic World & Tower Floor Encounters (ADR 0004 Phase 3)
    let plazaCenters = plazas |> List.map (fun p -> p.Center)
    let floorEncounters = WorldEvents.generateFloorEncounters theme floorNumber plazaCenters

    for (encPos, enc) in floorEncounters do
      tiles <- Map.add encPos (Floor surface) tiles
      if encPos <> spawn && encPos <> stairway && not (Map.containsKey encPos entities) then
        entities <- Map.add encPos (EntityEncounter enc) entities

    // 7. Verify Path Connectivity using A* Pathfinding
    let isPassable (p: Point) =
      match Map.tryFind p tiles with
      | Some t -> t.IsWalkable
      | None -> false

    let path = Pathfinding.aStar isPassable spawn stairway
    match path with
    | Some _ -> ()
    | None ->
      // If path was obstructed by procedural jitter, carve direct emergency line
      carveCauseway spawn stairway

    { FloorNumber = floorNumber
      Theme = theme
      Width = mapWidth
      Height = mapHeight
      Tiles = tiles
      Entities = entities
      StairwayLocation = stairway
      SpawnLocation = spawn
      Explored = Set.empty.Add spawn
      Visible = Set.empty.Add spawn
      ActiveQuests = npc.Quest |> Option.toList }

  /// Generates a complete Tower floor layout with open architectural plazas
  let generateFloor (seed: int) (floorNumber: int) : TowerFloor =
    generateFloorWithMode seed floorNumber false
