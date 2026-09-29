namespace Fornach.Domain

open System
open Fornach.Spatial

/// Floor biomes recycling and expanding upon the 5 Grief environments and beyond
type FloorTheme =
  | QuarryPlazas
  | PineCloisters
  | BasaltCalderas
  | TempestTerraces
  | SunkenBoulevards
  | ElysianSanctuaries
  | CelestialSpires

  member this.Name : string =
    match this with
    | QuarryPlazas -> "The Obsidian Quarry Plazas"
    | PineCloisters -> "The Shrouded Pine Cloisters"
    | BasaltCalderas -> "The Basalt Caldera Causeways"
    | TempestTerraces -> "The Tempest Promontory Terraces"
    | SunkenBoulevards -> "The Sunken Metropolis Boulevards"
    | ElysianSanctuaries -> "The Elysian Meadow Sanctuaries"
    | CelestialSpires -> "The Celestial Spire Bridges"

  member this.Description : string =
    match this with
    | QuarryPlazas -> "Wide stone pavilions, steam-vent walkways, and iron gantry plazas in the dark rain."
    | PineCloisters -> "Expansive moonlit glades flanked by colossal ancient pines, mossy arches, and shifting mist."
    | BasaltCalderas -> "Broad obsidian causeways suspended over rivers of magma, glowing with crackling heat."
    | TempestTerraces -> "Sweeping stone terraces buffeted by gale-force spray above a roaring, boundless ocean."
    | SunkenBoulevards -> "Grand flooded marble avenues and drowned colonnades reflecting silent, calm floodwaters."
    | ElysianSanctuaries -> "Endless white lily sanctuaries bathed in gentle golden sunlight and fragrant breezes."
    | CelestialSpires -> "Starlit glass causeways piercing through the void, suspended beneath cosmic constellations."

  member this.ColorHex : string =
    match this with
    | QuarryPlazas -> "#6272a4" // Slate / Comment
    | PineCloisters -> "#50fa7b" // Green
    | BasaltCalderas -> "#ff5555" // Red
    | TempestTerraces -> "#8be9fd" // Cyan
    | SunkenBoulevards -> "#bd93f9" // Purple
    | ElysianSanctuaries -> "#f1fa8c" // Yellow / Gold
    | CelestialSpires -> "#ff79c6" // Pink / Cosmic

  member this.FloorGlyph : char =
    match this with
    | QuarryPlazas -> '.'
    | PineCloisters -> '"'
    | BasaltCalderas -> ','
    | TempestTerraces -> '~'
    | SunkenBoulevards -> '·'
    | ElysianSanctuaries -> '❀'
    | CelestialSpires -> '✧'

  member this.PillarGlyph : char =
    match this with
    | QuarryPlazas -> '∏'
    | PineCloisters -> '♠'
    | BasaltCalderas -> '▲'
    | TempestTerraces -> '☗'
    | SunkenBoulevards -> '∩'
    | ElysianSanctuaries -> '⛩'
    | CelestialSpires -> '✦'

  member this.ChasmGlyph : char =
    match this with
    | BasaltCalderas -> '≈' // Magma lake
    | TempestTerraces -> '≋' // Sea abyss
    | SunkenBoulevards -> '░' // Deep murky flood
    | _ -> ' ' // Open void

/// Surface terrain texture for floor tiles
type SurfaceType =
  | PavedStone
  | ForestMoss
  | BasaltRock
  | ShallowWater
  | LilyPetals
  | StarlitGlass

/// Environmental hazard types
type HazardType =
  | LavaRift
  | AcidSlag
  | DeepCurrent
  | CalmingSpores

/// The state of the Ascension Door leading to the next floor
type DoorState =
  | Open
  | LockedByKey of keyId: string * keyName: string * hint: string
  | LockedByQuest of questId: string * questTitle: string * requirement: string

/// Distinct architectural tile types (avoids blocky cave walls and hidden doors)
type TowerTile =
  /// Walkable open plaza or colonnade floor
  | Floor of SurfaceType
  /// Open chasm, void, or abyss: blocks movement, but transparent to line-of-sight and vision!
  | Chasm
  /// Architectural pillar or monolith: occupies 1 tile, blocks movement and vision, creating grand colonnades
  | Pillar
  /// Open architectural archway connecting courtyards: fully walkable and visible
  | Archway
  /// Walkable hazardous terrain that triggers side effects upon entering
  | Hazard of HazardType
  /// Grand Ascension Door leading to the stairway to the next floor
  | StairwayPortal of DoorState

  member this.IsWalkable : bool =
    match this with
    | Floor _
    | Archway
    | Hazard _
    | StairwayPortal _ -> true
    | Chasm
    | Pillar -> false

  member this.BlocksVision : bool =
    match this with
    | Pillar -> true
    | Floor _
    | Chasm // Chasms can be seen across!
    | Archway
    | Hazard _
    | StairwayPortal _ -> false

/// Quests offered by floor NPCs
type TowerQuest =
  { Id: string
    Title: string
    Description: string
    IsCompleted: bool
    RewardKeyId: string option
    RewardDescription: string }

/// NPCs found throughout the Tower with dialogue, lore, and quests
type TowerNpc =
  { Id: string
    Name: string
    Role: string
    Dialogue: string list
    Quest: TowerQuest option
    HasGivenReward: bool }

/// Floor enemies and guardians
type TowerEnemy =
  { Id: string
    Name: string
    Combatant: Combatant
    DropsKeyId: string option
    IsDefeated: bool }

/// Loot caches and treasure chests
type TowerChest =
  { Id: string
    Description: string
    LootKeyId: string option
    ItemReward: EquipmentItem option
    IsOpen: bool }

/// Ancient shrines providing blessings or relief
type TowerShrine =
  { Id: string
    Name: string
    BlessingDescription: string
    IsUsed: bool }

/// Discrete interactive entities occupying points on the floor grid
type TowerEntity =
  | EntityNpc of TowerNpc
  | EntityEnemy of TowerEnemy
  | EntityChest of TowerChest
  | EntityShrine of TowerShrine

  member this.BlocksMovement : bool =
    match this with
    | EntityEnemy e -> not e.IsDefeated
    | EntityNpc _ -> true
    | EntityChest _ -> true
    | EntityShrine _ -> true

/// Aggregate domain model representing a complete Tower floor
type TowerFloor =
  { FloorNumber: int
    Theme: FloorTheme
    Width: int
    Height: int
    Tiles: Map<Point, TowerTile>
    Entities: Map<Point, TowerEntity>
    StairwayLocation: Point
    SpawnLocation: Point
    Explored: Set<Point>
    Visible: Set<Point>
    ActiveQuests: TowerQuest list }

  /// Checks if a tile is valid and unobstructed for movement
  member this.CanMoveTo (pt: Point) : bool =
    match Map.tryFind pt this.Tiles with
    | Some tile when tile.IsWalkable ->
      match Map.tryFind pt this.Entities with
      | Some entity when entity.BlocksMovement -> false
      | _ -> true
    | _ -> false

  /// Checks if a tile blocks line of sight
  member this.IsOpaque (pt: Point) : bool =
    match Map.tryFind pt this.Tiles with
    | Some tile -> tile.BlocksVision
    | None -> true // Out of bounds is opaque

  /// Unlocks the stairway portal if locked by key or quest
  member this.UnlockStairway () : TowerFloor =
    let updatedTiles =
      match Map.tryFind this.StairwayLocation this.Tiles with
      | Some (StairwayPortal _) -> Map.add this.StairwayLocation (StairwayPortal DoorState.Open) this.Tiles
      | _ -> this.Tiles
    { this with Tiles = updatedTiles }

  /// Checks if the stairway portal is currently open
  member this.IsStairwayOpen : bool =
    match Map.tryFind this.StairwayLocation this.Tiles with
    | Some (StairwayPortal DoorState.Open) -> true
    | _ -> false
