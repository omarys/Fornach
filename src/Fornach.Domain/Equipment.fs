namespace Fornach.Domain

type EquipmentSlot =
  | Weapon
  | Armor
  | MentalRelic

type EquipmentItem =
  { Name: string
    Slot: EquipmentSlot
    Description: string
    /// Percentages, each resolved against the bearer's own current stat when the item is applied —
    /// not flat points, which went stale as attributes grew. `StatBonus.authoredBaseline` records
    /// the level-1 scale the numbers were converted from.
    StatPercentBonuses: (StatId * int) list
    HealthBonus: int
    MoraleBonus: int
    StartingRecklessnessDelta: int
    Triggers: EquipmentTrigger list }

type Loadout =
  { Weapon: EquipmentItem option
    Armor: EquipmentItem option
    MentalRelic: EquipmentItem option }

  static member Empty =
    { Weapon = None
      Armor = None
      MentalRelic = None }

  member this.AllEquipped =
    [ this.Weapon; this.Armor; this.MentalRelic ] |> List.choose id
