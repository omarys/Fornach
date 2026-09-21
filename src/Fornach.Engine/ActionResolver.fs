namespace Fornach.Engine

open System
open Fornach.Domain

type DiceRoller = int -> int -> int

module ActionResolver =

  // =========================================================================
  // 1. Tiered Multiplier & Damage Application
  // =========================================================================

  /// Tiered NetHits multiplier mapping contest outcome to damage lethality
  let computeTierMultiplier (netHits: int) : float =
    if netHits <= 0 then 0.0
    elif netHits = 1 then 1.15
    elif netHits = 2 then 1.30
    elif netHits = 3 then 1.50
    elif netHits = 4 then 1.80
    elif netHits = 5 then 2.20
    elif netHits = 6 then 2.50
    elif netHits = 7 then 3.00
    elif netHits = 8 then 3.50
    else 4.00 + (float (netHits - 8) * 0.50)

  /// Applies pool damage, armor soak, and massive blow armor shredding
  let private applyDamage
    (plane: Plane)
    (rawAmount: int)
    (isCrit: bool)
    (target: Combatant)
    : Combatant * DamageEvent =
    match plane with
    | Physical ->
      let absorbed = int (float rawAmount * target.Armor.AbsorptionRatio)
      let actualDmg = Math.Max(1, rawAmount - absorbed)
      let updatedPool = target.Health.ApplyDelta -actualDmg

      // Massive blows automatically shred armor durability: shred = max 15 (damageDealt / 3)
      let updatedArmor =
        if isCrit || actualDmg >= 40 then
          let shredAmount = Math.Max(15, actualDmg / 3)
          target.Armor.Shred shredAmount
        else
          target.Armor

      let updatedTarget =
        { target with
            Health = updatedPool
            Armor = updatedArmor }

      let evt =
        { TargetId = target.Id
          Plane = Physical
          Amount = actualDmg
          IsCritical = isCrit
          IsArmorCompromised = updatedArmor.IsShredded || isCrit }

      updatedTarget, evt

    | Mental ->
      let actualDmg = Math.Max(1, rawAmount)
      let updatedPool = target.Morale.ApplyDelta -actualDmg
      let updatedTarget = { target with Morale = updatedPool }

      let evt =
        { TargetId = target.Id
          Plane = Mental
          Amount = actualDmg
          IsCritical = isCrit
          IsArmorCompromised = false }

      updatedTarget, evt

  // =========================================================================
  // 2. Equipment Hook Execution
  // =========================================================================

  let private applyTriggerEffect
    (effect: TriggerEffect)
    (sourceItemName: string)
    (owner: Combatant)
    (opponent: Combatant)
    : Combatant * Combatant * CombatEvent list =
    match effect with
    | InflictDebuff(targetIsSelf, meterName, amount) ->
      let recipient = if targetIsSelf then owner else opponent

      let updatedRecipient =
        recipient
        |> Combatant.updateMeters (fun m ->
          match meterName with
          | "Overwhelm" -> { m with Overwhelm = m.Overwhelm + amount }
          | "Exhaustion" -> { m with Exhaustion = m.Exhaustion + amount }
          | "Frustration" -> { m with Frustration = m.Frustration + amount }
          | "CognitiveFatigue" -> { m with CognitiveFatigue = m.CognitiveFatigue + amount }
          | "Confusion" -> { m with Confusion = m.Confusion + amount }
          | "Provoke" -> { m with Provoke = m.Provoke + amount }
          | "Recklessness" -> { m with Recklessness = m.Recklessness + amount }
          | _ -> m)

      let desc = sprintf "Inflicted +%d %s on %s" amount meterName updatedRecipient.Name
      let evt = CombatEvent.EquipmentProcTriggered(sourceItemName, updatedRecipient.Id, desc)

      if targetIsSelf then
        (updatedRecipient, opponent, [ evt ])
      else
        (owner, updatedRecipient, [ evt ])

    | BonusDamage(plane, amount) ->
      let updatedOpponent =
        if plane = Physical then
          { opponent with
              Health = opponent.Health.ApplyDelta -amount }
        else
          { opponent with
              Morale = opponent.Morale.ApplyDelta -amount }

      let desc = sprintf "Dealt %d bonus %A proc damage" amount plane
      let evt = CombatEvent.EquipmentProcTriggered(sourceItemName, opponent.Id, desc)
      (owner, updatedOpponent, [ evt ])

    | RestorePool(isHealth, amount) ->
      let updatedOwner =
        if isHealth then
          { owner with
              Health = owner.Health.ApplyDelta amount }
        else
          { owner with
              Morale = owner.Morale.ApplyDelta amount }

      let poolName = if isHealth then "Health" else "Morale"
      let desc = sprintf "Restored %d %s" amount poolName
      let evt = CombatEvent.EquipmentProcTriggered(sourceItemName, owner.Id, desc)
      (updatedOwner, opponent, [ evt ])

    | GainStudyStacks count ->
      let updatedOwner = owner |> Combatant.addStudyStacks count

      let desc =
        sprintf "Accumulated +%d Study / Insight Stacks (Total: %d)" count updatedOwner.StudyStacks

      let evt = CombatEvent.EquipmentProcTriggered(sourceItemName, owner.Id, desc)
      (updatedOwner, opponent, [ evt ])

    | FreeCounterStrike(plane, flatDmg) ->
      let updatedOpponent =
        if plane = Physical then
          { opponent with
              Health = opponent.Health.ApplyDelta -flatDmg }
        else
          { opponent with
              Morale = opponent.Morale.ApplyDelta -flatDmg }

      let desc =
        sprintf "Retaliated with a counter-strike for %d %A damage!" flatDmg plane

      let evt = CombatEvent.EquipmentProcTriggered(sourceItemName, opponent.Id, desc)
      (owner, updatedOpponent, [ evt ])

    | ShredTargetArmor amount ->
      let newArmor = opponent.Armor.Shred amount
      let updatedOpponent = { opponent with Armor = newArmor }

      let desc =
        sprintf "Punctured %d armor integrity (Remaining: %d)" amount newArmor.Current

      let evt = CombatEvent.EquipmentProcTriggered(sourceItemName, opponent.Id, desc)
      (owner, updatedOpponent, [ evt ])

  let private evaluateHooks (ctx: TriggerContext) (attacker: Combatant) (defender: Combatant) =
    let mutable currentAttacker = attacker
    let mutable currentDefender = defender
    let mutable events = []

    // Attacker Offensive Hooks: OnHitLanded and OnCriticalStrike
    for item in currentAttacker.EquippedItems do
      for trigger in item.Triggers do
        let effects =
          match trigger with
          | OnHitLanded f when ctx.DamageDealt > 0 -> f ctx
          | OnCriticalStrike f when ctx.IsCritical -> f ctx
          | _ -> []

        for eff in effects do
          let a, d, evts = applyTriggerEffect eff item.Name currentAttacker currentDefender
          currentAttacker <- a
          currentDefender <- d
          events <- events @ evts

    // Defender Defensive Hooks: OnDamageReceived
    if ctx.DamageDealt > 0 then
      for item in currentDefender.EquippedItems do
        for trigger in item.Triggers do
          let effects =
            match trigger with
            | OnDamageReceived f -> f ctx
            | _ -> []

          for eff in effects do
            let d, a, evts = applyTriggerEffect eff item.Name currentDefender currentAttacker
            currentDefender <- d
            currentAttacker <- a
            events <- events @ evts

    currentAttacker, currentDefender, events

  // =========================================================================
  // 3. Consecutive Passives (Sunder, Vital Opening, Study)
  // =========================================================================

  let private evaluatePassives
    (roller: DiceRoller)
    (vector: Vector)
    (plane: Plane)
    (actor: Combatant)
    (target: Combatant)
    : Combatant * Combatant * CombatEvent list =
    let mutable currentActor = actor
    let mutable currentTarget = target
    let mutable events = []

    // 1. Power Passive: Sunder Armor / Focus Shatter
    let powerStat =
      match plane with
      | Physical -> currentActor.GetStat Force
      | Mental -> currentActor.GetStat Intellect

    let sunderThreshold = Math.Min(80, 20 + powerStat / 4)

    if roller 1 100 <= sunderThreshold then
      match plane with
      | Physical ->
        let shredAmount = 15 + (currentActor.ComboTracker.ConsecutivePowerHits * 5)
        let newArmor = currentTarget.Armor.Shred shredAmount
        currentTarget <- { currentTarget with Armor = newArmor }

        events <-
          CombatEvent.PassiveProcTriggered(
            currentActor.Id,
            currentTarget.Id,
            ArmorSundered(shredAmount, newArmor.Current)
          )
          :: events
      | Mental ->
        let drain = 20

        currentTarget <-
          currentTarget
          |> Combatant.updateMeters (fun m ->
            { m with
                CognitiveFatigue = m.CognitiveFatigue + drain })

        events <-
          CombatEvent.PassiveProcTriggered(currentActor.Id, currentTarget.Id, FocusShattered drain)
          :: events

    // 2. Agility Passive: Vital Opening / Cognitive Blindspot
    let openingBonus = currentActor.ComboTracker.VitalOpeningBonus

    if openingBonus > 0 && roller 1 100 <= openingBonus then
      let bonusDmg = int (float openingBonus * 0.75)

      match plane with
      | Physical ->
        currentTarget <-
          { currentTarget with
              Health = currentTarget.Health.ApplyDelta -bonusDmg }

        currentTarget <-
          currentTarget
          |> Combatant.updateMeters (fun m -> { m with Overwhelm = m.Overwhelm + 15 })

        events <-
          CombatEvent.PassiveProcTriggered(
            currentActor.Id,
            currentTarget.Id,
            VitalOpeningTriggered(bonusDmg, true)
          )
          :: events
      | Mental ->
        currentTarget <-
          { currentTarget with
              Morale = currentTarget.Morale.ApplyDelta -bonusDmg }

        currentTarget <-
          currentTarget
          |> Combatant.updateMeters (fun m -> { m with Confusion = m.Confusion + 15 })

        events <-
          CombatEvent.PassiveProcTriggered(
            currentActor.Id,
            currentTarget.Id,
            VitalOpeningTriggered(bonusDmg, false)
          )
          :: events

    // 3. Discipline Passive: Study & Prescience Stacks
    let disciplineStat =
      match plane with
      | Physical -> currentActor.GetStat Prowess
      | Mental -> currentActor.GetStat Acumen

    let studyThreshold = Math.Min(85, 25 + disciplineStat / 3)

    if roller 1 100 <= studyThreshold || vector = Discipline then
      let generated = if vector = Discipline then 2 else 1
      currentActor <- currentActor |> Combatant.addStudyStacks generated

      events <-
        CombatEvent.PassiveProcTriggered(
          currentActor.Id,
          currentTarget.Id,
          StudyStackGenerated currentActor.StudyStacks
        )
        :: events

    currentActor <-
      { currentActor with
          ComboTracker = currentActor.ComboTracker.RegisterHit() }

    currentActor, currentTarget, events

  // =========================================================================
  // 4. Recovery & Execution Resolvers
  // =========================================================================

  let private resolveRecovery (reset: DefensiveReset) (actor: Combatant) (target: Combatant) : ActionResult =
    let resetActor =
      { actor with
          ComboTracker = actor.ComboTracker.ResetCombo() }

    let resetEvt =
      CombatEvent.ComboReset(actor.Id, "Stance shifted into recovery; combo momentum cleared.")

    match reset with
    | SteadyForm ->
      let poise = resetActor.GetStat Poise
      let prowess = resetActor.GetStat Prowess
      let reckDrain = poise + 10
      let studyGain = Math.Max(1, prowess / 4)

      let updatedActor =
        resetActor
        |> Combatant.updateMeters (fun m ->
          { m with
              Recklessness = m.Recklessness - reckDrain })
        |> Combatant.addStudyStacks studyGain

      { Actor = updatedActor
        Target = target
        Events = [ resetEvt; CombatEvent.FormStabilized(actor.Id, reckDrain, studyGain) ]
        Contest = None }

    | CenterMind ->
      let composure = resetActor.GetStat Composure
      let acumen = resetActor.GetStat Acumen
      let reckDrain = composure + 10
      let studyGain = Math.Max(1, acumen / 4)

      let updatedActor =
        resetActor
        |> Combatant.updateMeters (fun m ->
          { m with
              Recklessness = m.Recklessness - reckDrain
              Confusion = m.Confusion - (composure / 2) })
        |> Combatant.addStudyStacks studyGain

      { Actor = updatedActor
        Target = target
        Events = [ resetEvt; CombatEvent.FormStabilized(actor.Id, reckDrain, studyGain) ]
        Contest = None }

  let private resolveExecute (plane: Plane) (actor: Combatant) (target: Combatant) : ActionResult =
    if not target.IsExecuteEligible then
      { Actor = actor
        Target = target
        Events = []
        Contest = None }
    else
      let killDamage =
        match plane with
        | Physical -> target.Health.Current + 9999
        | Mental -> target.Morale.Current + 9999

      let updatedTarget, dmgEvt = applyDamage plane killDamage true target
      let events = [ CombatEvent.DamageApplied dmgEvt; CombatEvent.Executed(actor.Id, target.Id, plane) ]

      { Actor = actor
        Target = updatedTarget
        Events = events
        Contest = None }

  // =========================================================================
  // 5. Offensive Strike Resolver (All 9 Classifications via DicePool)
  // =========================================================================

  let private resolveAttack
    (roller: DiceRoller)
    (atk: AttackClassification)
    (actor: Combatant)
    (target: Combatant)
    : ActionResult =
    let mutable currentActor = actor
    let mutable currentTarget = target
    let mutable events = []

    // --- Step A: Identify Gambit Properties ---
    let isGambit, gambitName, selfReckSpike =
      match atk with
      | ForceStrike true -> true, "Physical Gambit: Wild Blow", 30
      | FinesseCadence true -> true, "Physical Gambit: Relentless Cadence", 25
      | ProwessStrike true -> true, "Martial Gambit: Invitational Bait", 35
      | AuthorityDecree true -> true, "Social Gambit: Imperious Demand", 30
      | GuileDeception true -> true, "Social Gambit: Confidence Trap", 25
      | AcumenInterrogation true -> true, "Social Gambit: Calculated Sacrilege", 35
      | ArcaneCataclysm true -> true, "Arcane Gambit: Overchanneled Cataclysm", 35
      | SynapticGlamour true -> true, "Arcane Gambit: Neural Fracture", 30
      | RunicWardTrap true -> true, "Arcane Gambit: Anomalous Glyph", 30
      | _ -> false, "", 0

    if isGambit then
      currentActor <-
        Combatant.updateMeters
          (fun m ->
            { m with
                Recklessness = m.Recklessness + selfReckSpike })
          currentActor

      events <- CombatEvent.GambitDeclared(currentActor.Id, gambitName, selfReckSpike) :: events

    // --- Step B: Determine Coordinates, Opposed Stats & Vector Parameters ---
    let vector = atk.Vector
    let plane = atk.Plane

    let offStat, defStat, classMult, disparityFactory, meterUpdates =
      match atk with
      // 1. Physical Attacks
      | ForceStrike isWild ->
        let off = currentActor.GetStat Force
        let def = currentTarget.GetStat Fortitude
        let wildMult = if isWild then 1.3 else 1.0
        let exhaust = fun isCrit -> if isCrit then 35 else 15
        let disp = fun isCrit -> if isCrit then Some(CrushingBlow(exhaust true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Exhaustion = m.Exhaustion + (exhaust isCrit)
              Recklessness = m.Recklessness + 10 }

        off, def, wildMult, disp, upd

      | FinesseCadence isRelentless ->
        let off = currentActor.GetStat Finesse
        let def = currentTarget.GetStat Reflex
        let hits = if isRelentless then 3 else 1
        let reckMult = 1.0 + (float currentTarget.Meters.Recklessness.Value / 100.0)
        let overwhelm = fun isCrit -> if isCrit then 15 * hits else 10
        let disp = fun isCrit -> if isCrit then Some(ArterialRupture(overwhelm true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Overwhelm = m.Overwhelm + (overwhelm isCrit)
              Recklessness = m.Recklessness + (5 * hits) }

        off, def, (float hits * reckMult), disp, upd

      | ProwessStrike isInvitational ->
        let off = currentActor.GetStat Prowess
        let def = currentTarget.GetStat Poise
        let studyMult = 1.0 + (float currentActor.StudyStacks * 0.20)
        let baitMult = if isInvitational then 1.25 else 1.0
        let frustrate = fun isCrit -> if isCrit then 35 else 20
        let disp = fun isCrit -> if isCrit then Some DisarmOrLimbDisable else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Frustration = m.Frustration + (frustrate isCrit)
              Recklessness = m.Recklessness + 15 }

        off, def, (studyMult * baitMult), disp, upd

      // 2. Social Attacks
      | AuthorityDecree isImperious ->
        let off = currentActor.GetStat Intellect
        let def = currentTarget.GetStat Resolve
        let imperiousMult = if isImperious then 1.3 else 1.0
        let fatigue = fun isCrit -> if isCrit then 35 else 18
        let disp = fun isCrit -> if isCrit then Some(CognitiveRupture(fatigue true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              CognitiveFatigue = m.CognitiveFatigue + (fatigue isCrit)
              Recklessness = m.Recklessness + 10 }

        off, def, imperiousMult, disp, upd

      | GuileDeception isConfidenceTrap ->
        let off = currentActor.GetStat Acuity
        let def = currentTarget.GetStat Intuition
        let args = if isConfidenceTrap then 3 else 1
        let reckMult = 1.0 + (float currentTarget.Meters.Recklessness.Value / 100.0)
        let confusion = fun isCrit -> if isCrit then 15 * args else 10
        let disp = fun isCrit -> if isCrit then Some DialecticalParalysis else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Confusion = m.Confusion + (confusion isCrit)
              Recklessness = m.Recklessness + (5 * args) }

        off, def, (float args * reckMult), disp, upd

      | AcumenInterrogation isCheckmate ->
        let off = currentActor.GetStat Acumen
        let def = currentTarget.GetStat Composure
        let studyMult = 1.0 + (float currentActor.StudyStacks * 0.25)
        let checkmateMult = if isCheckmate then 1.25 else 1.0
        let provoke = fun isCrit -> if isCrit then 45 else 20
        let disp = fun isCrit -> if isCrit then Some(StrippedCredibility(provoke true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Provoke = m.Provoke + (provoke isCrit)
              Recklessness = m.Recklessness + 20 }

        off, def, (studyMult * checkmateMult), disp, upd

      // 3. Arcane Attacks
      | ArcaneCataclysm isOverchannel ->
        let off = currentActor.GetStat Intellect
        let def = currentTarget.GetStat Resolve

        let surge =
          if isOverchannel then
            float currentActor.Meters.Recklessness.Value * 0.5
          else
            0.0

        let cataclysmMult = 1.0 + (surge / Math.Max(10.0, float off))
        let fatigue = fun isCrit -> if isCrit then 45 else 20
        let disp = fun isCrit -> if isCrit then Some(CognitiveRupture(fatigue true)) else None

        let upd isCrit (m: StatusMeters) =
          let f = fatigue isCrit

          { m with
              CognitiveFatigue = m.CognitiveFatigue + f
              Exhaustion = m.Exhaustion + (f / 2)
              Recklessness = m.Recklessness + 10 }

        off, def, cataclysmMult, disp, upd

      | SynapticGlamour isMindFracture ->
        let off = currentActor.GetStat Acuity
        let def = currentTarget.GetStat Intuition
        let pulses = if isMindFracture then 3 else 1
        let confusion = fun isCrit -> if isCrit then 15 * pulses else 10
        let disp = fun isCrit -> if isCrit then Some DialecticalParalysis else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Confusion = m.Confusion + (confusion isCrit)
              Recklessness = m.Recklessness + (5 * pulses) }

        off, def, float pulses, disp, upd

      | RunicWardTrap isAnomalousGlyph ->
        let off = currentActor.GetStat Acumen
        let def = currentTarget.GetStat Composure
        let studyMult = 1.0 + (float currentActor.StudyStacks * 0.25)
        let glyphMult = if isAnomalousGlyph then 1.2 else 1.0
        let provoke = fun isCrit -> if isCrit then 40 else 15
        let disp = fun isCrit -> if isCrit then Some(StrippedCredibility(provoke true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Provoke = m.Provoke + (provoke isCrit)
              Recklessness = m.Recklessness + 15 }

        off, def, (studyMult * glyphMult), disp, upd

    // --- Step C: Opposed Resolution via DicePool ---
    let contest =
      DicePool.resolveContest
        roller
        vector
        offStat
        currentActor.StudyStacks
        defStat
        currentTarget.StudyStacks

    // --- Step D: Whiff vs. Landed Hit Branching ---
    if contest.IsWhiff then
      // Whiff: 0 damage, clear combo momentum
      currentActor <-
        { currentActor with
            ComboTracker = currentActor.ComboTracker.ResetCombo() }

      events <-
        CombatEvent.ComboReset(currentActor.Id, "Attack failed to penetrate defenses; combo momentum cleared.")
        :: events

      // Evaluate collapse on actor in case gambit self-spiked Recklessness
      let finalActor = Combatant.evaluateCollapse currentActor

      if
        not (CollapseState.isCollapsed currentActor.Collapse)
        && (CollapseState.isCollapsed finalActor.Collapse)
      then
        match finalActor.Collapse with
        | CollapseState.Collapsed reason ->
          events <- events @ [ CombatEvent.CollapseTriggered(finalActor.Id, reason) ]
        | CollapseState.Stable -> ()

      { Actor = finalActor
        Target = currentTarget
        Events = events
        Contest = Some contest }

    else
      // Landed Strike: Scaled Tiered NetHits Damage
      let baseDamage = offStat * 2
      let tierMult = computeTierMultiplier contest.NetHits
      let gambitMult = if isGambit then 1.5 else 1.0
      let rawDmg = Math.Max(1, int (float baseDamage * tierMult * gambitMult * classMult))

      let isCrit = contest.IsCritical

      // Apply dynamic status meters to target
      currentTarget <- Combatant.updateMeters (meterUpdates isCrit) currentTarget

      // Apply core pool damage and potential armor shred
      let updatedTarget, dmgEvt = applyDamage plane rawDmg isCrit currentTarget
      currentTarget <- updatedTarget
      events <- CombatEvent.DamageApplied dmgEvt :: events

      // Disparity trigger if critical
      match disparityFactory isCrit with
      | Some disp -> events <- CombatEvent.DisparityTriggered(currentActor.Id, currentTarget.Id, disp) :: events
      | None -> ()

      // Step E: Evaluate Consecutive Passives
      let postPassiveActor, postPassiveTarget, passiveEvents =
        evaluatePassives roller vector plane currentActor currentTarget

      currentActor <- postPassiveActor
      currentTarget <- postPassiveTarget
      events <- events @ passiveEvents

      // Step F: Evaluate Equipment Hooks
      let triggerCtx =
        { ActorId = currentActor.Id
          TargetId = currentTarget.Id
          DamageDealt = dmgEvt.Amount
          Plane = plane
          IsCritical = isCrit
          IsGambit = isGambit }

      let postHookActor, postHookTarget, hookEvents =
        evaluateHooks triggerCtx currentActor currentTarget

      currentActor <- postHookActor
      currentTarget <- postHookTarget
      events <- events @ hookEvents

      // Step G: Evaluate Threshold Collapses
      let finalTarget = Combatant.evaluateCollapse currentTarget

      if
        not (CollapseState.isCollapsed currentTarget.Collapse)
        && (CollapseState.isCollapsed finalTarget.Collapse)
      then
        match finalTarget.Collapse with
        | CollapseState.Collapsed reason ->
          events <- events @ [ CombatEvent.CollapseTriggered(finalTarget.Id, reason) ]
        | CollapseState.Stable -> ()

      let finalActor = Combatant.evaluateCollapse currentActor

      if
        not (CollapseState.isCollapsed currentActor.Collapse)
        && (CollapseState.isCollapsed finalActor.Collapse)
      then
        match finalActor.Collapse with
        | CollapseState.Collapsed reason ->
          events <- events @ [ CombatEvent.CollapseTriggered(finalActor.Id, reason) ]
        | CollapseState.Stable -> ()

      { Actor = finalActor
        Target = finalTarget
        Events = events
        Contest = Some contest }

  // =========================================================================
  // 6. Public Dispatcher
  // =========================================================================

  let resolve (roller: DiceRoller) (intent: ActionIntent) (actor: Combatant) (target: Combatant) : ActionResult =
    match intent with
    | RecoveryAction reset -> resolveRecovery reset actor target
    | ExecuteStrike plane -> resolveExecute plane actor target
    | StandardAttack atk -> resolveAttack roller atk actor target
