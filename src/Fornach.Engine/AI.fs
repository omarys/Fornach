namespace Fornach.Engine

open System
open Fornach.Domain
open Fornach.Engine

module AI =

  /// A ruleset-supplied behaviour for one named combatant: the point at which it breaks off to recover,
  /// and the press it commits to when it decides to attack. The engine owns neither -- it looks the
  /// combatant's `Policy` up in the table its caller supplies and keeps its own generic discipline when
  /// the table registers nothing, so a new setting hands the engine its signature moves without the
  /// engine learning what any of them mean.
  type AttackPolicy =
    { /// Recklessness at or above which this combatant recovers rather than pressing.
      RecoveryThreshold: int
      /// The attack to commit to, given the actor and the target it is facing.
      ChooseAttack: Combatant -> Combatant -> ActionIntent }

  /// What a setting registers so the engine can resolve a combatant's `Policy`.
  type PolicyTable = Map<PolicyId, AttackPolicy>

  /// Selects the execution plane based on the actor's operative discipline
  let private chooseExecutePlane (actor: Combatant) : Plane =
    actor.Plane

  /// Points held back from the crit cap when budgeting a gamble. The budget only sees the spike the
  /// actor is about to pay; a foe's own gambits, a tincture and the environment all add on top, so a
  /// press that lands exactly on the cap is one point of bad luck from fighting with every defence
  /// quartered. Measured: a prologue caster pressing to the line died with its Morale untouched.
  let recklessnessHeadroom = 10

  /// The Recklessness past which the crit cap opens: every incoming hit lands as a crit and a quarter
  /// of the actor's defence goes with it (ADR 0009), so a recommendation that would spend its actor
  /// past this line is recommending the exposure the meter exists to threaten.
  let critCap = 75

  /// A player past this much Recklessness is one clean hit from the crit cap, so the recommendation
  /// sheds instead of pressing: the shield hold bleeds the meter hardest and raises the plating that
  /// answers the exposure.
  let shieldHoldAt = 60

  /// The shield hold is the Recklessness shed alone: it bleeds Recklessness hardest, but leaves
  /// Frustration, Confusion, Cognitive Fatigue and Provoke untouched and restores no Morale. A
  /// combatant shedding the wrong meter stalls at the recovery gate forever, so any other meter this
  /// high routes to Steady Form, which bleeds every one of them.
  let steadyFormAt = 50

  /// Evaluates an actor's specialization and chooses the optimal defensive reset. A physical combatant
  /// that has spent the Recklessness budget into the shield-hold band sheds with the shield: it bleeds
  /// the meter hardest and raises the plating that answers the crit cap, where Steady Form bleeds every
  /// meter a little and answers nothing.
  let private chooseRecovery (self: Combatant) : ActionIntent =
    // SteadyForm drains Recklessness, Exhaustion, Frustration and Frustration, but not
    // CognitiveFatigue, Confusion or Provoke. Those three also trip the recovery gate, so routing
    // a physical actor to SteadyForm on mental pressure locks it out permanently: the offending
    // meter never falls back under the threshold, so every later turn is another no-op recovery.
    // A single Arcane cast puts a physical combatant at ~62 CognitiveFatigue against a gate of 65.
    let mentalPressure =
      self.Meters.CognitiveFatigue.Value >= 65
      || self.Meters.Confusion.Value >= 65
      || self.Meters.Provoke.Value >= 65

    let otherPressure =
      self.Meters.Exhaustion.Value >= steadyFormAt
      || self.Meters.Frustration.Value >= steadyFormAt
      || self.Meters.CognitiveFatigue.Value >= steadyFormAt
      || self.Meters.Confusion.Value >= steadyFormAt
      || self.Meters.Provoke.Value >= steadyFormAt

    if self.Plane <> Physical then
      // CenterMind drains mental meters and Recklessness, but not Exhaustion. A mental actor
      // tripping the recovery gate on physical Exhaustion must take SteadyForm, or the lockout
      // repeats every turn until somatic collapse.
      if self.Meters.Exhaustion.Value >= 65 then
        RecoveryAction SteadyForm
      else
        RecoveryAction CenterMind
    elif mentalPressure then
      // Steady Form cannot drain the three mental meters, and they are what tripped the gate: a
      // physical actor under mental pressure must centre, or the lockout repeats every turn.
      RecoveryAction CenterMind
    elif self.Meters.Recklessness.Value > shieldHoldAt && not otherPressure then
      RecoveryAction RaiseShield
    else
      RecoveryAction SteadyForm

  /// A foe is dangerous when the exchange outlasts a burst. The estimator already predicts damage per
  /// The basic strikes this combatant's plane may legally throw, through the resolver's own gate: a
  /// refused attack is not downgraded, it costs the whole turn, so it is never a candidate.
  ///
  /// Only the strikes the generic discipline already chooses between. `ProwessStrike` and `ShieldBash`
  /// are excluded deliberately: `estimate` scores each attack from the stance that attack wants, and
  /// those two carry their own stance multiplier on top, so they would win every comparison on the
  /// multiplier rather than on the combatant's stats.
  let basicAttacks (self: Combatant) : AttackClassification list =
    let candidates =
      match self.Plane with
      | Physical -> [ ForceStrike false; FinesseCadence false ]
      | Mental -> [ SynapticGlamour false; ArcaneCataclysm false; AcumenInterrogation false ]

    candidates |> List.filter (fun attack -> attack.IsAllowedFor self)

  /// The gambit form of a basic strike: the same contest, bought with Recklessness.
  let asGambit (attack: AttackClassification) : AttackClassification =
    match attack with
    | ForceStrike _ -> ForceStrike true
    | FinesseCadence _ -> FinesseCadence true
    | ProwessStrike _ -> ProwessStrike true
    | ShieldBash _ -> ShieldBash true
    | SynapticGlamour _ -> SynapticGlamour true
    | ArcaneCataclysm _ -> ArcaneCataclysm true
    | AcumenInterrogation _ -> AcumenInterrogation true
    | other -> other

  /// The player's own attack: the hardest-hitting action the Recklessness budget can pay for, chosen
  /// from the strike that actually wins its bind. Every legal basic strike is considered in both its
  /// plain and its gambit form, the forms whose spike would land the actor on the crit cap are dropped,
  /// and what remains is ranked the way the menu ranks it — contest margin first, then expected damage.
  /// Ranking on damage alone threw a strike the guard outclasses (measured: a Disciplined foe took a
  /// -254-margin Interrogation while a +114 Cataclysm sat unthrown), so the margin leads and damage
  /// breaks ties. A plain strike that already finishes the foe wins outright, because Recklessness
  /// spent on a corpse is exposure for nothing, and equal damage resolves to the cheaper action.
  /// Opposition keeps the generic discipline; this keys on `PlayerStyle`, the flag the ramp, the
  /// estimator and the mental-defence relief already read.
  let chooseEstimatedAttack (self: Combatant) (opponent: Combatant) : ActionIntent option =
    if not self.PlayerStyle then
      None
    else
      match basicAttacks self with
      | [] -> None
      | basics ->
        let reck = self.Meters.Recklessness.Value
        let oppHp = float opponent.Health.Current
        let mutable lethalPick : AttackClassification option = None
        let mutable hasBest = false
        let mutable bestAttack = Unchecked.defaultof<AttackClassification>
        let mutable bestNegMargin = 0.0
        let mutable bestNegDmg = 0.0
        let mutable bestSpike = 0

        let consider (attack: AttackClassification) =
          let s = ActionResolver.gambitReckSpike attack
          if reck + s + recklessnessHeadroom < critCap then
            let d = AttackEstimator.estimate self opponent attack
            if lethalPick.IsNone && s = 0 && d >= oppHp then
              lethalPick <- Some attack
            elif lethalPick.IsNone then
              let negM = -float (AttackEstimator.contestMargin self opponent attack)
              let negD = -d
              if not hasBest
                 || negM < bestNegMargin
                 || (negM = bestNegMargin && (negD < bestNegDmg || (negD = bestNegDmg && s < bestSpike))) then
                hasBest <- true
                bestAttack <- attack
                bestNegMargin <- negM
                bestNegDmg <- negD
                bestSpike <- s

        for attack in basics do
          if lethalPick.IsNone then
            consider attack
          if lethalPick.IsNone then
            consider (asGambit attack)

        // First-class Stack Spenders & ShieldBash (Physical plane):
        // Considered after basic strikes so a lethal plain strike still saves stacks/Recklessness,
        // while spenders (>= 3 stacks, 0 Recklessness spike) and ShieldBash (against high-Recklessness,
        // Discipline-guarding, or combo-ramping foes) compete directly on margin and expected damage.
        if self.Plane = Physical && lethalPick.IsNone then
          if Combatant.stacksIn Vector.Power self >= 3 && ForceInsightStrike.IsAllowedFor self then
            consider ForceInsightStrike
          if lethalPick.IsNone && Combatant.stacksIn Vector.Agility self >= 3 && FinesseInsightStrike.IsAllowedFor self then
            consider FinesseInsightStrike
          if lethalPick.IsNone && Combatant.stacksIn Vector.Discipline self >= 3 && CalculatedFlawStrike.IsAllowedFor self then
            consider CalculatedFlawStrike
          if lethalPick.IsNone && opponent.WeaponCondition <> WeaponCondition.Broken then
            let off = self.GetStat Prowess
            let def = opponent.GetGuard(Plane.Physical, false)
            let isPossible = off >= int (Math.Round(float def * 0.75))
            let required = Math.Max(3, int (Math.Ceiling((float def / Math.Max(1.0, float off)) * 3.5)))
            if isPossible && required <= Combatant.StackCap && Combatant.stacksIn Vector.Discipline self >= required then
              let disarm = MasterfulDisarm required
              if disarm.IsAllowedFor self then
                consider disarm

          let bashWarranted =
            opponent.Meters.Recklessness.Value >= 40
            || opponent.Stance = CombatStance.DisciplineStance
            || opponent.ComboTracker.ConsecutiveHits >= 2
          if lethalPick.IsNone && bashWarranted && (ShieldBash false).IsAllowedFor self then
            consider (ShieldBash false)
            if lethalPick.IsNone then
              consider (ShieldBash true)

        match lethalPick with
        | Some plainKill -> Some(StandardAttack plainKill)
        | None ->
          if hasBest then Some(StandardAttack bestAttack)
          else failwith "The input sequence was empty."

  /// Selects an offensive action intent for a combatant with no registered policy: species instinct
  /// first, then the discipline that suits the plane it actually resolves on.
  let private chooseGenericAttack (self: Combatant) (opponent: Combatant) (surroundingOpponents: int) : ActionIntent =
    let force = self.GetStat Force
    let finesse = self.GetStat Finesse
    let prowess = self.GetStat Prowess
    let intellect = self.GetStat Intellect
    let acuity = self.GetStat Acuity
    let acumen = self.GetStat Prowess

    // Risk threshold for gambits: only fire when reckless entropy is low
    let isGambit = self.Meters.Recklessness.Value < 25

    // Compare physical vs mental capabilities
    let bestPhysical = Math.Max(force, Math.Max(finesse, prowess))
    let bestMental = Math.Max(intellect, Math.Max(acuity, acumen))

    // -------------------------------------------------------------------------
    // Monster Species Instinct Profiles (ADR 0004)
    // -------------------------------------------------------------------------
    if self.MonsterFamily.IsSome then
      // A species instinct names the vector that suits its temperament, and the temperament is written
      // for the archetype rather than for the plane the combatant actually resolves on. ADR 0009 zeroes
      // Prowess on non-humanoids, which by itself can tip a Warrior-class monster over the mental
      // threshold, so the dragon lunges, the sentinel swings, the wraith sings -- and the resolver
      // refuses all three. `IsAllowedFor` has no fallback: a refused attack is not downgraded, it costs
      // the whole turn. Commit through the same gate the resolver applies, and fall back to the basic
      // strike of the plane the combatant really occupies rather than of the one it looks like.
      let commit (preferred: AttackClassification) =
        if preferred.IsAllowedFor self then
          StandardAttack preferred
        else
          StandardAttack (if self.Plane = Mental then SynapticGlamour isGambit else ForceStrike isGambit)

      match self.MonsterFamily with
      | Some MonsterFamily.Beast ->
        // Beasts strike ferally, exploiting bleeding prey or lunging with agile cadences
        commit (if opponent.BleedStacks > 0 || finesse >= force then
                  FinesseCadence isGambit
                else
                  ForceStrike isGambit)
      | Some MonsterFamily.Construct ->
        // Constructs crush reckless or combo-ramping foes with heavy shield slams, otherwise stone blows
        if self.Plane = Physical && (opponent.Meters.Recklessness.Value >= 45 || opponent.ComboTracker.ConsecutiveHits >= 2) && (ShieldBash isGambit).IsAllowedFor self then
          StandardAttack (ShieldBash isGambit)
        else
          commit (ForceStrike isGambit)
      | Some MonsterFamily.UndeadWraith ->
        // Wraiths weave mirror decoys at Veteran+ tier when exposed, otherwise erode Morale with glamours
        if self.Plane = Mental && self.MirrorClones = 0 && ProgressionScale.levelToTier self.Level <> CombatTier.Novice && (MirrorIllusion false).IsAllowedFor self then
          StandardAttack (MirrorIllusion false)
        else
          commit (SynapticGlamour isGambit)
      | Some MonsterFamily.Aberration ->
        // Aberrations disrupt combo momentum with shockwaves, or warp focus with cataclysms and glamours
        if self.Plane = Mental && opponent.ComboTracker.ConsecutiveHits >= 2 && (DisorientingShockwave isGambit).IsAllowedFor self then
          StandardAttack (DisorientingShockwave isGambit)
        else
          commit (if intellect >= acuity then ArcaneCataclysm isGambit else SynapticGlamour isGambit)
      | Some MonsterFamily.GriefManifestation ->
        // Manifestations have no Prowess, so they lean on Power/Agility vectors and disrupt combo surges
        if self.Plane = Mental && acuity > intellect then
          commit (SynapticGlamour isGambit)
        else
          commit (ArcaneCataclysm isGambit)
      | _ -> commit (ForceStrike isGambit)
    elif self.Plane = Physical then
      // Tactical Stance & Attack Evaluation (ADR 0009):
      // When outnumbered (surroundingOpponents >= 2) and Prowess is viable, shift to Discipline Stance for immediate bastion stabilization.
      // In 1-on-1 exchanges, attacks fluidly transition stance after the swing, so no standalone ShiftStance turn is wasted.
      let wantsDiscipline =
        opponent.Plane = Physical
        && surroundingOpponents >= 2
        && prowess >= 60
        && (prowess >= int (float force * 0.70) || self.Meters.Recklessness.Value >= 40)
        && (self.Class <> CharacterClass.Fighter || self.Meters.Recklessness.Value >= 40)

      if wantsDiscipline && self.Stance <> CombatStance.DisciplineStance then
        ShiftStance CombatStance.DisciplineStance
      elif force >= finesse then
        StandardAttack (ForceStrike isGambit)
      else
        StandardAttack (FinesseCadence isGambit)
    else
      // Mental discipline: attacks fluidly thread their Complex Form after the strike, so no standalone ThreadComplexForm turn is wasted.
      if intellect >= acuity && intellect >= acumen then
        let isSafeForOverchannel = isGambit && self.ComplexForm <> Some ComplexForm.AegisLattice
        StandardAttack (ArcaneCataclysm isSafeForOverchannel)
      elif acuity >= acumen then
        // Guile/Trickery specialist: if without clones and not passively generating them, conjure mirror decoys first!
        if self.MirrorClones = 0 && self.ComplexForm <> Some ComplexForm.PhantasmalDiffusion then
          StandardAttack (MirrorIllusion isGambit)
        else
          StandardAttack (SynapticGlamour isGambit)
      else
        // Defensive/CC specialist: if mobbed or opponent has combo momentum, disorient them! If ward low, erect ward!
        if surroundingOpponents >= 2 || opponent.ComboTracker.ConsecutiveHits >= 2 then
          StandardAttack (DisorientingShockwave isGambit)
        elif self.ArcaneWard < 25 then
          StandardAttack (RunicWardTrap isGambit)
        else
          StandardAttack (DisorientingShockwave isGambit)

  /// Selects an offensive action intent, deferring to the ruleset's registered policy for this combatant
  /// when there is one and falling back to the engine's generic discipline when there is not.
  let private chooseOffensiveAttack
    (policy: AttackPolicy option)
    (self: Combatant)
    (opponent: Combatant)
    (surroundingOpponents: int)
    : ActionIntent =
    match policy with
    | Some registered -> registered.ChooseAttack self opponent
    | None ->
      let generic = chooseGenericAttack self opponent surroundingOpponents

      // The player's own strike is the estimator's, but only where the generic discipline had already
      // decided to attack: stance shifts and form threading still belong to it.
      match generic, chooseEstimatedAttack self opponent with
      | StandardAttack _, Some estimated -> estimated
      | _ -> generic

  /// Selects the optimal active defense in the Nach based on telegraphed attack vector and discipline reading (ADR 0009)
  ///
  /// The plane is read off `self`, never the opponent: a discipline may only take the defences its own plane
  /// fields. Keying on the opponent's plane handed mental defences to a Fighter facing a mental foe, and those
  /// set an aetheric form on a character that cannot hold one.
  let chooseActiveDefense (self: Combatant) (opponent: Combatant) : DefenseClassification =
    if self.Plane = Mental then
      let oppPower = opponent.GetCombatStat StatId.Intellect
      let oppAgility = opponent.GetCombatStat StatId.Acuity
      if self.IsHumanoid && self.GetStat StatId.Prowess >= opponent.GetStat StatId.Poise then
        if oppPower >= oppAgility then ResolveAnchor false
        else IntuitionPerception false
      else
        if self.GetCombatStat StatId.Resolve >= self.GetCombatStat StatId.Intuition then ResolveAnchor false
        else IntuitionPerception false
    else
      let oppPower = opponent.GetCombatStat StatId.Force
      let oppAgility = opponent.GetCombatStat StatId.Finesse
      if self.IsHumanoid && self.GetStat StatId.Prowess >= opponent.GetStat StatId.Poise then
        if oppPower >= oppAgility then FortitudeGuard false
        else ReflexSlip false
      else
        if self.GetCombatStat StatId.Fortitude >= self.GetCombatStat StatId.Reflex then FortitudeGuard false
        else ReflexSlip false

  /// The behaviour the caller's ruleset registered for this combatant, if any.
  let private policyFor (policies: Map<PolicyId, AttackPolicy>) (self: Combatant) : AttackPolicy option =
    self.Policy |> Option.bind (fun id -> Map.tryFind id policies)

  /// Top-level tactical decision evaluator for autonomous combatants with surrounding opponent count.
  /// `policies` is the ruleset's table of named behaviours: whatever it registers is what its combatants
  /// do, and a combatant it registers nothing for falls back to the generic discipline.
  let private chooseIntentCore
    (policies: Map<PolicyId, AttackPolicy>)
    (self: Combatant)
    (opponent: Combatant)
    (surroundingOpponents: int)
    : ActionIntent =
    let policy = policyFor policies self
    // Where this combatant breaks off to recover. In 1-on-1 duels, 55 sits close to shieldHoldAt (60)
    // so combatants press through active exchanges instead of dropping into recovery loops every 3 rounds.
    // In multi-opponent swarms (surroundingOpponents >= 2), cleaves and chains add substantial per-target
    // Recklessness spikes in a single turn, so hold extra headroom (40) to avoid spiking straight into
    // RecklessExposure collapse. A registered Policy still overrides either default.
    let defaultRecoveryAt = if surroundingOpponents >= 2 then 40 else 55
    let recoveryAt =
      policy
      |> Option.map (fun registered -> registered.RecoveryThreshold)
      |> Option.defaultValue defaultRecoveryAt

    if self.Meters.Recklessness.Value >= 75
       || self.Meters.Exhaustion.Value >= 75
       || self.Meters.Frustration.Value >= 75
       || self.Meters.CognitiveFatigue.Value >= 75
       || self.Meters.Confusion.Value >= 75
       || self.Meters.Provoke.Value >= 75 then
      chooseRecovery self
    elif not self.PlayerStyle && self.MonsterFamily.IsNone && policy.IsNone then
      let tier = ProgressionScale.levelToTier self.Level
      if tier = CombatTier.Novice then
        // Early-game Novice humanoid opposition keeps a straightforward basic strike for pacing.
        if self.Plane = Mental then
          let intellect = self.GetStat Intellect
          let acuity = self.GetStat Acuity
          if intellect >= acuity then StandardAttack (ArcaneCataclysm false)
          else StandardAttack (SynapticGlamour false)
        else
          let force = self.GetStat Force
          let finesse = self.GetStat Finesse
          if force >= finesse then StandardAttack (ForceStrike false)
          else StandardAttack (FinesseCadence false)
      else
        // Veteran+ humanoid opposition fights with real tactical discipline: executes collapsed foes,
        // recovers when strained, uses ShieldBash against reckless foes, and commits to gambits when
        // Recklessness < 25 (active guards in the Nach are handled via `chooseActiveDefense`).
        if opponent.IsExecuteEligible then
          ExecuteStrike (chooseExecutePlane self)
        elif self.Meters.Recklessness.Value >= recoveryAt
             || self.Meters.Exhaustion.Value >= 65
             || self.Meters.Frustration.Value >= 65
             || self.Meters.CognitiveFatigue.Value >= 65
             || self.Meters.Confusion.Value >= 65
             || self.Meters.Provoke.Value >= 65 then
          chooseRecovery self
        else
          let gambitCap = if surroundingOpponents >= 2 then 10 else 25
          let isGambit = self.Meters.Recklessness.Value < gambitCap
          if self.Plane = Mental then
            let intellect = self.GetStat Intellect
            let acuity = self.GetStat Acuity
            if opponent.ComboTracker.ConsecutiveHits >= 2 && (DisorientingShockwave isGambit).IsAllowedFor self then
              StandardAttack (DisorientingShockwave isGambit)
            elif intellect >= acuity then
              StandardAttack (ArcaneCataclysm isGambit)
            else
              StandardAttack (SynapticGlamour isGambit)
          else
            let force = self.GetStat Force
            let finesse = self.GetStat Finesse
            if surroundingOpponents <= 1 && opponent.Meters.Recklessness.Value >= 45 && (ShieldBash isGambit).IsAllowedFor self then
              StandardAttack (ShieldBash isGambit)
            elif force >= finesse then
              StandardAttack (ForceStrike isGambit)
            else
              StandardAttack (FinesseCadence isGambit)
    else

    // 0. Tactical Preparations Deployment
    let prepIntentOpt =
      if surroundingOpponents >= 2 then
        // Multi-opponent Swarm:
        // Carrying a Berserk Tincture, enraging deadens incoming physical damage and unleashes Frenzy strikes immediately
        let berserkPrep =
          if not (self.HasActivePreparation PreparationType.BerserkTincture) then
            self.Preparations
            |> List.tryFind (fun slot -> slot.Type = PreparationType.BerserkTincture && slot.Type.Stance = self.Stance && slot.RemainingUses > 0)
            |> Option.bind (fun slot ->
              if self.Health.Current > 30 && self.Meters.Recklessness.Value < 75 then
                Some (DeployPreparation (PreparationType.BerserkTincture, None))
              else None)
          else None

        match berserkPrep with
        | Some p -> Some p
        | None ->
          // Deploy available Crowd Control preparation if not active
          let crowdPrep =
            self.Preparations
            |> List.tryFind (fun slot ->
              slot.Category = CrowdControl
              && slot.Type.Stance = self.Stance
              && slot.RemainingUses > 0
              && (match self.ActivePreparations |> List.tryFind (fun a -> a.Type = slot.Type) with
                  | Some active -> active.DurationTurns <= 1
                  | None -> true))
            |> Option.bind (fun slot ->
              match slot.Type with
              | PreparationType.HeraldicTreatise when Combatant.stacksIn Vector.Discipline self >= Combatant.StackCap -> None
              | prepType -> Some (DeployPreparation (prepType, None)))

          match crowdPrep with
          | Some p -> Some p
          | None ->
            // In swarms, also deploy BerserkTincture to enrage or AegisOfRetribution if available
            self.Preparations
            |> List.tryFind (fun slot ->
              slot.RemainingUses > 0
              && slot.Type.Stance = self.Stance
              && not (self.HasActivePreparation slot.Type)
              && (slot.Type = PreparationType.BerserkTincture || slot.Type = PreparationType.AegisOfRetribution))
            |> Option.bind (fun slot ->
              match slot.Type with
              | PreparationType.BerserkTincture when self.Health.Current < (self.Health.Maximum / 3) || self.Meters.Recklessness.Value >= 70 -> None
              | prepType -> Some (DeployPreparation (prepType, None)))
      else
        // 1-on-1 Duel: deploy available Single-Target preparation or signature defensive barrier
        self.Preparations
        |> List.tryFind (fun slot ->
          (slot.Category = SingleTargetDuel || slot.Type = PreparationType.AegisOfRetribution)
          && slot.Type.Stance = self.Stance
          && slot.RemainingUses > 0
          && not (self.HasActivePreparation slot.Type))
        |> Option.bind (fun slot ->
          match slot.Type with
          | PreparationType.SocraticDossier when opponent.Meters.Recklessness.Value < 25 -> None
          | PreparationType.BerserkTincture when self.Health.Current < (self.Health.Maximum / 3) || self.Meters.Recklessness.Value >= 60 -> None
          | PreparationType.BerserkTincture -> Some (DeployPreparation (slot.Type, None))
          | PreparationType.AegisOfRetribution -> Some (DeployPreparation (PreparationType.AegisOfRetribution, None))
          | prepType -> Some (DeployPreparation (prepType, Some opponent.Id)))

    // A combatant in the Nach is reacting, not setting up: it strikes now to win the initiative
    // back (a landed strike seizes the Vor) instead of spending the turn deploying. Only the side
    // holding the Vor gets to choose its ground, which is what makes losing it cost something.
    match prepIntentOpt with
    | Some prepIntent when not self.IsInNach -> prepIntent
    | _ ->
      // 1. If opponent is in a Collapsed threshold state, seize the moment with an Execution finisher
      if opponent.IsExecuteEligible then
        ExecuteStrike (chooseExecutePlane self)

      // 2. If self is reaching dangerous entropy or status debuff levels, bleed Recklessness
      elif self.Meters.Recklessness.Value >= recoveryAt
           || self.Meters.Exhaustion.Value >= 65
           || self.Meters.Frustration.Value >= 65
           || self.Meters.CognitiveFatigue.Value >= 65
           || self.Meters.Confusion.Value >= 65
           || self.Meters.Provoke.Value >= 65 then
        chooseRecovery self

      // 3. Otherwise, launch an offensive strike leveraging primary attributes and stance mechanics
      else
        chooseOffensiveAttack policy self opponent surroundingOpponents

  /// Top-level tactical decision evaluator for autonomous combatants with surrounding opponent count,
  /// against the engine's generic discipline with no ruleset policies registered.
  let chooseIntentWithContext (self: Combatant) (opponent: Combatant) (surroundingOpponents: int) : ActionIntent =
    chooseIntentCore Map.empty self opponent surroundingOpponents

  /// Top-level tactical decision evaluator for autonomous combatants in 1-on-1 duels
  let chooseIntent (self: Combatant) (opponent: Combatant) : ActionIntent =
    chooseIntentWithContext self opponent 1

  /// Top-level tactical decision evaluator that resolves each combatant's registered policy through the
  /// ruleset's table. Anything the table does not name behaves exactly as `chooseIntent` does.
  let chooseIntentUsing (policies: Map<PolicyId, AttackPolicy>) (self: Combatant) (opponent: Combatant) : ActionIntent =
    chooseIntentCore policies self opponent 1

  /// Swarm-aware variant of `chooseIntentUsing`.
  let chooseIntentWithContextUsing
    (policies: Map<PolicyId, AttackPolicy>)
    (self: Combatant)
    (opponent: Combatant)
    (surroundingOpponents: int)
    : ActionIntent =
    chooseIntentCore policies self opponent surroundingOpponents

  /// Selects the optimal target from a group of opponents:
  /// 1. Prioritize Execute-eligible targets to eliminate an action economy threat immediately.
  /// 2. Prioritize targets closest to collapse (status meters >= 75%).
  /// 3. Prioritize targets with lowest current vitality to focus fire and thin the herd.
  let chooseGroupTarget (self: Combatant) (opponents: Combatant list) : Combatant =
    match opponents with
    | [] -> failwith "Cannot choose target from empty opponents list"
    | [ single ] -> single
    | list ->
      // 1. Any combatant seizes execution on collapsed targets
      match list |> List.tryFind (fun m -> m.IsExecuteEligible) with
      | Some target -> target
      | None ->
        // 2. Species Instinct Target Profiles
        match self.MonsterFamily with
        | Some MonsterFamily.Beast ->
          // Beasts hunt bleeding prey first; otherwise focus down lowest HP target
          let bleeding = list |> List.tryFind (fun m -> m.BleedStacks > 0)
          match bleeding with
          | Some target -> target
          | None -> list |> List.minBy (fun m -> m.Health.Current)
        | Some MonsterFamily.UndeadWraith ->
          // Wraiths and Specters prioritize targets with lowest current Morale
          list |> List.minBy (fun m -> m.Morale.Current)
        | Some MonsterFamily.Construct ->
          // Constructs hold choke points and crush heavily armored frontline combatants
          list |> List.maxBy (fun m -> m.Armor.Current)
        | Some MonsterFamily.Aberration ->
          // Aberrations target chaotic entropy: combatants with highest Recklessness or Confusion
          list |> List.maxBy (fun m -> m.Meters.Recklessness.Value + m.Meters.Confusion.Value)
        | Some MonsterFamily.GriefManifestation ->
          // Grief manifestations target the combatant with highest Frustration
          list |> List.maxBy (fun m -> m.Meters.Frustration.Value)
        | None ->
          // Standard humanoid targeting: near-collapse -> lowest combined vitality
          let nearCollapse =
            list
            |> List.filter (fun m ->
              m.Meters.Exhaustion.Value >= 75
              || m.Meters.Frustration.Value >= 75
              || m.Meters.Frustration.Value >= 75
              || m.Meters.CognitiveFatigue.Value >= 75
              || m.Meters.Confusion.Value >= 75
              || m.Meters.Provoke.Value >= 75)
          match nearCollapse with
          | target :: _ -> target
          | [] ->
            list |> List.minBy (fun m -> m.Health.Current + m.Morale.Current)

  /// Evaluates whether an intelligent or elite enemy adapts to repetitive defensive postures.
  /// If the player repeats the exact same defense posture consecutively against an enemy of level >= 2 or humanoid,
  /// the enemy reads the pattern and gains a tactical advantage.
  let adaptToDefensiveRepetition (enemy: Combatant) (prevDefense: DefenseChoice option) (currentDefense: DefenseChoice option) : bool =
    match prevDefense, currentDefense with
    | Some (Guard d1), Some (Guard d2) when d1 = d2 && (enemy.Level >= 2 || enemy.IsHumanoid) -> true
    | _ -> false

  /// Feints: given an elite enemy's telegraphed intent, converts the attack vector to counter the player's anticipated defense
  let feintConvertVector (intent: ActionIntent) (anticipatedDefense: DefenseClassification) : ActionIntent =
    match intent, anticipatedDefense with
    | StandardAttack (ForceStrike isHeavy), FortitudeGuard _ ->
      // Telegraphed a Power blow, but if player turtles with FortitudeGuard, convert to slicing Finesse Cadence
      StandardAttack (FinesseCadence isHeavy)
    | StandardAttack (FinesseCadence isHeavy), ReflexSlip _ ->
      // Telegraphed an Agility blow, but if player slips/dodges, convert to crushing ForceStrike
      StandardAttack (ForceStrike isHeavy)
    | StandardAttack (ArcaneCataclysm isHeavy), ResolveAnchor _ ->
      // Telegraphed a Mental Power spell, but if player anchors Resolve, convert to Synaptic Glamour
      StandardAttack (SynapticGlamour isHeavy)
    | StandardAttack (SynapticGlamour isHeavy), IntuitionPerception _ ->
      // Telegraphed a Mental Agility glamour, but if player reads with Intuition, convert to Arcane Cataclysm
      StandardAttack (ArcaneCataclysm isHeavy)
    | other, _ -> other

  /// Adapts an enemy's chosen intent when the opponent repeats the same active guard
  /// (via `adaptToDefensiveRepetition` + `feintConvertVector`) or turtles behind
  /// consecutive `RaiseShield` / `Wait` actions, breaking infinite defensive counter loops.
  let adaptIntentToRepetition
    (enemy: Combatant)
    (intent: ActionIntent)
    (prevDefense: DefenseChoice option)
    (currentDefense: DefenseChoice option)
    (consecutiveShieldTurtles: int)
    : ActionIntent =
    if adaptToDefensiveRepetition enemy prevDefense currentDefense then
      match currentDefense with
      | Some (Guard d) -> feintConvertVector intent d
      | _ -> intent
    elif consecutiveShieldTurtles >= 2 && (enemy.Level >= 2 || enemy.IsHumanoid) then
      match intent with
      | StandardAttack (ForceStrike isHeavy) when enemy.Plane = Physical ->
        if (ShieldBash isHeavy).IsAllowedFor enemy then StandardAttack (ShieldBash isHeavy)
        else StandardAttack (FinesseCadence isHeavy)
      | StandardAttack (FinesseCadence isHeavy) when enemy.Plane = Physical ->
        if (ShieldBash isHeavy).IsAllowedFor enemy then StandardAttack (ShieldBash isHeavy)
        else intent
      | other -> other
    else
      intent
