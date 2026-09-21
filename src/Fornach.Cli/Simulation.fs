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
      elif combA.Health.IsDepleted then
        winner <- Some combB.Name
        winnerId <- Some combB.Id
        winnerIsA <- Some false
        loser <- Some combA.Name
        condition <- HealthDepleted
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
        elif combB.Health.IsDepleted then
          winner <- Some combA.Name
          winnerId <- Some combA.Id
          winnerIsA <- Some true
          loser <- Some combB.Name
          condition <- HealthDepleted
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
          let task = ctx.AddTask(sprintf "[bold %s]Simulating %s vs %s...[/]" Theme.Green archetypeA.Name archetypeB.Name, maxValue = float iterations)
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
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]Monte-Carlo Balance Report: %s vs. %s (%d Duels)[/]" Theme.Purple summary.ArchetypeNameA summary.ArchetypeNameB summary.TotalIterations)
        .Centered()
        .RuleStyle(Theme.StyleCurrentLine)
    )
    AnsiConsole.WriteLine()

    // 1. Matchup Profile
    let configTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    configTable.AddColumn(TableColumn(sprintf "[bold %s]Metric[/]" Theme.Foreground)) |> ignore
    configTable.AddColumn(TableColumn(sprintf "[bold %s]%s[/]" Theme.Green summary.ArchetypeNameA)) |> ignore
    configTable.AddColumn(TableColumn(sprintf "[bold %s]%s[/]" Theme.Pink summary.ArchetypeNameB)) |> ignore

    configTable.AddRow("Max HP (Physical)", sprintf "[%s]%d[/]" Theme.Red sampleA.Health.Maximum, sprintf "[%s]%d[/]" Theme.Red sampleB.Health.Maximum) |> ignore
    configTable.AddRow("Max Morale (Mental)", sprintf "[%s]%d[/]" Theme.Cyan sampleA.Morale.Maximum, sprintf "[%s]%d[/]" Theme.Cyan sampleB.Morale.Maximum) |> ignore
    configTable.AddRow("Armor Durability / Soak", sprintf "[%s]%d (%.0f%%)[/]" Theme.Yellow sampleA.Armor.Max (sampleA.Armor.AbsorptionRatio * 100.0), sprintf "[%s]%d (%.0f%%)[/]" Theme.Yellow sampleB.Armor.Max (sampleB.Armor.AbsorptionRatio * 100.0)) |> ignore
    configTable.AddRow("Top Phys Stat (Force/Prowess)", sprintf "%d / %d" (sampleA.GetStat Force) (sampleA.GetStat Prowess), sprintf "%d / %d" (sampleB.GetStat Force) (sampleB.GetStat Prowess)) |> ignore
    configTable.AddRow("Top Mental Stat (Intellect/Acumen)", sprintf "%d / %d" (sampleA.GetStat Intellect) (sampleA.GetStat Acumen), sprintf "%d / %d" (sampleB.GetStat Intellect) (sampleB.GetStat Acumen)) |> ignore
    AnsiConsole.Write(configTable)
    AnsiConsole.WriteLine()

    // 2. Win/Loss Distribution Table
    let pctA = (float summary.WinsA / float summary.TotalIterations) * 100.0
    let pctB = (float summary.WinsB / float summary.TotalIterations) * 100.0
    let pctStale = (float summary.Stalemates / float summary.TotalIterations) * 100.0

    let winTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    winTable.AddColumn(TableColumn(sprintf "[bold %s]Combatant[/]" Theme.Foreground)) |> ignore
    winTable.AddColumn(TableColumn(sprintf "[bold %s]Wins[/]" Theme.Foreground)) |> ignore
    winTable.AddColumn(TableColumn(sprintf "[bold %s]Win Rate[/]" Theme.Foreground)) |> ignore
    winTable.AddColumn(TableColumn(sprintf "[bold %s]Visual Share[/]" Theme.Foreground)) |> ignore

    let barA = String('█', int (Math.Round(pctA / 5.0)))
    let barB = String('█', int (Math.Round(pctB / 5.0)))
    let barStale = String('█', int (Math.Round(pctStale / 5.0)))

    winTable.AddRow(
      Markup(sprintf "[bold %s]%s[/]" Theme.Green summary.ArchetypeNameA),
      Markup(sprintf "[bold %s]%d[/]" Theme.Foreground summary.WinsA),
      Markup(sprintf "[bold %s]%.1f%%[/]" Theme.Green pctA),
      Markup(sprintf "[%s]%s[/]" Theme.Green barA)
    ) |> ignore

    winTable.AddRow(
      Markup(sprintf "[bold %s]%s[/]" Theme.Pink summary.ArchetypeNameB),
      Markup(sprintf "[bold %s]%d[/]" Theme.Foreground summary.WinsB),
      Markup(sprintf "[bold %s]%.1f%%[/]" Theme.Pink pctB),
      Markup(sprintf "[%s]%s[/]" Theme.Pink barB)
    ) |> ignore

    if summary.Stalemates > 0 then
      winTable.AddRow(
        Markup(sprintf "[%s]Stalemates (Max Rounds)[/]" Theme.Comment),
        Markup(sprintf "[%s]%d[/]" Theme.Comment summary.Stalemates),
        Markup(sprintf "[%s]%.1f%%[/]" Theme.Comment pctStale),
        Markup(sprintf "[%s]%s[/]" Theme.Comment barStale)
      ) |> ignore

    AnsiConsole.Write(winTable)
    AnsiConsole.WriteLine()

    // 3. Pacing & Lethality Metrics
    let pacingTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    pacingTable.AddColumn(TableColumn(sprintf "[bold %s]Pacing Metric[/]" Theme.Foreground)) |> ignore
    pacingTable.AddColumn(TableColumn(sprintf "[bold %s]Observed Value[/]" Theme.Foreground)) |> ignore
    pacingTable.AddColumn(TableColumn(sprintf "[bold %s]Design Target Reference[/]" Theme.Comment)) |> ignore

    let pacingBadge =
      if summary.AvgRounds <= 2.5 then sprintf "[bold %s]Fast / Decisive Blowout (Skewed Matchup Target: 1–2 Rounds)[/]" Theme.Green
      elif summary.AvgRounds <= 8.0 then sprintf "[bold %s]Balanced Tactical Duel (Even Matchup Target: 4–6 Rounds)[/]" Theme.Green
      else sprintf "[bold %s]Attritional Grind (> 8 Rounds)[/]" Theme.Yellow

    pacingTable.AddRow("Average Rounds to Kill", sprintf "[bold %s]%.2f[/]" Theme.Cyan summary.AvgRounds, pacingBadge) |> ignore
    pacingTable.AddRow("Round Range (Min - Max)", sprintf "%d - %d" summary.MinRounds summary.MaxRounds, "Bounded by 60-round timeout cap") |> ignore
    pacingTable.AddRow("Physical HP Depletions", sprintf "[%s]%d (%.1f%%)[/]" Theme.Red summary.HealthDepletions ((float summary.HealthDepletions / float summary.TotalIterations) * 100.0), "Defeated via Physical trauma") |> ignore
    pacingTable.AddRow("Mental Morale Depletions", sprintf "[%s]%d (%.1f%%)[/]" Theme.Cyan summary.MoraleDepletions ((float summary.MoraleDepletions / float summary.TotalIterations) * 100.0), "Defeated via Mental/Social breakdown") |> ignore
    pacingTable.AddRow("Execution Finishers", sprintf "[bold %s]%d (%.1f%%)[/]" Theme.Yellow summary.Executions ((float summary.Executions / float summary.TotalIterations) * 100.0), "Lethal strike delivered during Collapse") |> ignore
    pacingTable.AddRow("Whiff Rate per Duel", sprintf "%.2f whiffs" (float summary.TotalWhiffs / float summary.TotalIterations), "Deflected / zero net hit attacks") |> ignore
    pacingTable.AddRow("Critical Rate per Duel", sprintf "[bold %s]%.2f crits[/]" Theme.Yellow (float summary.TotalCrits / float summary.TotalIterations), "Blowouts and high-disparity ruptures") |> ignore

    AnsiConsole.Write(pacingTable)
    AnsiConsole.WriteLine()

  // =========================================================================
  // 4. 1 vs N Encirclement Swarm Simulation
  // =========================================================================

  let private runSingleGroupMatch
    (roller: DiceRoller)
    (soloFactory: unit -> Combatant)
    (mobFactory: unit -> Combatant)
    (mobCount: int)
    (maxRounds: int)
    : GroupSingleResult =
    let mutable solo = soloFactory ()
    let mutable mob = List.init mobCount (fun _ -> mobFactory ())
    let mutable round = 1
    let mutable winnerIsSolo : bool option = None
    let mutable condition = Stalemate
    let mutable mobCrits = 0
    let mutable peakPenalty = 0
    let mutable totalAoOs = 0
    let mutable totalCleaves = 0
    let mutable totalChains = 0

    while winnerIsSolo.IsNone && round <= maxRounds do
      // Phase 1: Solo Turn (Focus fire optimal target + Cleave / Chain adjacent opponents)
      if not mob.IsEmpty then
        let target = AI.chooseGroupTarget solo mob
        let adjacentTargets = mob |> List.filter (fun m -> m.Id <> target.Id)
        let intentSolo = AI.chooseIntentWithContext solo target mob.Length
        let groupRes = ActionResolver.resolveGroupTurn roller intentSolo solo target adjacentTargets
        solo <- groupRes.Actor

        let cleaveHits = groupRes.Events |> List.filter (function CombatEvent.CleaveExecuted _ -> true | _ -> false) |> List.length
        let chainHits = groupRes.Events |> List.filter (function CombatEvent.StrikeChained _ -> true | _ -> false) |> List.length
        totalCleaves <- totalCleaves + cleaveHits
        totalChains <- totalChains + chainHits

        let executedIds =
          groupRes.Events
          |> List.choose (function CombatEvent.Executed (_, tid, _) -> Some tid | _ -> None)
          |> Set.ofList

        let allResolved = groupRes.PrimaryTarget :: groupRes.SecondaryTargets

        // Update mob with resolved targets, apply turn upkeep, and filter out defeated
        mob <-
          allResolved
          |> List.map (fun m ->
            let up, _ = ActionResolver.applyTurnUpkeep m
            up)
          |> List.filter (fun m ->
            not (Set.contains m.Id executedIds)
            && not m.Health.IsDepleted
            && not m.Morale.IsDepleted)

      // Check win/loss after solo attack
      if mob.IsEmpty then
        winnerIsSolo <- Some true
        condition <- HealthDepleted
      elif solo.Health.IsDepleted then
        winnerIsSolo <- Some false
        condition <- HealthDepleted
      elif solo.Morale.IsDepleted then
        winnerIsSolo <- Some false
        condition <- MoraleDepleted
      else
        // Phase 2: Swarm Turn (All surviving mob members attack Solo concurrently)
        let mutable priorDefenses = 0
        let mutable mobIdx = 0
        while mobIdx < mob.Length && winnerIsSolo.IsNone do
          let attacker = mob.[mobIdx]
          let intentMob = AI.chooseIntent attacker solo
          let mobRes = ActionResolver.resolveEx roller intentMob attacker solo priorDefenses
          let newAttacker = mobRes.Actor
          solo <- mobRes.Target

          let aooHits = mobRes.Events |> List.filter (function CombatEvent.AttackOfOpportunityTriggered _ -> true | _ -> false) |> List.length
          totalAoOs <- totalAoOs + aooHits

          match mobRes.Contest with
          | Some c ->
            if c.IsCritical then mobCrits <- mobCrits + 1
            if c.EncirclementPenalty > peakPenalty then peakPenalty <- c.EncirclementPenalty
          | None -> ()

          let soloExecuted = mobRes.Events |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)
          if soloExecuted then
            let plane = match intentMob with ExecuteStrike p -> p | _ -> Physical
            winnerIsSolo <- Some false
            condition <- ExecutionFinisher plane
          elif solo.Health.IsDepleted then
            winnerIsSolo <- Some false
            condition <- HealthDepleted
          elif solo.Morale.IsDepleted then
            winnerIsSolo <- Some false
            condition <- MoraleDepleted

          // Handle if attacker died to an Attack of Opportunity or reactive riposte counter
          let attackerDefeated = newAttacker.Health.IsDepleted || newAttacker.Morale.IsDepleted
          if attackerDefeated then
            mob <- mob |> List.filter (fun m -> m.Id <> newAttacker.Id)
          else
            mob <- mob |> List.mapi (fun i m -> if i = mobIdx then newAttacker else m)
            priorDefenses <- priorDefenses + 1
            mobIdx <- mobIdx + 1

        // Phase 3: Solo Upkeep at end of round
        if winnerIsSolo.IsNone then
          let soloUpkeep, _ = ActionResolver.applyTurnUpkeep solo
          let swarmExertion = if mob.Length >= 3 then 1 else 0
          solo <-
            if swarmExertion > 0 then
              soloUpkeep |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + swarmExertion })
            else
              soloUpkeep
          if solo.Health.IsDepleted then
            winnerIsSolo <- Some false
            condition <- HealthDepleted
          elif solo.Morale.IsDepleted then
            winnerIsSolo <- Some false
            condition <- MoraleDepleted

      if winnerIsSolo.IsNone then
        round <- round + 1

    let elim = mobCount - mob.Length
    let soloHPPct = Math.Max(0.0, (float solo.Health.Current / float solo.Health.Maximum) * 100.0)
    let soloMoralePct = Math.Max(0.0, (float solo.Morale.Current / float solo.Morale.Maximum) * 100.0)

    { SoloWon = winnerIsSolo = Some true
      Rounds = Math.Min(round, maxRounds)
      OpponentsInitial = mobCount
      OpponentsEliminated = elim
      SoloRemainingHPPct = soloHPPct
      SoloRemainingMoralePct = soloMoralePct
      SoloExhaustion = solo.Meters.Exhaustion.Value
      SoloOverwhelm = solo.Meters.Overwhelm.Value
      TotalCritsDealtByMob = mobCrits
      PeakEncirclementPenalty = peakPenalty
      TotalAoOsTriggered = totalAoOs
      TotalCleaves = totalCleaves
      TotalChains = totalChains
      Condition = condition }


  let runGroupBatch (soloArch: ArchetypeInfo) (mobArch: ArchetypeInfo) (mobCount: int) (iterations: int) : GroupSimulationSummary =
    let rng = Random()
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
    let maxRoundsPerMatch = 60

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
          let task = ctx.AddTask(sprintf "[bold %s]Simulating 1 vs %d: %s vs %s swarm...[/]" Theme.Cyan mobCount soloArch.Name mobArch.Name, maxValue = float iterations)
          let acc = ResizeArray<GroupSingleResult>(iterations)
          for _ in 1 .. iterations do
            let res = runSingleGroupMatch roller soloArch.Factory mobArch.Factory mobCount maxRoundsPerMatch
            acc.Add res
            task.Increment 1.0
          acc |> Seq.toList
        )

    let soloWins = results |> List.filter (fun r -> r.SoloWon) |> List.length
    let mobWins = results |> List.filter (fun r -> not r.SoloWon && r.Condition <> Stalemate) |> List.length
    let stalemates = results |> List.filter (fun r -> r.Condition = Stalemate) |> List.length
    let soloWinRate = (float soloWins / float iterations) * 100.0

    let resolvedRounds = results |> List.filter (fun r -> r.Condition <> Stalemate) |> List.map (fun r -> r.Rounds)
    let avgRounds = if resolvedRounds.IsEmpty then 0.0 else resolvedRounds |> List.averageBy float
    let minRounds = if resolvedRounds.IsEmpty then 0 else List.min resolvedRounds
    let maxRounds = if resolvedRounds.IsEmpty then 0 else List.max resolvedRounds

    let avgElim = results |> List.averageBy (fun r -> float r.OpponentsEliminated)
    let avgPeakPen = results |> List.averageBy (fun r -> float r.PeakEncirclementPenalty)
    let avgAoO = results |> List.averageBy (fun r -> float r.TotalAoOsTriggered)
    let avgCleaves = results |> List.averageBy (fun r -> float r.TotalCleaves)
    let avgChains = results |> List.averageBy (fun r -> float r.TotalChains)

    let winningResults = results |> List.filter (fun r -> r.SoloWon)
    let avgSoloHP = if winningResults.IsEmpty then 0.0 else winningResults |> List.averageBy (fun r -> r.SoloRemainingHPPct)

    let elimDist =
      [ 0 .. mobCount ]
      |> List.map (fun k ->
        let count = results |> List.filter (fun r -> r.OpponentsEliminated = k) |> List.length
        let pct = (float count / float iterations) * 100.0
        (k, count, pct))

    let healthKills = results |> List.filter (fun r -> r.Condition = HealthDepleted) |> List.length
    let moraleKills = results |> List.filter (fun r -> r.Condition = MoraleDepleted) |> List.length
    let executions = results |> List.filter (fun r -> match r.Condition with ExecutionFinisher _ -> true | _ -> false) |> List.length

    { SoloArchetypeName = soloArch.Name
      MobArchetypeName = mobArch.Name
      MobCount = mobCount
      TotalIterations = iterations
      SoloWins = soloWins
      MobWins = mobWins
      Stalemates = stalemates
      SoloWinRate = soloWinRate
      AvgRounds = avgRounds
      MinRounds = minRounds
      MaxRounds = maxRounds
      AvgEliminations = avgElim
      EliminationDistribution = elimDist
      AvgPeakPenalty = avgPeakPen
      AvgSoloRemainingHP = avgSoloHP
      AvgAoOsTriggered = avgAoO
      AvgCleaves = avgCleaves
      AvgChains = avgChains
      HealthDepletions = healthKills
      MoraleDepletions = moraleKills
      Executions = executions }


  let renderGroupDashboard (summary: GroupSimulationSummary) (sampleSolo: Combatant) (sampleMob: Combatant) =
    AnsiConsole.WriteLine()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]1 vs %d Encirclement Swarm Report: %s vs. %d× %s (%d Duels)[/]"
        Theme.Purple summary.MobCount summary.SoloArchetypeName summary.MobCount summary.MobArchetypeName summary.TotalIterations)
        .Centered()
        .RuleStyle(Theme.StyleCurrentLine)
    )
    AnsiConsole.WriteLine()

    // 1. Action Economy Profile Table
    let configTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    configTable.AddColumn(TableColumn(sprintf "[bold %s]Metric[/]" Theme.Foreground)) |> ignore
    configTable.AddColumn(TableColumn(sprintf "[bold %s]%s (Solo)[/]" Theme.Green summary.SoloArchetypeName)) |> ignore
    configTable.AddColumn(TableColumn(sprintf "[bold %s]%d× %s (Swarm)[/]" Theme.Pink summary.MobCount summary.MobArchetypeName)) |> ignore

    let totalMobHP = sampleMob.Health.Maximum * summary.MobCount
    let totalMobMorale = sampleMob.Morale.Maximum * summary.MobCount
    let actionRateSolo = "1 Action / Round"
    let actionRateMob = sprintf "%d Actions / Round (%d:1 Disparity)" summary.MobCount summary.MobCount

    configTable.AddRow("Action Economy", sprintf "[bold %s]%s[/]" Theme.Green actionRateSolo, sprintf "[bold %s]%s[/]" Theme.Pink actionRateMob) |> ignore
    configTable.AddRow("Total HP Pool", sprintf "[%s]%d[/]" Theme.Red sampleSolo.Health.Maximum, sprintf "[%s]%d total[/] (%d each)" Theme.Red totalMobHP sampleMob.Health.Maximum) |> ignore
    configTable.AddRow("Total Morale Pool", sprintf "[%s]%d[/]" Theme.Cyan sampleSolo.Morale.Maximum, sprintf "[%s]%d total[/] (%d each)" Theme.Cyan totalMobMorale sampleMob.Morale.Maximum) |> ignore
    configTable.AddRow("Armor Durability / Soak", sprintf "[%s]%d (%.0f%%)[/]" Theme.Yellow sampleSolo.Armor.Max (sampleSolo.Armor.AbsorptionRatio * 100.0), sprintf "[%s]%d (%.0f%%)[/]" Theme.Yellow sampleMob.Armor.Max (sampleMob.Armor.AbsorptionRatio * 100.0)) |> ignore

    let soloDef = sampleSolo.GetStat Fortitude
    let mobAtk = sampleMob.GetStat Force
    let pressureRatio = float mobAtk / Math.Max(1.0, float soloDef)
    configTable.AddRow("Key Stat Disparity (Def vs. Atk)", sprintf "Fortitude: %d" soloDef, sprintf "Force: %d (Pressure Ratio: %.2fx)" mobAtk pressureRatio) |> ignore
    AnsiConsole.Write(configTable)
    AnsiConsole.WriteLine()

    // 2. Win/Loss Distribution Table
    let pctSolo = summary.SoloWinRate
    let pctMob = (float summary.MobWins / float summary.TotalIterations) * 100.0
    let pctStale = (float summary.Stalemates / float summary.TotalIterations) * 100.0

    let winTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    winTable.AddColumn(TableColumn(sprintf "[bold %s]Faction[/]" Theme.Foreground)) |> ignore
    winTable.AddColumn(TableColumn(sprintf "[bold %s]Wins[/]" Theme.Foreground)) |> ignore
    winTable.AddColumn(TableColumn(sprintf "[bold %s]Win Rate[/]" Theme.Foreground)) |> ignore
    winTable.AddColumn(TableColumn(sprintf "[bold %s]Visual Share[/]" Theme.Foreground)) |> ignore

    let barSolo = String('█', int (Math.Round(pctSolo / 5.0)))
    let barMob = String('█', int (Math.Round(pctMob / 5.0)))
    let barStale = String('█', int (Math.Round(pctStale / 5.0)))

    winTable.AddRow(
      Markup(sprintf "[bold %s]%s (Solo)[/]" Theme.Green summary.SoloArchetypeName),
      Markup(sprintf "[bold %s]%d[/]" Theme.Foreground summary.SoloWins),
      Markup(sprintf "[bold %s]%.1f%%[/]" Theme.Green pctSolo),
      Markup(sprintf "[%s]%s[/]" Theme.Green barSolo)
    ) |> ignore

    winTable.AddRow(
      Markup(sprintf "[bold %s]%d× %s (Swarm)[/]" Theme.Pink summary.MobCount summary.MobArchetypeName),
      Markup(sprintf "[bold %s]%d[/]" Theme.Foreground summary.MobWins),
      Markup(sprintf "[bold %s]%.1f%%[/]" Theme.Pink pctMob),
      Markup(sprintf "[%s]%s[/]" Theme.Pink barMob)
    ) |> ignore

    if summary.Stalemates > 0 then
      winTable.AddRow(
        Markup(sprintf "[%s]Stalemates (Max Rounds)[/]" Theme.Comment),
        Markup(sprintf "[%s]%d[/]" Theme.Comment summary.Stalemates),
        Markup(sprintf "[%s]%.1f%%[/]" Theme.Comment pctStale),
        Markup(sprintf "[%s]%s[/]" Theme.Comment barStale)
      ) |> ignore

    AnsiConsole.Write(winTable)
    AnsiConsole.WriteLine()

    // 3. Encirclement & Swarm Attrition Telemetry
    let telemetryTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    telemetryTable.AddColumn(TableColumn(sprintf "[bold %s]Encirclement Metric[/]" Theme.Foreground)) |> ignore
    telemetryTable.AddColumn(TableColumn(sprintf "[bold %s]Observed Value[/]" Theme.Foreground)) |> ignore
    telemetryTable.AddColumn(TableColumn(sprintf "[bold %s]Tactical Interpretation[/]" Theme.Comment)) |> ignore

    let assessmentBadge =
      if summary.SoloWinRate >= 80.0 then
        sprintf "[bold %s]Stalwart Domination (Champion reliably shrugs off swarm encirclement)[/]" Theme.Green
      elif summary.SoloWinRate >= 45.0 then
        sprintf "[bold %s]Contested Equilibrium (Tipping point: attrition & flank pressure decide match)[/]" Theme.Yellow
      else
        sprintf "[bold %s]Swarm Overrun (Encirclement penalty systematically breaks champion's defense)[/]" Theme.Red

    telemetryTable.AddRow("Tactical Matchup Balance", assessmentBadge, "Evaluated across encounter outcome threshold") |> ignore
    telemetryTable.AddRow("Average Opponents Eliminated", sprintf "[bold %s]%.2f / %d[/] (%.1f%%)" Theme.Cyan summary.AvgEliminations summary.MobCount ((summary.AvgEliminations / float summary.MobCount) * 100.0), "Mean number of enemies slain before combat concluded") |> ignore
    telemetryTable.AddRow("Average Peak Flank Penalty", sprintf "[bold %s]-%.1f Defense Hits[/]" Theme.Orange summary.AvgPeakPenalty, "Compounding Shadowrun-style defense degradation per round") |> ignore
    telemetryTable.AddRow("Attacks of Opportunity Triggered", sprintf "[bold %s]%.2f / encounter[/]" Theme.Cyan summary.AvgAoOsTriggered, "Preemptive interception of flankers via Agility / Discipline disparity") |> ignore
    telemetryTable.AddRow("Cleave Attacks Executed", sprintf "[bold %s]%.2f / encounter[/]" Theme.Red summary.AvgCleaves, "Power Stance sweeping multi-target cleaves (disparity-scaled recklessness)") |> ignore
    telemetryTable.AddRow("Cadence Chains Flowed", sprintf "[bold %s]%.2f / encounter[/]" Theme.Purple summary.AvgChains, "Discipline Stance fluid chained strikes (0 recklessness, +1 Study Stack)") |> ignore
    telemetryTable.AddRow("Average Rounds to Resolution", sprintf "[bold %s]%.2f rounds[/]" Theme.Cyan summary.AvgRounds, sprintf "Bounded between %d and %d rounds" summary.MinRounds summary.MaxRounds) |> ignore
    telemetryTable.AddRow("Victorious Champion HP", sprintf "[bold %s]%.1f%% remaining[/]" Theme.Green summary.AvgSoloRemainingHP, "Average HP buffer retained upon clearing the swarm") |> ignore


    let wipeouts = summary.EliminationDistribution |> List.tryFind (fun (k, _, _) -> k = summary.MobCount) |> Option.map (fun (_, _, p) -> p) |> Option.defaultValue 0.0
    let overruns = summary.EliminationDistribution |> List.tryFind (fun (k, _, _) -> k = 0) |> Option.map (fun (_, _, p) -> p) |> Option.defaultValue 0.0
    telemetryTable.AddRow("Total Swarm Wipeouts (All Slain)", sprintf "[bold %s]%.1f%%[/]" Theme.Green wipeouts, "Champion annihilated all opponents") |> ignore
    telemetryTable.AddRow("Total Champion Overruns (0 Slain)", sprintf "[bold %s]%.1f%%[/]" Theme.Red overruns, "Swarm overwhelmed champion before losing a single unit") |> ignore

    AnsiConsole.Write(telemetryTable)
    AnsiConsole.WriteLine()
