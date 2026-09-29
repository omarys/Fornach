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
  let generateFloor (seed: int) (floorNumber: int) : TowerFloor =
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

    // A. Floor NPC
    let npcPlaza = plazas.[1]
    let npcPos = { X = npcPlaza.Center.X + 2; Y = npcPlaza.Center.Y }
    tiles <- Map.add npcPos (Floor surface) tiles

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

    let npc =
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

    // B. Floor Shrines
    let shrinePlaza = plazas.[2]
    let shrinePos = { X = shrinePlaza.Center.X - 2; Y = shrinePlaza.Center.Y }
    tiles <- Map.add shrinePos (Floor surface) tiles

    let shrine =
      { Id = sprintf "shrine_floor_%d" floorNumber
        Name = sprintf "Monolith of %s" theme.Name
        BlessingDescription = "Restores 40 Morale and calms accumulated Recklessness."
        IsUsed = false }

    entities <- Map.add shrinePos (EntityShrine shrine) entities

    // C. Treasure Chests
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

    // D. Floor Enemies & Guardians
    let guardianPlaza = plazas.[4]
    let guardianPos = guardianPlaza.Center
    tiles <- Map.add guardianPos (Floor surface) tiles

    let guardianStats =
      StatBlock.Create
        [ StatId.Force, 70 + floorNumber * 5
          StatId.Fortitude, 60 + floorNumber * 4
          StatId.Finesse, 65 + floorNumber * 3
          StatId.Reflex, 60 + floorNumber * 3
          StatId.Poise, 65 + floorNumber * 3
          StatId.Prowess, 65 + floorNumber * 3 ]

    let guardianCombatant =
      Combatant.create (CombatantId.New()) (sprintf "Floor %d Sentinel" floorNumber) (200 + floorNumber * 30) (180 + floorNumber * 20) guardianStats

    let guardianEnemy =
      { Id = sprintf "enemy_guardian_%d" floorNumber
        Name = sprintf "Sentinel of %s" theme.Name
        Combatant = guardianCombatant
        DropsKeyId = requiredKey
        IsDefeated = false }

    entities <- Map.add guardianPos (EntityEnemy guardianEnemy) entities

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
      ActiveQuests = npcQuestOpt |> Option.toList }
