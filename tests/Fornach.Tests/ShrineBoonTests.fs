/// Shrine boons are class-locked: a Fighter may only ever be sharpened on physical attributes, a Mage
/// only on mental ones, and every stage offers exactly one shrine per available pair.
module Fornach.Tests.ShrineBoonTests

open Xunit
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine

let private allStats =
  [ Force
    Fortitude
    Finesse
    Reflex
    Prowess
    Poise
    Intellect
    Resolve
    Acuity
    Intuition
    Acumen
    Composure ]

let private physical = set [ Force; Fortitude; Finesse; Reflex ]
let private mental = set [ Intellect; Resolve; Acuity; Intuition ]

let private playerOf (cls: CharacterClass) =
  let stats = StatBlock.Create(allStats |> List.map (fun s -> s, 100))
  Combatant.createWithClass (CombatantId.New()) "Delver" 500 500 stats cls 1

let private shrineOf (boon: ShrineBoon) (blessed: bool) =
  { Id = "shrine_test"
    Name = "Monolith of Testing"
    BlessingDescription = "Restores everything"
    Boon = boon
    IsBlessed = blessed
    IsUsed = false }

let private commune (cls: CharacterClass) (boon: ShrineBoon) (blessed: bool) =
  let player = playerOf cls
  let state = TowerSession.initSession player 42 1
  let after, _ = TowerSession.shrineBlessing (shrineOf boon blessed) state.PlayerPosition state
  let before = player.Stats
  allStats |> List.map (fun s -> s, before.Get s, after.Player.Stats.Get s)

[<Fact>]
let ``every stage fields exactly two shrines, one per available pair`` () =
  for story in [ false; true ] do
    for floorNumber in [ 1; 2; 5; 7; 9 ] do
      let floor = TowerGenerator.generateFloorWithMode 4242 floorNumber story

      let shrines =
        floor.Entities
        |> Map.values
        |> Seq.choose (function EntityShrine s -> Some s | _ -> None)
        |> List.ofSeq

      Assert.Equal(2, shrines.Length)

      let boons = shrines |> List.map (fun s -> s.Boon) |> Set.ofList
      Assert.Equal<Set<ShrineBoon>>(set [ PrimaryBoon; SecondaryBoon ], boons)

[<Fact>]
let ``a fighter never receives a mental blessing`` () =
  for boon in [ PrimaryBoon; SecondaryBoon ] do
    for (stat, before, after) in commune Fighter boon false do
      if physical.Contains stat then
        Assert.True(after >= before, sprintf "fighter %A regressed on %A (%d -> %d)" boon stat before after)
      else
        Assert.Equal(before, after)

[<Fact>]
let ``a mage never receives a physical blessing`` () =
  for boon in [ PrimaryBoon; SecondaryBoon ] do
    for (stat, before, after) in commune Mage boon false do
      if mental.Contains stat then
        Assert.True(after >= before, sprintf "mage %A regressed on %A (%d -> %d)" boon stat before after)
      else
        Assert.Equal(before, after)

[<Fact>]
let ``the pair a shrine sharpens matches the slot`` () =
  let risen (cls: CharacterClass) (boon: ShrineBoon) =
    commune cls boon false
    |> List.choose (fun (stat, before, after) -> if after > before then Some stat else None)
    |> Set.ofList

  Assert.Equal<Set<StatId>>(set [ Force; Fortitude ], risen Fighter PrimaryBoon)
  Assert.Equal<Set<StatId>>(set [ Finesse; Reflex ], risen Fighter SecondaryBoon)
  Assert.Equal<Set<StatId>>(set [ Intellect; Resolve ], risen Mage PrimaryBoon)
  Assert.Equal<Set<StatId>>(set [ Acuity; Intuition ], risen Mage SecondaryBoon)

[<Fact>]
let ``a shrine grants its stat boon once and cannot be farmed`` () =
  for cls in [ Fighter; Mage ] do
    for boon in [ PrimaryBoon; SecondaryBoon ] do
      for (stat, before, after) in commune cls boon true do
        Assert.Equal(before, after)
