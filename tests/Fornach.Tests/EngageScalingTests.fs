module Fornach.Tests.EngageScalingTests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// A player built at the level the tower's progression assumes when it reaches floors 6 and 7.
let private playerAt (level: int) =
  let p = TierFactory.createClassLevel CharacterClass.Fighter level
  { p with PlayerStyle = true }

[<Fact>]
let ``a guardian authored far above the player is clamped to the engage allowance`` () =
  let player = playerAt 36
  let raw = Bestiary.createMonster Bestiary.solarSphinx

  let clamped = TierFactory.clampEngagedToPlayerLevel raw player

  Assert.Equal(185, raw.Level)
  Assert.Equal(38, clamped.Level) // player level + 2
  Assert.Equal(raw.Name, clamped.Name) // identity survives the rescale

[<Fact>]
let ``clamping turns an unhittable guardian into a fightable one`` () =
  let player = playerAt 36
  let attack = AttackClassification.FinesseCadence false
  let raw = Bestiary.createMonster Bestiary.starlitEidolon

  let clamped = TierFactory.clampEngagedToPlayerLevel raw player

  // The floor 7 bug: a level 190 Reflex made every attack estimate exactly zero damage.
  Assert.Equal(0.0, AttackEstimator.estimate player raw attack)
  Assert.True(AttackEstimator.estimate player clamped attack > 0.0)

[<Fact>]
let ``a clamped combatant keeps every stat, pool and armor entry positive`` () =
  let player = playerAt 36

  for template in [ Bestiary.solarSphinx; Bestiary.starlitEidolon ] do
    let clamped = TierFactory.clampEngagedToPlayerLevel (Bestiary.createMonster template) player

    for stat in Attributes.all do
      Assert.True(clamped.Stats.Get stat > 0, sprintf "%s was clipped to %d" (stat.ToString()) (clamped.Stats.Get stat))

    // An additive rescale drove armor negative here; a uniform rescale must not.
    Assert.True(clamped.Health.Maximum > 0)
    Assert.True(clamped.Health.Current > 0)
    Assert.True(clamped.Morale.Maximum > 0)
    Assert.True(clamped.Armor.Max > 0)
    Assert.True(clamped.Armor.Current > 0)

[<Fact>]
let ``an enemy already within the allowance is handed to the duel untouched`` () =
  let player = playerAt 200
  let raw = Bestiary.createMonster Bestiary.solarSphinx

  let engaged = TierFactory.clampEngagedToPlayerLevel raw player

  Assert.True(obj.ReferenceEquals(raw, engaged))
  Assert.Equal(185, engaged.Level)

[<Fact>]
let ``striking shifts the actor into its vector stance so the bonus lands automatically`` () =
  let roller : DiceRoller = fun lo hi -> (lo + hi) / 2
  let actor = { playerAt 36 with Stance = CombatStance.PowerStance }
  let target = TierFactory.createClassLevel CharacterClass.Fighter 36

  let result =
    ActionResolver.resolve roller (ActionIntent.StandardAttack(AttackClassification.FinesseCadence false)) actor target

  // Finesse strikes are contested on Reflex/Intuition; without the shift the actor would eat
  // the off-vector penalty instead of gaining the in-vector bonus.
  Assert.Equal(CombatStance.AgilityStance, result.Actor.Stance)
