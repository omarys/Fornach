namespace Fornach.Engine

open System
open Fornach.Domain

/// Where the balance harness measures: fights played with the real resolver, the real scaling and the
/// real growth, driven without a console. Nothing here draws anything -- the CLI renders what these
/// return, and a different setting can run the same measurements against its own policy table.
type WinCondition =
  | HealthDepleted
  | MoraleDepleted
  | ExecutionFinisher of Plane
  | CollapseDecisive of CollapseReason
  | Stalemate

type SingleCombatResult =
  { WinnerName: string option
    WinnerId: CombatantId option
    WinnerIsA: bool option
    LoserName: string option
    Rounds: int
    Condition: WinCondition
    TotalWhiffs: int
    TotalCrits: int }

type SimulationSummary =
  { ArchetypeNameA: string
    ArchetypeNameB: string
    TotalIterations: int
    WinsA: int
    WinsB: int
    Stalemates: int
    AvgRounds: float
    MinRounds: int
    MaxRounds: int
    HealthDepletions: int
    MoraleDepletions: int
    Executions: int
    TotalWhiffs: int
    TotalCrits: int }

type GroupSingleResult =
  { SoloWon: bool
    Rounds: int
    OpponentsInitial: int
    OpponentsEliminated: int
    SoloRemainingHPPct: float
    SoloRemainingMoralePct: float
    SoloExhaustion: int
    SoloFrustration: int
    TotalCritsDealtByMob: int
    PeakEncirclementPenalty: int
    TotalAoOsTriggered: int
    TotalCleaves: int
    TotalChains: int
    TotalTempoShifts: int
    Condition: WinCondition }

type GroupSimulationSummary =
  { SoloArchetypeName: string
    MobArchetypeName: string
    MobCount: int
    TotalIterations: int
    SoloWins: int
    MobWins: int
    Stalemates: int
    SoloWinRate: float
    AvgRounds: float
    MinRounds: int
    MaxRounds: int
    AvgEliminations: float
    EliminationDistribution: (int * int * float) list // (kills, occurrences, percentage)
    AvgPeakPenalty: float
    AvgSoloRemainingHP: float
    AvgAoOsTriggered: float
    AvgCleaves: float
    AvgChains: float
    AvgTempoShifts: float
    HealthDepletions: int
    MoraleDepletions: int
    Executions: int }

/// One floor's contribution to the progression report.
type FloorSimStat =
  { Floor: int
    Reached: int
    Wins: int
    AvgLevel: float
    AvgHpPct: float
    AvgMoralePct: float }

/// Whole-run view: how deep runs get when every floor is fought with the real scaling and the real
/// growth applied. This is the only measurement that can answer "does the player outscale the tower".
type ProgressionSimReport =
  { Runs: int
    MaxFloors: int
    RunsWon: int
    AvgFloorsReached: float
    Floors: FloorSimStat list }

/// How the solo side chooses its moves. Every existing sweep and the interactive tower use `aiPolicy`;
/// a balance harness injects a stronger policy here rather than forking the fight loop.
type GroupPolicy =
  { ChooseTarget: Combatant -> Combatant list -> Combatant
    ChooseIntent: Combatant -> Combatant -> int -> ActionIntent
    ChooseDefense: Combatant -> Combatant -> DefenseClassification }

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

type MonsterSwarmResultRow = {
  Monster: MonsterTemplate
  /// Champion level this row was measured at (GrandMaster = 200, Legend = 1000).
  ChampionLevel: int
  TippingPoints: Map<CharacterClass, TippingPointResult>
}

module CombatSimulation =

  /// Fixed base seed: a given matchup must replay identically across runs.
  /// A balance workbench that drifts between invocations is not a workbench.
  let batchBaseSeed = 20240517

  /// Fans a pure per-iteration body across the thread pool.
  ///
  /// Each iteration draws its own `Random` from a serially-generated seed list, so
  /// results are race-free AND scheduling-independent: iteration `i` rolls the same
  /// numbers no matter which thread picked it up. A single shared `Random` cannot be
  /// parallelised at all — `System.Random` is not thread-safe, and concurrent `Next`
  /// calls corrupt its internal seed state.
  let runParallelBatch
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

  let runSingleMatch (policies: AI.PolicyTable) (roller: DiceRoller) (factoryA: unit -> Combatant) (factoryB: unit -> Combatant) (maxRounds: int) : SingleCombatResult =
    let mutable combA = factoryA () |> Combatant.seizeVor
    let mutable combB = factoryB () |> Combatant.yieldVor
    let mutable round = 1
    let mutable winner : string option = None
    let mutable winnerId : CombatantId option = None
    let mutable winnerIsA : bool option = None
    let mutable loser : string option = None
    let mutable condition = Stalemate
    let mutable whiffs = 0
    let mutable crits = 0

    let executeTurn (actorIsA: bool) =
      let actor, target = if actorIsA then combA, combB else combB, combA
      if actor.IsStaggered then
        let recovered = actor |> Combatant.clearStagger
        if actorIsA then combA <- recovered else combB <- recovered
      else
        let intent = AI.chooseIntentUsing policies actor target
        let defOpt =
          if target.IsHumanoid && target.IsInNach then
            Some(Guard(AI.chooseActiveDefense target actor))
          else None
        let res = ActionResolver.resolveExWithDefense roller intent defOpt actor target 0
        if actorIsA then
          combA <- res.Actor
          combB <- res.Target
        else
          combB <- res.Actor
          combA <- res.Target

        match res.Contest with
        | Some c ->
          if c.IsWhiff then whiffs <- whiffs + 1
          if c.IsCritical then crits <- crits + 1
        | None -> ()

        let hasExecutedTarget =
          res.Events
          |> List.exists (function CombatEvent.Executed _ -> true | _ -> false)

        let currentActor = if actorIsA then combA else combB
        let currentTarget = if actorIsA then combB else combA

        if hasExecutedTarget then
          let plane = match intent with ExecuteStrike p -> p | _ -> Physical
          winner <- Some currentActor.Name
          winnerId <- Some currentActor.Id
          winnerIsA <- Some actorIsA
          loser <- Some currentTarget.Name
          condition <- ExecutionFinisher plane
        elif currentTarget.IsExecuteEligible then
          let r = match currentTarget.Collapse with CollapseState.Collapsed r -> r | _ -> RecklessExposure
          winner <- Some currentActor.Name
          winnerId <- Some currentActor.Id
          winnerIsA <- Some actorIsA
          loser <- Some currentTarget.Name
          condition <- CollapseDecisive r
        elif currentActor.IsExecuteEligible then
          let r = match currentActor.Collapse with CollapseState.Collapsed r -> r | _ -> RecklessExposure
          winner <- Some currentTarget.Name
          winnerId <- Some currentTarget.Id
          winnerIsA <- Some (not actorIsA)
          loser <- Some currentActor.Name
          condition <- CollapseDecisive r
        elif currentActor.Health.IsDepleted then
          winner <- Some currentTarget.Name
          winnerId <- Some currentTarget.Id
          winnerIsA <- Some (not actorIsA)
          loser <- Some currentActor.Name
          condition <- HealthDepleted
        elif currentTarget.Health.IsDepleted then
          winner <- Some currentActor.Name
          winnerId <- Some currentActor.Id
          winnerIsA <- Some actorIsA
          loser <- Some currentTarget.Name
          condition <- HealthDepleted
        elif currentTarget.Morale.IsDepleted then
          winner <- Some currentActor.Name
          winnerId <- Some currentActor.Id
          winnerIsA <- Some actorIsA
          loser <- Some currentTarget.Name
          condition <- MoraleDepleted
        elif currentActor.Morale.IsDepleted then
          winner <- Some currentTarget.Name
          winnerId <- Some currentTarget.Id
          winnerIsA <- Some (not actorIsA)
          loser <- Some currentActor.Name
          condition <- MoraleDepleted

    let checkBeastFlee (beast: Combatant) (opponent: Combatant) (beastIsA: bool) =
      if beast.IsBeast
         && float beast.Health.Current / float beast.Health.Maximum < 0.30
         && float opponent.Health.Current / float opponent.Health.Maximum > 0.70 then
        let isMerciful = opponent.Meters.Recklessness.Value < 50
        let updatedOpponent =
          if isMerciful then
            opponent
            |> Combatant.updateMeters (fun m -> { m with Recklessness = Meter.Zero })
            |> (fun p -> { p with Morale = p.Morale.ApplyDelta 20 })
            |> Combatant.growFromEncounter [ beast ]
          else
            opponent
            |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 15 })
            |> Combatant.growFromEncounter [ beast ]

        if beastIsA then
          combB <- updatedOpponent
          winner <- Some combB.Name
          winnerId <- Some combB.Id
          winnerIsA <- Some false
          loser <- Some combA.Name
        else
          combA <- updatedOpponent
          winner <- Some combA.Name
          winnerId <- Some combA.Id
          winnerIsA <- Some true
          loser <- Some combB.Name
        condition <- HealthDepleted
        true
      else
        false

    while winner.IsNone && round <= maxRounds do
      if checkBeastFlee combA combB true || checkBeastFlee combB combA false then
        ()
      else
        // First turn of the round belongs to whichever combatant holds the Vor
        let aHasVor = combA.IsInVor || not combB.IsInVor
        executeTurn aHasVor
        if winner.IsNone && not (checkBeastFlee combA combB true || checkBeastFlee combB combA false) then
          executeTurn (not aHasVor)

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

  /// Headless batch simulation of 1 vs 1 duel combat without console progress bars.
  /// Alternates turn order across iterations to eliminate first-mover bias.
  let runHeadlessBatch
    (policies: AI.PolicyTable)
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
          runSingleMatch policies roller factoryA factoryB maxRoundsPerMatch
        else
          let revRes = runSingleMatch policies roller factoryB factoryA maxRoundsPerMatch
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

  let aiPolicy (policies: AI.PolicyTable) =
    { ChooseTarget = fun solo mob -> AI.chooseGroupTarget solo mob
      ChooseIntent = fun solo target swings -> AI.chooseIntentWithContextUsing policies solo target swings
      ChooseDefense = fun solo attacker -> AI.chooseActiveDefense solo attacker }

  /// One pack fight starting from combatants that already exist. Returns the result plus the
  /// surviving solo, so a caller can carry a player forward between floors (`runProgressionSim`).
  let runPreparedGroupMatchWith
    (policies: AI.PolicyTable)
    (policy: GroupPolicy)
    (roller: DiceRoller)
    (soloStart: Combatant)
    (mobsStart: Combatant list)
    (maxRounds: int)
    : GroupSingleResult * Combatant =
    let initialMobCount = mobsStart.Length
    let mutable solo = soloStart
    let mutable mob = mobsStart
    let mutable round = 1
    let mutable winnerIsSolo : bool option = None
    let mutable condition = Stalemate
    let mutable mobCrits = 0
    let mutable peakPenalty = 0
    let mutable totalAoOs = 0
    let mutable totalCleaves = 0
    let mutable totalChains = 0
    let mutable tempoShifts = 0

    while winnerIsSolo.IsNone && round <= maxRounds do
      // Who holds the Vor decides which side resolves first. A living pack member in the Vor while the
      // solo opens in the Nach means the pack strikes first. A contested or ambiguous state -- both
      // sides holding it, or neither -- deliberately falls back to solo-first so the harness stays
      // deterministic.
      let packHasVor =
        mob
        |> List.exists (fun m ->
          m.IsInVor
          && not m.Health.IsDepleted
          && not m.Morale.IsDepleted)

      // Phase 1: Solo Turn (Focus fire optimal target + Cleave / Chain adjacent opponents)
      let resolveSoloPhase () =
        if not mob.IsEmpty then
          let target = policy.ChooseTarget solo mob
          let adjacentTargets = mob |> List.filter (fun m -> m.Id <> target.Id)
          let intentSolo = policy.ChooseIntent solo target mob.Length
          let groupRes = ActionResolver.resolveGroupTurn roller intentSolo solo target adjacentTargets
          solo <- groupRes.Actor

          let cleaveHits = groupRes.Events |> List.filter (function CombatEvent.CleaveExecuted _ -> true | _ -> false) |> List.length
          let chainHits = groupRes.Events |> List.filter (function CombatEvent.StrikeChained _ -> true | _ -> false) |> List.length
          let tempoHits = groupRes.Events |> List.filter (function CombatEvent.TempoShifted (_, _, _) -> true | _ -> false) |> List.length
          totalCleaves <- totalCleaves + cleaveHits
          totalChains <- totalChains + chainHits
          tempoShifts <- tempoShifts + tempoHits

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
            let frenzyTarget = policy.ChooseTarget solo mob
            let frenzyAdjacent = mob |> List.filter (fun m -> m.Id <> frenzyTarget.Id)
            let frenzyIntent = StandardAttack (ForceStrike false)
            let frenzyRes = ActionResolver.resolveGroupTurn roller frenzyIntent solo frenzyTarget frenzyAdjacent
            solo <- frenzyRes.Actor

            let frenzyCleaves = frenzyRes.Events |> List.filter (function CombatEvent.CleaveExecuted _ -> true | _ -> false) |> List.length
            let frenzyTempoHits = frenzyRes.Events |> List.filter (function CombatEvent.TempoShifted (_, _, _) -> true | _ -> false) |> List.length
            totalCleaves <- totalCleaves + frenzyCleaves
            tempoShifts <- tempoShifts + frenzyTempoHits

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


      // Phase 2: Swarm Turn (All surviving mob members attack Solo concurrently)
      let resolveSwarmPhase () =
        // Poise sets the frontage: how many bodies can reach the solo at once, from eight down to one.
        // When BastionZoneControl is active, only the 3 frontline tiles in front of the Warden can swing.
        // Each clone that vanishes/shatters blocks that tile space until the end of the round!
        let initialMaxSwings =
          let frontage = Math.Min(solo.Frontage, mob.Length)
          if solo.HasActivePreparation PreparationType.BastionZoneControl then Math.Min(3, frontage)
          else frontage

        let mutable availableSwings = initialMaxSwings
        let mutable priorDefenses = 0
        let mutable mobIdx = 0
        let mutable swingsCount = 0

        while mobIdx < mob.Length && swingsCount < availableSwings && winnerIsSolo.IsNone do
          let attacker = mob.[mobIdx]
          let intentMob = AI.chooseIntentUsing policies attacker solo
          let defOpt = if priorDefenses = 0 && solo.IsHumanoid then Some(Guard(policy.ChooseDefense solo attacker)) else None
          let mobRes = ActionResolver.resolveExWithDefense roller intentMob defOpt attacker solo priorDefenses
          let newAttacker = mobRes.Actor
          solo <- mobRes.Target
          swingsCount <- swingsCount + 1

          // If a mirror clone was shattered/deceived, that tile is blocked by the dissipating phantasm for the rest of the round!
          let cloneShattered =
            mobRes.Events
            |> List.exists (function CombatEvent.MirrorCloneShattered _ | CombatEvent.MirrorMirageDeceived _ -> true | _ -> false)
          if cloneShattered then
            availableSwings <- Math.Max(0, availableSwings - 1)

          let aooHits = mobRes.Events |> List.filter (function CombatEvent.AttackOfOpportunityTriggered _ -> true | _ -> false) |> List.length
          let tempoHits = mobRes.Events |> List.filter (function CombatEvent.TempoShifted (_, _, _) -> true | _ -> false) |> List.length
          totalAoOs <- totalAoOs + aooHits
          tempoShifts <- tempoShifts + tempoHits

          match mobRes.Contest with
          | Some c ->
            if c.IsCritical then mobCrits <- mobCrits + 1
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
            if solo.Class = CharacterClass.Fighter then
              // Reading the School: neutralizing a student of this martial school feeds the ramp
              solo <- solo |> Combatant.addStacks 1
            mob <- mob |> List.filter (fun m -> m.Id <> newAttacker.Id)
          else
            mob <- mob |> List.mapi (fun i m -> if i = mobIdx then newAttacker else m)
            priorDefenses <- priorDefenses + 1
            peakPenalty <- Math.Max(peakPenalty, priorDefenses)
            mobIdx <- mobIdx + 1


      // Phase 3: Solo Upkeep at end of round
      let resolveSoloUpkeep () =
        if winnerIsSolo.IsNone then
          let swarmExertion = if mob.Length >= 3 then 1 else 0
          solo <-
            if swarmExertion > 0 then
              solo |> Combatant.updateMeters (fun m ->
                if solo.Plane = Mental then
                  { m with
                      Exhaustion = m.Exhaustion + swarmExertion
                      CognitiveFatigue = m.CognitiveFatigue + (if mob.Length >= 10 then 3 elif mob.Length >= 5 then 2 else 1) }
                else
                  { m with Exhaustion = m.Exhaustion + swarmExertion })
            else
              solo
          if solo.Health.IsDepleted then
            winnerIsSolo <- Some false
            condition <- HealthDepleted
          elif solo.Morale.IsDepleted then
            winnerIsSolo <- Some false
            condition <- MoraleDepleted


      // Win/loss adjudication, checked at the same points as before: a phase that clears the pack or
      // drops the solo ends the fight before the other side acts. A phase that already decided the fight
      // (execution finisher, collapse) keeps its own condition.
      let adjudicate () =
        if winnerIsSolo.IsNone then
          if mob.IsEmpty then
            winnerIsSolo <- Some true
            condition <- HealthDepleted
          elif solo.Health.IsDepleted then
            winnerIsSolo <- Some false
            condition <- HealthDepleted
          elif solo.Morale.IsDepleted then
            winnerIsSolo <- Some false
            condition <- MoraleDepleted

      if packHasVor && solo.IsInNach then
        resolveSwarmPhase ()
        adjudicate ()
        if winnerIsSolo.IsNone then
          resolveSoloPhase ()
          adjudicate ()
      else
        resolveSoloPhase ()
        adjudicate ()
        if winnerIsSolo.IsNone then resolveSwarmPhase ()

      resolveSoloUpkeep ()

      if winnerIsSolo.IsNone then
        round <- round + 1

    let elim = initialMobCount - mob.Length
    let soloHPPct = Math.Max(0.0, (float solo.Health.Current / float solo.Health.Maximum) * 100.0)
    let soloMoralePct = Math.Max(0.0, (float solo.Morale.Current / float solo.Morale.Maximum) * 100.0)

    { SoloWon = (winnerIsSolo = Some true)
      Rounds = Math.Min(round, maxRounds)
      OpponentsInitial = initialMobCount
      OpponentsEliminated = elim
      SoloRemainingHPPct = soloHPPct
      SoloRemainingMoralePct = soloMoralePct
      SoloExhaustion = solo.Meters.Exhaustion.Value
      SoloFrustration = solo.Meters.Frustration.Value
      TotalCritsDealtByMob = mobCrits
      PeakEncirclementPenalty = peakPenalty
      TotalAoOsTriggered = totalAoOs
      TotalCleaves = totalCleaves
      TotalChains = totalChains
      TotalTempoShifts = tempoShifts
      Condition = condition }, solo

  let runPreparedGroupMatch
    (policies: AI.PolicyTable)
    (roller: DiceRoller)
    (soloStart: Combatant)
    (mobsStart: Combatant list)
    (maxRounds: int)
    : GroupSingleResult * Combatant =
    runPreparedGroupMatchWith policies (aiPolicy policies) roller soloStart mobsStart maxRounds

  let runSingleGroupMatch
    (policies: AI.PolicyTable)
    (roller: DiceRoller)
    (soloFactory: unit -> Combatant)
    (mobFactory: unit -> Combatant)
    (mobCount: int)
    (maxRounds: int)
    : GroupSingleResult =
    runPreparedGroupMatch policies roller (soloFactory ()) (List.init mobCount (fun _ -> mobFactory ())) maxRounds
    |> fst

  /// Headless batch simulation of 1 vs N swarm combat without console progress bars
  let runHeadlessGroupBatch
    (policies: AI.PolicyTable)
    (soloFactory: unit -> Combatant)
    (mobFactory: unit -> Combatant)
    (mobCount: int)
    (iterations: int)
    (roller: DiceRoller)
    : GroupSingleResult list =
    let maxRounds = Math.Max(60, mobCount + 20)
    List.init iterations (fun _ ->
      runSingleGroupMatch policies roller soloFactory mobFactory mobCount maxRounds)

  let evaluateMobWinRate
    (policies: AI.PolicyTable)
    (championFactory: unit -> Combatant)
    (mobFactory: unit -> Combatant)
    (mobCount: int)
    (iterations: int)
    (roller: DiceRoller) : float =
    let results = runHeadlessGroupBatch policies championFactory mobFactory mobCount iterations roller
    let mobWins = results |> List.filter (fun r -> not r.SoloWon && r.Condition <> Stalemate) |> List.length
    float mobWins / float results.Length

  let findSwarmTippingPoint
    (policies: AI.PolicyTable)
    (championFactory: unit -> Combatant)
    (mobFactory: unit -> Combatant)
    (maxMobCount: int)
    (iterationsPerProbe: int)
    (roller: DiceRoller) : TippingPointResult =

    let rate1 = evaluateMobWinRate policies championFactory mobFactory 1 iterationsPerProbe roller
    if rate1 >= 0.50 then
      OverrunBy 1
    else
      let brackets = [ 2; 3; 5; 8; 12; 16; 24; 32; 48; 64; 80; maxMobCount ] |> List.filter (fun b -> b <= maxMobCount) |> List.distinct |> List.sort
      let rec searchBracket prevN remaining =
        match remaining with
        | [] -> None
        | n :: rest ->
          let rate = evaluateMobWinRate policies championFactory mobFactory n iterationsPerProbe roller
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
            let rate = evaluateMobWinRate policies championFactory mobFactory mid iterationsPerProbe roller
            if rate >= 0.50 then
              binarySearch l mid
            else
              binarySearch mid h
        OverrunBy (binarySearch low high)

  /// Runs a headless batch evaluating all 21 Bestiary monsters as swarms against the two player classes
  /// (Fighter and Mage) at a given champion level. The monsters are plain opposition: they auto-attack
  /// with a single basic strike and never shift stance, so the matrix measures the player kits against
  /// stat blocks rather than against monster kits.
  let runBestiarySwarmBatch
    (policies: AI.PolicyTable)
    (championLevel: int)
    (maxMobCount: int)
    (iterationsPerProbe: int)
    (roller: DiceRoller)
    (onProgress: unit -> unit)
    : MonsterSwarmResultRow list =
    let classes = CharacterClass.PlayerClasses
    Bestiary.allMonsters
    |> List.map (fun monster ->
      let tpMap =
        classes
        |> List.map (fun cls ->
          // The champion is a player build; the monster is auto-attack opposition. Clearing the monster
          // family keeps its species instincts and the fluid transitions out of the matrix.
          let champFactory () = { TierFactory.createClassLevel cls championLevel with PlayerStyle = true }
          let mobFactory () = { Bestiary.createMonster monster with MonsterFamily = None }
          let tp = findSwarmTippingPoint policies champFactory mobFactory maxMobCount iterationsPerProbe roller
          onProgress ()
          (cls, tp))
        |> Map.ofList
      { Monster = monster; ChampionLevel = championLevel; TippingPoints = tpMap })

  /// The floor's live mobs, scaled exactly the way the interactive tower scales them. Guardians are
  /// dropped: a progression sweep measures the mob curve, and one boss per floor would dominate it.
  let private floorMobs (seed: int) (player: Combatant) (floorNumber: int) : Combatant list =
    TowerGenerator.generateFloorForPlane seed floorNumber false player.Plane
    |> TowerSession.rescaleFloorEnemies player
    |> fun floor -> floor.Entities |> Map.toList |> List.map snd
    |> List.choose (function
      | TowerEntity.EntityEnemy enemy when not enemy.IsDefeated && not enemy.IsBoss -> Some enemy.Combatant
      | _ -> None)
    |> List.truncate 4

  /// Plays whole runs: real floor generation, real level scaling, real growth and XP, no healing between
  /// floors (the tower heals only at shrines and rests, so a run that spends neither is the honest floor).
  let runProgressionSimWith
    (policies: AI.PolicyTable)
    (policy: GroupPolicy)
    (classFor: int -> CharacterClass)
    (runs: int)
    (maxFloors: int)
    : ProgressionSimReport =
    let reached = Array.zeroCreate (maxFloors + 1)
    let wins = Array.zeroCreate (maxFloors + 1)
    let levels = Array.create (maxFloors + 1) 0.0
    let hp = Array.create (maxFloors + 1) 0.0
    let morale = Array.create (maxFloors + 1) 0.0
    let mutable clearedAll = 0
    let mutable totalDepth = 0

    for run in 0 .. runs - 1 do
      let seed = batchBaseSeed + run
      let rng = Random(seed)
      let roller: DiceRoller = fun min max -> rng.Next(min, max + 1)

      // Alternate the two player classes so the sweep does not report one class's curve twice.
      let playerClass = classFor run

      // The climber is the protagonist, not a mob. `resolveMentalDefenseFor` picks its player branch off
      // this name, and a generic class like Rogue would otherwise be defended as an enemy — which is how
      // the early-level enemy relief below ended up shaving a third off the Rogue's own guard. `PlayerStyle`
      // is the same distinction for the ramp: the climb harness must build a player, or the protagonist
      // climbs with the opposition's flat, non-compounding stats.
      let mutable player =
        { TierFactory.createClassLevel playerClass 1 with
            Name = "PlayerCharacter"
            PlayerStyle = true }
        |> Combatant.seizeVor
      let mutable floor = 1
      let mutable alive = true

      while alive && floor <= maxFloors do
        reached.[floor] <- reached.[floor] + 1
        let foes = floorMobs seed player floor

        if foes.IsEmpty then
          wins.[floor] <- wins.[floor] + 1
          floor <- floor + 1
        else
          let maxRounds = Math.Max(60, foes.Length + 20)
          let result, survivor = runPreparedGroupMatchWith policies policy roller player foes maxRounds

          if result.SoloWon then
            wins.[floor] <- wins.[floor] + 1
            levels.[floor] <- levels.[floor] + float survivor.Level
            hp.[floor] <- hp.[floor] + (float survivor.Health.Current / float survivor.Health.Maximum * 100.0)
            morale.[floor] <- morale.[floor] + (float survivor.Morale.Current / float survivor.Morale.Maximum * 100.0)

            // The tower's own progression: growth from the pack, XP for the pack, stacks cleared on the stairs.
            let grown = Combatant.growFromEncounter foes survivor
            let xp = foes |> List.sumBy (fun f -> f.Level * 10 + 25)
            let levelled, _ = TowerSession.applyExperience grown xp
            player <- { Combatant.clearStacks levelled with Meters = StatusMeters.Zero }
            floor <- floor + 1
          else
            alive <- false

      totalDepth <- totalDepth + (floor - 1)
      if floor > maxFloors then clearedAll <- clearedAll + 1

    let perFloor =
      [ 1 .. maxFloors ]
      |> List.map (fun f ->
        let n = reached.[f]
        let average (total: float) =
          // Survivor readings only exist for runs that won the floor, so divide by wins, not by arrivals.
          let w = wins.[f]
          if w = 0 then 0.0 else total / float w

        { Floor = f
          Reached = n
          Wins = wins.[f]
          AvgLevel = average levels.[f]
          AvgHpPct = average hp.[f]
          AvgMoralePct = average morale.[f] })

    { Runs = runs
      MaxFloors = maxFloors
      RunsWon = clearedAll
      AvgFloorsReached = (if runs = 0 then 0.0 else float totalDepth / float runs)
      Floors = perFloor }

  /// The interactive sweep: the AI policy, alternating Fighter and Mage run by run.
  let runProgressionSim (policies: AI.PolicyTable) (runs: int) (maxFloors: int) : ProgressionSimReport =
    runProgressionSimWith
      policies
      (aiPolicy policies)
      (fun run -> if run % 2 = 0 then CharacterClass.Fighter else CharacterClass.Mage)
      runs
      maxFloors
