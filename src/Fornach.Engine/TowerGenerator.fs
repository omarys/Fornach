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
    let themeOfIndex (index: int) =
      match index with
      | 0 -> QuarryPlazas
      | 1 -> PineCloisters
      | 2 -> BasaltCalderas
      | 3 -> TempestTerraces
      | 4 -> SunkenBoulevards
      | 5 -> ElysianSanctuaries
      | _ -> CelestialSpires

    let theme =
      if isStory then
        // Story floor 1 is the rain-soaked forest tutorial; the six grief stages shift
        // down one slot (floor 2 = Guilt quarry ... floor 7 = Acceptance meadow).
        if floorNumber = 1 then PineCloisters
        else themeOfIndex ((floorNumber - 2) % 7)
      else
        themeOfIndex ((floorNumber - 1) % 7)

    let surface = surfaceForTheme theme
    let hazard = hazardForTheme theme

    let mapWidth = 88
    let mapHeight = 56

    // Default: Entire floor begins as boundless open Chasm/Void
    let mutable tiles = Map.empty<Point, TowerTile>
    for y in 0 .. mapHeight - 1 do
      for x in 0 .. mapWidth - 1 do
        tiles <- Map.add { X = x; Y = y } Chasm tiles

    // 2. Rejection-sample 9 to 12 chambers, each kept at least 2 tiles of chasm clear of the last
    let roomCount = 9 + rng.Next(0, 4)

    let inflated (r: Plaza) =
      { r with
          X = r.X - 2
          Y = r.Y - 2
          Width = r.Width + 4
          Height = r.Height + 4 }

    let overlaps (a: Plaza) (b: Plaza) =
      a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height

    let mutable rooms : Plaza list = []
    let mutable attempts = 0

    while rooms.Length < roomCount && attempts < 300 do
      attempts <- attempts + 1
      let w = rng.Next(10, 17)
      let h = rng.Next(8, 13)

      let candidate =
        { Id = rooms.Length
          X = rng.Next(2, mapWidth - w - 2)
          Y = rng.Next(2, mapHeight - h - 2)
          Width = w
          Height = h }

      if not (rooms |> List.exists (fun r -> overlaps (inflated candidate) (inflated r))) then
        rooms <- rooms @ [ candidate ]

    // A starved sample would leave the six encounter anchors without rooms to sit in
    let fallbackRooms =
      [ 4, 4, 14, 12
        26, 4, 14, 12
        44, 4, 16, 12
        6, 24, 14, 14
        26, 24, 14, 14
        44, 24, 16, 14 ]
      |> List.mapi (fun i (px, py, pw, ph) ->
        { Id = i
          X = px
          Y = py
          Width = pw
          Height = ph })

    let plazas = if rooms.Length >= 6 then rooms else fallbackRooms

    // Room roles are index-based (1 NPC, 2 shrine, 3 chest, 4 guardian); clamp so a short
    // sample can never throw before the fallback above applies.
    let plazaAt (index: int) : Plaza = plazas.[Math.Min(index, plazas.Length - 1)]

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

    let chebyshev (a: Point) (b: Point) =
      Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y))

    let mutable links = Set.empty<int * int>

    let connect (i: int) (j: int) =
      let a, b = plazas.[i].Center, plazas.[j].Center
      carveCauseway a b

      // 2 endpoint archways plus up to 2 more along the run, so no two causeways look alike
      for _ in 1 .. rng.Next(0, 3) do
        let x = rng.Next(Math.Min(a.X, b.X), Math.Max(a.X, b.X) + 1)
        tiles <- Map.add { X = x; Y = a.Y } Archway tiles

      links <- links.Add((Math.Min(i, j), Math.Max(i, j)))

    // Spanning tree: every chamber opens one causeway to its nearest already-placed chamber
    for i in 1 .. plazas.Length - 1 do
      let nearest =
        [ 0 .. i - 1 ] |> List.minBy (fun j -> chebyshev plazas.[j].Center plazas.[i].Center)

      connect i nearest

    // Loops: ~20% of the remaining near pairs get a second path, capped at 3
    let mutable loopCount = 0

    for i in 0 .. plazas.Length - 1 do
      for j in i + 1 .. plazas.Length - 1 do
        if loopCount < 3
           && not (links.Contains((i, j)))
           && chebyshev plazas.[i].Center plazas.[j].Center < 30
           && rng.Next(5) = 0 then
          connect i j
          loopCount <- loopCount + 1

    // Dead ends: 2-wide spurs run off a chamber edge and stop in the chasm
    let carveStub (room: Plaza) (horizontal: bool) (sign: int) (length: int) =
      let startX, startY =
        if horizontal then
          (if sign > 0 then room.X + room.Width else room.X - 1),
          rng.Next(room.Y, room.Y + room.Height - 1)
        else
          rng.Next(room.X, room.X + room.Width - 1),
          (if sign > 0 then room.Y + room.Height else room.Y - 1)

      for step in 0 .. length - 1 do
        for spread in 0 .. 1 do
          let x = if horizontal then startX + sign * step else startX + spread
          let y = if horizontal then startY + spread else startY + sign * step

          if x >= 1 && x <= mapWidth - 2 && y >= 1 && y <= mapHeight - 2 then
            tiles <- Map.add { X = x; Y = y } (Floor surface) tiles

    for _ in 1 .. rng.Next(2, 5) do
      let room = plazas.[rng.Next(plazas.Length)]
      carveStub room (rng.Next(2) = 0) (if rng.Next(2) = 0 then -1 else 1) (rng.Next(4, 9))

    // 5. Spawn in the chamber nearest the top-left corner; stairway in the farthest one
    let origin = { X = 0; Y = 0 }
    let spawnPlaza = plazas |> List.minBy (fun p -> chebyshev p.Center origin)

    let spawn = spawnPlaza.Center

    let stairwayPlaza =
      plazas
      |> List.filter (fun p -> p.Center <> spawn)
      |> List.maxBy (fun p -> chebyshev p.Center spawn)

    let stairway = stairwayPlaza.Center

    // Make spawn and stairway tiles guaranteed walkable
    tiles <- Map.add spawn (Floor surface) tiles

    // Door State logic based on floor progression
    let keyIdForFloor = sprintf "key_floor_%d" floorNumber
    let questIdForFloor = sprintf "quest_floor_%d" floorNumber

    let doorState, requiredKey, requiredQuest =
      if isStory && floorNumber <= 7 then
        let kId = sprintf "keystone_stage_%d" floorNumber
        let bossName =
          match floorNumber with
          | 1 -> "Bandit Leader"
          | 2 -> "Aspect of Guilt"
          | 3 -> "Aspect of Denial"
          | 4 -> "Aspect of Anger"
          | 5 -> "Aspect of Bargaining"
          | 6 -> "Aspect of Depression"
          | _ -> "Aspect of Acceptance"
        let hint =
          if floorNumber = 1 then "Held by the Bandit Leader at the cave-mouth descending to the quarry"
          else sprintf "Held by the %s in the central arena" bossName
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
          StatPercentBonuses = [ StatId.Reflex, 11 ]
          HealthBonus = 0
          MoraleBonus = 0
          StartingRecklessnessDelta = 0
          Triggers = [] }
      // The prologue already has the player loot this chest (the class/weapon selection happens
      // before the crawl), so the in-map chest starts plundered and cannot re-offer a class pick.
      let batteredChest =
        { Id = "battered_chest"
          Description = "A battered iron chest resting in the mud, its lock broken and hanging loose."
          LootKeyId = None
          ItemReward = Some caltropsItem
          IsOpen = true }
      entities <- Map.add batteredChestPos (EntityChest batteredChest) entities

    // B. Floor NPC & Story Quests
    // Souls + Morale + key is the shape of six of the seven authored rewards; the odd two (a health
    // grant, a full restore) are written out where they are declared.
    let questReward souls morale keyId : QuestReward =
      { QuestReward.Empty with
          Souls = souls
          Morale = morale
          KeyId = keyId }

    let npcPlaza = plazaAt 1
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
              [ "M-mister... please! Bandits dragged my twin sister Lyra into the black pines! They said she was their bounty!"
                "Their leader camps by the cave-mouth in the woods, near the old quarry shaft. He wears iron and hits like a falling tree!"
                "Please... cut through them and bring my sister home." ]
            Quest =
              Some {
                Id = "quest_save_lyra"
                Title = "The Boy's Plea: Rescue Lyra"
                Description = "Cut through the bandit warband and defeat their leader to save the boy's twin sister."
                IsCompleted = false
                Reward = questReward 50 40 (Some "lyra_keepsake")
                RewardDescription = "Awards +40 Morale, 50 Souls, and a keepsake from Lyra."
                RequiredEnemyName = Some "Bandit Leader"
              }
            HasGivenReward = false }
        | 2 ->
          { Id = "npc_quarry_foreman"
            Name = "The Quarry Foreman"
            Role = "Iron Taskmaster"
            Dialogue =
              [ "So you are the one who pulled the girl from the bandits? Lyra, was it? The forest keeps what it takes... but you got her back."
                "These quarry colonnades crawl with feral slag hounds and stone gargoyles. Clear them and the deep roads will open." ]
            Quest =
              Some {
                Id = "quest_quarry_hounds"
                Title = "Trial of Guilt: Cull the Feral Slag Hounds"
                Description = "Defeat the feral beasts prowling the quarry colonnades."
                IsCompleted = false
                Reward = questReward 50 40 (Some "quarry_talisman")
                RewardDescription = "Awards +40 Morale, 50 Souls, and the Talisman of Poise."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 3 ->
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
                Reward = questReward 60 40 (Some "cloak_clarity")
                RewardDescription = "Awards +40 Morale, 60 Souls, and the Cloak of Clarity."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 4 ->
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
                Reward =
                  { QuestReward.Empty with
                      Souls = 75
                      Health = 50
                      KeyId = Some "cinder_sigil" }
                RewardDescription = "Awards +50 Health, 75 Souls, and the Cinder-Forged Sigil."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 5 ->
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
                Reward = questReward 90 40 (Some "gale_pendant")
                RewardDescription = "Awards +40 Morale, 90 Souls, and the Gale-Wind Pendant."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 6 ->
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
                Reward = questReward 100 50 (Some "anchor_resolve")
                RewardDescription = "Awards +50 Morale, 100 Souls, and the Anchor of Resolve."
                RequiredEnemyName = None
              }
            HasGivenReward = false }
        | 7 ->
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
                Reward =
                  { QuestReward.Empty with
                      FullVitality = true
                      KeyId = Some "keepsake_pendant" }
                RewardDescription = "Awards full vitality restoration and the Keepsake Pendant."
                RequiredEnemyName = None
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
              Reward = QuestReward.Empty
              RewardDescription = "Unlocks the stairway portal to the next floor."
              RequiredEnemyName = None
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

    // B2. Story Stage 1 places the captive sister beside the Bandit Leader's den. Her
    // quest only resolves once the leader falls, so the payoff reads as a reunion.
    if isStory && floorNumber = 1 then
      let lyraPlaza = plazaAt 4
      let lyraPos = { X = lyraPlaza.Center.X + 2; Y = lyraPlaza.Center.Y }
      tiles <- Map.add lyraPos (Floor surface) tiles
      let lyra =
        { Id = "npc_lyra"
          Name = "Lyra"
          Role = "The Boy's Twin Sister"
          Dialogue =
            [ "You... you came for me? The bandits are broken — you truly did it!"
              "I thought I would never see my brother again. Tell him I am coming home." ]
          Quest =
            Some {
              Id = "quest_save_lyra"
              Title = "The Boy's Plea: Rescue Lyra"
              Description = "Cut through the bandit warband and defeat their leader to save the boy's twin sister."
              IsCompleted = false
              Reward = questReward 50 40 (Some "lyra_keepsake")
              RewardDescription = "Awards +40 Morale, 50 Souls, and a keepsake from Lyra."
              RequiredEnemyName = Some "Bandit Leader"
            }
          HasGivenReward = false }
      entities <- Map.add lyraPos (EntityNpc lyra) entities
    // Two shrines per stage, one per class-appropriate stat pair, so a run can sharpen both halves of
    // its plane. Shrines never sit in the altar rooms (plaza indices 1 and 4 in `WorldEvents`).
    let shrineSites =
      [ 2, PrimaryPair, sprintf "shrine_floor_%d" floorNumber
        5, SecondaryPair, sprintf "shrine_floor_%d_prime" floorNumber ]

    for shrineIndex, boon, shrineId in shrineSites do
      let shrinePlaza = plazaAt shrineIndex
      let shrinePos = { X = shrinePlaza.Center.X - 2; Y = shrinePlaza.Center.Y }

      if not (entities |> Map.exists (fun pt _ -> pt = shrinePos)) then
        tiles <- Map.add shrinePos (Floor surface) tiles

        let shrine =
          { Id = shrineId
            Name = sprintf "Monolith of %s" theme.Name
            BlessingDescription = "Fully restores Health and Morale, repairs damaged Armor, restores Weapon to Pristine, and calms accumulated Recklessness."
            Boon = boon
            IsBlessed = false
            IsUsed = false }

        entities <- Map.add shrinePos (EntityShrine shrine) entities

    // D. Treasure Chests
    let chestPlaza = plazaAt 3
    let chestPos = { X = chestPlaza.Center.X; Y = chestPlaza.Center.Y + 3 }
    tiles <- Map.add chestPos (Floor surface) tiles

    let relicItem : EquipmentItem =
      { Name = sprintf "Relic of %s" theme.Name
        Slot = EquipmentSlot.MentalRelic
        Description = sprintf "An ancient heirloom resonant with %s." theme.Name
        StatPercentBonuses = [ StatId.Resolve, 33; StatId.Intuition, 22 ]
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
    // The player spawns in the entrance chamber and the stairway is in the far one. The guardian
    // must sit in neither, or a floor can open with the boss inside the room the player starts in,
    // or standing on the exit. It is still anchored to a room center, so "same room" is exactly
    // "same center point". A degenerate one-room floor has nowhere else to go, so fall back.
    let guardianPlaza =
      let away = plazas |> List.filter (fun p -> p.Center <> spawn && p.Center <> stairway)
      let pool = if List.isEmpty away then plazas else away
      pool.[Math.Min(4, pool.Length - 1)]

    let guardianPos = guardianPlaza.Center
    tiles <- Map.add guardianPos (Floor surface) tiles

    let biomeMonsters = Bestiary.byBiome theme

    let guardianEnemy =
      if isStory && floorNumber <= 7 then
        let bossCombatant =
          if floorNumber = 1 then
            // The tutorial guardian is a mundane bandit chieftain, not a Grief Aspect. Authored at
            // Veteran magnitude (level 30-35 band) and re-levelled by the floor's scaling like any other
            // enemy: at `player.Level + 10` it is the strongest thing on the floor, which is the point.
            let chieftain = TierFactory.createClassLevel CharacterClass.Warrior 32
            { chieftain with Name = "Bandit Leader" }
          else
            match floorNumber with
            | 2 -> StoryBosses.createGuiltAspect ()
            | 3 -> StoryBosses.createDenialAspect ()
            | 4 -> StoryBosses.createAngerAspect ()
            | 5 -> StoryBosses.createBargainingAspect ()
            | 6 -> StoryBosses.createDepressionAspect ()
            | _ -> StoryBosses.createAcceptanceAspect ()

        // Authored at Veteran magnitude; lift the Aspect to the tier its stage assumes. The tutorial
        // chieftain is already levelled, so the call leaves it untouched.
        let bossCombatant = StoryBosses.scaleToStage floorNumber bossCombatant
        let bossName =
          if floorNumber = 1 then "Bandit Leader"
          else
            match floorNumber with
            | 2 -> "Aspect of Guilt"
            | 3 -> "Aspect of Denial"
            | 4 -> "Aspect of Anger"
            | 5 -> "Aspect of Bargaining"
            | 6 -> "Aspect of Depression"
            | _ -> "Aspect of Acceptance"
        { Id = sprintf "boss_stage_%d" floorNumber
          Name = bossName
          Combatant = bossCombatant
          DropsKeyId = requiredKey
          IsDefeated = false
          ChaseCharge = 0.0 }
      else
        let monsterTemplate =
          match biomeMonsters with
          | [] -> Bestiary.slagHound
          | list ->
            let idx = Math.Min(list.Length - 1, (floorNumber - 1) % list.Length)
            let rotated = list.[idx]
            // The rotation alone used to drop GrandMaster templates on early floors (the Obsidian
            // Fiend, level 160, as the tower-floor-3 guardian). Clamp to the level this floor's
            // progression assumes and take the strongest legal template instead.
            let ceiling = ProgressionScale.storyGuardianLevel floorNumber
            if rotated.Level <= ceiling then rotated
            else
              match list |> List.filter (fun m -> m.Level <= ceiling) with
              | [] -> list |> List.minBy (fun m -> m.Level)
              | eligible -> eligible |> List.maxBy (fun m -> m.Level)
        let guardianCombatant = Bestiary.createMonster monsterTemplate
        { Id = sprintf "enemy_guardian_%d" floorNumber
          Name = monsterTemplate.Name
          Combatant = guardianCombatant
          DropsKeyId = requiredKey
          IsDefeated = false
          ChaseCharge = 0.0 }

    entities <- Map.add guardianPos (EntityEnemy guardianEnemy) entities

    // F. Roaming mob packs: 3 to 5 enemies per chamber, 2 per chamber on the tutorial floor
    let isFreeTile (pos: Point) =
      pos <> spawn
      && pos <> stairway
      && (match Map.tryFind pos tiles with
          | Some t -> t.IsWalkable
          | None -> false)
      && not (Map.containsKey pos entities)

    let mobOffsets =
      [ 2, 2
        -2, 2
        2, -2
        -2, -2
        3, 0
        -3, 0
        0, 3
        0, -3
        3, 3
        -3, -3
        // The mid ring, inside the same envelope as the corners: a room needs somewhere to put the
        // extra three mobs the density curve now asks for.
        3, 2
        -3, 2
        3, -2
        -3, -2
        2, 3
        -2, 3
        2, -3
        -2, -3 ]

    let mobSpecies (i: int) =
      if isStory && floorNumber = 1 then
        // Tutorial floor: alternate low-level bandits (Warrior / Rogue) with equally low-level woodland
        // creatures so a level-1 protagonist always has survivable fights. The PineCloisters Novice
        // template (Blight Sprite) is level 20 and would one-shot a level-1 player, so creatures are
        // generated at the tutorial level.
        //
        // Every mob is authored at the SAME level: `TowerSession.rescaleFloorEnemies` re-levels the floor
        // by multiplying pools with `target / authored`, so a ramp (this used to be `1 + i * 2`, up to
        // level 8) made equal-looking Lv.3 enemies span 478 to 2250 health — the 2250 one unkillable.
        // Only the class varies now; the re-levelling sets the real level.
        let tutorialLevel = 3

        if i % 2 = 0 then
          let cls = if (i / 2) % 2 = 0 then CharacterClass.Warrior else CharacterClass.Rogue
          let name = if cls = CharacterClass.Warrior then "Bandit" else "Bandit Scout"
          let combatant = TierFactory.createClassLevel cls tutorialLevel
          name, { combatant with Name = name }
        else
          let cls = if (i / 2) % 2 = 0 then CharacterClass.Rogue else CharacterClass.Warrior
          let name = if cls = CharacterClass.Rogue then "Forest Wolf" else "Bramble Boar"
          let combatant = TierFactory.createClassLevel cls tutorialLevel
          name, { combatant with Name = name }
      else
        let mobTemplate =
          if biomeMonsters.IsEmpty then Bestiary.slagHound
          else
            let available =
              if isStory && floorNumber <= 3 then
                // Story stages 2-3 are narrative tutorial tiers: keep encounters to the
                // Novice-tier biome mob so a Veteran (e.g. Thorn Weaver) never opens on
                // the protagonist.
                [ biomeMonsters.[0] ]
              elif floorNumber <= 1 then
                // Floor 1: First plazas have Novice mobs (e.g. Slag Hound), later plazas have Veteran mobs (e.g. Stone Gargoyle)
                if i < 2 then [ biomeMonsters.[0] ]
                else [ biomeMonsters.[Math.Min(1, biomeMonsters.Length - 1)] ]
              elif floorNumber <= 3 then
                // Floors 2-3: Novice and Veteran mobs
                biomeMonsters |> List.truncate 2
              else
                // Floors 4+: Full spectrum of biome mobs, clamped to what this stage's progression
                // assumes. The clamp existed for the guardian only, so story floor 5 happily placed a
                // level-175 Storm Drake three tiles from the player's spawn while the protagonist was
                // level 105. Same ceiling, same lowest-legal fallback.
                let ceiling = ProgressionScale.storyGuardianLevel floorNumber

                if not isStory then
                  biomeMonsters
                else
                  match biomeMonsters |> List.filter (fun m -> m.Level <= ceiling) with
                  | [] -> [ biomeMonsters |> List.minBy (fun m -> m.Level) ]
                  | legal -> legal
            let mIdx = (floorNumber + i) % available.Length
            available.[mIdx]
        mobTemplate.Name, Bestiary.createMonster mobTemplate

    let mutable mobIndex = 0

    for roomIndex in 0 .. plazas.Length - 1 do
      let room = plazas.[roomIndex]
      // Entrance chambers past Floor 1 are safe arrival thresholds (e.g. Quarry Foreman staging area)
      // and do not spawn roaming mob packs directly on top of the stairway landing.
      // Roaming density climbs smoothly with depth: Floor 1 tutorial fields 2 mobs/room;
      // Floor 2 (Quarry stage) fields 2 to 3 mobs/chamber so tactical duels remain readable;
      // deeper floors gradually scale up to full pack density.
      let targetCount =
        if room = spawnPlaza && (not isStory || floorNumber > 1) then 0
        elif isStory && floorNumber <= 1 then 2
        elif isStory && floorNumber <= 2 then 2 + rng.Next(0, 2)
        elif isStory && floorNumber <= 4 then 3 + rng.Next(0, 2)
        elif floorNumber <= 1 then 2
        else Math.Min(mobOffsets.Length, 4 + floorNumber / 2 + rng.Next(0, 2))
      let mutable placedCount = 0
      let mutable offsetIndex = 0

      while placedCount < targetCount && offsetIndex < mobOffsets.Length do
        let ox, oy = mobOffsets.[offsetIndex]
        offsetIndex <- offsetIndex + 1
        let mobPos = { X = room.Center.X + ox; Y = room.Center.Y + oy }

        if isFreeTile mobPos then
          let mobName, mobCombatant = mobSpecies mobIndex
          let mobEnemy =
            { Id = sprintf "grind_mob_%d_%d" floorNumber mobIndex
              Name = mobName
              Combatant = mobCombatant
              DropsKeyId = None
              IsDefeated = false
              ChaseCharge = 0.0 }
          tiles <- Map.add mobPos (Floor surface) tiles
          entities <- Map.add mobPos (EntityEnemy mobEnemy) entities
          placedCount <- placedCount + 1
          mobIndex <- mobIndex + 1

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
