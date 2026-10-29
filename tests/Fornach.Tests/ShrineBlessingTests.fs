/// Shrines, altars, traders and vault relics may only hand out the four blessing pairs:
/// Force/Fortitude or Finesse/Reflex to a Fighter, Intellect/Resolve or Acuity/Intuition to a Mage.
/// Prowess, Poise and Acumen are earned exclusively through encounter growth and levelling, so no
/// piece of found or bought gear may carry them. This walks every generator rather than listing the
/// offenders, so a new reward cannot quietly reintroduce one.
module Fornach.Tests.ShrineBlessingTests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// The stats no blessing, relic, ware or chest may grant.
let private reservedForGrowth = set [ StatId.Prowess; StatId.Poise; StatId.Acumen ]

let private themes =
  [ FloorTheme.QuarryPlazas
    FloorTheme.PineCloisters
    FloorTheme.BasaltCalderas
    FloorTheme.TempestTerraces
    FloorTheme.SunkenBoulevards
    FloorTheme.ElysianSanctuaries
    FloorTheme.CelestialSpires ]

let private bonusesOf (item: EquipmentItem) = item.StatPercentBonuses |> List.map fst |> set

/// Every equipment item the world events can hand out for one theme and floor.
let private grantedItems (theme: FloorTheme) (floorNum: int) : (string * EquipmentItem) list =
  let altar = WorldEvents.createAltar theme floorNum PrimaryPair
  let trader = WorldEvents.createTrader theme floorNum
  let vault = WorldEvents.createVault theme floorNum

  let altarRelic =
    match altar.Reward with
    | AltarReward.RelicReward item -> [ altar.Name, item ]
    | AltarReward.TemperedRelic (item, _, _, _) -> [ altar.Name, item ]
    | _ -> []

  [ altarRelic
    (trader.Wares |> List.map (fun ware -> ware.Item.Name, ware.Item))
    (vault.Relics |> List.map (fun item -> item.Name, item)) ]
  |> List.concat

[<Fact>]
let ``no blessing, ware or relic grants Prowess, Poise or Acumen`` () =
  for theme in themes do
    for floorNum in [ 1; 5; 12; 40 ] do
      for name, item in grantedItems theme floorNum do
        let reserved = Set.intersect (bonusesOf item) reservedForGrowth

        Assert.True(
          reserved.IsEmpty,
          sprintf "%s (floor %d, %A) grants %A — those are earned through combat, not bought or found" name floorNum theme reserved)

[<Fact>]
let ``every blessing pair is one a class can actually use`` () =
  // Fighter pairs: Force/Fortitude, Finesse/Reflex. Mage pairs: Intellect/Resolve, Acuity/Intuition.
  let legal =
    set [ StatId.Force; StatId.Fortitude; StatId.Finesse; StatId.Reflex
          StatId.Intellect; StatId.Resolve; StatId.Acuity; StatId.Intuition ]

  for theme in themes do
    for name, item in grantedItems theme 3 do
      let illegal = Set.difference (bonusesOf item) legal

      Assert.True(
        illegal.IsEmpty,
        sprintf "%s (%A) grants %A, which belongs to neither blessing pair" name theme illegal)
