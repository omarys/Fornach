namespace Fornach.Engine

open System
open Fornach.Domain

/// Tactical timing in the historical German fencing (Vor / Nach / Indes) paradigm
type TacticalTiming =
  /// Vor (Before): Possessing initiative, attacking first, driving the tempo
  | Vor
  /// Nach (After): Reacting, parrying, defending against an incoming strike
  | Nach
  /// Indes (Meanwhile / In the midst): Intercepting at the tempo midpoint; simultaneous counter-action
  | Indes

/// Evaluation result for an Indes tempo-seizure opportunity
type IndesEvaluation = {
  CanSeizeVor: bool
  Threshold: int
  Margin: int
  DefenderHits: int
  AttackerHits: int
}

module Indes =

  /// Default baseline margin of net defense hits needed to seize the Vor from the Nach
  let defaultThreshold = 3

  /// Calculates the Indes threshold for a combatant.
  /// A ParryingBuckler modifies Indes.calculateThreshold by -1, widening the window for Justicars to seize the Vor.
  let calculateThreshold (combatant: Combatant) : int =
    let baseThresh = defaultThreshold
    let bucklerMod =
      if combatant.HasActivePreparation PreparationType.ParryingBuckler then -1
      else 0
    Math.Max(1, baseThresh + bucklerMod)

  /// Evaluates whether a defender in the Nach can seize the Vor via an Indes counter-timing
  let evaluateIndes (defender: Combatant) (defenseHits: int) (attackHits: int) : IndesEvaluation =
    let threshold = calculateThreshold defender
    let margin = defenseHits - attackHits
    { CanSeizeVor = margin >= threshold
      Threshold = threshold
      Margin = margin
      DefenderHits = defenseHits
      AttackerHits = attackHits }

module IndesResolver =

  /// Resolves an immediate quick-draw Concealed Blade counter-puncture from the Nach.
  /// When targeted in the Nach, allows interrupting the incoming attack with an immediate counter-puncture
  /// before damage calculates.
  let resolveConcealedBlade
    (roller: DiceRoller)
    (defender: Combatant)
    (attacker: Combatant)
    : Combatant * Combatant * CombatEvent list * bool =
    let defFinesse = defender.GetStat Finesse
    let atkReflex = attacker.GetStat Reflex
    let ratio = float defFinesse / Math.Max(1.0, float atkReflex)
    let baseDmg = Math.Max(15, int (Math.Round(float defFinesse * 0.80 * ratio)))
    let punctureDmg = baseDmg + (if roller 1 100 <= 35 then 25 else 0)

    let updatedAttacker =
      { attacker with
          Health = attacker.Health.ApplyDelta -punctureDmg }
      |> Combatant.updateMeters (fun m -> { m with Overwhelm = m.Overwhelm + 15 })

    // Attack is disrupted if attacker collapsed, died, or if finesse disparity significantly overwhelms reflex (>= 1.6x)
    let isDisrupted =
      updatedAttacker.Health.IsDepleted
      || float defFinesse >= float atkReflex * 1.60
      || (defFinesse >= atkReflex && roller 1 100 <= 35)

    let finalAttacker =
      if isDisrupted then
        { updatedAttacker with ComboTracker = updatedAttacker.ComboTracker.ResetCombo() }
      else
        updatedAttacker

    // Consume the one-time active ConcealedBlade from defender
    let updatedDefender = Combatant.removeActivePreparation PreparationType.ConcealedBlade defender

    let evts = [
      CombatEvent.ConcealedBladeCounter(defender.Id, attacker.Id, punctureDmg, isDisrupted)
      CombatEvent.DamageApplied {
        TargetId = attacker.Id
        Plane = Physical
        Amount = punctureDmg
        IsCritical = false
        IsArmorCompromised = false
      }
      if isDisrupted then
        CombatEvent.ComboReset(attacker.Id, "Incoming strike interrupted and defused by quick-draw Concealed Blade!")
    ]

    updatedDefender, finalAttacker, evts, isDisrupted

  /// Evaluates Indes tempo seizure when defense hits significantly exceed attack hits
  let resolveIndesOpportunity
    (defender: Combatant)
    (attacker: Combatant)
    (contest: ContestResult)
    : Combatant * Combatant * CombatEvent list * bool =
    let eval = Indes.evaluateIndes defender contest.Defender.TotalHits contest.Attacker.TotalHits
    if eval.CanSeizeVor then
      let updatedDef = defender |> Combatant.addStudyStacks 1
      let updatedAtk =
        attacker
        |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 10 })
        |> fun a -> { a with ComboTracker = a.ComboTracker.ResetCombo() }
      let evts = [
        CombatEvent.IndesSeized(defender.Id, attacker.Id, eval.Threshold, eval.Margin)
        CombatEvent.ComboReset(attacker.Id, "Defender seized the Vor in Indes; offensive momentum arrested.")
      ]
      updatedDef, updatedAtk, evts, true
    else
      defender, attacker, [], false
