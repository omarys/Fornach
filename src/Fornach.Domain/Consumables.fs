namespace Fornach.Domain

/// Single-use item carried by the delver. Identification is per kind: every kit looks alike until a
/// shrine reveals what the row is.
type ConsumableKind =
  | ArmorRepairKit
  | GrindingStone
  | Ration
  | SpiritSeed
  | WeaponMaterial
  | MentalMaterial

  member this.Name : string =
    match this with
    | ArmorRepairKit -> "Armor Repair Kit"
    | GrindingStone -> "Grinding Stone"
    | Ration -> "Ration"
    | SpiritSeed -> "Spirit Seed"
    | WeaponMaterial -> "Weapon Material"
    | MentalMaterial -> "Mental Material"

  member this.Description : string =
    match this with
    | ArmorRepairKit -> "Restores armor to full — past anything resting can do."
    | GrindingStone -> "Hones a weapon back to Pristine — past anything resting can do."
    | Ration -> "Raises Health to 80% of maximum."
    | SpiritSeed -> "Restores Morale to maximum."
    | WeaponMaterial -> "Works a permanent +8 into the class's offensive stat."
    | MentalMaterial -> "Works a permanent +8 into the class's mental offensive stat."

  /// The loot category a kill must be in to drop this kind.
  member this.DropCategory : LootCategory =
    match this with
    | ArmorRepairKit
    | GrindingStone -> LootCategory.Humanoid
    | Ration -> LootCategory.Beast
    | SpiritSeed -> LootCategory.Plant
    | WeaponMaterial -> LootCategory.Construct
    | MentalMaterial -> LootCategory.Spirit

module Consumables =

  /// Permanent stat growth one rare material puts into the player, as a percentage of the stat it
  /// lands on — 18% is the old flat +8 on the level-1 primary scale, so a material still matters at
  /// level 200 instead of fading to nothing.
  let materialUpgrade = 18

  /// What a kill in a category can salvage, with each kind's percentage. The match is total over the
  /// category, so a new category cannot ship without a drop table.
  let dropsFor (category: LootCategory) : (ConsumableKind * int) list =
    match category with
    | LootCategory.Humanoid -> [ ArmorRepairKit, 45; GrindingStone, 25 ]
    | LootCategory.Beast -> [ Ration, 50 ]
    | LootCategory.Plant -> [ SpiritSeed, 50 ]
    | LootCategory.Construct -> [ WeaponMaterial, 35 ]
    | LootCategory.Spirit -> [ MentalMaterial, 30 ]

  /// One roll (0-99) decides both whether a consumable drops and which one: entries are read in order
  /// and the roll must fall inside a kind's percentage. A roll past every entry drops nothing.
  let rollDrop (roll: int) (category: LootCategory) : ConsumableKind option =
    let bound = ((roll % 100) + 100) % 100
    let table = dropsFor category
    let ceilings = table |> List.map snd |> List.scan (+) 0 |> List.tail

    List.zip table ceilings
    |> List.tryPick (fun ((kind, _), ceiling) -> if bound < ceiling then Some kind else None)

  /// The stat a rare material sharpens, per class: the weapon material takes the class's offensive
  /// stat; the mental material takes a stat on the bearer's own plane, so a physical class is never
  /// sharpened on a mental attribute (Fortitude is Resolve's counterpart — see `Attributes.counterparts`).
  /// None for the kinds that are not materials.
  let materialStat (kind: ConsumableKind) (cls: CharacterClass) : StatId option =
    match kind with
    | WeaponMaterial ->
      Some(
        match cls with
        | Fighter
        | Warrior -> StatId.Force
        | Rogue -> StatId.Finesse
        | Mage -> StatId.Intellect
      )
    | MentalMaterial ->
      Some(
        match cls.Plane with
        | Mental -> StatId.Intellect
        | Physical -> StatId.Fortitude
      )
    | ArmorRepairKit
    | GrindingStone
    | Ration
    | SpiritSeed -> None

  /// Adds one unit to the bag. The kind moves to the front: bag order is newest-first, which is
  /// what the inventory panel sorts "by most recent" on.
  let add (kind: ConsumableKind) (bag: (ConsumableKind * int) list) : (ConsumableKind * int) list =
    match bag |> List.tryFind (fun (k, _) -> k = kind) with
    | Some (_, n) -> (kind, n + 1) :: (bag |> List.filter (fun (k, _) -> k <> kind))
    | None -> (kind, 1) :: bag

  /// Rows for the inventory panel: the kind, its display name and description, and the count. An
  /// unidentified kind is concealed — its real name and description must not leak before a shrine
  /// reveals it; the kind is still returned so the caller knows which row was picked. `byName` sorts
  /// alphabetically; otherwise bag order is kept, which is newest-first (see `add`).
  let describe
    (unidentified: Set<ConsumableKind>)
    (byName: bool)
    (bag: (ConsumableKind * int) list)
    : (ConsumableKind * string * string * int) list =
    let rows =
      bag
      |> List.map (fun (kind, count) ->
        if unidentified.Contains kind then
          kind, "Unidentified", "A shrine can reveal what this is.", count
        else
          kind, kind.Name, kind.Description, count)

    if byName then rows |> List.sortBy (fun (_, name, _, _) -> name) else rows

  let count (kind: ConsumableKind) (bag: (ConsumableKind * int) list) : int =
    bag |> List.sumBy (fun (k, n) -> if k = kind then n else 0)

  /// Spends one unit, dropping the row when it hits zero.
  let remove (kind: ConsumableKind) (bag: (ConsumableKind * int) list) : (ConsumableKind * int) list =
    bag
    |> List.choose (fun (k, n) ->
      if k <> kind then Some(k, n)
      elif n > 1 then Some(k, n - 1)
      else None)

  /// Drops a whole row, for the shrine that works every carried material in at once.
  let clear (kind: ConsumableKind) (bag: (ConsumableKind * int) list) : (ConsumableKind * int) list =
    bag |> List.filter (fun (k, _) -> k <> kind)

  /// Applies one unit's effect. The repair and restore kinds beat the rest caps; the material kinds
  /// stack permanently on Stats.
  let apply (kind: ConsumableKind) (player: Combatant) : Combatant =
    match kind with
    | ArmorRepairKit -> { player with Armor = ArmorIntegrity.Create player.Armor.Max }
    | GrindingStone -> { player with WeaponCondition = WeaponCondition.Pristine }
    | Ration ->
      // Raises to 80% of max, never lowers it: eating at 95% must not cost health.
      let target = player.Health.Maximum * 4 / 5
      { player with Health = player.Health.ApplyDelta(max 0 (target - player.Health.Current)) }
    | SpiritSeed -> { player with Morale = Pool.Create player.Morale.Maximum }
    | WeaponMaterial
    | MentalMaterial ->
      match materialStat kind player.Class with
      | Some stat -> { player with Stats = player.Stats.ApplyPercentBonuses [ stat, materialUpgrade ] }
      | None -> player
