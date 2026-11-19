namespace Fornach.Tests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// Species familiarity (ADR 0009) moves 1% per encounter across a +/-25% span, so fifty encounters
/// take a player from -25% to +25% against a species. The same curve feeds damage in both directions
/// and the reactive riposte/disarm chance - a first meeting fumbles the counter, a veteran lands it.
module FamiliarityTests =

  let private known (encounters: int) (species: string) (c: Combatant) =
    let bestiary = Map [ ProgressionProfile.NormalizeSpeciesName species, encounters ]
    { c with Progression = { c.Progression with BestiaryKnowledge = bestiary } }

  let private player () =
    { TierFactory.createClassLevel CharacterClass.Fighter 30 with PlayerStyle = true }

  let private mob () = Bestiary.createMonster Bestiary.blightSprite

  [<Fact>]
  let ``familiarity climbs 1% per encounter from -25% to a +25% cap at fifty encounters`` () =
    let p = player ()
    let beast = mob ()

    Assert.Equal(0.75, ActionResolver.familiarityFactor (known 0 beast.Name p) beast, 4)
    Assert.Equal(0.76, ActionResolver.familiarityFactor (known 1 beast.Name p) beast, 4)
    Assert.Equal(1.00, ActionResolver.familiarityFactor (known 25 beast.Name p) beast, 4)
    Assert.Equal(1.25, ActionResolver.familiarityFactor (known 50 beast.Name p) beast, 4)
    // The cap holds: a hundred encounters teach no more than fifty.
    Assert.Equal(1.25, ActionResolver.familiarityFactor (known 100 beast.Name p) beast, 4)

  [<Fact>]
  let ``mobs keep no bestiary and always fight at 1.0`` () =
    let beast = mob ()
    Assert.Equal(1.0, ActionResolver.familiarityFactor beast (player ()), 4)

  [<Fact>]
  let ``familiarity runs the mirror on damage dealt and taken`` () =
    let beast = mob ()
    let stranger = known 0 beast.Name (player ())
    let veteran = known 50 beast.Name (player ())

    // Outgoing: -25% on a first meeting, +25% once mastered. Incoming mirrors it.
    Assert.Equal(0.75, ActionResolver.speciesDamageFactor stranger beast, 4)
    Assert.Equal(1.25, ActionResolver.speciesDamageFactor beast stranger, 4)
    Assert.Equal(1.25, ActionResolver.speciesDamageFactor veteran beast, 4)
    Assert.Equal(0.75, ActionResolver.speciesDamageFactor beast veteran, 4)

  [<Fact>]
  let ``familiarity scales the riposte chance so a veteran counters where a stranger does not`` () =
    // Five Power stacks reach the 70% ceiling: a stranger fumbles it down to 52%, a veteran holds it.
    // A flat roll of 60 therefore lands the counter only for the combatant who knows the species.
    let roller: DiceRoller = fun _ _ -> 60
    let attacker = TierFactory.createClassTier CharacterClass.Warrior CombatTier.Novice
    let engaged (defender: Combatant) =
      { defender with Momentum = Combatant.StackCap }

    let countered (defender: Combatant) =
      let target = engaged defender

      ActionResolver.resolve roller (StandardAttack(ForceStrike false)) attacker target
      |> fun res ->
        res.Events
        |> List.exists (function
          | CombatEvent.RiposteExecuted _ -> true
          | CombatEvent.DisarmExecuted _ -> true
          | _ -> false)

    Assert.False(countered (known 0 attacker.Name (player ())), "a first meeting should not counter")
    Assert.True(countered (known 50 attacker.Name (player ())), "a mastered species should counter")
