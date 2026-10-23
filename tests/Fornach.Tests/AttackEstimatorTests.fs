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
let ``a DisciplineStance defender is estimated against its real Poise guard`` () =
  let attacker = TierFactory.createClassLevel CharacterClass.Rogue 100
  let baseTarget = TierFactory.createClassLevel CharacterClass.Soldier 100
  let prowl = AttackClassification.ProwessStrike false

  let stanced stance = { baseTarget with Stance = stance }
  let power = stanced CombatStance.PowerStance
  let discipline = stanced CombatStance.DisciplineStance

  // Same stats, 1.5x Poise guard: the estimate can only drop, never rise, from the stance.
  Assert.True(discipline.GetGuard(Physical, true) > power.GetGuard(Physical, true))
  Assert.True(AttackEstimator.estimate attacker discipline prowl <= AttackEstimator.estimate attacker power prowl)

  // A Prowess strike that cannot beat the guard is estimated as a whiff, not as the best option.
  let weak = { attacker with Stats = attacker.Stats.With(Prowess, 1) }
  Assert.Equal(0.0, AttackEstimator.estimate weak discipline prowl)

[<Fact>]
let ``an off-stance strike estimates lower than an on-stance one`` () =
  let power = TierFactory.createClassLevel CharacterClass.Fighter 100 // PowerStance
  let target = TierFactory.createClassLevel CharacterClass.Soldier 100

  // The contest reads GetCombatStat, so the Power vector strikes at 1.25x and Agility at 0.75x.
  let force = AttackEstimator.estimate power target (AttackClassification.ForceStrike false)
  let agility = AttackEstimator.estimate power target (AttackClassification.FinesseCadence false)
  Assert.True(force > agility)

[<Fact>]
let ``a flagged strike variant estimates higher than its plain form`` () =
  let fighter = TierFactory.createClassLevel CharacterClass.Fighter 100
  let baseTarget = TierFactory.createClassLevel CharacterClass.Soldier 100
  // Soft target, so the variance bars are wide enough for both variants to land.
  let soft =
    { baseTarget with
        Stats = baseTarget.Stats.With(Fortitude, 1).With(Resolve, 1) }

  let wild = AttackEstimator.estimate fighter soft (AttackClassification.ForceStrike true)
  let plain = AttackEstimator.estimate fighter soft (AttackClassification.ForceStrike false)
  Assert.True(wild > plain)

[<Fact>]
let ``a mental strike is estimated against the defender's mental grounding`` () =
  let attacker = TierFactory.createClassLevel CharacterClass.Mage 100
  let plain = TierFactory.createClassLevel CharacterClass.Fighter 100
  let grounded = { plain with PlayerStyle = true } // same stats, but the class grounding applies
  let decree = AttackClassification.AuthorityDecree false

  Assert.True(AttackEstimator.estimate attacker grounded decree <= AttackEstimator.estimate attacker plain decree)

[<Fact>]
let ``a softer defense estimates more damage than a hardened one`` () =
  let attacker = TierFactory.createClassLevel CharacterClass.Rogue 100
  let baseTarget = TierFactory.createClassLevel CharacterClass.Soldier 100
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
  let player = TierFactory.createClassLevel CharacterClass.Rogue 100

  // Agility-poised duelist against an enemy who shrugs off agility but folds to force:
  // the archetype's own order lists Finesse first, so a correct ranking has to reorder it.
  let baseEnemy = TierFactory.createClassLevel CharacterClass.Soldier 100

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

[<Fact>]
let ``the contest margin adds the guard half of the exchange`` () =
  let player = TierFactory.createClassLevel CharacterClass.Fighter 40
  let enemy = TierFactory.createClassLevel CharacterClass.Warrior 40
  let strike = AttackClassification.ForceStrike false

  let offence = player.GetCombatStat StatId.Force - AttackEstimator.defenseValue enemy strike

  let guard =
    AttackEstimator.defenseValue player strike - enemy.GetCombatStat StatId.Force

  Assert.Equal(offence + guard, AttackEstimator.contestMargin player enemy strike)

[<Fact>]
let ``the menu promotes the strike whose guard half outweighs its damage`` () =
  let basePlayer = TierFactory.createClassLevel CharacterClass.Fighter 100
  let baseEnemy = TierFactory.createClassLevel CharacterClass.Warrior 100

  // Force barely scratches this enemy, but the player's Fortitude dwarfs the enemy's Force, so the
  // exchange is won by standing in Power rather than by out-damaging anything in Agility.
  let player =
    { basePlayer with
        Stats = basePlayer.Stats.With(Force, 1).With(Finesse, 900).With(Fortitude, 4000) }

  let enemy = { baseEnemy with Stats = baseEnemy.Stats.With(Fortitude, 1).With(Resolve, 1) }

  let force = AttackClassification.ForceStrike false
  let finesse = AttackClassification.FinesseCadence false

  // Offence alone would rank the Agility strike first; the margin has to override it.
  Assert.True(AttackEstimator.estimate player enemy finesse > AttackEstimator.estimate player enemy force)
  Assert.True(AttackEstimator.contestMargin player enemy force > AttackEstimator.contestMargin player enemy finesse)

  let menu = Program.buildActionChoices player enemy
  let indexOf (needle: string) = menu |> List.findIndex (fun label -> label.Contains needle)
  Assert.True(indexOf "Force Strike: Standard Cleave" < indexOf "Finesse Cadence: Rapid Probing")
