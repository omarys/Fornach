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
  // SteadyForm drains neither CognitiveFatigue nor Confusion nor Provoke, so choosing it here
  // would re-trip the gate forever and the fighter would never attack again.
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
let ``steady form genuinely cannot drain cognitive fatigue`` () =
  // Pins the premise of the fix: if this ever starts draining, the AI branch can be simplified.
  let roller: DiceRoller = fun lo hi -> (lo + hi) / 2

  let pressured =
    fighter ()
    |> Combatant.updateMeters (fun m ->
      { m with
          CognitiveFatigue = m.CognitiveFatigue + 70 })

  let before = pressured.Meters.CognitiveFatigue.Value
  let after = ActionResolver.resolve roller (ActionIntent.RecoveryAction DefensiveReset.SteadyForm) pressured (mage ())

  Assert.Equal(before, after.Actor.Meters.CognitiveFatigue.Value)
