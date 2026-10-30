module Fornach.Tests.VeteranSwarmTests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

/// Per-cell seeded roller, so one matchup's result does not depend on how many dice earlier cells used.
let private roller (seed: int) : DiceRoller =
  let rng = System.Random(seed)
  fun min max -> rng.Next(min, max + 1)

let private mobClasses =
  [ CharacterClass.Warrior
    CharacterClass.Rogue
    CharacterClass.Mage ]

/// There is no Legend case in CombatTier (it ends at GrandMaster, level 200); Legend is level 1000 on
/// the same curve, so it is built with createClassLevel rather than createClassTier.
let private championTiers =
  [ "GrandMaster", (fun (cls: CharacterClass) -> TierFactory.createClassTier cls CombatTier.GrandMaster)
    "Legend", (fun (cls: CharacterClass) -> TierFactory.createClassLevel cls 1000) ]

let private swarmCap = 64
let private trialsPerProbe = 10

[<Fact>]
let ``Grandmaster and Legend champions are not overrun by swarming veterans`` () =
  let measured : (string * int) list =
    [ for (tierIndex, (tierName, makeChampion)) in List.indexed championTiers do
        for (classIndex, cls) in List.indexed CharacterClass.PlayerClasses do
          for (mobIndex, mobCls) in List.indexed mobClasses do
            let seed = 7000 + tierIndex * 400 + classIndex * 100 + mobIndex * 10

            let tippingPoint =
              Simulation.findSwarmTippingPoint
                (fun () -> makeChampion cls)
                (fun () -> TierFactory.createClassTier mobCls CombatTier.Veteran)
                swarmCap
                trialsPerProbe
                (roller seed)

            yield (sprintf "%s %A vs Veteran %A" tierName cls mobCls, tippingPoint.Value) ]

  let cellsOf (tierName: string) =
    measured
    |> List.filter (fun (label, _) -> label.StartsWith(tierName + " "))
    |> List.map (fun (label, tp) -> label, tp)

  let tp (tierName: string) (cls: CharacterClass) (mobCls: CharacterClass) =
    let key = sprintf "%s %A vs Veteran %A" tierName cls mobCls
    measured |> List.find (fun (k, _) -> k = key) |> snd

  let table =
    measured
    |> List.map (fun (label, tp) -> sprintf "  %-45s %d" label tp)
    |> String.concat "\n"

  // GrandMaster is 160 levels above a Veteran mob, and still beatable by a warband. The floor is 8 rather
  // than 10 because a GrandMaster Fighter guards with (Poise 841 + Composure 631) / 2 = 736: Composure is
  // a mental stat and the off plane grows a step behind the own plane. Re-pin this if the guard rule
  // changes again.
  for (label, value) in cellsOf "GrandMaster" do
    Assert.True(value >= 8, sprintf "%s fell to only %d Veteran mobs.\n\nFull table:\n%s" label value table)
    Assert.True(value < swarmCap, sprintf "%s saturated the %d-mob cap; GrandMaster is no longer measurable against Veterans.\n\nFull table:\n%s" label swarmCap table)

  // Legend (level 1000) is not merely tough against level-40 Veterans, it is structurally untouchable:
  // its defence is ~3031 against a Veteran's ~201 offence, so netHits is <= 0 on every attack no matter
  // how many mobs swarm. Every cell therefore sits at the cap — a whiff-gate property of the contest,
  // not a swarm-size limit. Raise the cap and the numbers will not move.
  for (label, value) in cellsOf "Legend" do
    Assert.True(
      (value = swarmCap),
      sprintf "%s fell at %d, below the %d-mob cap: a level-1000 champion should not be reachable by level-40 Veterans.\n\nFull table:\n%s" label value swarmCap table
    )

  // Progression has to keep paying off: Legend must never be worse than GrandMaster.
  for cls in CharacterClass.PlayerClasses do
    for mobCls in mobClasses do
      let gm = tp "GrandMaster" cls mobCls
      let legend = tp "Legend" cls mobCls

      Assert.True(
        legend >= gm,
        sprintf "%A %A: Legend (%d) should not be outlasted by GrandMaster (%d)\n\nFull table:\n%s" cls mobCls legend gm table
      )

  // Drift alarm, on the tier where the numbers still discriminate: today GrandMaster Mage holds 19-21
  // against GrandMaster Fighter's 10-13, so ~2x. Past 3x the mage has drifted back into the mental-plane
  // dominance the class reduction was meant to end. Legend cannot host this check — everything saturates.
  for mobCls in mobClasses do
    let mage = tp "GrandMaster" CharacterClass.Mage mobCls
    let fighter = tp "GrandMaster" CharacterClass.Fighter mobCls

    Assert.True(
      mage <= fighter * 3,
      sprintf "GrandMaster Mage (%d) outlasts GrandMaster Fighter (%d) by more than 3x against Veteran %A\n\nFull table:\n%s" mage fighter mobCls table
    )
