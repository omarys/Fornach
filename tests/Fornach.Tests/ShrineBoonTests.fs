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
        | Tempered (relic, defences, temper) ->
          // The relic vaults the altar's own pair, on the bearer's plane; the enlightenment lands on
          // the other plane's three defences, which the bearer cannot reach through their own pair.
          let first, second = StatBoon.pairOf plane boon
          Assert.Equal<StatId list>([ first; second ], relic.StatPercentBonuses |> List.map fst)
          Assert.True(relic.StatPercentBonuses |> List.forall (fun (_, pct) -> pct > 0))

          let expectedDefences =
            if plane = Physical then
              [ Resolve; Intuition; Composure ]
            else
              [ Fortitude; Reflex; Poise ]

          Assert.Equal<StatId list>(expectedDefences, defences)
          Assert.Equal(5, temper)
        | StatBuff (stats, _) ->
          let first, second = StatBoon.pairOf plane boon
          Assert.Equal<StatId list>([ first; second ], stats)
        | _ -> () // surges, keystones and relics are plane-neutral

[<Fact>]
let ``a fighter's two altars sharpen the two physical pairs and temper every mental defence`` () =
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

    match TowerSession.applyAltarSacrifice pt placed with
    | Ok s -> s
    | Error e -> failwith e

  // PineCloisters authors 18% on the altar's pair and +5 enlightenment. A fighter's pairs are
  // Force/Fortitude and Finesse/Reflex, so every mental defence — the plane they do not walk — takes
  // the temper instead, and no other attribute moves.
  let check (boon: StatBoon) =
    let baseline = playerOf Fighter
    let after = apply boon
    let first, second = StatBoon.pairOf Physical boon
    let pair = set [ first; second ]
    let mentalDefences = set [ Resolve; Intuition; Composure ]

    for stat in allStats do
      let before = baseline.Stats.Get stat
      let expected =
        if pair.Contains stat then before + StatBonus.percentOf before 18
        elif mentalDefences.Contains stat then before + 5
        else before

      Assert.Equal(expected, after.Player.Stats.Get stat)

  check PrimaryPair
  check SecondaryPair

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

      // Themed altars hand a tempered relic: the relic lands on that altar's own pair, so a stage's
      // second altar is not a duplicate of the first. Basalt and Tempest hand plane-neutral relics
      // and surges instead, so a floor of those yields no pairs at all.
      let pairs =
        altars
        |> List.choose (fun a ->
          match a.Reward.ResolvedFor Physical with
          | Tempered (relic, defences, temper) ->
            Assert.Equal(5, temper)
            Assert.Equal<StatId list>([ Resolve; Intuition; Composure ], defences)
            Some(relic.StatPercentBonuses |> List.map fst |> Set.ofList)
          | _ -> None)

      if not pairs.IsEmpty then
        Assert.Equal(2, pairs.Length)
        Assert.Equal(2, pairs |> List.map (fun pair -> pair |> Set.toList |> List.sort) |> List.distinct |> List.length)

[<Fact>]
let ``a shrine grants its stat boon once and cannot be farmed`` () =
  for cls in [ Fighter; Mage ] do
    for boon in [ PrimaryPair; SecondaryPair ] do
      for (stat, before, after) in commune cls boon true do
        Assert.Equal(before, after)
