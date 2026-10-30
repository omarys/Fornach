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
  let baseTarget = TierFactory.createClassLevel CharacterClass.Warrior 100
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
let ``a strike is estimated in the stance it transitions into, not the one held`` () =
  let power = TierFactory.createClassLevel CharacterClass.Fighter 100 // holds PowerStance
  let target = TierFactory.createClassLevel CharacterClass.Warrior 100
  let agility = { power with Stance = CombatStance.AgilityStance }
  let discipline = { power with Stance = CombatStance.DisciplineStance }

  // FinesseCadence always transitions to AgilityStance, so the held stance must not matter.
  let finesse = AttackClassification.FinesseCadence false
  let fromPower = AttackEstimator.estimate power target finesse
  Assert.Equal(fromPower, AttackEstimator.estimate agility target finesse)
  Assert.Equal(fromPower, AttackEstimator.estimate discipline target finesse)

[<Fact>]
let ``each strike scales with its own meter`` () =
  let fighter = TierFactory.createClassLevel CharacterClass.Fighter 100
  // Soft target: every vector must actually land damage, or the floors hide the ramp.
  let baseTarget = TierFactory.createClassLevel CharacterClass.Warrior 100
  let target =
    { baseTarget with
        Stats = baseTarget.Stats.With(Fortitude, 1).With(Reflex, 1).With(Poise, 1) }
  let charged = { fighter with Flow = 5; Momentum = 5 }

  // Flow and Momentum are read by GetCombatStat, so a charged pile must raise every vector's strike.
  for attack in
    [ AttackClassification.ForceStrike false
      AttackClassification.FinesseCadence false
      AttackClassification.ProwessStrike false ] do
    Assert.True(
      AttackEstimator.estimate charged target attack > AttackEstimator.estimate fighter target attack,
      sprintf "%A must scale with its own meter" attack
    )

[<Fact>]
let ``a flagged strike variant estimates higher than its plain form`` () =
  let fighter = TierFactory.createClassLevel CharacterClass.Fighter 100
  let baseTarget = TierFactory.createClassLevel CharacterClass.Warrior 100
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
  let baseTarget = TierFactory.createClassLevel CharacterClass.Warrior 100
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
  let baseEnemy = TierFactory.createClassLevel CharacterClass.Warrior 100

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

  let offence = player.GetCombatStat StatId.Force - AttackEstimator.defenseValue player enemy strike

  let guard =
    AttackEstimator.defenseValue player player strike - enemy.GetCombatStat StatId.Force

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
