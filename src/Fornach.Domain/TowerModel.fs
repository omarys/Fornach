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

/// Quests offered by floor NPCs.
/// When `RequiredEnemyName` is set, the quest cannot be completed until the named
/// enemy on the current floor has been defeated.
/// What a finished quest actually pays out.
///
/// It exists because the payout used to be a hardcoded +50 Souls / +40 Morale in `TowerSession`
/// while the authored `RewardDescription` promised 60/75/90/100 Souls, health restores and
/// keepsakes. Two sources of truth, and only the prose was ever read. Granting reads this record,
/// so the completion card and the actual reward cannot drift.
type QuestReward =
  { Souls: int
    /// Flat points restored to the pool, clamped at its maximum by `Pool.ApplyDelta`.
    Health: int
    Morale: int
    /// Fills Health and Morale before the flat amounts above are added.
    FullVitality: bool
    /// Key added to the keyring — this is what a later chest or door tests for.
    KeyId: string option }

  static member Empty = { Souls = 0; Health = 0; Morale = 0; FullVitality = false; KeyId = None }

/// Quests found on NPCs. The reward is machine-readable (`QuestReward`); `RewardDescription` is the
/// card's prose and is expected to say the same thing in words.
type TowerQuest =
  { Id: string
    Title: string
    Description: string
    IsCompleted: bool
    Reward: QuestReward
    RewardDescription: string
    RequiredEnemyName: string option }

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
    IsDefeated: bool
    /// Fraction of a pursuit step banked from earlier turns. Carries the Finesse-gap pace across
    /// turns so a slower pursuer closes over several turns; see TowerSession.paceOf.
    ChaseCharge: float }
  /// The floor's boss: the story boss and the stage keystone guardian. Everything else is a mob.
  member this.IsBoss : bool =
    this.Id.StartsWith("boss_") || this.Id.StartsWith("enemy_guardian_")

/// Loot caches and treasure chests
type TowerChest =
  { Id: string
    Description: string
    LootKeyId: string option
    ItemReward: EquipmentItem option
    IsOpen: bool }

/// Which of the two class-appropriate stat pairs a boon sharpens: the plane's first pair (a Fighter's
/// Force/Fortitude, a Mage's Intellect/Resolve) or its second (Finesse/Reflex, Acuity/Intuition).
/// Floors — shrines and altars alike — are laid out without knowing the protagonist, so the slot is
/// stored and resolved against their plane when the boon is claimed.
type StatBoon =
  | PrimaryPair
  | SecondaryPair

  member this.Description =
    match this with
    | PrimaryPair -> "first"
    | SecondaryPair -> "second"

/// The two attributes a slot names on a given plane. A Fighter is only ever sharpened on physical
/// attributes and a Mage only on mental ones, so this is the single place the class lock lives.
module StatBoon =
  let pairOf (plane: Plane) (boon: StatBoon) : StatId * StatId =
    match plane, boon with
    | Physical, PrimaryPair -> StatId.Force, StatId.Fortitude
    | Physical, SecondaryPair -> StatId.Finesse, StatId.Reflex
    | Mental, PrimaryPair -> StatId.Intellect, StatId.Resolve
    | Mental, SecondaryPair -> StatId.Acuity, StatId.Intuition

/// Ancient shrines providing blessings or relief
type TowerShrine =
  { Id: string
    Name: string
    BlessingDescription: string
    Boon: StatBoon
    /// The stat blessing is one-time; the restorative blessing repeats on every visit.
    IsBlessed: bool
    IsUsed: bool }

/// Cost required to receive an altar's blessing. The amount is a PERCENTAGE of the player's
/// maximum, applied when the altar is accepted, so the toll still bites once pools run to thousands.
type AltarCost =
  | SacrificeHealth of amount: int
  | SacrificeMorale of amount: int
  | ShredArmor of amount: int

  member this.Description : string =
    match this with
    | SacrificeHealth amt -> sprintf "Sacrifice %d%% Health" amt
    | SacrificeMorale amt -> sprintf "Sacrifice %d%% Morale" amt
    | ShredArmor amt -> sprintf "Shred %d%% Armor durability" amt

/// Reward granted upon fulfilling an altar's sacrifice.
type AltarReward =
  /// Authored form: sharpens one of the bearer's two class pairs by a flat `bonus` points each.
  | PairBuff of boon: StatBoon * bonus: int
  /// Resolved form of `PairBuff`: carries the concrete attributes so the grant can be described.
  | StatBuff of stats: StatId list * bonus: int
  | VitalitySurge of healthPct: int * moralePct: int
  | KeyReward of keyId: string * keyName: string
  | RelicReward of EquipmentItem

  member this.Description : string =
    match this with
    | PairBuff (boon, bonus) -> sprintf "+%d %s pair" bonus boon.Description
    | StatBuff (stats, bonus) ->
      sprintf "+%d %s" bonus (stats |> List.map (sprintf "%A") |> String.concat " and ")
    | VitalitySurge (hpPct, morPct) -> sprintf "+%d%% HP and +%d%% Morale" hpPct morPct
    | KeyReward (_, keyName) -> sprintf "Keystone: %s" keyName
    | RelicReward item -> sprintf "Relic: %s" item.Name

  /// The reward as a given plane can actually receive it. A Fighter may only be granted physical
  /// attributes and a Mage only mental ones, so a pair buff is resolved against the bearer's plane
  /// here and nothing authored can name a stat on the other side of the veil. Surges, keystones and
  /// relics are plane-neutral. Callers must describe and apply the resolved reward, so the offer a
  /// player reads is the boon they are handed.
  member this.ResolvedFor(plane: Plane) : AltarReward =
    match this with
    | PairBuff (boon, bonus) ->
      let first, second = StatBoon.pairOf plane boon
      StatBuff([ first; second ], bonus)
    | StatBuff (stats, bonus) ->
      // A hand-authored single stat still cannot cross the veil: it lands on its counterpart.
      StatBuff(
        stats
        |> List.map (fun stat -> if (Attributes.descriptorOf stat).Plane = plane then stat else Attributes.counterparts stat),
        bonus
      )
    | other -> other

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
