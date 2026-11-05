namespace Fornach.Domain.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

// Spectre.Console allows only ONE live progress display per process, and throws
// InvalidOperationException if a second one starts concurrently. The batch helpers below are
// headless, but every test module that measures the frontier stays in this collection so the
// numbers cannot interleave with a module that does draw.
//
// The Monte-Carlo frontier measures the two player classes against plain opposition: the mobs are the
// generic NPC archetypes (Warrior / Rogue / Mage) with no preparations, no stance or Complex
// Form transitions, and a single basic attack type (see Combatant.PlayerStyle). Fluid stances, Complex
// Forms and varied attacks are exactly what sets the player apart, so they only appear on the Fighter
// and Mage side of these simulations.
[<Collection("SimulationBatch")>]
module MonteCarloSwarmTests =

  let private runSwarm = BalanceBatches.groupSummary
  let private grandmasterFighter = BalanceBatches.grandmasterFighter
  let private grandmasterMage = BalanceBatches.grandmasterMage
  let private novice = BalanceBatches.novice

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Fighter holds a 100-warrior press with its preparation kit and the eight-tile limit`` () =
    let summary = runSwarm grandmasterFighter (novice CharacterClass.Warrior "Warrior") 100 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Fighter should survive 100 warriors (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 80.0, sprintf "Fighter should eliminate most warriors (Actual: %.1f)" summary.AvgEliminations)
    // Opportunity attacks are per-encounter volume, so they scale with how long the press lasts. The Fighter's
    // inherited Shockwave Slam cleaves the press down in ~3 rounds, where the pre-reduction Bastion+Buckler
    // kit ground out ~51 rounds at 114.8 AoOs (2.2 per round). The interception RATE is the mechanic the
    // press asserts, and it rose to ~3.7 per round.
    // Frontage is Poise-driven now: at Poise 841 a Grandmaster Fighter is reachable by one warrior at a
    // time, so the press never becomes a flank. An Attack of Opportunity is the punishment for a flanker
    // arriving, and there is never a second body to punish — the count is zero by design, not by failure.
    // What the press still has to prove is that it is held, which the win-rate and elimination bars above
    // measure. The interception rate this test used to pin now belongs to champions whose Poise has not
    // yet closed the ring.
    let aoosPerRound = summary.AvgAoOsTriggered / summary.AvgRounds
    Assert.True(aoosPerRound < 0.5, sprintf "Frontage 1 should leave no flanker to punish (Actual: %.1f per round, %.1f over %.0f rounds)" aoosPerRound summary.AvgAoOsTriggered summary.AvgRounds)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Fighter dismantles 12 novice warriors`` () =
    let summary = runSwarm grandmasterFighter (novice CharacterClass.Warrior "Warrior") 12 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Fighter should beat 12 warriors (Actual: %.1f%%)" summary.SoloWinRate)
    Assert.True(summary.AvgEliminations >= 10.0, sprintf "Fighter should clear most warriors (Actual: %.1f)" summary.AvgEliminations)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Fighter outclasses 10 novice rogues`` () =
    let summary = runSwarm grandmasterFighter (novice CharacterClass.Rogue "Rogue") 10 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Fighter should beat 10 rogues (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Mage burns down 15 novice mages without their disciplines`` () =
    let summary = runSwarm grandmasterMage (novice CharacterClass.Mage "Mage") 15 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Mage should beat 15 plain casters (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Mage repels 8 novice warriors through Complex Forms`` () =
    let summary = runSwarm grandmasterMage (novice CharacterClass.Warrior "Warrior") 8 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Mage should repel 8 warriors (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: Grandmaster Mage outlasts 8 novice warriors`` () =
    let summary = runSwarm grandmasterMage (novice CharacterClass.Warrior "Warrior") 8 10

    Assert.True(summary.SoloWinRate >= 95.0, sprintf "Mage should outlast 8 warriors (Actual: %.1f%%)" summary.SoloWinRate)

  [<Fact>]
  let ``Monte-Carlo: a Novice Fighter is overrun where the Grandmaster is not`` () =
    // The frontier should be a mastery cliff, not a free win: the same mobs that a Grandmaster holds
    // off must still beat a novice.
    let noviceSummary = runSwarm (novice CharacterClass.Fighter "Fighter") (novice CharacterClass.Warrior "Warrior") 8 10
    let grandmasterSummary = runSwarm grandmasterFighter (novice CharacterClass.Warrior "Warrior") 8 10

    Assert.True(noviceSummary.SoloWinRate < 50.0, sprintf "8 warriors should overwhelm a novice Fighter (Actual: %.1f%%)" noviceSummary.SoloWinRate)
    Assert.True(grandmasterSummary.SoloWinRate >= 95.0, sprintf "The Grandmaster should hold the same press (Actual: %.1f%%)" grandmasterSummary.SoloWinRate)
