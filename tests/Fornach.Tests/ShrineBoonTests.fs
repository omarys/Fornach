/// Shrine boons are class-locked: a Fighter may only ever be sharpened on physical attributes, a Mage
/// only on mental ones, and every stage offers exactly one shrine per available pair.
module Fornach.Tests.StatBoonTests

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

let private shrineOf (boon: StatBoon) (blessed: bool) =
  { Id = "shrine_test"
    Name = "Monolith of Testing"
    BlessingDescription = "Restores everything"
    Boon = boon
    IsBlessed = blessed
    IsUsed = false }

let private commune (cls: CharacterClass) (boon: StatBoon) (blessed: bool) =
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
      Assert.Equal<Set<StatBoon>>(set [ PrimaryPair; SecondaryPair ], boons)

[<Fact>]
let ``a fighter never receives a mental blessing`` () =
  for boon in [ PrimaryPair; SecondaryPair ] do
    for (stat, before, after) in commune Fighter boon false do
      if physical.Contains stat then
        Assert.True(after >= before, sprintf "fighter %A regressed on %A (%d -> %d)" boon stat before after)
      else
        Assert.Equal(before, after)

[<Fact>]
let ``a mage never receives a physical blessing`` () =
  for boon in [ PrimaryPair; SecondaryPair ] do
    for (stat, before, after) in commune Mage boon false do
      if mental.Contains stat then
        Assert.True(after >= before, sprintf "mage %A regressed on %A (%d -> %d)" boon stat before after)
      else
        Assert.Equal(before, after)

[<Fact>]
let ``the pair a shrine sharpens matches the slot`` () =
  let risen (cls: CharacterClass) (boon: StatBoon) =
    commune cls boon false
    |> List.choose (fun (stat, before, after) -> if after > before then Some stat else None)
    |> Set.ofList

  Assert.Equal<Set<StatId>>(set [ Force; Fortitude ], risen Fighter PrimaryPair)
  Assert.Equal<Set<StatId>>(set [ Finesse; Reflex ], risen Fighter SecondaryPair)
  Assert.Equal<Set<StatId>>(set [ Intellect; Resolve ], risen Mage PrimaryPair)
  Assert.Equal<Set<StatId>>(set [ Acuity; Intuition ], risen Mage SecondaryPair)

[<Fact>]
let ``a shrine visit touches no mental stat for a fighter carrying materials`` () =
  // The boon respects the plane, but the material step used to hand a Fighter Intellect from its own
  // MentalMaterial. Both halves are exercised here on the real step path.
  for boon in [ PrimaryPair; SecondaryPair ] do
    let player = playerOf Fighter
    let shrine = shrineOf boon false

    let state =
      { TowerSession.initSession player 4242 3 with
          Consumables = [ MentalMaterial, 3; WeaponMaterial, 2 ]
          UnidentifiedConsumables = Set.empty }

    let after, _ = TowerSession.shrineBlessing shrine state.PlayerPosition state

    for stat in allStats do
      if mental.Contains stat then
        Assert.Equal(state.Player.Stats.Get stat, after.Player.Stats.Get stat)
      elif stat = Force || stat = Fortitude then
        Assert.True(after.Player.Stats.Get stat > state.Player.Stats.Get stat, sprintf "%A should rise" stat)

[<Fact>]
let ``every themed altar resolves onto the bearer's own plane, one per pair`` () =
  let themes =
    [ FloorTheme.QuarryPlazas
      FloorTheme.PineCloisters
      FloorTheme.BasaltCalderas
      FloorTheme.TempestTerraces
      FloorTheme.SunkenBoulevards
      FloorTheme.ElysianSanctuaries
      FloorTheme.CelestialSpires ]

  for theme in themes do
    for cls in [ Fighter; Mage ] do
      let plane = (Combatant.createWithClass (CombatantId.New()) "D" 500 500 StatBlock.Baseline cls 1).Plane

      for boon in [ PrimaryPair; SecondaryPair ] do
        let altar = WorldEvents.createAltar theme 3 boon

        match altar.Reward.ResolvedFor plane with
        | StatBuff (stats, _) ->
          let first, second = StatBoon.pairOf plane boon
          Assert.Equal<StatId list>([ first; second ], stats)
        | _ -> () // surges, keystones and relics are plane-neutral

[<Fact>]
let ``a fighter's two altars sharpen the two physical pairs by a flat forty`` () =
  let apply (boon: StatBoon) =
    let altar = WorldEvents.createAltar FloorTheme.PineCloisters 3 boon
    let player = playerOf Fighter
    let state = TowerSession.initSession player 4242 7
    let pt = state.PlayerPosition

    let placed =
      { state with
          CurrentFloor =
            { state.CurrentFloor with
                Entities = state.CurrentFloor.Entities |> Map.add pt (EntityEncounter (FloorEncounter.SacrificialAltar altar)) } }

    let after =
      match TowerSession.applyAltarSacrifice pt placed with
      | Ok s -> s
      | Error e -> failwith e

    for stat in allStats do
      let before = state.Player.Stats.Get stat
      let now = after.Player.Stats.Get stat

      if mental.Contains stat then Assert.Equal(before, now)

    allStats |> List.map (fun stat -> stat, after.Player.Stats.Get stat - state.Player.Stats.Get stat)

  // PineCloisters authored +15% Resolve — a mental stat. Both of its altars must now hand a fighter
  // physical pairs instead, forty flat points each.
  let risen boon =
    apply boon
    |> List.choose (fun (stat, delta) -> if delta > 0 then Some (stat, delta) else None)
    |> Set.ofList

  Assert.Equal<Set<StatId * int>>(set [ Force, 40; Fortitude, 40 ], risen PrimaryPair)
  Assert.Equal<Set<StatId * int>>(set [ Finesse, 40; Reflex, 40 ], risen SecondaryPair)

[<Fact>]
let ``every stage fields one altar of each pair`` () =
  for story in [ false; true ] do
    for floorNumber in [ 1; 3; 6 ] do
      let floor = TowerGenerator.generateFloorWithMode 4242 floorNumber story

      let altars =
        floor.Entities
        |> Map.values
        |> Seq.choose (function
          | EntityEncounter (FloorEncounter.SacrificialAltar a) -> Some a
          | _ -> None)
        |> List.ofSeq

      Assert.Equal(2, altars.Length)
      Assert.Equal(2, altars |> List.map (fun a -> a.Id) |> Set.ofList |> Set.count)

      let bonuses =
        altars
        |> List.choose (fun a ->
          match a.Reward with
          | PairBuff (boon, bonus) -> Some (boon, bonus)
          | _ -> None)

      // Basalt and Tempest altars hand relics and surges instead of stats; the rest are pair buffs.
      for (_, bonus) in bonuses do
        Assert.Equal(40, bonus)

[<Fact>]
let ``a shrine grants its stat boon once and cannot be farmed`` () =
  for cls in [ Fighter; Mage ] do
    for boon in [ PrimaryPair; SecondaryPair ] do
      for (stat, before, after) in commune cls boon true do
        Assert.Equal(before, after)
