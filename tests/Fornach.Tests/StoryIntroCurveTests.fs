namespace Fornach.Tests

open Xunit
open Xunit.Abstractions
open System
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine
open Fornach.Cli

/// The story intro is the one place a player can be wrecked with no way out, and it is the one place the
/// `--run-sim` sweep never measured: that sweep builds tower floors (`isStory = false`) from a bare template
/// combatant. This fights the real authored floor — generated in story mode, rescaled by
/// `TowerSession.initSessionWithMode`, against the real prologue protagonist — chamber by chamber in the
/// order the player meets them, so a difficulty change to either side shows up here.
module StoryIntroCurve =

  let private enemiesOf (floor: TowerFloor) =
    floor.Entities
    |> Map.toList
    |> List.map snd
    |> List.choose (function
      | TowerEntity.EntityEnemy e when not e.IsDefeated -> Some e
      | _ -> None)

  /// Roaming enemies grouped into the chambers the player meets them in, nearest to spawn first.
  let private chambersOf (floor: TowerFloor) (packSize: int) =
    let spawn = floor.SpawnLocation

    let distance (pt: Point) = abs (pt.X - spawn.X) + abs (pt.Y - spawn.Y)

    floor.Entities
    |> Map.toList
    |> List.choose (fun (pt, ent) ->
      match ent with
      | TowerEntity.EntityEnemy e when not e.IsDefeated && not e.IsBoss -> Some(distance pt, e)
      | _ -> None)
    |> List.sortBy fst
    |> List.map snd
    |> List.chunkBySize packSize

  /// Plays one chamber against a fresh protagonist, so each reading assumes the player rested (or was
  /// healed) before walking in. Wins and the average health left over.
  let private fightChamber (player: Combatant) (pack: TowerEnemy list) (trials: int) =
    let mutable wins = 0
    let mutable hpPct = 0.0

    for trial in 0 .. trials - 1 do
      let rng = Random(7000 + trial)
      let roller: DiceRoller = fun min max -> rng.Next(min, max + 1)

      let result, survivor =
        Simulation.runPreparedGroupMatch roller player (pack |> List.map (fun e -> e.Combatant)) 80

      if result.SoloWon then
        wins <- wins + 1
        hpPct <- hpPct + (float survivor.Health.Current / float survivor.Health.Maximum * 100.0)

    wins, (if wins = 0 then 0.0 else hpPct / float wins)

  /// Walks the floor's chambers in the order the player meets them, carrying health and morale from one
  /// fight into the next (the tower heals only at shrines and rests), and reports where a run ends.
  let private walkFloor (log: string -> unit) (player: Combatant) (packSize: int) (seed: int) (floorNumber: int) =
    let floor = (TowerSession.initSessionWithMode player seed floorNumber true).CurrentFloor

    // The guardian is the floor's last fight, not a roaming chamber: a walk that skips it measures the
    // tutorial up to the wall and calls the wall clear.
    let chambers =
      match enemiesOf floor |> List.filter (fun e -> e.IsBoss) with
      | [] -> chambersOf floor packSize
      | guardians -> chambersOf floor packSize @ [ guardians ]
    let mutable survivor = player
    let mutable diedIn = None

    chambers
    |> List.iteri (fun index chamber ->
      if diedIn.IsNone then
        let rng = Random(seed + index)
        let roller: DiceRoller = fun min max -> rng.Next(min, max + 1)

        let result, after =
          Simulation.runPreparedGroupMatch roller survivor (chamber |> List.map (fun e -> e.Combatant)) 80

        log (sprintf "    chamber %d (%s): won=%b rounds=%d hp%%=%.0f cond=%A solo %d/%d | foes %s" (index + 1) (chamber |> List.map (fun e -> e.Name) |> String.concat " + ") result.SoloWon result.Rounds result.SoloRemainingHPPct result.Condition survivor.Health.Current survivor.Health.Maximum (chamber |> List.map (fun e -> sprintf "%s Lv.%d hp%d/%d" e.Name e.Combatant.Level e.Combatant.Health.Current e.Combatant.Health.Maximum) |> String.concat ", "))

        if not result.SoloWon then
          diedIn <- Some(index + 1, chamber |> List.map (fun e -> e.Name) |> String.concat " + ")
          survivor <- after
        else
          // The tower's own progression: growth from the pack, XP for the pack. Levelling mid-floor is
          // what makes an eleven-chamber tutorial survivable, so the walk has to model it.
          let grown = Combatant.growFromEncounter (chamber |> List.map (fun e -> e.Combatant)) after
          let xp = chamber |> List.sumBy (fun e -> e.Combatant.Level * 10 + 25)
          let levelled, _ = TowerSession.applyExperience grown xp
          survivor <- { Combatant.clearStacks levelled with Meters = StatusMeters.Zero })

    floor, chambers.Length, survivor, diedIn

  let describe (floor: TowerFloor) =
    enemiesOf floor
    |> List.map (fun e ->
      sprintf "%s Lv.%d HP %d%s" e.Name e.Combatant.Level e.Combatant.Health.Maximum (if e.IsBoss then " BOSS" else ""))
    |> String.concat ", "

  type StoryIntroCurveTests(output: ITestOutputHelper) =

    [<Theory>]
    [<InlineData("fighter")>]
    [<InlineData("mage")>]
    [<InlineData("berserker")>]
    [<InlineData("duelist")>]
    [<InlineData("warden")>]
    [<InlineData("mesmer")>]
    [<InlineData("abjurer")>]
    [<InlineData("inquisitor")>]
    member _.``a prologue protagonist can walk the whole first story floor without resting``(className: string) =
      let player = Fornach.Domain.StoryBosses.createProloguePlayer className
      let mutable cleared = 0

      for trial in 0 .. 9 do
        // The tutorial floor authors a pair per room, so two per chamber. Per-chamber detail is printed
        // for the first seed only, which is what a failure has to be read against.
        let floor, chamberCount, survivor, diedIn =
          walkFloor (fun line -> if trial = 0 then output.WriteLine line) player 2 (1234 + trial) 1

        match diedIn with
        | Some(index, pack) ->
          output.WriteLine(
            sprintf
              "%s seed %d: floor %d died in chamber %d/%d (%s), reached Lv.%d HP %d"
              className
              (1234 + trial)
              floor.FloorNumber
              index
              chamberCount
              pack
              survivor.Level
              survivor.Health.Current
          )
        | None ->
          cleared <- cleared + 1

          output.WriteLine(
            sprintf
              "%s seed %d: cleared all %d chambers at Lv.%d, HP %d/%d (%.0f%%)"
              className
              (1234 + trial)
              chamberCount
              survivor.Level
              survivor.Health.Current
              survivor.Health.Maximum
              (float survivor.Health.Current / float survivor.Health.Maximum * 100.0)
          )

      Assert.True(cleared >= 8, sprintf "%s survives the first story floor only %d/10 times" className cleared)
