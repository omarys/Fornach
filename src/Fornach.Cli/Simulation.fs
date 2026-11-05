namespace Fornach.Cli

open System
open Spectre.Console
open Fornach.Domain
open Fornach.Engine
open Fornach.Engine.CombatSimulation
open Fornach.Story

/// Presentation only. The harness itself lives in `Fornach.Engine.CombatSimulation`; what stays here is
/// the Spectre progress bars and the tables, plus the one place where the story binds its own policy
/// table to the engine's measurements.
module Simulation =

  /// The story's press table, bound once so every measurement below fights with the Aspects' real moves.
  let private policies = Policies.all

  type GroupPolicy = Fornach.Engine.GroupPolicy
  type TippingPointResult = Fornach.Engine.TippingPointResult
  type MonsterSwarmResultRow = Fornach.Engine.MonsterSwarmResultRow

  let runSingleMatch = CombatSimulation.runSingleMatch policies
  let runParallelBatch = CombatSimulation.runParallelBatch
  let aiPolicy = CombatSimulation.aiPolicy policies
  let runHeadlessBatch = CombatSimulation.runHeadlessBatch policies
  let runPreparedGroupMatchWith = CombatSimulation.runPreparedGroupMatchWith policies
  let runPreparedGroupMatch = CombatSimulation.runPreparedGroupMatch policies
  let runSingleGroupMatch = CombatSimulation.runSingleGroupMatch policies
  let runHeadlessGroupBatch = CombatSimulation.runHeadlessGroupBatch policies
  let evaluateMobWinRate = CombatSimulation.evaluateMobWinRate policies
  let findSwarmTippingPoint = CombatSimulation.findSwarmTippingPoint policies
  let runBestiarySwarmBatch = CombatSimulation.runBestiarySwarmBatch policies
  let runProgressionSimWith = CombatSimulation.runProgressionSimWith policies
  let runProgressionSim = CombatSimulation.runProgressionSim policies

  let runBatch (archetypeA: ArchetypeInfo) (archetypeB: ArchetypeInfo) (iterations: int) : SimulationSummary =
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
          // Spectre's ProgressTask mutation is not documented as thread-safe, so
          // serialise the ticks. Contention is irrelevant next to a combat sim.
          let gate = obj()

          runParallelBatch
            batchBaseSeed
            iterations
            (fun () -> lock gate (fun () -> task.Increment 1.0))
            (fun roller -> runSingleMatch roller archetypeA.Factory archetypeB.Factory maxRoundsPerMatch)
          |> Array.toList
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
    let topPhysOff (c: Combatant) = [ c.GetStat Force; c.GetStat Finesse; c.GetStat Prowess ] |> List.max
    let topPhysDef (c: Combatant) = [ c.GetStat Fortitude; c.GetStat Reflex; c.GetStat Poise ] |> List.max
    let topMentalOff (c: Combatant) = [ c.GetStat Intellect; c.GetStat Acuity; c.GetStat Acumen ] |> List.max
    let topMentalDef (c: Combatant) = [ c.GetStat Resolve; c.GetStat Intuition; c.GetStat Composure ] |> List.max

    configTable.AddRow("Top Phys Stat (Off / Def)", sprintf "%d / %d" (topPhysOff sampleA) (topPhysDef sampleA), sprintf "%d / %d" (topPhysOff sampleB) (topPhysDef sampleB)) |> ignore
    configTable.AddRow("Top Mental Stat (Off / Def)", sprintf "%d / %d" (topMentalOff sampleA) (topMentalDef sampleA), sprintf "%d / %d" (topMentalOff sampleB) (topMentalDef sampleB)) |> ignore
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

  let runGroupBatch (soloArch: ArchetypeInfo) (mobArch: ArchetypeInfo) (mobCount: int) (iterations: int) : GroupSimulationSummary =
    let maxRoundsPerMatch = Math.Max(60, mobCount + 20)

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
          let gate = obj()

          runParallelBatch
            batchBaseSeed
            iterations
            (fun () -> lock gate (fun () -> task.Increment 1.0))
            (fun roller ->
              // The swarm's solo is the player-style champion; its mobs are plain opposition.
              runSingleGroupMatch
                roller
                (fun () -> { soloArch.Factory() with PlayerStyle = true })
                (fun () -> { mobArch.Factory() with PlayerStyle = false })
                mobCount
                maxRoundsPerMatch)
          |> Array.toList
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
    let avgTempoShifts = results |> List.averageBy (fun r -> float r.TotalTempoShifts)

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
      AvgTempoShifts = avgTempoShifts
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
    telemetryTable.AddRow("Cadence Chains Flowed", sprintf "[bold %s]%.2f / encounter[/]" Theme.Purple summary.AvgChains, "Discipline Stance fluid chained strikes (0 recklessness, +1 Combat Ramp)") |> ignore
    telemetryTable.AddRow("Vor / Nach Tempo Shifts", sprintf "[bold %s]%.2f / encounter[/]" Theme.Yellow summary.AvgTempoShifts, "Initiative changing hands between the solo and the pack (previously invisible to this sweep)") |> ignore
    telemetryTable.AddRow("Average Rounds to Resolution", sprintf "[bold %s]%.2f rounds[/]" Theme.Cyan summary.AvgRounds, sprintf "Bounded between %d and %d rounds" summary.MinRounds summary.MaxRounds) |> ignore
    telemetryTable.AddRow("Victorious Champion HP", sprintf "[bold %s]%.1f%% remaining[/]" Theme.Green summary.AvgSoloRemainingHP, "Average HP buffer retained upon clearing the swarm") |> ignore


    let wipeouts = summary.EliminationDistribution |> List.tryFind (fun (k, _, _) -> k = summary.MobCount) |> Option.map (fun (_, _, p) -> p) |> Option.defaultValue 0.0
    let overruns = summary.EliminationDistribution |> List.tryFind (fun (k, _, _) -> k = 0) |> Option.map (fun (_, _, p) -> p) |> Option.defaultValue 0.0
    telemetryTable.AddRow("Total Swarm Wipeouts (All Slain)", sprintf "[bold %s]%.1f%%[/]" Theme.Green wipeouts, "Champion annihilated all opponents") |> ignore
    telemetryTable.AddRow("Total Champion Overruns (0 Slain)", sprintf "[bold %s]%.1f%%[/]" Theme.Red overruns, "Swarm overwhelmed champion before losing a single unit") |> ignore

    AnsiConsole.Write(telemetryTable)
    AnsiConsole.WriteLine()

  let renderBalanceMatrix () =
    AnsiConsole.WriteLine()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]FORNACH ARCHETYPE BALANCE BENCHMARK: FIGHTER & MAGE MATCHUP MATRIX[/]" Theme.Purple)
        .Centered()
        .RuleStyle(Theme.StyleCurrentLine)
    )
    AnsiConsole.WriteLine()

    // The macro benchmark measures the two classes the game lets you build.
    let championClasses = CharacterClass.PlayerClasses

    let tiers = [
      CombatTier.Novice
      CombatTier.Veteran
      CombatTier.Master
      CombatTier.GrandMaster
    ]

    let maxMobCount = 100
    let iterations = 100

    // Every cell gets its own seeded roller, so a cell's number depends only on that matchup and is
    // reproducible on its own. A single shared Random made each result depend on how many dice every
    // earlier cell had consumed, which is why the table used to shift whenever an unrelated balance
    // change altered a fight's length.
    let cellRoller (cellSeed: int) : DiceRoller =
      let rng = Random(cellSeed)
      fun min max -> rng.Next(min, max + 1)

    let matrixTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Champion Archetype[/]" Theme.Foreground)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Tier[/]" Theme.Cyan)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Warrior (Power)[/]" Theme.Red)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Rogue (Finesse)[/]" Theme.Green)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Mage (Arcane)[/]" Theme.Pink)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Tactical Dynamics & Observations[/]" Theme.Comment)) |> ignore

    AnsiConsole.Progress()
      .AutoClear(false)
      .Columns([|
        TaskDescriptionColumn() :> ProgressColumn
        ProgressBarColumn() :> ProgressColumn
        PercentageColumn() :> ProgressColumn
        RemainingTimeColumn() :> ProgressColumn
      |])
      .Start(fun ctx ->
        let task = ctx.AddTask(sprintf "[bold %s]Sweeping 24 Archetype Tiers across 3 Base Classes (72 Matchups x 100 Trials)...[/]" Theme.Cyan, maxValue = 24.0)

        for (clsIndex, cls) in List.indexed championClasses do
          for (tierIndex, tier) in List.indexed tiers do
            let champFactory () = TierFactory.createClassTier cls tier
            let cellSeed (mobIndex: int) = 1000 + clsIndex * 400 + tierIndex * 100 + mobIndex * 10
            let warriorTP = findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Warrior Novice) maxMobCount iterations (cellRoller (cellSeed 0))
            let rogueTP = findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Rogue Novice) maxMobCount iterations (cellRoller (cellSeed 1))
            let mageTP = findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Mage Novice) maxMobCount iterations (cellRoller (cellSeed 3))

            let displayName =
              match cls with
              | _ -> cls.Name

            let tierColor =
              match tier with
              | Novice -> Theme.Comment
              | Veteran -> Theme.Cyan
              | Master -> Theme.Yellow
              | GrandMaster -> Theme.Purple

            let formatTP (tp: TippingPointResult) (color: string) =
              match tp with
              | Impenetrable cap -> sprintf "[bold %s]%d+ (Impenetrable)[/]" Theme.Green cap
              | OverrunBy n when n >= 20 -> sprintf "[bold %s]%d[/]" Theme.Cyan n
              | OverrunBy n when n >= 10 -> sprintf "[bold %s]%d[/]" Theme.Yellow n
              | OverrunBy n -> sprintf "[%s]%d[/]" color n

            let notes =
              match cls, tier with
              | CharacterClass.Fighter, GrandMaster -> "Fluid stances keep every vector available; BastionZoneControl holds the frontline to 3 while Prowess disparity drives massed AoO counters."
              | CharacterClass.Fighter, _ -> "Bastion frontage and stance-shifted parries resist early encirclement; compounding flank penalties still erode it in prolonged swarms."
              | CharacterClass.Mage, GrandMaster -> "Threaded Complex Forms: Resonance spikes burn, Phantasmal Diffusion sheds decoys, Aegis lattices ward and reflect the swarm."
              | CharacterClass.Mage, _ -> "Complex Forms disrupt and shed attackers; an unspecialised form is still punished by physical swarms."
              | _ -> "Standard archetype profile."

            matrixTable.AddRow(
              Markup(sprintf "[bold %s]%s[/]" Theme.Foreground displayName),
              Markup(sprintf "[bold %s]%A[/]" tierColor tier),
              Markup(formatTP warriorTP Theme.Red),
              Markup(formatTP rogueTP Theme.Green),
              Markup(formatTP mageTP Theme.Pink),
              Markup(sprintf "[%s]%s[/]" Theme.Comment notes)
            ) |> ignore

            task.Increment(1.0)
      )

    AnsiConsole.WriteLine()
    AnsiConsole.Write(matrixTable)
    AnsiConsole.WriteLine()

  /// Renders a cross-tier 1v1 peer balance matrix for the 6 canonical character classes.
  /// Runs 100 iterations per pairwise matchup with alternating opening strike (50 A-first, 50 B-first).
  let renderPeerBalanceMatrix () =
    AnsiConsole.WriteLine()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]FORNACH PEER CLASS 1v1 BALANCE MATRIX (100 RUNS, ALTERNATING INITIATIVE)[/]" Theme.Purple)
        .Centered()
        .RuleStyle(Theme.StyleCurrentLine)
    )
    AnsiConsole.WriteLine()

    let classes = CharacterClass.PlayerClasses

    let tiers = [
      CombatTier.Novice
      CombatTier.Veteran
      CombatTier.Master
      CombatTier.GrandMaster
    ]

    let iterations = 100
    let rng = Random(42)
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)

    // Precalculate all 15 pairwise matchups for each of the 4 tiers (60 matchups total)
    let matchupResults = System.Collections.Generic.Dictionary<(CombatTier * CharacterClass * CharacterClass), SimulationSummary>()

    AnsiConsole.Progress()
      .AutoClear(false)
      .Columns([|
        TaskDescriptionColumn() :> ProgressColumn
        ProgressBarColumn() :> ProgressColumn
        PercentageColumn() :> ProgressColumn
        RemainingTimeColumn() :> ProgressColumn
      |])
      .Start(fun ctx ->
        let task = ctx.AddTask(sprintf "[bold %s]Simulating 60 Peer Matchups (6,000 Duels across 4 Tiers)...[/]" Theme.Cyan, maxValue = 60.0)

        for tier in tiers do
          for i in 0 .. classes.Length - 2 do
            for j in i + 1 .. classes.Length - 1 do
              let clsA = classes.[i]
              let clsB = classes.[j]
              // Only the main character compounds the offense/guard ramps, so the matrix puts the player
              // on side A and plain opposition on side B. Both sides used to be player-style, which let a
              // class comparison measure stack mechanics only the protagonist is meant to have -- and made
              // the matrix hypersensitive to guard tuning, since a long duel banked guard on both sides
              // and stalled. `runHeadlessBatch` alternates turn order, so A is not simply the first mover.
              let factoryA () = { TierFactory.createClassTier clsA tier with PlayerStyle = true }
              let factoryB () = TierFactory.createClassTier clsB tier
              let summary = runHeadlessBatch factoryA factoryB iterations roller
              matchupResults.[(tier, clsA, clsB)] <- summary
              task.Increment(1.0)
      )

    let getPairResult (t: CombatTier) (a: CharacterClass) (b: CharacterClass) =
      if matchupResults.ContainsKey((t, a, b)) then
        let s = matchupResults.[(t, a, b)]
        s.WinsA, s.AvgRounds
      elif matchupResults.ContainsKey((t, b, a)) then
        let s = matchupResults.[(t, b, a)]
        s.WinsB, s.AvgRounds
      else
        0, 0.0

    let matrixTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Tier[/]" Theme.Cyan).NoWrap()) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Character[/]" Theme.Foreground).NoWrap()) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Fighter[/]" Theme.Red).Centered().Padding(0, 0, 0, 0)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Mage[/]" Theme.Purple).Centered().Padding(0, 0, 0, 0)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Net Win%%[/]" Theme.Foreground).Centered().NoWrap().Padding(0, 0, 0, 0)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Avg TTK[/]" Theme.Comment).Centered().NoWrap().Padding(0, 0, 0, 0)) |> ignore

    let formatWinRate (wins: int) (total: int) =
      let pct = (float wins / float total) * 100.0
      let color =
        if pct >= 65.0 then Theme.Green
        elif pct >= 55.0 then Theme.Cyan
        elif pct >= 45.0 then Theme.Yellow
        elif pct >= 35.0 then Theme.Orange
        else Theme.Red
      sprintf "[bold %s]%.0f%%[/]" color pct

    for tierIdx in 0 .. tiers.Length - 1 do
      let tier = tiers.[tierIdx]
      let tierColor, tierName =
        match tier with
        | Novice -> Theme.Comment, "Novice"
        | Veteran -> Theme.Cyan, "Veteran"
        | Master -> Theme.Yellow, "Master"
        | GrandMaster -> Theme.Purple, "GrMaster"

      for clsA in classes do
        let mutable totalWins = 0
        let mutable totalMatches = 0
        let mutable roundSums = 0.0
        let mutable roundCount = 0

        let matchCells =
          classes
          |> List.map (fun clsB ->
            if clsA = clsB then
              sprintf "[%s]──[/]" Theme.Comment
            else
              let winsA, rounds = getPairResult tier clsA clsB
              totalWins <- totalWins + winsA
              totalMatches <- totalMatches + iterations
              roundSums <- roundSums + rounds
              roundCount <- roundCount + 1
              formatWinRate winsA iterations
          )

        let overallPct = if totalMatches > 0 then (float totalWins / float totalMatches) * 100.0 else 0.0
        let overallColor =
          if overallPct >= 65.0 then Theme.Green
          elif overallPct >= 55.0 then Theme.Cyan
          elif overallPct >= 45.0 then Theme.Yellow
          elif overallPct >= 35.0 then Theme.Orange
          else Theme.Red

        let avgRounds = if roundCount > 0 then roundSums / float roundCount else 0.0

        let rowStrings : string array =
          [|
            sprintf "[bold %s]%s[/]" tierColor tierName
            sprintf "[bold %s]%s[/]" Theme.Foreground clsA.Name
            yield! matchCells
            sprintf "[bold %s]%.1f%%[/]" overallColor overallPct
            sprintf "[%s]%.1fr[/]" Theme.Comment avgRounds
          |]

        matrixTable.AddRow(rowStrings) |> ignore

      if tierIdx < tiers.Length - 1 then
        matrixTable.AddEmptyRow() |> ignore

    let legendGrid = Grid()
    legendGrid.AddColumn(GridColumn()) |> ignore
    legendGrid.AddColumn(GridColumn()) |> ignore
    legendGrid.AddRow(
      Markup(sprintf "[bold %s]Methodology:[/] 100 runs per pairwise duel (50 runs A-first, 50 runs B-first) to eliminate initiative bias." Theme.Cyan),
      Markup(sprintf "[bold %s]Scale:[/] [bold %s]>=65%%[/] Dominant  [bold %s]55-64%%[/] Favorable  [bold %s]45-54%%[/] Parity  [bold %s]35-44%%[/] Disadvantage  [bold %s]<35%%[/] Vulnerable" Theme.Yellow Theme.Green Theme.Cyan Theme.Yellow Theme.Orange Theme.Red)
    ) |> ignore
    legendGrid.AddRow(
      Markup(sprintf "[bold %s]Trauma Vectors:[/] Power ➔ Exhaustion (Fort/Res)  •  Agility ➔ Confusion (Ref/Int)  •  Discipline ➔ Frustration (Poise/Comp)" Theme.Comment),
      Markup(sprintf "[bold %s]Stat Scaling:[/] Canonical 1.0 : 0.75 : 0.75 attribute ratio parity across all 6 classes." Theme.Comment)
    ) |> ignore

    let legendPanel =
      Panel(legendGrid)
        .Border(BoxBorder.Rounded)
        .BorderStyle(Theme.StyleCurrentLine)
        .Header(Display.stripNerdGlyphs (sprintf "[bold %s] 󰒋 PEER MATRIX BENCHMARK TELEMETRY & TRAUMA VECTOR KEY [/]" Theme.Yellow))

    AnsiConsole.WriteLine()
    AnsiConsole.Write(matrixTable)
    AnsiConsole.WriteLine()
    AnsiConsole.Write(legendPanel)
    AnsiConsole.WriteLine()

  /// Evaluates and renders the Bestiary Monster Swarm Tipping Point matrix for the two player classes at
  /// the apex tier and at the Legend tier (a level-1000 champion). The --tier argument is accepted for
  /// compatibility but no longer narrows the report: the frontier is only meaningful at the top.
  let renderBestiarySwarmMatrix (_selectedTierOpt: CombatTier option) =
    let tiersToRender = [ "GrandMaster", 200; "Legend", 1000 ]

    let classes = CharacterClass.PlayerClasses

    let maxMobCount = 25
    let iterations = 10
    let rng = Random(42)
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
    let totalMatchups = float (tiersToRender.Length * Bestiary.allMonsters.Length * classes.Length)

    let allResults = ResizeArray<MonsterSwarmResultRow>()

    AnsiConsole.Progress()
      .AutoClear(false)
      .Columns([|
        TaskDescriptionColumn() :> ProgressColumn
        ProgressBarColumn() :> ProgressColumn
        PercentageColumn() :> ProgressColumn
        RemainingTimeColumn() :> ProgressColumn
      |])
      .Start(fun ctx ->
        let task = ctx.AddTask(sprintf "[bold %s]Simulating Bestiary Monster Swarms vs Fighter & Mage across Tiers...[/]" Theme.Red, maxValue = totalMatchups)
        let gate = obj()
        let increment () = lock gate (fun () -> task.Increment 1.0)

        for _, championLevel in tiersToRender do
          let tierRows = runBestiarySwarmBatch championLevel maxMobCount iterations roller increment
          allResults.AddRange tierRows
      )

    let formatTP (tp: TippingPointResult) =
      match tp with
      | Impenetrable cap -> sprintf "[bold %s]%d+ (Impenetrable)[/]" Theme.Green cap
      | OverrunBy 1 -> sprintf "[bold %s]1 (Lethal 1v1)[/]" Theme.Red
      | OverrunBy n when n <= 3 -> sprintf "[bold %s]%d (Deadly Pack)[/]" Theme.Orange n
      | OverrunBy n when n <= 8 -> sprintf "[bold %s]%d[/]" Theme.Yellow n
      | OverrunBy n -> sprintf "[bold %s]%d[/]" Theme.Cyan n

    for tierLabel, championLevel in tiersToRender do
      let tierColor = if championLevel >= 1000 then Theme.Yellow else Theme.Purple

      AnsiConsole.WriteLine()
      AnsiConsole.Write(
        Rule(sprintf "[bold %s]FORNACH BESTIARY SWARM BENCHMARK: %s TIER (Level %d Champions)[/]" tierColor tierLabel championLevel)
          .Centered()
          .RuleStyle(Theme.StyleCurrentLine)
      )
      AnsiConsole.WriteLine()

      let table = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
      table.AddColumn(TableColumn(sprintf "[bold %s]Monster Adversary[/]" Theme.Foreground)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]Biome[/]" Theme.Cyan)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]Role / Family[/]" Theme.Comment)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]Monster Lv & Tier[/]" Theme.Yellow)) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]vs. Fighter[/]" Theme.Red).Centered()) |> ignore
      table.AddColumn(TableColumn(sprintf "[bold %s]vs. Mage[/]" Theme.Purple).Centered()) |> ignore

      let tierRows = allResults |> Seq.filter (fun r -> r.ChampionLevel = championLevel) |> Seq.toList
      for r in tierRows do
        let m = r.Monster
        let monsterGlyphAndName = sprintf "[bold %s]%c[/] [bold %s]%s[/]" m.ColorHex m.Glyph Theme.Foreground m.Name
        let biomeName = m.NativeBiome.Name
        let roleAndFamily = sprintf "%A %A" m.Family m.Role
        let monsterTierStr = sprintf "Lv %d (%A)" m.Level m.Tier

        let getTP cls =
          match Map.tryFind cls r.TippingPoints with
          | Some tp -> formatTP tp
          | None -> "-"

        table.AddRow(
          Markup(monsterGlyphAndName),
          Markup(sprintf "[%s]%s[/]" Theme.Cyan biomeName),
          Markup(sprintf "[%s]%s[/]" Theme.Comment roleAndFamily),
          Markup(sprintf "[%s]%s[/]" Theme.Yellow monsterTierStr),
          Markup(getTP CharacterClass.Fighter),
          Markup(getTP CharacterClass.Mage)
        ) |> ignore

      AnsiConsole.Write(table)
      AnsiConsole.WriteLine()

    let legendGrid = Grid()
    legendGrid.AddColumn(GridColumn()) |> ignore
    legendGrid.AddColumn(GridColumn()) |> ignore
    legendGrid.AddRow(
      Markup(sprintf "[bold %s]Metric (N*):[/] Average monster mob count required to achieve >= 50%% win rate against Champion." Theme.Cyan),
      Markup(sprintf "[bold %s]Scale:[/] [bold %s]25+[/] Impenetrable  [bold %s]9-24[/] Swarm  [bold %s]4-8[/] Pack  [bold %s]2-3[/] Deadly  [bold %s]1[/] Lethal 1v1" Theme.Yellow Theme.Green Theme.Cyan Theme.Yellow Theme.Orange Theme.Red)
    ) |> ignore
    legendGrid.AddRow(
      Markup(sprintf "[bold %s]Swarm Mechanics:[/] Encirclement Flank Penalty (-(N-1) Defense Hits), Cleave splashes, Bastion frontline cap (3), Mirror decoys." Theme.Comment),
      Markup(sprintf "[bold %s]Scope:[/] All 21 Bestiary monsters across all 7 Tower biomes pitted against all 6 canonical classes." Theme.Comment)
    ) |> ignore

    let legendPanel =
      Panel(legendGrid)
        .Border(BoxBorder.Rounded)
        .BorderStyle(Theme.StyleCurrentLine)
        .Header(Display.stripNerdGlyphs (sprintf "[bold %s] 󰞁 BESTIARY SWARM BENCHMARK TELEMETRY & TIPPING POINT KEY [/]" Theme.Yellow))

    AnsiConsole.Write(legendPanel)
    AnsiConsole.WriteLine()


  // ---------------------------------------------------------------------------
  // Progression simulation: does the player outscale the tower?
  // ---------------------------------------------------------------------------

  let renderProgressionDashboard (report: ProgressionSimReport) =
    let table = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)

    for header in [ "Floor"; "Reached"; "Cleared"; "Clear %"; "Avg Level"; "Avg HP %"; "Avg Morale %" ] do
      table.AddColumn(TableColumn(sprintf "[bold %s]%s[/]" Theme.Foreground header)) |> ignore

    for f in report.Floors do
      let clearPct = if f.Reached = 0 then 0.0 else float f.Wins / float f.Reached * 100.0

      table.AddRow(
        string f.Floor,
        string f.Reached,
        sprintf "[%s]%d[/]" Theme.Green f.Wins,
        sprintf "%.0f%%" clearPct,
        sprintf "%.1f" f.AvgLevel,
        sprintf "[%s]%.0f%%[/]" Theme.Red f.AvgHpPct,
        sprintf "[%s]%.0f%%[/]" Theme.Cyan f.AvgMoralePct
      )
      |> ignore

    AnsiConsole.Write(table)
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(
      sprintf "[bold]Runs:[/] %d   [bold]Cleared all %d floors:[/] %d (%.0f%%)   [bold]Avg floors reached:[/] %.2f"
        report.Runs report.MaxFloors report.RunsWon
        (if report.Runs = 0 then 0.0 else float report.RunsWon / float report.Runs * 100.0)
        report.AvgFloorsReached
    )

    // Say the assumptions out loud: these numbers are a pessimistic floor, not a typical playthrough.
    AnsiConsole.MarkupLine(
      sprintf
        "[%s]Pessimistic model: whole pack (up to 4) fights at once, no rest, honing, consumables or gear; enemies sit up to 5 levels above the player.[/]"
        Theme.Comment
    )
