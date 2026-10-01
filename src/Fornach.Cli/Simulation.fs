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

  /// Fixed base seed: a given matchup must replay identically across runs.
  /// A balance workbench that drifts between invocations is not a workbench.
  let private batchBaseSeed = 20240517

  /// Fans a pure per-iteration body across the thread pool.
  ///
  /// Each iteration draws its own `Random` from a serially-generated seed list, so
  /// results are race-free AND scheduling-independent: iteration `i` rolls the same
  /// numbers no matter which thread picked it up. A single shared `Random` cannot be
  /// parallelised at all — `System.Random` is not thread-safe, and concurrent `Next`
  /// calls corrupt its internal seed state.
  let private runParallelBatch
    (baseSeed: int)
    (iterations: int)
    (onProgress: unit -> unit)
    (body: DiceRoller -> 'r)
    : 'r[] =
    let seedSource = Random(baseSeed)
    let seeds = Array.init iterations (fun _ -> seedSource.Next())

    Array.Parallel.init iterations (fun i ->
      let rng = Random(seeds[i])
      let result = body (fun min max -> rng.Next(min, max + 1))
      onProgress ()
      result)

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

  /// Headless batch simulation of 1 vs 1 duel combat without console progress bars.
  /// Alternates turn order across iterations to eliminate first-mover bias.
  let runHeadlessBatch
    (factoryA: unit -> Combatant)
    (factoryB: unit -> Combatant)
    (iterations: int)
    (roller: DiceRoller)
    : SimulationSummary =
    let maxRoundsPerMatch = 60
    let sampleA = factoryA()
    let sampleB = factoryB()

    let results =
      List.init iterations (fun i ->
        if i % 2 = 0 then
          runSingleMatch roller factoryA factoryB maxRoundsPerMatch
        else
          let revRes = runSingleMatch roller factoryB factoryA maxRoundsPerMatch
          { revRes with
              WinnerName = revRes.WinnerName
              WinnerIsA = revRes.WinnerIsA |> Option.map not })

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

    { ArchetypeNameA = sampleA.Name
      ArchetypeNameB = sampleB.Name
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

  let runSingleGroupMatch
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

        // Enraged Berserker Frenzy Attack: Berserk Tincture unleashes an immediate savage follow-up swing!
        if solo.HasActivePreparation PreparationType.BerserkTincture && not mob.IsEmpty && not solo.Health.IsDepleted && not solo.Morale.IsDepleted then
          let frenzyTarget = AI.chooseGroupTarget solo mob
          let frenzyAdjacent = mob |> List.filter (fun m -> m.Id <> frenzyTarget.Id)
          let frenzyIntent = StandardAttack (ForceStrike false)
          let frenzyRes = ActionResolver.resolveGroupTurn roller frenzyIntent solo frenzyTarget frenzyAdjacent
          solo <- frenzyRes.Actor

          let frenzyCleaves = frenzyRes.Events |> List.filter (function CombatEvent.CleaveExecuted _ -> true | _ -> false) |> List.length
          totalCleaves <- totalCleaves + frenzyCleaves

          let frenzyExecutedIds =
            frenzyRes.Events
            |> List.choose (function CombatEvent.Executed (_, tid, _) -> Some tid | _ -> None)
            |> Set.ofList

          let allFrenzyResolved = frenzyRes.PrimaryTarget :: frenzyRes.SecondaryTargets

          mob <-
            allFrenzyResolved
            |> List.map (fun m ->
              let up, _ = ActionResolver.applyTurnUpkeep m
              up)
            |> List.filter (fun m ->
              not (Set.contains m.Id frenzyExecutedIds)
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
        // When BastionZoneControl is active, only the 3 frontline tiles in front of the Warden can swing this round.
        // For Mesmer, at most 8 enemies can attack per round (the 8 surrounding tiles).
        // Each clone that vanishes/shatters blocks that tile space until the end of the round!
        let initialMaxSwings =
          if solo.HasActivePreparation PreparationType.BastionZoneControl then 3
          elif solo.Class = CharacterClass.Mesmer then 8
          else mob.Length

        let mutable availableSwings = initialMaxSwings
        let mutable priorDefenses = 0
        let mutable mobIdx = 0
        let mutable swingsCount = 0

        while mobIdx < mob.Length && swingsCount < availableSwings && winnerIsSolo.IsNone do
          let attacker = mob.[mobIdx]
          let intentMob = AI.chooseIntent attacker solo
          let mobRes = ActionResolver.resolveEx roller intentMob attacker solo priorDefenses
          let newAttacker = mobRes.Actor
          solo <- mobRes.Target
          swingsCount <- swingsCount + 1

          // If a mirror clone was shattered/deceived, that tile is blocked by the dissipating phantasm for the rest of the round!
          let cloneShattered =
            mobRes.Events
            |> List.exists (function CombatEvent.MirrorCloneShattered _ | CombatEvent.MirrorMirageDeceived _ -> true | _ -> false)
          if cloneShattered && solo.Class = CharacterClass.Mesmer then
            availableSwings <- Math.Max(0, availableSwings - 1)

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

          // Handle if attacker died to an Attack of Opportunity, caltrops, reactive retribution, or collapsed from Frustration/meters
          let attackerDefeated = newAttacker.Health.IsDepleted || newAttacker.Morale.IsDepleted || CollapseState.isCollapsed newAttacker.Collapse
          if attackerDefeated then
            if solo.Class = CharacterClass.Warden || solo.Class = CharacterClass.Justicar then
              // Reading the School: Neutralizing a student of this martial school yields persistent Study Stacks
              solo <- solo |> Combatant.addStudyStacks 1
            mob <- mob |> List.filter (fun m -> m.Id <> newAttacker.Id)
          else
            mob <- mob |> List.mapi (fun i m -> if i = mobIdx then newAttacker else m)
            priorDefenses <- priorDefenses + 1
            mobIdx <- mobIdx + 1

        // Phase 3: Solo Upkeep at end of round
        if winnerIsSolo.IsNone then
          let swarmExertion = if mob.Length >= 3 then 1 else 0
          solo <-
            if swarmExertion > 0 then
              solo |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + swarmExertion })
            else
              solo
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


  /// Headless batch simulation of 1 vs N swarm combat without console progress bars
  let runHeadlessGroupBatch
    (soloFactory: unit -> Combatant)
    (mobFactory: unit -> Combatant)
    (mobCount: int)
    (iterations: int)
    (roller: DiceRoller)
    : GroupSingleResult list =
    let maxRounds = Math.Max(60, mobCount + 20)
    List.init iterations (fun _ ->
      runSingleGroupMatch roller soloFactory mobFactory mobCount maxRounds)

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
            (fun roller -> runSingleGroupMatch roller soloArch.Factory mobArch.Factory mobCount maxRoundsPerMatch)
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


  type TippingPointResult =
    | OverrunBy of int
    | Impenetrable of int
    with
      member this.Value =
        match this with
        | OverrunBy n -> n
        | Impenetrable cap -> cap

      override this.ToString() =
        match this with
        | OverrunBy n -> sprintf "%d" n
        | Impenetrable cap -> sprintf "%d+" cap

  let evaluateMobWinRate
    (championFactory: unit -> Combatant)
    (mobFactory: unit -> Combatant)
    (mobCount: int)
    (iterations: int)
    (roller: DiceRoller) : float =
    let results = runHeadlessGroupBatch championFactory mobFactory mobCount iterations roller
    let mobWins = results |> List.filter (fun r -> not r.SoloWon && r.Condition <> Stalemate) |> List.length
    float mobWins / float results.Length

  let findSwarmTippingPoint
    (championFactory: unit -> Combatant)
    (mobFactory: unit -> Combatant)
    (maxMobCount: int)
    (iterationsPerProbe: int)
    (roller: DiceRoller) : TippingPointResult =

    let rate1 = evaluateMobWinRate championFactory mobFactory 1 iterationsPerProbe roller
    if rate1 >= 0.50 then
      OverrunBy 1
    else
      let brackets = [ 2; 3; 5; 8; 12; 16; 24; 32; 48; 64; 80; maxMobCount ] |> List.distinct |> List.sort
      let rec searchBracket prevN remaining =
        match remaining with
        | [] -> None
        | n :: rest ->
          let rate = evaluateMobWinRate championFactory mobFactory n iterationsPerProbe roller
          if rate >= 0.50 then
            Some (prevN, n)
          else
            searchBracket n rest

      match searchBracket 1 brackets with
      | None -> Impenetrable maxMobCount
      | Some (low, high) ->
        let rec binarySearch l h =
          if l >= h - 1 then
            h
          else
            let mid = (l + h) / 2
            let rate = evaluateMobWinRate championFactory mobFactory mid iterationsPerProbe roller
            if rate >= 0.50 then
              binarySearch l mid
            else
              binarySearch mid h
        OverrunBy (binarySearch low high)

  let renderBalanceMatrix () =
    AnsiConsole.WriteLine()
    AnsiConsole.Write(
      Rule(sprintf "[bold %s]FORNACH ARCHETYPE BALANCE BENCHMARK: 128 MATCHUP MATRIX[/]" Theme.Purple)
        .Centered()
        .RuleStyle(Theme.StyleCurrentLine)
    )
    AnsiConsole.WriteLine()

    let championClasses = [
      CharacterClass.Berserker
      CharacterClass.Duelist
      CharacterClass.Warden
      CharacterClass.Inquisitor
      CharacterClass.Mesmer
      CharacterClass.Abjurer
    ]

    let tiers = [
      CombatTier.Novice
      CombatTier.Veteran
      CombatTier.Master
      CombatTier.GrandMaster
    ]

    let rng = Random(42)
    let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
    let maxMobCount = 100
    let iterations = 10

    let matrixTable = Table().Border(TableBorder.Rounded).BorderColor(Theme.ColorCurrentLine)
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Champion Archetype[/]" Theme.Foreground)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]Tier[/]" Theme.Cyan)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Warrior (Power)[/]" Theme.Red)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Rogue (Finesse)[/]" Theme.Green)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Soldier (Discipline)[/]" Theme.Yellow)) |> ignore
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
        let task = ctx.AddTask(sprintf "[bold %s]Sweeping 24 Archetype Tiers across 4 Base Classes (96 Matchups)...[/]" Theme.Cyan, maxValue = 24.0)

        for cls in championClasses do
          for tier in tiers do
            let champFactory () = TierFactory.createClassTier cls tier
            let warriorTP = findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Warrior Novice) maxMobCount iterations roller
            let rogueTP = findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Rogue Novice) maxMobCount iterations roller
            let soldierTP = findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Soldier Novice) maxMobCount iterations roller
            let mageTP = findSwarmTippingPoint champFactory (fun () -> TierFactory.createClassTier CharacterClass.Mage Novice) maxMobCount iterations roller

            let displayName =
              match cls with
              | CharacterClass.Warden -> "Warden"
              | CharacterClass.Justicar -> "Warden (Justicar)"
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
              | CharacterClass.Warden, GrandMaster
              | CharacterClass.Justicar, GrandMaster -> "BastionZoneControl limits frontline to 3; Prowess disparity triggers massive AoOs; resilient against swarms."
              | CharacterClass.Warden, Master
              | CharacterClass.Justicar, Master -> "BastionZoneControl limits frontline to 3; exceptional defense soak against physical hordes."
              | CharacterClass.Warden, _
              | CharacterClass.Justicar, _ -> "Discipline posture & bastion geometry resist early encirclement penalties."
              | CharacterClass.Berserker, GrandMaster -> "Berserk Tincture deadens 35% physical damage & unleashes Frenzy bonus swings; cleaves up to 5 adjacent foes."
              | CharacterClass.Berserker, _ -> "Brute kinetic Force & high HP pool; vulnerable to compounding flank penalties over prolonged duels."
              | CharacterClass.Duelist, GrandMaster -> "Caltrop Pouch strips flank penalties for 2 turns; Agility disparity triggers lethal AoO counters."
              | CharacterClass.Duelist, _ -> "High Finesse & Reflex dodge initial attacks; overwhelmed once caltrops expire against large mobs."
              | CharacterClass.Inquisitor, GrandMaster -> "Dread Warhorn inflicts +25 Cognitive Fatigue on all attackers; devastates Mage morale & triggers mental routs."
              | CharacterClass.Inquisitor, _ -> "Formidable mental dominance; vulnerable if physical brute force bypasses lower physical armor."
              | CharacterClass.Mesmer, GrandMaster -> "Mirror Mirage forces flankers to attack decoys; Prismatic Flare punishes enemy recklessness."
              | CharacterClass.Mesmer, _ -> "Deceptive sensory phantasms disrupt attackers; susceptible to dogpiling once illusions exhaust."
              | CharacterClass.Abjurer, GrandMaster
              | CharacterClass.Strategist, GrandMaster -> "Aegis of Retribution reduces damage by 35% and reflects 50% back; destabilizing ground wards trip flankers with heavy Frustration."
              | CharacterClass.Abjurer, _
              | CharacterClass.Strategist, _ -> "Runic composure wards & destabilizing ground glyphs disrupt oncoming attackers through calculated attrition."
              | _ -> "Standard archetype profile."

            matrixTable.AddRow(
              Markup(sprintf "[bold %s]%s[/]" Theme.Foreground displayName),
              Markup(sprintf "[bold %s]%A[/]" tierColor tier),
              Markup(formatTP warriorTP Theme.Red),
              Markup(formatTP rogueTP Theme.Green),
              Markup(formatTP soldierTP Theme.Yellow),
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

    let classes = [
      CharacterClass.Berserker
      CharacterClass.Duelist
      CharacterClass.Warden
      CharacterClass.Inquisitor
      CharacterClass.Mesmer
      CharacterClass.Abjurer
    ]

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
              let factoryA () = TierFactory.createClassTier clsA tier
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
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Berserker[/]" Theme.Red).Centered().Padding(0, 0, 0, 0)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Duelist[/]" Theme.Green).Centered().Padding(0, 0, 0, 0)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Warden[/]" Theme.Yellow).Centered().Padding(0, 0, 0, 0)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Inquisitor[/]" Theme.Pink).Centered().Padding(0, 0, 0, 0)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Mesmer[/]" Theme.Cyan).Centered().Padding(0, 0, 0, 0)) |> ignore
    matrixTable.AddColumn(TableColumn(sprintf "[bold %s]vs. Abjurer[/]" Theme.Purple).Centered().Padding(0, 0, 0, 0)) |> ignore
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
        .Header(sprintf "[bold %s] 󰒋 PEER MATRIX BENCHMARK TELEMETRY & TRAUMA VECTOR KEY [/]" Theme.Yellow)

    AnsiConsole.WriteLine()
    AnsiConsole.Write(matrixTable)
    AnsiConsole.WriteLine()
    AnsiConsole.Write(legendPanel)
    AnsiConsole.WriteLine()
