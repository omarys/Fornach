namespace Fornach.Cli

open System
open Spectre.Console
open Fornach.Domain
open Fornach.Engine

module Simulation =

  let private runSingleMatch (roller: DiceRoller) (factoryA: unit -> Combatant) (factoryB: unit -> Combatant) (maxRounds: int) : SingleCombatResult =
    let mutable combA = factoryA ()
    let mutable combB = factoryB ()
    let mutable round = 1
    let mutable winner : string option = None
    let mutable winnerId : CombatantId option = None
    let mutable winnerIsA : bool option = None
    let mutable loser : string option = None
    let mutable condition = Stalemate
    let mutable whiffs = 0
    let mutable crits = 0

    while winner.IsNone && round <= maxRounds do
      // Combatant A turn
      let intentA = AI.chooseIntent combA combB
      let resA = ActionResolver.resolve roller intentA combA combB
      combA <- resA.Actor
      combB <- resA.Target

      match resA.Contest with
      | Some c ->
        if c.IsWhiff then whiffs <- whiffs + 1
        if c.IsCritical then crits <- crits + 1
      | None -> ()

      let hasExecutedB =
        resA.Events
        |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

      if hasExecutedB then
        let plane = match intentA with ExecuteStrike p -> p | _ -> Physical
        winner <- Some combA.Name
        winnerId <- Some combA.Id
        winnerIsA <- Some true
        loser <- Some combB.Name
        condition <- ExecutionFinisher plane
      elif combB.Health.IsDepleted then
        winner <- Some combA.Name
        winnerId <- Some combA.Id
        winnerIsA <- Some true
        loser <- Some combB.Name
        condition <- HealthDepleted
      elif combB.Morale.IsDepleted then
        winner <- Some combA.Name
        winnerId <- Some combA.Id
        winnerIsA <- Some true
        loser <- Some combB.Name
        condition <- MoraleDepleted
      else
        // Combatant B turn
        let intentB = AI.chooseIntent combB combA
        let resB = ActionResolver.resolve roller intentB combB combA
        combB <- resB.Actor
        combA <- resB.Target

        match resB.Contest with
        | Some c ->
          if c.IsWhiff then whiffs <- whiffs + 1
          if c.IsCritical then crits <- crits + 1
        | None -> ()

        let hasExecutedA =
          resB.Events
          |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

        if hasExecutedA then
          let plane = match intentB with ExecuteStrike p -> p | _ -> Physical
          winner <- Some combB.Name
          winnerId <- Some combB.Id
          winnerIsA <- Some false
          loser <- Some combA.Name
          condition <- ExecutionFinisher plane
        elif combA.Health.IsDepleted then
          winner <- Some combB.Name
          winnerId <- Some combB.Id
          winnerIsA <- Some false
          loser <- Some combA.Name
          condition <- HealthDepleted
        elif combA.Morale.IsDepleted then
          winner <- Some combB.Name
          winnerId <- Some combB.Id
          winnerIsA <- Some false
          loser <- Some combA.Name
          condition <- MoraleDepleted

      if winner.IsNone then
        round <- round + 1

    { WinnerName = winner
      WinnerId = winnerId
      WinnerIsA = winnerIsA
      LoserName = loser
      Rounds = Math.Min(round, maxRounds)
      Condition = condition
      TotalWhiffs = whiffs
      TotalCrits = crits }

  let runBatch (archetypeA: ArchetypeInfo) (archetypeB: ArchetypeInfo) (iterations: int) : SimulationSummary =
    let rng = Random()
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
    let maxRoundsPerMatch = 60

    let sampleA = archetypeA.Factory()
    let sampleB = archetypeB.Factory()

    let results =
      AnsiConsole.Progress()
        .AutoClear(false)
        .Columns([|
          TaskDescriptionColumn() :> ProgressColumn
          ProgressBarColumn() :> ProgressColumn
          PercentageColumn() :> ProgressColumn
          RemainingTimeColumn() :> ProgressColumn
        |])
        .Start(fun ctx ->
          let task = ctx.AddTask(sprintf "[green]Simulating %s vs %s...[/]" archetypeA.Name archetypeB.Name, maxValue = float iterations)
          let acc = ResizeArray<SingleCombatResult>(iterations)
          for _ in 1 .. iterations do
            let res = runSingleMatch roller archetypeA.Factory archetypeB.Factory maxRoundsPerMatch
            acc.Add res
            task.Increment 1.0
          acc |> Seq.toList
        )

    let totalWinsA = results |> List.filter (fun r -> r.WinnerIsA = Some true) |> List.length
    let totalWinsB = results |> List.filter (fun r -> r.WinnerIsA = Some false) |> List.length
    let totalStalemates = results |> List.filter (fun r -> r.WinnerIsA.IsNone) |> List.length

    let resolvedRounds = results |> List.filter (fun r -> r.Condition <> Stalemate) |> List.map (fun r -> r.Rounds)
    let avgRounds = if resolvedRounds.IsEmpty then 0.0 else resolvedRounds |> List.averageBy float
    let minRounds = if resolvedRounds.IsEmpty then 0 else List.min resolvedRounds
    let maxRounds = if resolvedRounds.IsEmpty then 0 else List.max resolvedRounds

    let healthKills = results |> List.filter (fun r -> r.Condition = HealthDepleted) |> List.length
    let moraleKills = results |> List.filter (fun r -> r.Condition = MoraleDepleted) |> List.length
    let executions = results |> List.filter (fun r -> match r.Condition with ExecutionFinisher _ -> true | _ -> false) |> List.length

    let totalWhiffs = results |> List.sumBy (fun r -> r.TotalWhiffs)
    let totalCrits = results |> List.sumBy (fun r -> r.TotalCrits)

    { ArchetypeNameA = archetypeA.Name
      ArchetypeNameB = archetypeB.Name
      TotalIterations = iterations
      WinsA = totalWinsA
      WinsB = totalWinsB
      Stalemates = totalStalemates
      AvgRounds = avgRounds
      MinRounds = minRounds
      MaxRounds = maxRounds
      HealthDepletions = healthKills
      MoraleDepletions = moraleKills
      Executions = executions
      TotalWhiffs = totalWhiffs
      TotalCrits = totalCrits }

  let renderDashboard (summary: SimulationSummary) (sampleA: Combatant) (sampleB: Combatant) =
    AnsiConsole.WriteLine()
    AnsiConsole.Write(Rule(sprintf "[bold yellow]Monte-Carlo Balance Report: %s vs. %s (%d Duels)[/]" summary.ArchetypeNameA summary.ArchetypeNameB summary.TotalIterations).Centered())
    AnsiConsole.WriteLine()

    // 1. Matchup Profile
    let configTable = Table().Border(TableBorder.Rounded).BorderColor(Color.Grey)
    configTable.AddColumn(TableColumn("[bold white]Metric[/]")) |> ignore
    configTable.AddColumn(TableColumn(sprintf "[bold green]%s[/]" summary.ArchetypeNameA)) |> ignore
    configTable.AddColumn(TableColumn(sprintf "[bold red]%s[/]" summary.ArchetypeNameB)) |> ignore

    configTable.AddRow("Max HP (Physical)", sprintf "%d" sampleA.Health.Maximum, sprintf "%d" sampleB.Health.Maximum) |> ignore
    configTable.AddRow("Max Morale (Mental)", sprintf "%d" sampleA.Morale.Maximum, sprintf "%d" sampleB.Morale.Maximum) |> ignore
    configTable.AddRow("Armor Durability / Soak", sprintf "%d (%.0f%%)" sampleA.Armor.Max (sampleA.Armor.AbsorptionRatio * 100.0), sprintf "%d (%.0f%%)" sampleB.Armor.Max (sampleB.Armor.AbsorptionRatio * 100.0)) |> ignore
    configTable.AddRow("Top Phys Stat (Force/Prowess)", sprintf "%d / %d" (sampleA.GetStat Force) (sampleA.GetStat Prowess), sprintf "%d / %d" (sampleB.GetStat Force) (sampleB.GetStat Prowess)) |> ignore
    configTable.AddRow("Top Mental Stat (Intellect/Acumen)", sprintf "%d / %d" (sampleA.GetStat Intellect) (sampleA.GetStat Acumen), sprintf "%d / %d" (sampleB.GetStat Intellect) (sampleB.GetStat Acumen)) |> ignore
    AnsiConsole.Write(configTable)
    AnsiConsole.WriteLine()

    // 2. Win/Loss Distribution Table
    let pctA = (float summary.WinsA / float summary.TotalIterations) * 100.0
    let pctB = (float summary.WinsB / float summary.TotalIterations) * 100.0
    let pctStale = (float summary.Stalemates / float summary.TotalIterations) * 100.0

    let winTable = Table().Border(TableBorder.Double).BorderColor(Color.Gold1)
    winTable.AddColumn(TableColumn("[bold white]Combatant[/]")) |> ignore
    winTable.AddColumn(TableColumn("[bold white]Wins[/]")) |> ignore
    winTable.AddColumn(TableColumn("[bold white]Win Rate[/]")) |> ignore
    winTable.AddColumn(TableColumn("[bold white]Visual Share[/]")) |> ignore

    let barA = String('█', int (Math.Round(pctA / 5.0)))
    let barB = String('█', int (Math.Round(pctB / 5.0)))
    let barStale = String('█', int (Math.Round(pctStale / 5.0)))

    winTable.AddRow(
      Markup(sprintf "[bold green]%s[/]" summary.ArchetypeNameA),
      Markup(sprintf "%d" summary.WinsA),
      Markup(sprintf "[bold green]%.1f%%[/]" pctA),
      Markup(sprintf "[green]%s[/]" barA)
    ) |> ignore

    winTable.AddRow(
      Markup(sprintf "[bold red]%s[/]" summary.ArchetypeNameB),
      Markup(sprintf "%d" summary.WinsB),
      Markup(sprintf "[bold red]%.1f%%[/]" pctB),
      Markup(sprintf "[red]%s[/]" barB)
    ) |> ignore

    if summary.Stalemates > 0 then
      winTable.AddRow(
        Markup("[grey]Stalemates (Max Rounds)[/]"),
        Markup(sprintf "%d" summary.Stalemates),
        Markup(sprintf "[grey]%.1f%%[/]" pctStale),
        Markup(sprintf "[grey]%s[/]" barStale)
      ) |> ignore

    AnsiConsole.Write(winTable)
    AnsiConsole.WriteLine()

    // 3. Pacing & Lethality Metrics
    let pacingTable = Table().Border(TableBorder.Rounded).BorderColor(Color.Cyan1)
    pacingTable.AddColumn(TableColumn("[bold white]Pacing Metric[/]")) |> ignore
    pacingTable.AddColumn(TableColumn("[bold white]Observed Value[/]")) |> ignore
    pacingTable.AddColumn(TableColumn("[bold white]Design Target Reference[/]")) |> ignore

    let pacingBadge =
      if summary.AvgRounds <= 2.5 then "[bold green]Fast / Decisive Blowout (Skewed Matchup Target: 1–2 Rounds)[/]"
      elif summary.AvgRounds <= 8.0 then "[bold green]Balanced Tactical Duel (Even Matchup Target: 4–6 Rounds)[/]"
      else "[bold yellow]Attritional Grind (> 8 Rounds)[/]"

    pacingTable.AddRow("Average Rounds to Kill", sprintf "[bold cyan]%.2f[/]" summary.AvgRounds, pacingBadge) |> ignore
    pacingTable.AddRow("Round Range (Min - Max)", sprintf "%d - %d" summary.MinRounds summary.MaxRounds, "Bounded by 60-round timeout cap") |> ignore
    pacingTable.AddRow("Physical HP Depletions", sprintf "%d (%.1f%%)" summary.HealthDepletions ((float summary.HealthDepletions / float summary.TotalIterations) * 100.0), "Defeated via Physical trauma") |> ignore
    pacingTable.AddRow("Mental Morale Depletions", sprintf "%d (%.1f%%)" summary.MoraleDepletions ((float summary.MoraleDepletions / float summary.TotalIterations) * 100.0), "Defeated via Mental/Social breakdown") |> ignore
    pacingTable.AddRow("Execution Finishers", sprintf "%d (%.1f%%)" summary.Executions ((float summary.Executions / float summary.TotalIterations) * 100.0), "Lethal strike delivered during Collapse") |> ignore
    pacingTable.AddRow("Whiff Rate per Duel", sprintf "%.2f whiffs" (float summary.TotalWhiffs / float summary.TotalIterations), "Deflected / zero net hit attacks") |> ignore
    pacingTable.AddRow("Critical Rate per Duel", sprintf "%.2f crits" (float summary.TotalCrits / float summary.TotalIterations), "Blowouts and high-disparity ruptures") |> ignore

    AnsiConsole.Write(pacingTable)
    AnsiConsole.WriteLine()
