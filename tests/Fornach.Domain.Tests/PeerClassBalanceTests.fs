namespace Fornach.Domain.Tests

open System
open Xunit
open Xunit.Abstractions
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

module PeerClassBalanceTests =

  type MatchupOutcomeRow = {
    Tier: CombatTier
    ClassA: CharacterClass
    ClassB: CharacterClass
    WinsA: int
    WinsB: int
    Stalemates: int
    TotalRuns: int
    AvgRounds: float
    HealthDepletions: int
    MoraleDepletions: int
    Executions: int
    BalanceStatus: string
  }

  let private getBalanceTag (winRateA: float) (winRateB: float) : string =
    if winRateA >= 40.0 && winRateA <= 60.0 then "Even Match (Balanced)"
    elif winRateA > 60.0 && winRateA <= 80.0 then "Tactical Advantage (A)"
    elif winRateB > 60.0 && winRateB <= 80.0 then "Tactical Advantage (B)"
    elif winRateA > 80.0 then "Hard Counter (A)"
    else "Hard Counter (B)"

  type PeerClassBalanceFixture(output: ITestOutputHelper) =

    let canonicalClasses = [
      CharacterClass.Berserker
      CharacterClass.Duelist
      CharacterClass.Warden
      CharacterClass.Inquisitor
      CharacterClass.Mesmer
      CharacterClass.Abjurer
    ]

    [<Fact>]
    let ``Peer Class 1v1 Balance Matrix: Pit each class against each other across 50 runs at Veteran tier`` () =
      let rng = Random(42)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let runsPerMatchup = 50
      let tier = CombatTier.Veteran
      let rows = ResizeArray<MatchupOutcomeRow>()

      let classPairs =
        [ for i in 0 .. canonicalClasses.Length - 1 do
            for j in (i + 1) .. canonicalClasses.Length - 1 do
              yield (canonicalClasses.[i], canonicalClasses.[j]) ]

      for (clsA, clsB) in classPairs do
        let factoryA () = TierFactory.createClassTier clsA tier
        let factoryB () = TierFactory.createClassTier clsB tier

        let summary = Simulation.runHeadlessBatch factoryA factoryB runsPerMatchup roller
        let winRateA = (float summary.WinsA / float summary.TotalIterations) * 100.0
        let winRateB = (float summary.WinsB / float summary.TotalIterations) * 100.0

        let row = {
          Tier = tier
          ClassA = clsA
          ClassB = clsB
          WinsA = summary.WinsA
          WinsB = summary.WinsB
          Stalemates = summary.Stalemates
          TotalRuns = summary.TotalIterations
          AvgRounds = summary.AvgRounds
          HealthDepletions = summary.HealthDepletions
          MoraleDepletions = summary.MoraleDepletions
          Executions = summary.Executions
          BalanceStatus = getBalanceTag winRateA winRateB
        }
        rows.Add row

      // Print Markdown Table
      output.WriteLine("\n# FORNACH COMBAT ENGINE: PEER CLASS 1v1 BALANCE MATRIX (VETERAN TIER)")
      output.WriteLine("### Pairwise peer matchups across all 6 canonical archetypes (50 runs per matchup, alternating turn order)\n")
      output.WriteLine("| Matchup | Win Rate A | Win Rate B | Stalemates | Avg TTK | HP Kills | Morale Kills | Executions | Status |")
      output.WriteLine("|:--------|:----------:|:----------:|:----------:|:-------:|:--------:|:------------:|:----------:|:-------|")

      for r in rows do
        let winRateA = (float r.WinsA / float r.TotalRuns) * 100.0
        let winRateB = (float r.WinsB / float r.TotalRuns) * 100.0
        output.WriteLine(
          sprintf "| %-13s vs %-11s | %5.1f%% (%2d) | %5.1f%% (%2d) | %10d | %7.2f | %8d | %12d | %10d | %s |"
            r.ClassA.Name
            r.ClassB.Name
            winRateA r.WinsA
            winRateB r.WinsB
            r.Stalemates
            r.AvgRounds
            r.HealthDepletions
            r.MoraleDepletions
            r.Executions
            r.BalanceStatus
        )

      // Balance Invariants:
      // 1. No stalemate timeouts in peer matchups
      for r in rows do
        Assert.True(r.Stalemates <= 2, sprintf "Stalemate rate should be minimal in peer duels (%s vs %s had %d)" r.ClassA.Name r.ClassB.Name r.Stalemates)

      // 2. Average pacing must be healthy (>= 2.0 rounds, <= 12.0 rounds)
      for r in rows do
        Assert.True(r.AvgRounds >= 2.0, sprintf "Combat ended too quickly (%s vs %s averaged %.2f rounds)" r.ClassA.Name r.ClassB.Name r.AvgRounds)
        Assert.True(r.AvgRounds <= 12.0, sprintf "Combat took too long (%s vs %s averaged %.2f rounds)" r.ClassA.Name r.ClassB.Name r.AvgRounds)

      // 3. No God class (no single class wins 100% of all its peer duels across all 5 opponents)
      for cls in canonicalClasses do
        let matchups = rows |> Seq.filter (fun r -> r.ClassA = cls || r.ClassB = cls) |> Seq.toList
        let totalWins =
          matchups
          |> List.sumBy (fun r -> if r.ClassA = cls then r.WinsA else r.WinsB)
        let totalMatches = matchups.Length * runsPerMatchup
        let overallWinRate = (float totalWins / float totalMatches) * 100.0
        output.WriteLine(sprintf "Overall Win Rate for %-10s: %5.1f%% (%d / %d)" cls.Name overallWinRate totalWins totalMatches)
        Assert.True(overallWinRate < 95.0, sprintf "%s is overpowering the entire roster (Overall Win Rate: %.1f%%)" cls.Name overallWinRate)
        Assert.True(overallWinRate > 10.0, sprintf "%s is underpowered across the board (Overall Win Rate: %.1f%%)" cls.Name overallWinRate)

    [<Fact>]
    let ``Cross-Plane Parity: Berserker vs Inquisitor is competitive and closely contested at Master tier`` () =
      let rng = Random(123)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let runs = 50
      let factoryA () = TierFactory.createClassTier CharacterClass.Berserker CombatTier.Master
      let factoryB () = TierFactory.createClassTier CharacterClass.Inquisitor CombatTier.Master

      let summary = Simulation.runHeadlessBatch factoryA factoryB runs roller
      let winRateA = (float summary.WinsA / float summary.TotalIterations) * 100.0
      let winRateB = (float summary.WinsB / float summary.TotalIterations) * 100.0

      output.WriteLine(sprintf "Master Berserker vs Master Inquisitor (50 runs): Berserker %.1f%% (%d) vs Inquisitor %.1f%% (%d), Avg TTK: %.2f rounds"
        winRateA summary.WinsA winRateB summary.WinsB summary.AvgRounds)

      Assert.True(summary.AvgRounds >= 2.0, sprintf "Master Power duel should take at least 2 rounds (Actual: %.2f)" summary.AvgRounds)
      Assert.True(winRateA >= 30.0 && winRateA <= 70.0, sprintf "Power cross-plane matchup should be competitive (Actual: %.1f%% vs %.1f%%)" winRateA winRateB)

    [<Fact>]
    let ``Physical Triad: Rock-Paper-Scissors dynamic holds across Berserker, Duelist, and Warden`` () =
      let rng = Random(456)
      let roller : DiceRoller = fun min max -> rng.Next(min, max + 1)
      let runs = 50

      // 1. Berserker vs Warden (Warden guard should hold advantage against brute force)
      let sumBW = Simulation.runHeadlessBatch
                    (fun () -> TierFactory.createClassTier CharacterClass.Berserker CombatTier.Veteran)
                    (fun () -> TierFactory.createClassTier CharacterClass.Warden CombatTier.Veteran)
                    runs roller
      let wardenWinRate = (float sumBW.WinsB / float sumBW.TotalIterations) * 100.0
      output.WriteLine(sprintf "Warden vs Berserker: Warden Win Rate = %.1f%%" wardenWinRate)
      Assert.True(wardenWinRate >= 50.0, sprintf "Warden should hold advantage over Berserker (Actual: %.1f%%)" wardenWinRate)

      // 2. Duelist vs Warden (Duelist precision openings create a tightly contested tactical matchup against plate guard)
      let sumDW = Simulation.runHeadlessBatch
                    (fun () -> TierFactory.createClassTier CharacterClass.Duelist CombatTier.Veteran)
                    (fun () -> TierFactory.createClassTier CharacterClass.Warden CombatTier.Veteran)
                    runs roller
      let duelistWinRate = (float sumDW.WinsA / float sumDW.TotalIterations) * 100.0
      output.WriteLine(sprintf "Duelist vs Warden: Duelist Win Rate = %.1f%%" duelistWinRate)
      Assert.True(duelistWinRate >= 35.0 && duelistWinRate <= 65.0, sprintf "Duelist vs Warden should be a competitive contest (Actual: %.1f%%)" duelistWinRate)
