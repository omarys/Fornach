namespace Fornach.Tests

open System
open System.IO
open Xunit
open Spectre.Console
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

module CombatLoggerTests =

  let private createTestCombatant name force reflex hp armor =
    let stats =
      StatBlock.Create
        [ StatId.Force, force
          StatId.Fortitude, 100
          StatId.Finesse, 100
          StatId.Reflex, reflex
          StatId.Prowess, 100
          StatId.Poise, 100
          StatId.Intellect, 80
          StatId.Resolve, 80
          StatId.Acuity, 80
          StatId.Intuition, 80
          StatId.Acumen, 80
          StatId.Composure, 80 ]
    let baseC = Combatant.create (CombatantId.New()) name hp hp stats
    { baseC with Armor = ArmorIntegrity.Create armor }

  [<Fact>]
  let ``CombatLogger creates session and formats participant summaries correctly`` () =
    let player = createTestCombatant "Test Berserker" 200 150 2400 120
    let enemy = createTestCombatant "Test Gargoyle" 180 140 2000 100

    let session = CombatLogger.startSession "Test Encounter" "Test Duel" player enemy
    Assert.Equal("Test Encounter", session.Title)
    Assert.Equal("Test Duel", session.EncounterType)
    Assert.Equal("Test Berserker", session.InitialPlayer.Name)
    Assert.Equal("Test Gargoyle", session.InitialOpponent.Name)

    let playerSummary = CombatLogger.formatCombatantSummary "PLAYER" player
    Assert.Contains("PLAYER: Test Berserker", playerSummary)
    Assert.Contains("Health: 2400/2400", playerSummary)
    Assert.Contains("Armor: 120/120", playerSummary)
    Assert.Contains("Force=200", playerSummary)

  [<Fact>]
  let ``CombatLogger records round turns with dice pool breakdown and vitals snapshot`` () =
    let rng = Random(42)
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    let player = createTestCombatant "Hero" 220 180 2000 100
    let enemy = createTestCombatant "Monster" 120 90 1000 50

    let session = CombatLogger.startSession "Hero vs Monster" "Arena Duel" player enemy

    // Resolve an action
    let action = StandardAttack (ForceStrike false)
    let result = ActionResolver.resolve roller action player enemy

    CombatLogger.recordTurn session 1 1 "ForceStrike" player enemy result

    Assert.Equal(1, session.Turns.Count)
    let turn = session.Turns.[0]
    Assert.Equal(1, turn.Round)
    Assert.Equal(1, turn.TurnNumber)
    Assert.Equal("Hero", turn.ActorName)
    Assert.Equal("Monster", turn.TargetName)
    Assert.Equal("ForceStrike", turn.ActionName)
    Assert.True(turn.Contest.IsSome)

    let contest = turn.Contest.Value
    Assert.True(contest.Attacker.RawRolls.Length >= 4)
    Assert.True(contest.Attacker.TotalHits >= contest.Attacker.FloorHits)

    // Format full session text
    let logText = CombatLogger.formatSession session "PlayerVictorious" result.Actor result.Target
    Assert.Contains("FORNACH COMBAT LOG - TACTICAL DUEL RECORD", logText)
    Assert.Contains("Hero vs Monster", logText)
    Assert.Contains("ROUND 1", logText)
    Assert.Contains("Contest Resolution:", logText)
    Assert.Contains("COMBAT RESOLUTION: PLAYERVICTORIOUS", logText)
    Assert.Contains("FINAL VITALS:", logText)

  [<Fact>]
  let ``CombatLogger writeSession writes latest log and appends history log to disk`` () =
    let tempDir = Path.Combine(Path.GetTempPath(), sprintf "fornach_test_logs_%s" (Guid.NewGuid().ToString("N")))
    try
      let player = createTestCombatant "Striker" 150 150 1000 50
      let enemy = createTestCombatant "Defender" 100 100 800 40

      let session = { CombatLogger.startSession "File Write Test" "Unit Test" player enemy with LogDirectory = tempDir }

      let latestFile, historyFile = CombatLogger.writeSession session "VICTORY" player enemy

      Assert.True(File.Exists(latestFile), sprintf "Latest log should exist at %s" latestFile)
      Assert.True(File.Exists(historyFile), sprintf "History log should exist at %s" historyFile)

      let latestContent = File.ReadAllText(latestFile)
      Assert.Contains("FORNACH COMBAT LOG", latestContent)
      Assert.Contains("File Write Test", latestContent)
      Assert.Contains("COMBAT RESOLUTION: VICTORY", latestContent)

      // Write a second session to test history append
      let session2 = { CombatLogger.startSession "File Write Test 2" "Unit Test" player enemy with LogDirectory = tempDir }
      CombatLogger.writeSession session2 "DEFEAT" player enemy |> ignore

      let historyContent = File.ReadAllText(historyFile)
      Assert.Contains("File Write Test", historyContent)
      Assert.Contains("File Write Test 2", historyContent)
      Assert.Contains("COMBAT RESOLUTION: DEFEAT", historyContent)
    finally
      if Directory.Exists(tempDir) then
        try Directory.Delete(tempDir, true) with _ -> ()

  [<Fact>]
  let ``WeaponCondition repair advances condition tiers towards Pristine`` () =
    Assert.Equal(WeaponCondition.Damaged, WeaponCondition.repair WeaponCondition.Broken)
    Assert.Equal(WeaponCondition.Notched, WeaponCondition.repair WeaponCondition.Damaged)
    Assert.Equal(WeaponCondition.Pristine, WeaponCondition.repair WeaponCondition.Notched)
    Assert.Equal(WeaponCondition.Pristine, WeaponCondition.repair WeaponCondition.Pristine)
    Assert.Equal(WeaponCondition.Pristine, WeaponCondition.restorePristine WeaponCondition.Broken)

  [<Fact>]
  let ``Display renderBar reflects dynamic health tiers with percentage and critical alerts`` () =
    // Full health -> Green tier (100%)
    let fullBar = Fornach.Cli.Display.renderBar "󰋑 Health" 2400 2400 Theme.Red
    Assert.Contains("(100%)", fullBar)
    Assert.DoesNotContain("CRITICAL", fullBar)

    // Half health -> Yellow tier (Wounded)
    let halfBar = Fornach.Cli.Display.renderBar "󰋑 Health" 1200 2400 Theme.Red
    Assert.Contains("( 50%)", halfBar)
    Assert.Contains("Wounded", halfBar)

    // Critical low health -> Red tier with CRITICAL badge
    let lowBar = Fornach.Cli.Display.renderBar "󰋑 Health" 100 2400 Theme.Red
    Assert.Contains("(  4%)", lowBar)
    Assert.Contains("CRITICAL", lowBar)

  /// Builds a DamageApplied event attributed to `target`, so a test can mix events landing on
  /// different combatants (cleave, cataclysm splash, molten-aura burns) into one event list.
  let private hit (target: CombatantId) (plane: Plane) (amount: int) : CombatEvent =
    CombatEvent.DamageApplied
      { TargetId = target
        Plane = plane
        Amount = amount
        IsCritical = false
        IsArmorCompromised = false }

  [<Fact>]
  let ``damage totals count only the events that landed on the target`` () =
    let victim = CombatantId.New()
    let bystander = CombatantId.New()

    let totals =
      Display.damageTotals victim [ hit victim Physical 47; hit victim Physical 8; hit victim Mental 12; hit bystander Physical 999 ]
      |> Map.ofList

    Assert.Equal(55, totals[Physical])
    Assert.Equal(12, totals[Mental])

  [<Fact>]
  let ``damage totals are empty when every event landed elsewhere`` () =
    let victim = CombatantId.New()
    Assert.Empty(Display.damageTotals victim [ hit (CombatantId.New()) Physical 999 ])

  /// Runs `f` against a detached, non-ANSI Spectre console and returns everything it wrote.
  let private captureConsole (f: unit -> unit) =
    let writer = new StringWriter()
    let settings = AnsiConsoleSettings()
    settings.Ansi <- AnsiSupport.No
    settings.Interactive <- InteractionSupport.No
    settings.Out <- AnsiConsoleOutput(writer)
    let previous = AnsiConsole.Console
    AnsiConsole.Console <- AnsiConsole.Create(settings)

    try
      f ()
    finally
      AnsiConsole.Console <- previous

    writer.ToString()

  [<Fact>]
  let ``damage summary prints per-plane damage and the vitals swing`` () =
    let before = createTestCombatant "Training Dummy" 100 100 200 0

    let after =
      { before with
          Health = before.Health.ApplyDelta -55
          Morale = before.Morale.ApplyDelta -12 }

    let output =
      captureConsole (fun () ->
        Display.logDamageSummary [ hit before.Id Physical 47; hit before.Id Physical 8; hit before.Id Mental 12 ] before after)

    // Per-plane totals are summed, so two Physical strikes report as one 55.
    Assert.Contains("55 Physical", output)
    Assert.Contains("12 Mental", output)
    Assert.Contains("(3 hits)", output)
    Assert.Contains(sprintf "HP %d → %d" before.Health.Current after.Health.Current, output)
    Assert.Contains(sprintf "Morale %d → %d" before.Morale.Current after.Morale.Current, output)

  [<Fact>]
  let ``damage summary says so when nothing landed on the target`` () =
    let before = createTestCombatant "Bystander" 100 100 200 0
    let output = captureConsole (fun () -> Display.logDamageSummary [] before before)

    Assert.Contains("no damage applied", output)
    Assert.DoesNotContain("HP", output)

  [<Fact>]
  let ``a lethal blow reports the health it removed, not its overkill`` () =
    let attacker = createTestCombatant "Executioner" 400 400 200 0
    let victim = createTestCombatant "Wretch" 100 100 4 0
    let roller (_min: int) (_max: int) = 6

    let res = ActionResolver.resolve roller (StandardAttack(ForceStrike false)) attacker victim

    let claimed =
      res.Events
      |> List.sumBy (function
        | CombatEvent.DamageApplied d -> d.Amount
        | _ -> 0)

    Assert.True(res.Target.Health.IsDepleted, sprintf "the wretch survived on %d HP" res.Target.Health.Current)
    Assert.True(claimed <= victim.Health.Current, sprintf "events claimed %d damage against a %d HP pool" claimed victim.Health.Current)

  [<Fact>]
  let ``log viewer sizes an entry by the rows it wraps into`` () =
    Assert.Equal(1, TowerDisplay.wrappedRows 100 "A short line")
    // 100 columns leaves 92 usable ones (index gutter + panel padding).
    Assert.Equal(2, TowerDisplay.wrappedRows 100 (String.replicate 150 "x"))
    Assert.Equal(4, TowerDisplay.wrappedRows 100 (String.replicate 300 "x"))
    Assert.Equal(1, TowerDisplay.wrappedRows 100 "")
