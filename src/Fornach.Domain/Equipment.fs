namespace Fornach.Domain

type EquipmentSlot =
  | Weapon
  | Armor
  | MentalRelic

type EquipmentItem =
  { Name: string
    Slot: EquipmentSlot
    Description: string
    StatModifiers: (StatId * int) list
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
