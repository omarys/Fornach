namespace Fornach.Tests

open System
open System.IO
open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

module DebugToolingTests =

  let private testStats () =
    StatBlock.Create
      [ StatId.Force, 60
        StatId.Fortitude, 60
        StatId.Finesse, 60
        StatId.Reflex, 60
        StatId.Prowess, 60
        StatId.Poise, 60 ]

  let private tempLogPath (tag: string) =
    Path.Combine(Path.GetTempPath(), sprintf "fornach_runtime_%s_%s.log" tag (Guid.NewGuid().ToString("N")))

  // ── Feature A: RuntimeLog ────────────────────────────────────────────────

  [<Fact>]
  let ``RuntimeLog.write appends timestamped lines when enabled`` () =
    let tmp = tempLogPath "on"
    let wasEnabled = RuntimeLog.enabled
    let wasPath = RuntimeLog.path
    try
      RuntimeLog.enabled <- true
      RuntimeLog.path <- tmp
      RuntimeLog.write "first entry"
      RuntimeLog.write "second entry"
      RuntimeLog.close ()
      let text = File.ReadAllText tmp
      Assert.Contains("first entry", text)
      Assert.Contains("second entry", text)
      Assert.Matches(@"\[\d{2}:\d{2}:\d{2}\.\d{3}\]", text)
    finally
      RuntimeLog.close ()
      RuntimeLog.enabled <- false
      RuntimeLog.path <- wasPath
      if File.Exists tmp then File.Delete tmp

  [<Fact>]
  let ``RuntimeLog.write is a no-op when disabled`` () =
    let tmp = tempLogPath "off"
    let wasEnabled = RuntimeLog.enabled
    let wasPath = RuntimeLog.path
    try
      RuntimeLog.enabled <- false
      RuntimeLog.path <- tmp
      RuntimeLog.write "should not be written"
      RuntimeLog.section "should not be written"
      Assert.False(File.Exists tmp, "disabled RuntimeLog must not create a log file")
    finally
      RuntimeLog.enabled <- wasEnabled
      RuntimeLog.path <- wasPath

  // ── Feature B: CLI flag parsing ──────────────────────────────────────────

  [<Fact>]
  let ``parseCliArgs reads --debug and --seed`` () =
    let mode, debug = Program.parseCliArgs [| "--debug"; "--seed"; "4242" |]
    Assert.True debug.Enabled
    Assert.Equal(Some 4242, debug.ForcedSeed)
    Assert.Equal(CliOptions.InteractiveMenu, mode)

  [<Fact>]
  let ``parseCliArgs reads the -d short flag and a simulation mode`` () =
    let mode, debug = Program.parseCliArgs [| "-d"; "--seed"; "7"; "--sim"; "-s"; "Novice Berserker"; "-e"; "Novice Slag Hound" |]
    Assert.True debug.Enabled
    Assert.Equal(Some 7, debug.ForcedSeed)
    match mode with
    | CliOptions.RunSimulation _ -> ()
    | other -> failwithf "expected RunSimulation, got %A" other

  [<Fact>]
  let ``parseCliArgs defaults to no debug and no forced seed`` () =
    let mode, debug = Program.parseCliArgs [||]
    Assert.False debug.Enabled
    Assert.Equal(None, debug.ForcedSeed)
    Assert.Equal(CliOptions.InteractiveMenu, mode)

  [<Fact>]
  let ``parseCliArgs ignores a malformed --seed value`` () =
    let _, debug = Program.parseCliArgs [| "--seed"; "not-a-number" |]
    Assert.Equal(None, debug.ForcedSeed)

  // ── Debug helper: god mode restore ───────────────────────────────────────

  [<Fact>]
  let ``godModeRestore refills health morale armor and weapon condition`` () =
    let player = Combatant.create (CombatantId.New()) "Debugger" 120 90 (testStats ())
    let damaged =
      { player with
          Health = { player.Health with Current = 1 }
          Morale = { player.Morale with Current = 0 }
          Armor = { player.Armor with Current = 0 }
          WeaponCondition = WeaponCondition.Broken }
    let restored = TowerDisplay.godModeRestore damaged
    Assert.Equal(restored.Health.Maximum, restored.Health.Current)
    Assert.Equal(restored.Morale.Maximum, restored.Morale.Current)
    Assert.Equal(restored.Armor.Max, restored.Armor.Current)
    Assert.Equal(WeaponCondition.Pristine, restored.WeaponCondition)
    // Identity and progression are untouched.
    Assert.Equal(damaged.Level, restored.Level)
    Assert.Equal(damaged.Name, restored.Name)
