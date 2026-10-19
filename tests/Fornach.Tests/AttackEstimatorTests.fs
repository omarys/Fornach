module Fornach.Tests.AttackEstimatorTests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

[<Fact>]
let ``statPair mirrors the stats ActionResolver contests against`` () =
  Assert.Equal((Force, [ Fortitude; Resolve ]), AttackEstimator.statPair (AttackClassification.ForceStrike false))
  Assert.Equal((Finesse, [ Reflex; Intuition ]), AttackEstimator.statPair (AttackClassification.FinesseCadence false))
  Assert.Equal((Prowess, [ Poise; Composure ]), AttackEstimator.statPair (AttackClassification.MasterfulDisarm 3))
  Assert.Equal((Intellect, [ Fortitude; Resolve ]), AttackEstimator.statPair (AttackClassification.ArcaneCataclysm false))
  Assert.Equal((Acuity, [ Reflex; Intuition ]), AttackEstimator.statPair (AttackClassification.SynapticGlamour false))
  Assert.Equal((Acumen, [ Poise; Composure ]), AttackEstimator.statPair (AttackClassification.RunicWardTrap false))

[<Fact>]
let ``a softer defense estimates more damage than a hardened one`` () =
  let attacker = TierFactory.createClassLevel CharacterClass.Duelist 100
  let baseTarget = TierFactory.createClassLevel CharacterClass.Warden 100
  let cadence = AttackClassification.FinesseCadence false

  let glassJaw =
    { baseTarget with
        Stats = baseTarget.Stats.With(Reflex, 1).With(Intuition, 1) }

  let bastion =
    { baseTarget with
        Stats = baseTarget.Stats.With(Reflex, 2000).With(Intuition, 2000) }

  Assert.True(AttackEstimator.estimate attacker glassJaw cadence > AttackEstimator.estimate attacker bastion cadence)
  Assert.Equal(0.0, AttackEstimator.estimate attacker bastion cadence)

[<Fact>]
let ``the duel menu promotes the strike the scouted enemy is softest to`` () =
  let player = TierFactory.createClassLevel CharacterClass.Duelist 100

  // Agility-poised duelist against an enemy who shrugs off agility but folds to force:
  // the archetype's own order lists Finesse first, so a correct ranking has to reorder it.
  let baseEnemy = TierFactory.createClassLevel CharacterClass.Warden 100

  let enemy =
    { baseEnemy with
        Stats =
          baseEnemy.Stats
            .With(Reflex, 2000)
            .With(Intuition, 2000)
            .With(Fortitude, 1)
            .With(Resolve, 1) }

  let menu = Program.buildActionChoices player enemy
  let indexOf (needle: string) = menu |> List.findIndex (fun label -> label.Contains needle)

  Assert.True(indexOf "Force Strike: Standard Cleave" < indexOf "Finesse Cadence: Rapid Probing")
