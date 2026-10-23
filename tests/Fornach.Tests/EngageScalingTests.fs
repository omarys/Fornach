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
  Assert.Equal(46, clamped.Level) // player level + 10
  Assert.Equal(raw.Name, clamped.Name) // identity survives the rescale

[<Fact>]
let ``clamping turns an unhittable guardian into a fightable one`` () =
  let player = playerAt 36
  // A Power-stance Fighter swings the Power vector; an Agility strike is off-vector (0.75x) and the
  // contest says so, which is exactly what the estimate now reflects.
  let attack = AttackClassification.ForceStrike false
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

[<Fact>]
let ``floor entry places mobs within five levels of the player and the boss ten above`` () =
  let player = playerAt 20
  let floor = TowerSession.rescaleFloorEnemies player (TowerGenerator.generateFloorWithMode 7 5 true)

  let enemies =
    floor.Entities
    |> Map.toList
    |> List.choose (fun (_, ent) ->
      match ent with
      | TowerEntity.EntityEnemy e -> Some e
      | _ -> None)

  Assert.NotEmpty(enemies)
  let mobs = enemies |> List.filter (fun e -> not e.IsBoss)
  let bosses = enemies |> List.filter (fun e -> e.IsBoss)
  Assert.NotEmpty(mobs)

  for mob in mobs do
    Assert.InRange(mob.Combatant.Level, 20, 25)

  // Floor 5 carries the stage guardian, which is the boss of the floor.
  Assert.NotEmpty(bosses)
  for boss in bosses do
    Assert.Equal(30, boss.Combatant.Level)
