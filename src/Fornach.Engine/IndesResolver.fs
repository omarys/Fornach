namespace Fornach.Engine

open System
open Fornach.Domain

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

  /// Calculates the Indes threshold for a combatant considering defender preparations, attacker overextension, and wind-up commitment.
  let calculateThresholdWithAttacker (defender: Combatant) (attackerOpt: Combatant option) (isWindup: bool) : int =
    let baseThresh = defaultThreshold
    let bucklerMod =
      if defender.HasActivePreparation PreparationType.ParryingBuckler then -1
      else 0
    let windupMod = if isWindup then -1 else 0
    let recklessnessMod =
      match attackerOpt with
      | Some atk when atk.Meters.Recklessness.Value >= 60 -> -1
      | _ -> 0
    Math.Max(1, baseThresh + bucklerMod + windupMod + recklessnessMod)

  /// Calculates the baseline Indes threshold for a combatant.
  /// A ParryingBuckler modifies Indes.calculateThreshold by -1, widening the window for combatants to seize the Vor.
  let calculateThreshold (combatant: Combatant) : int =
    calculateThresholdWithAttacker combatant None false

  /// Evaluates whether a defender in the Nach can seize the Vor via an Indes counter-timing with attacker context
  let evaluateIndesWithAttacker (defender: Combatant) (attackerOpt: Combatant option) (isWindup: bool) (defenseHits: int) (attackHits: int) : IndesEvaluation =
    let threshold = calculateThresholdWithAttacker defender attackerOpt isWindup
    let margin = defenseHits - attackHits
    { CanSeizeVor = margin >= threshold
      Threshold = threshold
      Margin = margin
      DefenderHits = defenseHits
      AttackerHits = attackHits }

  /// Evaluates whether a defender in the Nach can seize the Vor via an Indes counter-timing
  let evaluateIndes (defender: Combatant) (defenseHits: int) (attackHits: int) : IndesEvaluation =
    evaluateIndesWithAttacker defender None false defenseHits attackHits

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
      |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 15 })

    // Attack is disrupted if attacker collapsed, died, or if finesse disparity significantly overwhelms reflex (>= 1.6x)
    let isDisrupted =
      updatedAttacker.Health.IsDepleted
      || float defFinesse >= float atkReflex * 1.60
      || (defFinesse >= atkReflex && roller 1 100 <= 35)

    let finalAttacker =
      if isDisrupted then
        { updatedAttacker with ComboTracker = updatedAttacker.ComboTracker.ResetCombo() }
        |> Combatant.yieldVor
      else
        updatedAttacker

    // Consume the one-time active ConcealedBlade from defender
    let updatedDefender =
      Combatant.removeActivePreparation PreparationType.ConcealedBlade defender
      |> fun d -> if isDisrupted then Combatant.seizeVor d else d

    let evts = [
      CombatEvent.ConcealedBladeCounter(defender.Id, attacker.Id, punctureDmg, isDisrupted)
      CombatEvent.DamageApplied {
        TargetId = attacker.Id
        Plane = Physical
        // Same rule as the resolver's damage events: report what the pool actually lost, not overkill.
        Amount = Math.Min(punctureDmg, attacker.Health.Current)
        IsCritical = false
        IsArmorCompromised = false
      }
      if isDisrupted then
        CombatEvent.TempoShifted(defender.Id, attacker.Id, "Incoming strike interrupted from the Nach by quick-draw Concealed Blade; seized the Vor!")
        CombatEvent.ComboReset(attacker.Id, "Incoming strike interrupted and defused by quick-draw Concealed Blade!")
    ]

    updatedDefender, finalAttacker, evts, isDisrupted

  /// Evaluates Indes tempo seizure when defense hits significantly exceed attack hits, factoring in wind-up commitment
  let resolveIndesOpportunityWithWindup
    (defender: Combatant)
    (attacker: Combatant)
    (isWindup: bool)
    (contest: ContestResult)
    : Combatant * Combatant * CombatEvent list * bool =
    let eval = Indes.evaluateIndesWithAttacker defender (Some attacker) isWindup contest.Defender.TotalHits contest.Attacker.TotalHits
    if eval.CanSeizeVor then
      let updatedDef =
        defender
        |> Combatant.addStacks 1
        |> Combatant.seizeVor
      let updatedAtk =
        attacker
        |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 10 })
        |> Combatant.yieldVor
        |> fun a -> { a with ComboTracker = a.ComboTracker.ResetCombo() }
      let evts = [
        CombatEvent.IndesSeized(defender.Id, attacker.Id, eval.Threshold, eval.Margin)
        CombatEvent.TempoShifted(defender.Id, attacker.Id, sprintf "Defender seized the Vor in Indes (margin %d >= threshold %d); offensive momentum arrested." eval.Margin eval.Threshold)
        CombatEvent.ComboReset(attacker.Id, "Defender seized the Vor in Indes; offensive momentum arrested.")
      ]
      updatedDef, updatedAtk, evts, true
    else
      defender, attacker, [], false

  /// Evaluates Indes tempo seizure when defense hits significantly exceed attack hits
  let resolveIndesOpportunity
    (defender: Combatant)
    (attacker: Combatant)
    (contest: ContestResult)
    : Combatant * Combatant * CombatEvent list * bool =
    resolveIndesOpportunityWithWindup defender attacker false contest
