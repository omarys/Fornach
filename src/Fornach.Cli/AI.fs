namespace Fornach.Cli

open System
open Fornach.Domain
open Fornach.Engine

module AI =

  /// Selects the execution plane based on opponent collapse reason or depleted pool
  let private chooseExecutePlane (opponent: Combatant) : Plane =
    match opponent.Collapse with
    | CollapseState.Collapsed reason ->
      match reason with
      | SomaticAnoxia
      | Exsanguination
      | StanceFailure -> Physical
      | CatatonicStupor
      | ContradictionLock
      | HystericalMeltdown -> Mental
      | RecklessExposure ->
        if opponent.Health.Current < opponent.Morale.Current then Physical else Mental
    | CollapseState.Stable ->
      if opponent.Health.Current < opponent.Morale.Current then Physical else Mental

  /// Evaluates an actor's specialization and chooses the optimal defensive reset
  let private chooseRecovery (self: Combatant) : ActionIntent =
    let poise = self.GetStat Poise
    let composure = self.GetStat Composure
    if poise >= composure then
      RecoveryAction SteadyForm
    else
      RecoveryAction CenterMind

  /// Selects an offensive action intent based on the actor's strongest stat vector and discipline
  let private chooseOffensiveAttack (self: Combatant) (opponent: Combatant) : ActionIntent =
    let force = self.GetStat Force
    let finesse = self.GetStat Finesse
    let prowess = self.GetStat Prowess
    let intellect = self.GetStat Intellect
    let acuity = self.GetStat Acuity
    let acumen = self.GetStat Acumen

    // Risk threshold for gambits: only fire when reckless entropy is low
    let isGambit = self.Meters.Recklessness.Value < 25

    // Compare physical vs mental capabilities
    let bestPhysical = Math.Max(force, Math.Max(finesse, prowess))
    let bestMental = Math.Max(intellect, Math.Max(acuity, acumen))

    if bestPhysical >= bestMental then
      // Physical discipline
      if force >= finesse && force >= prowess then
        StandardAttack (ForceStrike isGambit)
      elif finesse >= prowess then
        StandardAttack (FinesseCadence isGambit)
      else
        StandardAttack (ProwessStrike isGambit)
    else
      // Mental discipline (select Arcane vs Social based on opponent armor or slight randomization)
      let isSocialPreferred = opponent.Armor.Current > 30 || opponent.Meters.Confusion.Value > 20
      if intellect >= acuity && intellect >= acumen then
        if isSocialPreferred then
          StandardAttack (AuthorityDecree isGambit)
        else
          StandardAttack (ArcaneCataclysm isGambit)
      elif acuity >= acumen then
        if isSocialPreferred then
          StandardAttack (GuileDeception isGambit)
        else
          StandardAttack (SynapticGlamour isGambit)
      else
        if isSocialPreferred then
          StandardAttack (AcumenInterrogation isGambit)
        else
          StandardAttack (RunicWardTrap isGambit)

  /// Top-level tactical decision evaluator for autonomous combatants
  let chooseIntent (self: Combatant) (opponent: Combatant) : ActionIntent =
    // 1. If opponent is in a Collapsed threshold state, seize the moment with an Execution finisher
    if opponent.IsExecuteEligible then
      ExecuteStrike (chooseExecutePlane opponent)

    // 2. If self is reaching dangerous entropy or status debuff levels, bleed Recklessness
    elif self.Meters.Recklessness.Value >= 40
         || self.Meters.Exhaustion.Value >= 65
         || self.Meters.Overwhelm.Value >= 65
         || self.Meters.CognitiveFatigue.Value >= 65
         || self.Meters.Confusion.Value >= 65
         || self.Meters.Provoke.Value >= 65 then
      chooseRecovery self

    // 3. Otherwise, launch an offensive strike leveraging primary attributes
    else
      chooseOffensiveAttack self opponent
