namespace Fornach.Domain

open System
open Fornach.Spatial

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

/// Cost required to receive an altar's blessing
type AltarCost =
  | SacrificeHealth of amount: int
  | SacrificeMorale of amount: int
  | ShredArmor of amount: int

  member this.Description : string =
    match this with
    | SacrificeHealth amt -> sprintf "Sacrifice %d Health" amt
    | SacrificeMorale amt -> sprintf "Sacrifice %d Morale" amt
    | ShredArmor amt -> sprintf "Shred %d Armor durability" amt

/// Reward granted upon fulfilling an altar's sacrifice
type AltarReward =
  | StatBuff of stat: StatId * bonus: int
  | VitalitySurge of health: int * morale: int
  | KeyReward of keyId: string * keyName: string
  | RelicReward of EquipmentItem

  member this.Description : string =
    match this with
    | StatBuff (stat, bonus) -> sprintf "+%d %A" bonus stat
    | VitalitySurge (hp, mor) -> sprintf "+%d HP and +%d Morale" hp mor
    | KeyReward (_, keyName) -> sprintf "Keystone: %s" keyName
    | RelicReward item -> sprintf "Relic: %s" item.Name

/// Ancient sacrificial altar offering dangerous risk/reward dilemmas
type AltarChoice =
  { Id: string
    Name: string
    Description: string
    Cost: AltarCost
    Reward: AltarReward
    IsUsed: bool }

/// Monster pack configuration lurking in an ambush lair
type AmbushPack =
  { Leader: MonsterTemplate
    Minions: MonsterTemplate list }

/// Monster pack ambush lair guarding bottleneck archways and causeways
type AmbushData =
  { Id: string
    Name: string
    Pack: AmbushPack
    TriggerDescription: string
    IsTriggered: bool }

/// Rare item ware offered by spectral merchants
type MerchantItem =
  { Item: EquipmentItem
    CostSouls: int
    RequiredTrophy: (AlchemicalTrophy * int) option
    IsPurchased: bool }

/// Wandering spectral trader exchanging souls and trophies for relics
type SpectralMerchant =
  { Id: string
    Name: string
    Title: string
    Dialogue: string list
    Wares: MerchantItem list
    HasTraded: bool }

/// Locking mechanism or puzzle barring a treasure vault
type VaultPuzzle =
  | KeyholeLock of keyId: string * keyName: string * hint: string
  | StatCheck of stat: StatId * requiredValue: int * testDescription: string
  | MemoryCipher of riddle: string * answer: string

/// Sealed treasure vault guarding ancient relics behind puzzles or stat checks
type VaultData =
  { Id: string
    Name: string
    Description: string
    Puzzle: VaultPuzzle
    Relics: EquipmentItem list
    BonusSouls: int
    IsOpen: bool }

/// Hazardous mechanical trap types
type TrapType =
  | FloorSpikes of damage: int
  | DartVolley of armorShred: int * damage: int
  | HallucinogenicGas of moraleDrain: int * exhaustion: int
  | ArcaneDischarge of directDamage: int

/// Mechanical trap corridor or gauntlet testing player reflexes or perception
type TrapGauntletData =
  { Id: string
    Name: string
    TrapType: TrapType
    DisarmStat: StatId
    DisarmThreshold: int
    IsDisarmed: bool
    IsTriggered: bool }

/// Narrative memory fragment illuminating repressed trauma and the lost twin
type MemoryEchoData =
  { Id: string
    Title: string
    SensoryDetail: string
    MemoryTranscript: string list
    MoraleRecovery: int
    IsCommuned: bool }

/// Dynamic world and Tower floor encounter types (ADR 0004 Phase 3)
type FloorEncounter =
  | AmbushLair of AmbushData
  | SacrificialAltar of AltarChoice
  | WanderingTrader of SpectralMerchant
  | TreasureVault of VaultData
  | MechanicalTrapGauntlet of TrapGauntletData
  | MemoryEchoFragment of MemoryEchoData

/// Discrete interactive entities occupying points on the floor grid
type TowerEntity =
  | EntityNpc of TowerNpc
  | EntityEnemy of TowerEnemy
  | EntityChest of TowerChest
  | EntityShrine of TowerShrine
  | EntityEncounter of FloorEncounter

  member this.BlocksMovement : bool =
    match this with
    | EntityEnemy e -> not e.IsDefeated
    | EntityNpc _ -> true
    | EntityChest _ -> true
    | EntityShrine _ -> true
    | EntityEncounter enc ->
      match enc with
      | AmbushLair _ -> false
      | SacrificialAltar a -> not a.IsUsed
      | WanderingTrader _ -> true
      | TreasureVault v -> not v.IsOpen
      | MechanicalTrapGauntlet _ -> false
      | MemoryEchoFragment _ -> false

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
