module Fornach.Tests.FlankSessionTests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Spatial

/// Finds a live foe on a generated floor so the test never has to fabricate a TowerEnemy.
let private anyFoe (state: TowerRunState) =
  state.CurrentFloor.Entities
  |> Map.toSeq
  |> Seq.tryPick (fun (_, e) ->
    match e with
    | EntityEnemy en when not en.IsDefeated -> Some en
    | _ -> None)

/// Puts one foe at `spot` and points the player north from (10,10).
let private stateWithFoeAt (spot: Point) =
  let player = StoryBosses.createProloguePlayer "fighter"

  [ 1 .. 12 ]
  |> List.tryPick (fun seed ->
    let st = TowerSession.initSession player seed 3

    anyFoe st
    |> Option.map (fun en ->
      let stripped =
        st.CurrentFloor.Entities
        |> Map.filter (fun _ e ->
          match e with
          | EntityEnemy _ -> false
          | _ -> true)

      let state =
        { st with
            PlayerPosition = { X = 10; Y = 10 }
            Facing = Some Direction.North
            CurrentFloor = { st.CurrentFloor with Entities = Map.add spot (EntityEnemy en) stripped } }

      state, en.Combatant.Id))

[<Fact>]
let ``a foe ahead of the player's facing is met head-on`` () =
  match stateWithFoeAt { X = 10; Y = 7 } with
  | None -> failwith "no generated floor carried a live foe"
  | Some(state, id) ->
    Assert.Equal(Flank.Front, TowerSession.flankOf state id)
    Assert.Equal(1.0, TowerSession.flankDamageMultiplier(Flank.Front))

[<Fact>]
let ``a foe beside the player is on the flank`` () =
  match stateWithFoeAt { X = 13; Y = 10 } with
  | None -> failwith "no generated floor carried a live foe"
  | Some(state, id) ->
    Assert.Equal(Flank.Flank, TowerSession.flankOf state id)
    Assert.Equal(1.15, TowerSession.flankDamageMultiplier(Flank.Flank))

[<Fact>]
let ``a foe behind the player's facing strikes the rear`` () =
  match stateWithFoeAt { X = 10; Y = 13 } with
  | None -> failwith "no generated floor carried a live foe"
  | Some(state, id) ->
    Assert.Equal(Flank.Rear, TowerSession.flankOf state id)
    Assert.Equal(1.30, TowerSession.flankDamageMultiplier(Flank.Rear))

[<Fact>]
let ``an unknown attacker is treated as head-on`` () =
  // Keeps callers without grid data exactly as they were before flanking existed.
  match stateWithFoeAt { X = 10; Y = 13 } with
  | None -> failwith "no generated floor carried a live foe"
  | Some(state, _) ->
    Assert.Equal(Flank.Front, TowerSession.flankOf state (CombatantId.New()))
