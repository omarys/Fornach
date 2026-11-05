module Fornach.Tests.AiRecoveryLockTests

open Xunit
open Fornach.Domain
open Fornach.Engine
open Fornach.Cli

let private fighter () =
  // Preparations deploy before the meter gate does, so clear them to isolate recovery selection.
  { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master with
      PlayerStyle = true
      Preparations = []
      ActivePreparations = [] }

let private mage () =
  { TierFactory.createClassTier CharacterClass.Mage CombatTier.Master with PlayerStyle = true }

[<Fact>]
let ``mental pressure sends a physical fighter to a recovery that can actually drain it`` () =
  // SteadyForm now vents all seven strain meters, so this gate can no longer strand a fighter;
  // CenterMind still routes mental pressure because it also banks Insight and restores the ward.
  let pressured =
    fighter ()
    |> Combatant.updateMeters (fun m ->
      { m with
          CognitiveFatigue = m.CognitiveFatigue + 70 })

  Assert.Equal(ActionIntent.RecoveryAction DefensiveReset.CenterMind, AI.chooseIntent pressured (mage ()))

[<Fact>]
let ``physical pressure still sends the fighter to its own steady form`` () =
  // The complement of the fix: only mental meters divert a physical actor off SteadyForm.
  let tired =
    fighter ()
    |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + 70 })

  Assert.Equal(ActionIntent.RecoveryAction DefensiveReset.SteadyForm, AI.chooseIntent tired (mage ()))

[<Fact>]
let ``a physical fighter with high confusion is not parked on steady form either`` () =
  let pressured =
    fighter ()
    |> Combatant.updateMeters (fun m -> { m with Confusion = m.Confusion + 70 })

  Assert.Equal(ActionIntent.RecoveryAction DefensiveReset.CenterMind, AI.chooseIntent pressured (mage ()))

[<Fact>]
let ``centering the mind clears the fatigue that steady form cannot touch`` () =
  let roller: DiceRoller = fun lo hi -> (lo + hi) / 2

  let pressured =
    fighter ()
    |> Combatant.updateMeters (fun m ->
      { m with
          CognitiveFatigue = m.CognitiveFatigue + 70 })

  let before = pressured.Meters.CognitiveFatigue.Value
  let after = ActionResolver.resolve roller (ActionIntent.RecoveryAction DefensiveReset.CenterMind) pressured (mage ())

  Assert.True(before >= 65)
  Assert.True(
    after.Actor.Meters.CognitiveFatigue.Value < before,
    sprintf "fatigue stayed at %d" after.Actor.Meters.CognitiveFatigue.Value
  )

[<Fact>]
let ``steady form drains cognitive fatigue since the two resets were merged`` () =
  // Pins the merged contract: the single recovery action vents every strain meter, mental included.
  let roller: DiceRoller = fun lo hi -> (lo + hi) / 2

  let pressured =
    fighter ()
    |> Combatant.updateMeters (fun m ->
      { m with
          CognitiveFatigue = m.CognitiveFatigue + 70 })

  let before = pressured.Meters.CognitiveFatigue.Value
  let after = ActionResolver.resolve roller (ActionIntent.RecoveryAction DefensiveReset.SteadyForm) pressured (mage ())

  Assert.True(
    after.Actor.Meters.CognitiveFatigue.Value < before,
    sprintf "fatigue stayed at %d" after.Actor.Meters.CognitiveFatigue.Value
  )

// ---------------------------------------------------------------------------
// Plane legality: the AI must never propose an attack the resolver will refuse.
//
// `AttackClassification.IsAllowedFor` has no fallback: a refused attack costs the actor its whole
// turn. A single illegal branch therefore deletes a combatant from the fight without any error,
// which is exactly how the Aspect of Guilt spent every round of every encounter doing nothing.
// ---------------------------------------------------------------------------

/// Straddles each threshold the offensive profiles branch on: recklessness 25/35/65/70, cognitive
/// fatigue 80, and the opponent's morale 40.
let private meterSweep = [ 0; 30; 70; 100 ]

let private setMeter (target: int) (meter: Meter) = meter + (target - meter.Value)

let private aspects () =
  [ "Guilt", StoryBosses.createGuiltAspect ()
    "Denial", StoryBosses.createDenialAspect ()
    "Anger", StoryBosses.createAngerAspect ()
    "Bargaining", StoryBosses.createBargainingAspect ()
    "Depression", StoryBosses.createDepressionAspect ()
    "Acceptance", StoryBosses.createAcceptanceAspect () ]

[<Fact>]
let ``no aspect is ever handed an attack its own plane refuses`` () =
  let opponents = [ "fighter", fighter (); "mage", mage () ]

  let illegal =
    [ for name, actor in aspects () do
        for opponentName, opponent in opponents do
          for recklessness in meterSweep do
            for cognitive in meterSweep do
              for morale in meterSweep do
                let actor =
                  actor
                  |> Combatant.updateMeters (fun m ->
                    { m with
                        Recklessness = setMeter recklessness m.Recklessness
                        CognitiveFatigue = setMeter cognitive m.CognitiveFatigue })

                let opponent = { opponent with Morale = { opponent.Morale with Current = morale } }

                match AI.chooseIntent actor opponent with
                | ActionIntent.StandardAttack attack when not (attack.IsAllowedFor actor) ->
                  yield
                    sprintf
                      "%s (plane %A) vs %s [reck %d, cognitive %d, morale %d] -> %A (plane %A)"
                      name
                      actor.Plane
                      opponentName
                      recklessness
                      cognitive
                      morale
                      attack
                      attack.Plane
                | _ -> () ]

  Assert.Empty illegal

[<Fact>]
let ``no bestiary family is ever handed an attack its own plane refuses`` () =
  let opponent = fighter ()

  let illegal =
    [ for template in Bestiary.allMonsters do
        let actor = Bestiary.createMonster template

        for recklessness in meterSweep do
          let actor =
            actor
            |> Combatant.updateMeters (fun m -> { m with Recklessness = setMeter recklessness m.Recklessness })

          match AI.chooseIntent actor opponent with
          | ActionIntent.StandardAttack attack when not (attack.IsAllowedFor actor) ->
            yield sprintf "%s (plane %A) -> %A (plane %A)" template.Name actor.Plane attack attack.Plane
          | _ -> () ]

  Assert.Empty illegal

