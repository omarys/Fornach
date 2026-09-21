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
    if self.ArcaneFocus = Discipline && self.ArcaneWard < 40 then
      RecoveryAction CenterMind
    elif poise >= composure then
      RecoveryAction SteadyForm
    else
      RecoveryAction CenterMind

  /// Selects an offensive action intent based on the actor's strongest stat vector and discipline
  let private chooseOffensiveAttack (self: Combatant) (opponent: Combatant) (surroundingOpponents: int) : ActionIntent =
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
      // Tactical Stance Evaluation:
      // When facing multiple opponents (surroundingOpponents >= 2):
      // - Prowess (Discipline Stance) is tactically superior: chains attacks with 0 Recklessness,
      //   builds Study Stacks for passive Ripostes/Disarms, and defuses flanks with Attacks of Opportunity.
      // - Force (Power Stance) is viable for high-Force combatants to Cleave multiple targets,
      //   provided Recklessness is safe enough to absorb disparity-scaled exposure.
      // - Finesse (Agility Stance) is strictly single-target dueling; a master shifts away from Agility when mobbed.
      if surroundingOpponents >= 2 then
        let wantsDiscipline =
          prowess >= 60 && (prowess >= int (float force * 0.70) || self.Meters.Recklessness.Value >= 20)
        let wantsPower =
          force >= 60 && not wantsDiscipline && self.Meters.Recklessness.Value < 30

        if wantsDiscipline && self.Stance <> CombatStance.DisciplineStance then
          ShiftStance CombatStance.DisciplineStance
        elif wantsPower && self.Stance <> CombatStance.PowerStance then
          ShiftStance CombatStance.PowerStance
        elif self.Stance = CombatStance.DisciplineStance then
          let oppPoise = opponent.GetStat Poise
          let canDisarm = prowess >= int (Math.Round(float oppPoise * 0.75))
          let reqDisarmStacks = Math.Max(3, int (Math.Ceiling((float oppPoise / Math.Max(1.0, float prowess)) * 3.5)))

          if canDisarm && self.StudyStacks >= reqDisarmStacks && opponent.WeaponCondition <> WeaponCondition.Broken then
            StandardAttack (MasterfulDisarm reqDisarmStacks)
          elif self.StudyStacks >= 3 then
            StandardAttack (CalculatedFlawStrike self.StudyStacks)
          else
            StandardAttack (ProwessStrike isGambit)
        elif self.Stance = CombatStance.PowerStance then
          StandardAttack (ForceStrike isGambit)
        else
          // If in AgilityStance or other, execute best available physical attack
          if force >= finesse && force >= prowess then
            StandardAttack (ForceStrike isGambit)
          elif finesse >= prowess then
            StandardAttack (FinesseCadence isGambit)
          else
            StandardAttack (ProwessStrike isGambit)
      else
        // 1-on-1 Duel: Standard stance selection based on primary attribute
        if force >= finesse && force >= prowess && self.Stance <> CombatStance.PowerStance && self.Meters.Recklessness.Value < 30 then
          ShiftStance CombatStance.PowerStance
        elif finesse >= force && finesse >= prowess && self.Stance <> CombatStance.AgilityStance && self.Meters.Recklessness.Value < 30 then
          ShiftStance CombatStance.AgilityStance
        elif prowess >= force && prowess >= finesse && self.Stance <> CombatStance.DisciplineStance && self.Meters.Recklessness.Value < 30 then
          ShiftStance CombatStance.DisciplineStance
        elif force >= finesse && force >= prowess then
          StandardAttack (ForceStrike isGambit)
        elif finesse >= prowess then
          StandardAttack (FinesseCadence isGambit)
        else
          // Discipline: Evaluate Dedicated Gambits vs Standard Strike
          let oppPoise = opponent.GetStat Poise
          let canDisarm = prowess >= int (Math.Round(float oppPoise * 0.75))
          let reqDisarmStacks = Math.Max(3, int (Math.Ceiling((float oppPoise / Math.Max(1.0, float prowess)) * 3.5)))

          if canDisarm && self.StudyStacks >= reqDisarmStacks && opponent.WeaponCondition <> WeaponCondition.Broken then
            StandardAttack (MasterfulDisarm reqDisarmStacks)
          elif self.StudyStacks >= 3 then
            StandardAttack (CalculatedFlawStrike self.StudyStacks)
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
          // Guile/Trickery specialist: if without clones, conjure mirror decoys first!
          if self.MirrorClones = 0 then
            StandardAttack (MirrorIllusion isGambit)
          else
            StandardAttack (SynapticGlamour isGambit)
      else
        if isSocialPreferred then
          StandardAttack (AcumenInterrogation isGambit)
        else
          // Defensive/CC specialist: if mobbed or opponent has combo momentum, disorient them! If ward low, erect ward!
          if surroundingOpponents >= 2 || opponent.ComboTracker.ConsecutiveHits >= 2 then
            StandardAttack (DisorientingShockwave isGambit)
          elif self.ArcaneWard < 25 then
            StandardAttack (RunicWardTrap isGambit)
          else
            StandardAttack (DisorientingShockwave isGambit)

  /// Top-level tactical decision evaluator for autonomous combatants with surrounding opponent count
  let chooseIntentWithContext (self: Combatant) (opponent: Combatant) (surroundingOpponents: int) : ActionIntent =
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

    // 3. Otherwise, launch an offensive strike leveraging primary attributes and stance mechanics
    else
      chooseOffensiveAttack self opponent surroundingOpponents

  /// Top-level tactical decision evaluator for autonomous combatants in 1-on-1 duels
  let chooseIntent (self: Combatant) (opponent: Combatant) : ActionIntent =
    chooseIntentWithContext self opponent 1

  /// Selects the optimal target from a group of opponents:
  /// 1. Prioritize Execute-eligible targets to eliminate an action economy threat immediately.
  /// 2. Prioritize targets closest to collapse (status meters >= 75%).
  /// 3. Prioritize targets with lowest current vitality to focus fire and thin the herd.
  let chooseGroupTarget (self: Combatant) (opponents: Combatant list) : Combatant =
    match opponents with
    | [] -> failwith "Cannot choose target from empty opponents list"
    | [ single ] -> single
    | list ->
      match list |> List.tryFind (fun m -> m.IsExecuteEligible) with
      | Some target -> target
      | None ->
        let nearCollapse =
          list
          |> List.filter (fun m ->
            m.Meters.Exhaustion.Value >= 75
            || m.Meters.Overwhelm.Value >= 75
            || m.Meters.Frustration.Value >= 75
            || m.Meters.CognitiveFatigue.Value >= 75
            || m.Meters.Confusion.Value >= 75
            || m.Meters.Provoke.Value >= 75)
        match nearCollapse with
        | target :: _ -> target
        | [] ->
          list |> List.minBy (fun m -> m.Health.Current + m.Morale.Current)
