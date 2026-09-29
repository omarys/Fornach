namespace Fornach.Engine

open System
open Fornach.Domain

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
    elif netHits = 9 then 4.00
    elif netHits = 10 then 4.50
    else Math.Min(12.00, 4.50 + (float (netHits - 10) * 0.50))

  /// Applies pool damage, arcane ward barrier soak, armor soak, and massive blow armor shredding
  let private applyDamage
    (plane: Plane)
    (rawAmount: int)
    (isCrit: bool)
    (target: Combatant)
    : Combatant * DamageEvent * CombatEvent list =
    let wardSoaked = Math.Min(target.ArcaneWard, rawAmount)
    let remAmount = rawAmount - wardSoaked
    let newWard = target.ArcaneWard - wardSoaked
    let wardEvts =
      if wardSoaked > 0 then
        [ CombatEvent.ArcaneWardAbsorbed(target.Id, wardSoaked, newWard) ]
      else []
    let targetWithWard = { target with ArcaneWard = newWard }

    match plane with
    | Physical ->
      // Berserk Tincture Enrage: deadens pain receptors, shrugging off physical damage (scales up to 50% with Force)
      let enrageShrugged =
        if targetWithWard.HasActivePreparation PreparationType.BerserkTincture && remAmount > 0 then
          let forceFactor = Math.Min(0.50, 0.35 + (float (targetWithWard.GetStat Force) / 1000.0))
          Math.Max(1, int (Math.Round(float remAmount * forceFactor)))
        else 0
      let postEnrageAmount = remAmount - enrageShrugged
      let absorbed = int (float postEnrageAmount * targetWithWard.Armor.AbsorptionRatio)
      let actualDmg = if postEnrageAmount <= 0 then 0 else Math.Max(1, postEnrageAmount - absorbed)
      let updatedPool = targetWithWard.Health.ApplyDelta -actualDmg
      let enrageEvts =
        if enrageShrugged > 0 then
          [ CombatEvent.EnrageDamageShrugged(target.Id, enrageShrugged) ]
        else []

      // Massive blows automatically shred armor durability: shred = max 15 (damageDealt / 3)
      let updatedArmor =
        if actualDmg > 0 && (isCrit || actualDmg >= 40) then
          let shredAmount = Math.Max(15, actualDmg / 3)
          targetWithWard.Armor.Shred shredAmount
        else
          targetWithWard.Armor

      let updatedTarget =
        { targetWithWard with
            Health = updatedPool
            Armor = updatedArmor }

      let evt =
        { TargetId = target.Id
          Plane = Physical
          Amount = actualDmg
          IsCritical = isCrit
          IsArmorCompromised = updatedArmor.IsShredded || isCrit }

      updatedTarget, evt, (wardEvts @ enrageEvts)

    | Mental ->
      let actualDmg = if remAmount <= 0 then 0 else Math.Max(1, remAmount)
      let updatedPool = targetWithWard.Morale.ApplyDelta -actualDmg
      let updatedTarget = { targetWithWard with Morale = updatedPool }

      let evt =
        { TargetId = target.Id
          Plane = Mental
          Amount = actualDmg
          IsCritical = isCrit
          IsArmorCompromised = false }

      updatedTarget, evt, wardEvts

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
    (damageDealt: int)
    (actor: Combatant)
    (target: Combatant)
    : Combatant * Combatant * CombatEvent list =
    let mutable currentActor = actor
    let mutable currentTarget = target
    let mutable events = []

    // 1. Power Passive: Sunder Armor / Focus Shatter & Weapon Degradation
    let powerStat =
      match plane with
      | Physical -> currentActor.GetStat Force
      | Mental -> currentActor.GetStat Intellect

    // The greater the damage dealt, the greater the chance of damaging armor or weapon
    let sunderThreshold = Math.Min(95, 20 + (damageDealt / 3) + (powerStat / 8))

    if roller 1 100 <= sunderThreshold then
      match plane with
      | Physical ->
        let shredAmount = Math.Max(15, damageDealt / 3) + (currentActor.ComboTracker.ConsecutivePowerHits * 5)
        let newArmor = currentTarget.Armor.Shred shredAmount
        currentTarget <- { currentTarget with Armor = newArmor }

        events <-
          CombatEvent.PassiveProcTriggered(
            currentActor.Id,
            currentTarget.Id,
            ArmorSundered(shredAmount, newArmor.Current)
          )
          :: events

        // Heavy Power strikes also degrade weapon condition
        let weaponDamageThreshold = Math.Min(80, Math.Max(0, (damageDealt - 20) / 2))
        if roller 1 100 <= weaponDamageThreshold then
          let oldCond = currentTarget.WeaponCondition
          let newCond = WeaponCondition.degradation oldCond
          if newCond <> oldCond then
            currentTarget <- { currentTarget with WeaponCondition = newCond }
            events <- CombatEvent.WeaponDegraded(currentTarget.Id, newCond) :: events

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

    // 2. Agility Passive: Vital Opening / Cognitive Blindspot & Bleed / Cripple
    // Overwhelming the opponent over time increases the chance
    let overwhelmBonus = currentTarget.Meters.Overwhelm.Value / 2
    let openingBonus = currentActor.ComboTracker.VitalOpeningBonus + overwhelmBonus

    if openingBonus > 0 && roller 1 100 <= openingBonus then
      let bonusDmg = int (float openingBonus * 0.85)

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

        // Critical vital opening inflicts stacking bleed and targets ligaments
        let bleedStacksToAdd = 2
        currentTarget <- currentTarget |> Combatant.addBleed bleedStacksToAdd
        events <- CombatEvent.BleedApplied(currentTarget.Id, bleedStacksToAdd, currentTarget.BleedStacks) :: events

        if roller 1 100 <= 50 then
          let penalty = 15
          currentTarget <- currentTarget |> Combatant.addLimbDebuff penalty
          events <- CombatEvent.LimbDisabled(currentTarget.Id, penalty) :: events

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
      let generated =
        if vector = Discipline || currentActor.Stance = CombatStance.DisciplineStance then 2 else 1
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
      let exhaustDrain = 15 + (poise / 4)
      let studyGain = Math.Max(1, prowess / 4)

      let updatedActor =
        resetActor
        |> Combatant.updateMeters (fun m ->
          { m with
              Recklessness = m.Recklessness - reckDrain
              Exhaustion = m.Exhaustion - exhaustDrain })
        |> Combatant.addStudyStacks studyGain

      { Actor = updatedActor
        Target = target
        Events = [ resetEvt; CombatEvent.FormStabilized(actor.Id, reckDrain, studyGain) ]
        Contest = None }

    | CenterMind ->
      let composure = resetActor.GetStat Composure
      let acumen = resetActor.GetStat Acumen
      let reckDrain = composure + 10
      let fatigueDrain = 15 + (composure / 4)
      let studyGain = Math.Max(1, acumen / 4)
      let profRatio = resetActor.GetArcaneProficiency Discipline
      let wardRestore = int (float acumen * 0.40 * profRatio)

      let updatedActor =
        resetActor
        |> Combatant.updateMeters (fun m ->
          { m with
              Recklessness = m.Recklessness - reckDrain
              Confusion = m.Confusion - (composure / 2)
              CognitiveFatigue = m.CognitiveFatigue - fatigueDrain })
        |> Combatant.addStudyStacks studyGain
        |> Combatant.addWard wardRestore

      let wardEvts =
        if wardRestore > 0 then
          [ CombatEvent.ArcaneWardErected(actor.Id, wardRestore, updatedActor.ArcaneWard) ]
        else []

      { Actor = updatedActor
        Target = target
        Events = [ resetEvt; CombatEvent.FormStabilized(actor.Id, reckDrain, studyGain) ] @ wardEvts
        Contest = None }

  let private resolveShiftStance (newStance: CombatStance) (actor: Combatant) (target: Combatant) : ActionResult =
    let oldStance = actor.Stance
    let resetActor =
      { actor with
          Stance = newStance
          ComboTracker = actor.ComboTracker.ResetCombo() }
      |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness - 10 })

    let evts = [
      CombatEvent.StanceShifted(actor.Id, oldStance, newStance)
      CombatEvent.ComboReset(actor.Id, "Stance shifted; combo momentum cleared.")
    ]

    { Actor = resetActor
      Target = target
      Events = evts
      Contest = None }

  let private resolveDeployPreparation
    (prepType: PreparationType)
    (targetIdOpt: CombatantId option)
    (actor: Combatant)
    (target: Combatant)
    : ActionResult =
    if not (actor.Progression.HasRemainingUses prepType) then
      { Actor = actor
        Target = target
        Events = [ CombatEvent.ComboReset(actor.Id, sprintf "Cannot deploy %s: no preparation charges remaining!" prepType.Name) ]
        Contest = None }
    else
      let actorSpent = actor |> Combatant.spendPreparation prepType
      let duration = prepType.CalculateDuration actorSpent.Progression.Level actorSpent.Progression.PrimaryStat
      match prepType with
      | PreparationType.ShockwaveSlam ->
        let updatedActor =
          actorSpent
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ShockwaveSlam None duration)
        let evts = [
          CombatEvent.PreparationDeployed(actor.Id, prepType, None, sprintf "Shockwave Slam prepared for %d turns: surplus NetHits (>= 3) will spill over as flat kinetic damage to all engaged flankers." duration)
        ]
        { Actor = updatedActor; Target = target; Events = evts; Contest = None }

      | PreparationType.BerserkTincture ->
        let healthCost = Math.Min(25, Math.Max(5, actorSpent.Health.Current / 4))
        let updatedActor =
          { actorSpent with Health = actorSpent.Health.ApplyDelta -healthCost }
          |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 35 })
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.BerserkTincture None duration)
        let evts = [
          CombatEvent.PreparationDeployed(actor.Id, prepType, None, sprintf "Consumed Berserk Tincture (lost %d HP): Recklessness spiked into Fever Pitch (+35) for %d turns!" healthCost duration)
          CombatEvent.DamageApplied { TargetId = actor.Id; Plane = Physical; Amount = healthCost; IsCritical = false; IsArmorCompromised = false }
        ]
        { Actor = updatedActor; Target = target; Events = evts; Contest = None }

      | PreparationType.DreadWarhorn ->
        let sonicDmg = Math.Max(20, int (float (actor.GetStat Intellect) * 0.35))
        let targetAfterDmg, dmgEvt, wardEvts = applyDamage Mental sonicDmg false target
        let updatedTarget =
          targetAfterDmg
          |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + 25 })
          |> Combatant.evaluateCollapse
        let evts =
          wardEvts
          @ [
            CombatEvent.PreparationDeployed(actor.Id, prepType, Some target.Id, sprintf "Dread Warhorn sounded! Blasted target for %d Morale damage and +25 Cognitive Fatigue." sonicDmg)
            CombatEvent.DamageApplied dmgEvt
          ]
        { Actor = actorSpent; Target = updatedTarget; Events = evts; Contest = None }

      | PreparationType.SynapticBrand ->
        let tgtId = targetIdOpt |> Option.defaultValue target.Id
        let updatedActor =
          actorSpent
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.SynapticBrand (Some tgtId) duration)
        let updatedTarget =
          target
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.SynapticBrand None duration)
        let evts = [
          CombatEvent.PreparationDeployed(actor.Id, prepType, Some tgtId, sprintf "Synaptic Brand inscribed for %d turns: critical strikes deal 2x Morale damage and inflict Rupture." duration)
        ]
        { Actor = updatedActor; Target = updatedTarget; Events = evts; Contest = None }

      | PreparationType.CaltropPouch ->
        let updatedActor =
          actorSpent
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.CaltropPouch None duration)
        let evts = [
          CombatEvent.PreparationDeployed(actor.Id, prepType, None, sprintf "Caltrop Pouch scattered sharp spikes across the 5 flanking spaces for %d turns!" duration)
        ]
        { Actor = updatedActor; Target = target; Events = evts; Contest = None }

      | PreparationType.ConcealedBlade ->
        let updatedActor =
          actorSpent
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ConcealedBlade None duration)
        let evts = [
          CombatEvent.PreparationDeployed(actor.Id, prepType, None, sprintf "Concealed boot blade readied for %d turns: prepared to counter-puncture from the Nach!" duration)
        ]
        { Actor = updatedActor; Target = target; Events = evts; Contest = None }

      | PreparationType.MirrorMirage ->
        let maxClones = Math.Clamp(actor.Progression.Level / 40 + 1, 1, 5)
        let clonesToAdd = Math.Max(0, Math.Min(3, maxClones - actorSpent.MirrorClones))
        let updatedActor =
          actorSpent
          |> (if clonesToAdd > 0 then Combatant.addClones clonesToAdd else id)
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.MirrorMirage None duration)
        let evts = [
          CombatEvent.PreparationDeployed(actor.Id, prepType, None, sprintf "Mirror Mirage wove phantasms (Active Clones: %d/%d): secondary flankers hit illusions (+20 Confusion) for %d turns." updatedActor.MirrorClones maxClones duration)
        ]
        { Actor = updatedActor; Target = target; Events = evts; Contest = None }

      | PreparationType.PrismaticFlare ->
        let tgtId = targetIdOpt |> Option.defaultValue target.Id
        let updatedActor =
          actorSpent
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.PrismaticFlare (Some tgtId) duration)
        let updatedTarget =
          target
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.PrismaticFlare None duration)
        let evts = [
          CombatEvent.PreparationDeployed(actor.Id, prepType, Some tgtId, sprintf "Prismatic Flare inscribed for %d turns: target suffers acute Morale shock and +20 Confusion on Recklessness accumulation." duration)
        ]
        { Actor = updatedActor; Target = updatedTarget; Events = evts; Contest = None }

      | PreparationType.BastionZoneControl ->
        let updatedActor =
          actorSpent
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.BastionZoneControl None duration)
        let evts = [
          CombatEvent.BastionZoneErected(actor.Id)
          CombatEvent.PreparationDeployed(actor.Id, prepType, None, sprintf "Bastion Zone planted for %d turns: limits simultaneous attackers strictly to 3 (front three tiles)!" duration)
        ]
        { Actor = updatedActor; Target = target; Events = evts; Contest = None }

      | PreparationType.ParryingBuckler ->
        let updatedActor =
          actorSpent
          |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.ParryingBuckler None duration)
        let evts = [
          CombatEvent.PreparationDeployed(actor.Id, prepType, None, sprintf "Parrying Buckler braced for %d turns: Indes threshold reduced by -1, widening the window to seize the Vor!" duration)
        ]
        { Actor = updatedActor; Target = target; Events = evts; Contest = None }

      | PreparationType.AegisOfRetribution ->
        let tgtId = targetIdOpt |> Option.defaultValue actor.Id
        let isSelf = tgtId = actor.Id
        let wardBoost = Math.Max(25, int (float (actor.GetStat Acumen) * 0.40))
        let updatedActor =
          actorSpent
          |> (if isSelf then Combatant.addWard wardBoost else id)
          |> (if isSelf then Combatant.addActivePreparation (ActivePreparation.create PreparationType.AegisOfRetribution (Some tgtId) duration) else id)
        let updatedTarget =
          if not isSelf && target.Id = tgtId then
            target
            |> Combatant.addWard wardBoost
            |> Combatant.addActivePreparation (ActivePreparation.create PreparationType.AegisOfRetribution (Some tgtId) duration)
          else target
        let evts = [
          CombatEvent.ArcaneWardErected(tgtId, wardBoost, (if isSelf then updatedActor.ArcaneWard else updatedTarget.ArcaneWard))
          CombatEvent.PreparationDeployed(actor.Id, prepType, Some tgtId, sprintf "Aegis of Retribution deployed for %d turns (+%d Ward barrier): damage taken reduced by 35%%, and 50%% reflected back as radiant retribution!" duration wardBoost)
        ]
        { Actor = updatedActor; Target = updatedTarget; Events = evts; Contest = None }

      | PreparationType.HeraldicTreatise ->
        let updatedActor =
          actorSpent
          |> Combatant.addStudyStacks 2
        let evts = [
          CombatEvent.HeraldicTreatiseStudied(actor.Id, 2)
          CombatEvent.PreparationDeployed(actor.Id, prepType, None, "Heraldic Treatise reviewed: +2 Study Stacks granted immediately across visible foes.")
        ]
        { Actor = updatedActor; Target = target; Events = evts; Contest = None }

      | PreparationType.SocraticDossier ->
        let tgtId = targetIdOpt |> Option.defaultValue target.Id
        let reckVal = Math.Max(10, target.Meters.Recklessness.Value)
        let updatedTarget =
          { target with Morale = target.Morale.ApplyDelta -reckVal }
          |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness - reckVal })
          |> Combatant.evaluateCollapse
        let evts = [
          CombatEvent.SocraticDossierExecuted(actor.Id, tgtId, reckVal, reckVal)
          CombatEvent.DamageApplied {
            TargetId = tgtId
            Plane = Mental
            Amount = reckVal
            IsCritical = false
            IsArmorCompromised = false
          }
          CombatEvent.PreparationDeployed(actor.Id, prepType, Some tgtId, sprintf "Socratic Dossier deployed: converted %d Recklessness directly to unmitigated Morale damage!" reckVal)
        ]
        { Actor = actorSpent; Target = updatedTarget; Events = evts; Contest = None }

  let applyTurnUpkeep (c: Combatant) : Combatant * CombatEvent list =
    let baseUpdated, baseEvents =
      if c.BleedStacks > 0 then
        let bleedDmg = c.BleedStacks * 12
        let newHealth = c.Health.ApplyDelta -bleedDmg
        let remStacks = Math.Max(0, c.BleedStacks - 1)
        let updated = { c with Health = newHealth; BleedStacks = remStacks }
        let evt = CombatEvent.BleedTicked(c.Id, bleedDmg, remStacks)
        let dmgEvt = CombatEvent.DamageApplied {
          TargetId = c.Id
          Plane = Physical
          Amount = bleedDmg
          IsCritical = false
          IsArmorCompromised = false
        }
        updated, [ evt; dmgEvt ]
      else
        c, []

    // Natural combat exertion in prolonged battle (+1 Exhaustion per active round)
    // Decrement active preparation timers and remove expired ones
    let withExertion =
      baseUpdated
      |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + 1 })
      |> Combatant.decrementActivePreparations

    withExertion, baseEvents


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

      let updatedTarget, dmgEvt, wardEvts = applyDamage plane killDamage true target
      let events = wardEvts @ [ CombatEvent.DamageApplied dmgEvt; CombatEvent.Executed(actor.Id, target.Id, plane) ]

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
    (priorDefenses: int)
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
      | CalculatedFlawStrike stacks ->
        let actualSpend = Math.Min(currentActor.StudyStacks, Math.Max(2, stacks))
        let isSameSchoolMaster = currentActor.Class = CharacterClass.Warden || currentActor.Class = CharacterClass.Justicar
        let retained = if isSameSchoolMaster then actualSpend / 2 else 0
        currentActor <- currentActor |> Combatant.addStudyStacks -(actualSpend - retained)
        events <- CombatEvent.DisciplineGambitExecuted(currentActor.Id, "Calculated Flaw Strike", actualSpend) :: events
        false, "", 0 // Consumes Study Stacks with 0 Recklessness self-spike!
      | MasterfulDisarm stacks ->
        let off = currentActor.GetStat Prowess
        let def = currentTarget.GetStat Poise
        let isPossible = off >= int (Math.Round(float def * 0.75))
        let required = Math.Max(3, int (Math.Ceiling((float def / Math.Max(1.0, float off)) * 3.5)))
        let actualSpend = Math.Min(currentActor.StudyStacks, required)
        if isPossible && currentActor.StudyStacks >= required then
          currentActor <- currentActor |> Combatant.addStudyStacks -actualSpend
          events <- CombatEvent.DisciplineGambitExecuted(currentActor.Id, "Masterful Disarm", actualSpend) :: events
          false, "", 0 // Consumes Study Stacks with 0 Recklessness self-spike!
        else
          false, "", 0
      | AuthorityDecree true -> true, "Social Gambit: Imperious Demand", 30
      | GuileDeception true -> true, "Social Gambit: Confidence Trap", 25
      | AcumenInterrogation true -> true, "Social Gambit: Calculated Sacrilege", 35
      | ArcaneCataclysm true -> true, "Arcane Gambit: Overchanneled Cataclysm", 35
      | SynapticGlamour true -> true, "Arcane Gambit: Mind Fracture", 25
      | MirrorIllusion true -> true, "Arcane Gambit: Decoy Swarm", 25
      | RunicWardTrap true -> true, "Arcane Gambit: Anomalous Glyph", 30
      | DisorientingShockwave true -> true, "Arcane Gambit: Resonant Shockwave", 25
      | TraumaAttack DenialPhaseShift -> true, "Trauma Gambit: Denial Phase Shift", 15
      | TraumaAttack BasaltEruption -> true, "Trauma Gambit: Basalt Eruption", 35
      | TraumaAttack CoerciveBargain -> true, "Trauma Gambit: Coercive Bargain", 20
      | TraumaAttack ApathyDoldrums -> true, "Trauma Gambit: Apathy Doldrums", 10
      | TraumaAttack SereneResolution -> false, "", 0
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

    // Arcane vector proficiency and off-specialization mental strain
    let profRatio =
      if atk.Mode = CombatMode.Arcane then
        currentActor.GetArcaneProficiency vector
      else
        1.0

    let profMult =
      if atk.Mode = CombatMode.Arcane then
        0.40 + 0.60 * profRatio
      else
        1.0

    if atk.Mode = CombatMode.Arcane && profRatio < 0.85 then
      let strain = Math.Max(1, int (Math.Round(16.0 * (1.0 - profRatio))))
      let profPct = int (Math.Round(profRatio * 100.0))
      let spellName =
        match atk with
        | ArcaneCataclysm _ -> "Arcane Cataclysm"
        | SynapticGlamour _ -> "Synaptic Glamour"
        | MirrorIllusion _ -> "Mirror Illusion"
        | RunicWardTrap _ -> "Runic Ward Trap"
        | DisorientingShockwave _ -> "Disorienting Shockwave"
        | TraumaAttack DenialPhaseShift -> "Denial Phase Shift"
        | TraumaAttack BasaltEruption -> "Basalt Eruption"
        | TraumaAttack CoerciveBargain -> "Coercive Bargain"
        | TraumaAttack ApathyDoldrums -> "Apathy Doldrums"
        | TraumaAttack SereneResolution -> "Serene Resolution"
        | _ -> "Arcane Spell"
      currentActor <- currentActor |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + strain })
      events <- CombatEvent.ArcaneStrainIncurred(currentActor.Id, spellName, strain, profPct) :: events

    // Arcane Spell Preparation Procs (weaving clones or erecting abjuration wards)
    match atk with
    | MirrorIllusion isDecoySwarm ->
      let actorAcuity = currentActor.GetStat Acuity
      let maxClones = Math.Clamp(1 + (actorAcuity / 140), 2, 6)
      let baseClones = if isDecoySwarm then 3.0 else 2.0
      let clonesConjured = Math.Max(1, int (Math.Round(baseClones * profRatio)))
      let room = Math.Max(0, maxClones - currentActor.MirrorClones)
      let actualConjured = Math.Min(clonesConjured, room)
      if actualConjured > 0 then
        currentActor <- currentActor |> Combatant.addClones actualConjured
        events <- CombatEvent.MirrorClonesConjured(currentActor.Id, actualConjured, currentActor.MirrorClones) :: events
    | RunicWardTrap isAnomalousGlyph ->
      let glyphScale = if isAnomalousGlyph then 1.0 else 0.6
      let wardErected = Math.Max(10, int (float (currentActor.GetStat Acumen) * glyphScale * profRatio))
      currentActor <- currentActor |> Combatant.addWard wardErected
      events <- CombatEvent.ArcaneWardErected(currentActor.Id, wardErected, currentActor.ArcaneWard) :: events
    | TraumaAttack DenialPhaseShift ->
      currentActor <- currentActor |> Combatant.addClones 1
      events <- CombatEvent.MirrorClonesConjured(currentActor.Id, 1, currentActor.MirrorClones) :: events
    | TraumaAttack SereneResolution ->
      currentActor <- currentActor |> Combatant.updateMeters (fun m -> { m with Recklessness = Meter.Zero })
      currentTarget <- currentTarget |> Combatant.updateMeters (fun m -> { m with Recklessness = Meter.Zero })
    | _ -> ()

    let offStat, defStat, classMult, disparityFactory, meterUpdates =
      match atk with
      // 1. Physical Attacks
      | ForceStrike isWild ->
        let off = currentActor.GetStat Force
        let def = currentTarget.GetStat Fortitude
        let stanceMult = if currentActor.Stance = CombatStance.PowerStance then 1.15 else 1.0
        let wildMult = (if isWild then 1.3 else 1.0) * stanceMult
        let ratio = float off / Math.Max(1.0, float def)
        let baseExhaust =
          if off >= def then
            10 + ((off - def) / 4)
          else
            int (Math.Round(10.0 * Math.Pow(ratio, 2.0)))
        let exhaust = fun isCrit -> baseExhaust + (if isCrit then 25 else 0)
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
        // Probing cadence deals lower base damage (0.55x) while seeking openings
        let baseProbingMult = 0.55 * float hits * reckMult
        let ratio = float off / Math.Max(1.0, float def)
        let baseOverwhelm =
          if off >= def then
            8 + ((off - def) / 5)
          else
            int (Math.Round(8.0 * Math.Pow(ratio, 2.0)))
        let overwhelm = fun isCrit -> (baseOverwhelm * hits) + (if isCrit then 20 else 0)
        let disp = fun isCrit -> if isCrit then Some(ArterialRupture(overwhelm true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Overwhelm = m.Overwhelm + (overwhelm isCrit)
              Recklessness = m.Recklessness + (5 * hits) }

        off, def, baseProbingMult, disp, upd

      | ProwessStrike isInvitational ->
        let off = currentActor.GetStat Prowess
        let def = currentTarget.GetStat Poise
        let studyMult = 1.0 + (float currentActor.StudyStacks * 0.15)
        let baitMult = if isInvitational then 1.25 else 1.0
        let stanceMult = if currentActor.Stance = CombatStance.DisciplineStance then 1.10 else 1.0
        let ratio = float off / Math.Max(1.0, float def)
        let baseFrustrate =
          if off >= def then
            12 + ((off - def) / 4)
          else
            int (Math.Round(12.0 * Math.Pow(ratio, 2.0)))
        let frustrate = fun isCrit -> baseFrustrate + (if isCrit then 25 else 0)
        let disp = fun isCrit -> if isCrit then Some DisarmOrLimbDisable else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Frustration = m.Frustration + (frustrate isCrit)
              Recklessness = m.Recklessness + 10 }

        off, def, (studyMult * baitMult * stanceMult), disp, upd


      | CalculatedFlawStrike stacks ->
        let off = currentActor.GetStat Prowess
        let def = currentTarget.GetStat Poise
        let disparity = Math.Max(0, off - def)
        let spend = Math.Min(currentActor.StudyStacks, Math.Max(2, stacks))
        let mult = 1.2 + (0.35 * float spend)
        let disp = fun isCrit -> if isCrit then Some DisarmOrLimbDisable else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Frustration = m.Frustration + 15 + (disparity / 4)
              Recklessness = m.Recklessness } // 0 recklessness cost!

        off, def, mult, disp, upd

      | MasterfulDisarm _ ->
        let off = currentActor.GetStat Prowess
        let def = currentTarget.GetStat Poise
        let disp = fun _ -> Some DisarmOrLimbDisable

        let upd _ (m: StatusMeters) =
          { m with
              Frustration = m.Frustration + 35
              Recklessness = m.Recklessness } // 0 recklessness cost!

        off, def, 1.0, disp, upd

      // 2. Social Attacks
      | AuthorityDecree isImperious ->
        let off = currentActor.GetStat Intellect
        let def = currentTarget.GetStat Resolve
        let disparity = Math.Max(0, off - def)
        let disparityReck = int (Math.Round(float disparity / 15.0))
        let imperiousMult = if isImperious then 1.3 else 1.0
        let fatigue = fun isCrit -> (if isCrit then 40 else 20) + (disparity / 25)
        let disp = fun isCrit -> if isCrit then Some(CognitiveRupture(fatigue true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              CognitiveFatigue = m.CognitiveFatigue + (fatigue isCrit)
              Recklessness = m.Recklessness + 15 + disparityReck }

        off, def, imperiousMult, disp, upd

      | GuileDeception isConfidenceTrap ->
        let off = currentActor.GetStat Acuity
        let def = currentTarget.GetStat Intuition
        let disparity = Math.Max(0, off - def)
        let disparityReck = int (Math.Round(float disparity / 10.0))
        let args = if isConfidenceTrap then 3 else 1
        let reckMult = 1.0 + (float currentTarget.Meters.Recklessness.Value / 80.0)
        let confusion = fun isCrit -> (if isCrit then 20 * args else 12) + (disparity / 20)
        let disp = fun isCrit -> if isCrit then Some DialecticalParalysis else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Confusion = m.Confusion + (confusion isCrit)
              Recklessness = m.Recklessness + (15 * args) + disparityReck }

        off, def, (float args * reckMult), disp, upd

      | AcumenInterrogation isCheckmate ->
        let off = currentActor.GetStat Acumen
        let def = currentTarget.GetStat Composure
        let disparity = Math.Max(0, off - def)
        let disparityReck = int (Math.Round(float disparity / 10.0))
        let studyMult = 1.0 + (float currentActor.StudyStacks * 0.25)
        let checkmateMult = if isCheckmate then 1.35 else 1.0
        let provoke = fun isCrit -> (if isCrit then 50 else 25) + (disparity / 20)
        let disp = fun isCrit -> if isCrit then Some(StrippedCredibility(provoke true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Provoke = m.Provoke + (provoke isCrit)
              Recklessness = m.Recklessness + 30 + disparityReck }

        off, def, (studyMult * checkmateMult), disp, upd

      // 3. Arcane Attacks
      | ArcaneCataclysm isOverchannel ->
        let off = currentActor.GetStat Intellect
        let def = currentTarget.GetStat Resolve
        let disparity = Math.Max(0, off - def)
        let disparityReck = int (Math.Round(float disparity / 15.0))

        let surge =
          if isOverchannel then
            float currentActor.Meters.Recklessness.Value * 0.5
          else
            0.0

        let cataclysmMult = (1.1 + (surge / Math.Max(10.0, float off))) * profMult
        let fatigue = fun isCrit -> int (float (if isCrit then 45 else 22) * profMult) + (disparity / 25)
        let disp = fun isCrit -> if isCrit then Some(CognitiveRupture(fatigue true)) else None

        let upd isCrit (m: StatusMeters) =
          let f = fatigue isCrit

          { m with
              CognitiveFatigue = m.CognitiveFatigue + f
              Exhaustion = m.Exhaustion + (f / 2)
              Recklessness = m.Recklessness + 15 + disparityReck }

        off, def, cataclysmMult, disp, upd

      | SynapticGlamour isMindFracture ->
        let off = currentActor.GetStat Acuity
        let def = currentTarget.GetStat Intuition
        let disparity = Math.Max(0, off - def)
        let disparityReck = int (Math.Round(float disparity / 10.0))
        let pulses = if isMindFracture then 3 else 1
        let confusion = fun isCrit -> int (float (if isCrit then 18 * pulses else 12) * profMult) + (disparity / 20)
        let disp = fun isCrit -> if isCrit then Some DialecticalParalysis else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Confusion = m.Confusion + (confusion isCrit)
              Recklessness = m.Recklessness + (15 * pulses) + disparityReck }

        off, def, (float pulses * profMult), disp, upd

      | MirrorIllusion isDecoySwarm ->
        let off = currentActor.GetStat Acuity
        let def = currentTarget.GetStat Intuition
        let disparity = Math.Max(0, off - def)
        let disparityReck = int (Math.Round(float disparity / 12.0))
        let swarms = if isDecoySwarm then 2 else 1
        let confusion = fun isCrit -> int (float (if isCrit then 25 else 14) * profMult) + (disparity / 25)
        let disp = fun isCrit -> if isCrit then Some DialecticalParalysis else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Confusion = m.Confusion + (confusion isCrit)
              Recklessness = m.Recklessness + (12 * swarms) + disparityReck }

        off, def, (0.85 * float swarms * profMult), disp, upd

      | RunicWardTrap isAnomalousGlyph ->
        let off = currentActor.GetStat Acumen
        let def = currentTarget.GetStat Composure
        let disparity = Math.Max(0, off - def)
        let disparityReck = int (Math.Round(float disparity / 12.0))
        let studyMult = 1.0 + (float currentActor.StudyStacks * 0.25)
        let glyphMult = if isAnomalousGlyph then 1.25 else 1.0
        let provoke = fun isCrit -> int (float (if isCrit then 45 else 20) * profMult) + (disparity / 25)
        let disp = fun isCrit -> if isCrit then Some(StrippedCredibility(provoke true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Provoke = m.Provoke + (provoke isCrit)
              Recklessness = m.Recklessness + 25 + disparityReck }

        off, def, (studyMult * glyphMult * profMult), disp, upd

      | DisorientingShockwave isStaggeringPulse ->
        let off = currentActor.GetStat Acumen
        let def = currentTarget.GetStat Composure
        let disparity = Math.Max(0, off - def)
        let disparityReck = int (Math.Round(float disparity / 12.0))
        let studyMult = 1.0 + (float currentActor.StudyStacks * 0.15)
        let pulseMult = if isStaggeringPulse then 1.35 else 1.0
        let provoke = fun isCrit -> int (float (if isCrit then 35 else 18) * profMult) + (disparity / 25)
        let disp = fun isCrit -> if isCrit then Some(StrippedCredibility(provoke true)) else None

        let upd isCrit (m: StatusMeters) =
          { m with
              Provoke = m.Provoke + (provoke isCrit)
              Confusion = m.Confusion + int (float (if isCrit then 25 else 12) * profMult)
              Recklessness = m.Recklessness + 20 + disparityReck }

        off, def, (studyMult * pulseMult * profMult), disp, upd

      | TraumaAttack DenialPhaseShift ->
        let off = currentActor.GetStat Finesse
        let def = currentTarget.GetStat Intuition
        let disparity = Math.Max(0, off - def)
        let disp = fun isCrit -> if isCrit then Some DialecticalParalysis else None
        let upd isCrit (m: StatusMeters) =
          { m with
              Confusion = m.Confusion + 25 + (disparity / 15)
              Frustration = m.Frustration - 15 }
        off, def, 1.25, disp, upd

      | TraumaAttack BasaltEruption ->
        let off = currentActor.GetStat Force
        let def = currentTarget.GetStat Fortitude
        let disparity = Math.Max(0, off - def)
        let disp = fun isCrit -> if isCrit then Some(CrushingBlow(35 + (disparity / 10))) else None
        let upd isCrit (m: StatusMeters) =
          { m with
              Overwhelm = m.Overwhelm + 30
              Exhaustion = m.Exhaustion + 20
              Recklessness = m.Recklessness + 25 }
        currentTarget <- { currentTarget with Armor = currentTarget.Armor.Shred 25 }
        off, def, 1.50, disp, upd

      | TraumaAttack CoerciveBargain ->
        let off = currentActor.GetStat Acumen
        let def = currentTarget.GetStat Resolve
        let disparity = Math.Max(0, off - def)
        let disp = fun isCrit -> if isCrit then Some DialecticalParalysis else None
        let upd isCrit (m: StatusMeters) =
          { m with
              Provoke = m.Provoke + 25
              Frustration = m.Frustration + 20 }
        currentTarget <- { currentTarget with Morale = currentTarget.Morale.ApplyDelta -35 }
        currentActor <- { currentActor with Health = currentActor.Health.ApplyDelta 25 }
        off, def, 1.15, disp, upd

      | TraumaAttack ApathyDoldrums ->
        let off = currentActor.GetStat Fortitude
        let def = currentTarget.GetStat Composure
        let disparity = Math.Max(0, off - def)
        let disp = fun isCrit -> if isCrit then Some(CognitiveRupture(40)) else None
        let upd isCrit (m: StatusMeters) =
          { m with
              CognitiveFatigue = m.CognitiveFatigue + 40 + (disparity / 10)
              Exhaustion = m.Exhaustion + 25 }
        off, def, 0.70, disp, upd

      | TraumaAttack SereneResolution ->
        let off = currentActor.GetStat Composure
        let def = currentTarget.GetStat Resolve
        let disp = fun _ -> None
        let upd _ (m: StatusMeters) =
          { m with
              Recklessness = Meter.Zero
              Frustration = Meter.Zero
              Provoke = Meter.Zero }
        currentActor <- { currentActor with Morale = currentActor.Morale.ApplyDelta 50 }
        currentTarget <- { currentTarget with Morale = currentTarget.Morale.ApplyDelta 50 }
        off, def, 0.0, disp, upd

    // Check if an offensive MasterfulDisarm was declared but conditions were not met
    let disarmFailedEarly =
      match atk with
      | MasterfulDisarm _ ->
        let off = currentActor.GetStat Prowess
        let def = currentTarget.GetStat Poise
        let isPossible = off >= int (Math.Round(float def * 0.75))
        let required = Math.Max(3, int (Math.Ceiling((float def / Math.Max(1.0, float off)) * 3.5)))
        if not isPossible || currentActor.StudyStacks < required then
          events <-
            CombatEvent.DisarmExecuted(
              currentTarget.Id,
              currentActor.Id,
              "Masterful disarm failed: opponent's Poise is too commanding for low Prowess."
            )
            :: events

          currentActor <- { currentActor with ComboTracker = currentActor.ComboTracker.ResetCombo() }
          true
        else
          false
      | _ -> false

    if disarmFailedEarly then
      let finalActor = Combatant.evaluateCollapse currentActor
      let finalTarget = Combatant.evaluateCollapse currentTarget
      { Actor = finalActor
        Target = finalTarget
        Events = events
        Contest = None }
    else

    // Calculate effective prior defenses taking into account defensive preparations
    // BastionZoneControl and CaltropPouch limit frontline attackers to 3, negating flanking spaces via tactical footwork / sharp ground denial.
    let effectivePriorDefenses =
      if currentTarget.HasActivePreparation PreparationType.BastionZoneControl then Math.Min(2, priorDefenses)
      elif currentTarget.HasActivePreparation PreparationType.CaltropPouch then Math.Min(2, priorDefenses)
      elif currentTarget.HasActivePreparation PreparationType.MirrorMirage then Math.Max(0, priorDefenses - 5)
      else priorDefenses

    // Caltrops: The 5 flanking spaces are nearly impassable because they are sharp and painful.
    // They do NOT reduce priorDefense to 0, but flankers traversing the caltrop field suffer sharp puncture wounds!
    let mutable caltropKilledFlanker = false
    if priorDefenses >= 1 && priorDefenses <= 5 && currentTarget.HasActivePreparation PreparationType.CaltropPouch then
      let defFinesse = currentTarget.GetStat Finesse
      let caltropDmg = Math.Max(20, int (float defFinesse * 0.20))
      let actorAfterCaltrop, caltropDmgEvt, wardEvts = applyDamage Physical caltropDmg false currentActor
      events <- wardEvts @ (CombatEvent.DamageApplied caltropDmgEvt :: CombatEvent.PreparationDeployed(currentTarget.Id, PreparationType.CaltropPouch, Some currentActor.Id, sprintf "Flanker traversed sharp caltrops, taking %d puncture damage and losing footing (+15 Overwhelm)!" caltropDmg) :: events)
      currentActor <-
        actorAfterCaltrop
        |> Combatant.updateMeters (fun m -> { m with Overwhelm = m.Overwhelm + 15 })
        |> Combatant.evaluateCollapse
      if currentActor.Health.IsDepleted || currentActor.Morale.IsDepleted then
        caltropKilledFlanker <- true

    if caltropKilledFlanker then
      let finalActor = Combatant.evaluateCollapse currentActor
      let finalTarget = Combatant.evaluateCollapse currentTarget
      { Actor = finalActor
        Target = finalTarget
        Events = events
        Contest = None }
    else

    // --- Step B.3.9: Defender Concealed Blade Counter-Puncture from the Nach ---
    let mutable concealedBladeDisrupted = false
    if currentTarget.HasActivePreparation PreparationType.ConcealedBlade then
      let updatedDefender, updatedAttacker, bladeEvents, isDisrupted =
        IndesResolver.resolveConcealedBlade roller currentTarget currentActor
      currentTarget <- updatedDefender
      currentActor <- updatedAttacker
      events <- bladeEvents @ events
      if isDisrupted then
        concealedBladeDisrupted <- true

    if concealedBladeDisrupted then
      let finalActor = Combatant.evaluateCollapse currentActor
      let finalTarget = Combatant.evaluateCollapse currentTarget
      { Actor = finalActor
        Target = finalTarget
        Events = events
        Contest = None }
    else

    // --- Step B.4.1: Defender Mirror Mirage Phantasm Deception (Acuity vs. Intuition) ---
    // Capped at up to 5 flanking spaces per round; priorDefenses penalty removed, limited by stat minus Exhaustion/Recklessness
    let mutable mirrorMirageDeceived = false
    if priorDefenses >= 1 && priorDefenses <= 5 && currentTarget.HasActivePreparation PreparationType.MirrorMirage then
      let defAcuity = currentTarget.GetStat Acuity
      let atkIntuition = currentActor.GetStat Intuition
      let delta = defAcuity - atkIntuition
      let rollMargin = (roller 1 6 - roller 1 6) * 3
      let fatiguePenalty = (currentTarget.Meters.Exhaustion.Value / 4) + (currentTarget.Meters.Recklessness.Value / 4)
      let slope = if delta >= 0 then 0.8 else 1.4
      let baseChance = 55.0
      let deceiveChance = Math.Clamp(int (baseChance + float delta * slope) + rollMargin - fatiguePenalty, 5, 95)
      if roller 1 100 <= deceiveChance then
        let blastDamage = Math.Max(15, int (float defAcuity * 0.08))
        let actorAfterBlast, _, wardEvts = applyDamage Mental blastDamage false currentActor
        events <- wardEvts @ events
        events <- CombatEvent.MirrorCloneShattered(currentTarget.Id, currentActor.Id, blastDamage, 0) :: events
        currentActor <-
          actorAfterBlast
          |> Combatant.updateMeters (fun m -> { m with Confusion = m.Confusion + 20; Recklessness = m.Recklessness + 10 })
          |> fun a -> { a with ComboTracker = a.ComboTracker.ResetCombo() }
        events <- CombatEvent.MirrorMirageDeceived(currentTarget.Id, currentActor.Id, 20) :: events
        events <- CombatEvent.ComboReset(currentActor.Id, sprintf "Strike struck Mirror Mirage phantasm! The decoy shattered, blasting flanker for %d Morale damage!" blastDamage) :: events
        mirrorMirageDeceived <- true

    if mirrorMirageDeceived then
      let finalActor = Combatant.evaluateCollapse currentActor
      let finalTarget = Combatant.evaluateCollapse currentTarget
      { Actor = finalActor
        Target = finalTarget
        Events = events
        Contest = None }
    else

    // --- Step B.4: Defender Preemptive Attack of Opportunity against Flank / Encirclement ---
    let mutable flankDefusedByAoO = false

    if priorDefenses >= 1 && plane = Physical then
      let defFinesse = currentTarget.GetStat Finesse
      let atkReflex = currentActor.GetStat Reflex
      let agilityDelta = defFinesse - atkReflex

      let defProwess = currentTarget.GetStat Prowess
      let atkPoise = currentActor.GetStat Poise
      let disciplineDelta = defProwess - atkPoise

      // Power is explicitly excluded from Attacks of Opportunity!
      let aooChoice =
        if currentTarget.Stance = CombatStance.AgilityStance && agilityDelta > 0 then
          Some ("Agility", agilityDelta, defFinesse)
        elif currentTarget.Stance = CombatStance.DisciplineStance && disciplineDelta > 0 then
          Some ("Discipline", disciplineDelta, defProwess)
        elif disciplineDelta >= agilityDelta && disciplineDelta > 0 then
          Some ("Discipline", disciplineDelta, defProwess)
        elif agilityDelta > 0 then
          Some ("Agility", agilityDelta, defFinesse)
        else
          None

      match aooChoice with
      | Some (vectorName, delta, defStat) ->
        let baseTriggerChance = Math.Min(85, Math.Max(15, int (Math.Round((float delta / float defStat) * 80.0)) + 15))

        // Defensive penalties directly degrade opportunity attack responsiveness:
        // 1. Finesse (Agility) stance focuses on linear 1-on-1 duels; duelist tunnel-vision incurs a -20% AoO penalty compared to Prowess (Discipline):
        let stanceAoOPenalty = if currentTarget.Stance = CombatStance.AgilityStance then 20 else 0
        // 2. Encirclement defense penalty (attention & guard split across multiple attackers)
        let encPenaltyHits = DicePool.computeEncirclementPenalty offStat defStat effectivePriorDefenses
        let encReduction = encPenaltyHits * 5
        // 3. Physical limb trauma / severed ligaments
        let limbReduction = currentTarget.LimbDebuff / 2
        // 4. Physical stamina exhaustion / fatigue
        let fatigueReduction = currentTarget.Meters.Exhaustion.Value / 4

        let totalDefensivePenalty = stanceAoOPenalty + encReduction + limbReduction + fatigueReduction
        let effectiveTriggerChance = Math.Max(0, baseTriggerChance - totalDefensivePenalty)

        if roller 1 100 <= effectiveTriggerChance then

          let aooDmg =
            if vectorName = "Agility" then
              Math.Max(10, int (float defFinesse * 0.65))
            else
              Math.Max(10, int (float defProwess * 0.75))

          currentActor <- { currentActor with Health = currentActor.Health.ApplyDelta -aooDmg }

          // Risk of exhaustion and recklessness increased on reactive lunges:
          let aooExhaustionDrain =
            if delta >= 250 then 1
            elif delta >= 150 then 1
            elif delta >= 60 then 2
            else 3

          let aooRecklessnessGain =
            if delta >= 250 then 1
            elif delta >= 150 then 2
            else 3

          currentTarget <-
            currentTarget
            |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + aooExhaustionDrain; Recklessness = m.Recklessness + aooRecklessnessGain })

          if vectorName = "Discipline" then
            // Discipline masters read their opponent's martial school and gain Study Stacks
            currentTarget <- currentTarget |> Combatant.addStudyStacks 1
          elif vectorName = "Agility" then
            currentActor <- currentActor |> Combatant.updateMeters (fun m -> { m with Overwhelm = m.Overwhelm + 10 })

          // Disruption window tightened:
          let isDisrupted =
            currentActor.Health.IsDepleted
            || delta >= 220
            || (delta >= 100 && roller 1 100 <= 35)

          events <- CombatEvent.AttackOfOpportunityTriggered(currentTarget.Id, currentActor.Id, vectorName, aooDmg, isDisrupted) :: events
          events <- CombatEvent.DamageApplied {
            TargetId = currentActor.Id
            Plane = Physical
            Amount = aooDmg
            IsCritical = false
            IsArmorCompromised = false
          } :: events

          if isDisrupted then
            currentActor <- { currentActor with ComboTracker = currentActor.ComboTracker.ResetCombo() }
            events <- CombatEvent.ComboReset(currentActor.Id, "Flank approach intercepted by Attack of Opportunity; incoming strike defused.") :: events
            flankDefusedByAoO <- true
      | None -> ()

    if flankDefusedByAoO then
      let finalActor = Combatant.evaluateCollapse currentActor
      let finalTarget = Combatant.evaluateCollapse currentTarget
      { Actor = finalActor
        Target = finalTarget
        Events = events
        Contest = None }
    else

    // --- Step B.4: Magic Class Passive Generators (Once per round on primary engagement, costs focus) ---
    if priorDefenses = 0 then
      // A. Mesmer: Passive Clone Weaving based on Intuition against target Acuity, max clones strictly capped at 5
      let defAcuity = currentTarget.GetStat Acuity
      let defIntuition = currentTarget.GetStat Intuition
      let atkAcuity = currentActor.GetStat Acuity
      let currentExhaustion = currentTarget.Meters.Exhaustion.Value
      let currentFatigue = currentTarget.Meters.CognitiveFatigue.Value
      let fatigueTolerance = Math.Min(85, 45 + (defIntuition / 12))
      let maxClones = Math.Clamp(currentTarget.Progression.Level / 40 + 1, 1, 5)

      if currentTarget.Class = CharacterClass.Mesmer && currentTarget.MirrorClones < maxClones && currentFatigue < fatigueTolerance && currentExhaustion < 75 then
        let statDelta = defIntuition - atkAcuity
        let fatiguePenalty = (currentFatigue / 4) + (currentExhaustion / 4)
        let statSlope = if statDelta >= 0 then 0.25 else 0.40
        let weaveChance = Math.Clamp(int (50.0 + float statDelta * statSlope) - fatiguePenalty, 10, 90)
        if roller 1 100 <= weaveChance then
          let atkIntuition = currentActor.GetStat Intuition
          let disparity = defIntuition - atkIntuition
          let isOverwhelming = (currentTarget.Progression.Level - currentActor.Progression.Level >= 60) || disparity >= 550
          let fatigueCost =
            if isOverwhelming then 0
            elif disparity >= 200 then 1
            elif disparity >= 30 then 2
            else 4

          let clonesToWeave = Math.Max(1, maxClones - currentTarget.MirrorClones)
          currentTarget <-
            currentTarget
            |> Combatant.addClones clonesToWeave
            |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + fatigueCost })
          let costMsg = if fatigueCost > 0 then sprintf "(+%d Fatigue) " fatigueCost else "(Effortless) "
          events <- CombatEvent.PassiveGenerationTriggered(currentTarget.Id, sprintf "Mesmer intuitively wove ambient mirror clones %s(Active Clones: %d/%d)." costMsg currentTarget.MirrorClones maxClones) :: events

      // B. Abjurer: Passive Composure Abjuration Ward (Disparity-Scaled Cost)
      if (currentTarget.Class = CharacterClass.Abjurer || currentTarget.Class = CharacterClass.Strategist) then
        let defComposure = currentTarget.GetStat Composure
        let atkComposure = currentActor.GetStat Composure
        let disparity = defComposure - atkComposure
        let isOverwhelming = (currentTarget.Progression.Level - currentActor.Progression.Level >= 60) || disparity >= 550
        let fatigueCost =
          if isOverwhelming then 0
          elif disparity >= 200 then 1
          elif disparity >= 30 then 2
          else 4
        let fatigueCap = if isOverwhelming then 90 else 65
        if currentTarget.Meters.CognitiveFatigue.Value < fatigueCap then
          let wardGen = Math.Max(25, defComposure / 8)
          currentTarget <-
            currentTarget
            |> Combatant.addWard wardGen
            |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + fatigueCost })
          let costMsg = if fatigueCost > 0 then sprintf "(+%d Fatigue) " fatigueCost else "(Effortless) "
          events <- CombatEvent.PassiveGenerationTriggered(currentTarget.Id, sprintf "Abjurer reinforced composure ward by +%d %s(Active Ward: %d)." wardGen costMsg currentTarget.ArcaneWard) :: events

      // C. Inquisitor: Imposing Dread Presence (Disparity-Scaled Cost)
      if currentTarget.Class = CharacterClass.Inquisitor && currentActor.Meters.Recklessness.Value < 20 then
        let defIntellect = currentTarget.GetStat Intellect
        let atkResolve = currentActor.GetStat Resolve
        let disparity = defIntellect - atkResolve
        let isOverwhelming = (currentTarget.Progression.Level - currentActor.Progression.Level >= 60) || disparity >= 550
        let fatigueCost =
          if isOverwhelming then 0
          elif disparity >= 200 then 1
          elif disparity >= 30 then 2
          else 4
        let fatigueCap = if isOverwhelming then 90 else 60
        if currentTarget.Meters.CognitiveFatigue.Value < fatigueCap then
          let dreadSpike = 12
          currentActor <- currentActor |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + dreadSpike })
          currentTarget <- currentTarget |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + fatigueCost })
          let costMsg = if fatigueCost > 0 then sprintf "(+%d Fatigue) " fatigueCost else ""
          events <- CombatEvent.PassiveGenerationTriggered(currentTarget.Id, sprintf "Inquisitor's imposing psychic presence %sunsettled the attacker (+%d Recklessness)!" costMsg dreadSpike) :: events

    // --- Step B.4.5: Magic & Tactical Nach/Indes Counter-Reactions ---
    let mutable decoyIntercepted = false
    let mutable attackDamageMitigation = 1.0

    // A. Mesmer / Mirror Weavers: Phantasmal Decoy Swap (Acuity vs. Intuition)
    // Capped at up to 5 flanking spaces; priorDefenses penalty removed, limited by stat minus Exhaustion/Recklessness
    if not decoyIntercepted && currentTarget.MirrorClones > 0 then
      let defAcuity = currentTarget.GetStat Acuity
      let atkIntuition = currentActor.GetStat Intuition
      let delta = defAcuity - atkIntuition
      let disparity = delta
      let isOverwhelming = (currentTarget.Progression.Level - currentActor.Progression.Level >= 60) || disparity >= 550
      let fatigueCap = if isOverwhelming then 90 else 75
      if currentTarget.Meters.CognitiveFatigue.Value < fatigueCap then
        let rollMargin = (roller 1 6 - roller 1 6) * 3
        let fatiguePenalty = (currentTarget.Meters.Exhaustion.Value / 4) + (currentTarget.Meters.Recklessness.Value / 4)
        let slope = if delta >= 0 then 0.8 else 1.4
        let baseChance = 55.0
        let swapChance = Math.Clamp(int (baseChance + float delta * slope) + rollMargin - fatiguePenalty, 5, 95)
        if priorDefenses < 5 && roller 1 100 <= swapChance then
          let fatigueCost, exhaustionCost =
            if isOverwhelming then 0, 0
            elif disparity >= 200 then 1, 1
            elif disparity >= 30 then 3, 2
            else 5, 3
          currentTarget <-
            currentTarget
            |> Combatant.addClones -1
            |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + fatigueCost; Exhaustion = m.Exhaustion + exhaustionCost })
          let costMsg = if fatigueCost > 0 || exhaustionCost > 0 then sprintf " (+%d Fatigue, +%d Exhaustion)" fatigueCost exhaustionCost else " (Effortless)"

          // The clone SHATTERS as soon as it is attacked!
          let blastDamage = Math.Max(30, int (float defAcuity * 0.18))
          let actorAfterBlast, _, wardEvts = applyDamage Mental blastDamage false currentActor
          events <- wardEvts @ events
          events <- CombatEvent.MirrorCloneShattered(currentTarget.Id, currentActor.Id, blastDamage, currentTarget.MirrorClones) :: events
          events <- CombatEvent.PhantasmalSwapExecuted(currentTarget.Id, currentActor.Id, true, sprintf "Mesmer actively swapped places with a decoy clone! The decoy SHATTERED upon impact, blasting the attacker for %d Morale damage%s!" blastDamage costMsg) :: events
          events <- CombatEvent.MirrorCloneDecoyed(currentTarget.Id, currentActor.Id, currentTarget.MirrorClones) :: events
          currentActor <-
            actorAfterBlast
            |> fun a -> { a with ComboTracker = a.ComboTracker.ResetCombo() }
            |> Combatant.updateMeters (fun m -> { m with Confusion = m.Confusion + 25; Recklessness = m.Recklessness + 15 })
          events <- CombatEvent.ComboReset(currentActor.Id, "Attacker blasted off-balance by shattered mirror clone.") :: events
          decoyIntercepted <- true
        elif priorDefenses < 5 then
          events <- CombatEvent.PhantasmalSwapExecuted(currentTarget.Id, currentActor.Id, false, "Attacker saw through the mirror swap mid-motion!") :: events

    // B. Abjurer: Destabilizing Ground Ward (Acumen vs. Poise)
    // Capped at up to 5 flanking spaces; priorDefenses penalty removed, limited by stat minus Exhaustion/Recklessness
    if not decoyIntercepted && (currentTarget.Class = CharacterClass.Abjurer || currentTarget.Class = CharacterClass.Strategist) then
      let defAcumen = currentTarget.GetStat Acumen
      let atkPoise = currentActor.GetStat Poise
      let disparity = defAcumen - atkPoise
      let isOverwhelming = (currentTarget.Progression.Level - currentActor.Progression.Level >= 60) || disparity >= 550
      let fatigueCap = if isOverwhelming then 90 else 75
      if currentTarget.Meters.CognitiveFatigue.Value < fatigueCap then
        let fatiguePenalty = (currentTarget.Meters.Exhaustion.Value / 4) + (currentTarget.Meters.Recklessness.Value / 4)
        let acumenMargin = (defAcumen / 20) - (atkPoise / 15) - fatiguePenalty + (roller 1 6 - roller 1 6)
        if acumenMargin >= 3 && priorDefenses < 5 then
          let fatigueCost, exhaustionCost =
            if isOverwhelming then 0, 0
            elif disparity >= 200 then 2, 1
            elif disparity >= 30 then 5, 2
            else 8, 4
          currentTarget <- currentTarget |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + fatigueCost; Exhaustion = m.Exhaustion + exhaustionCost })
          let costMsg = if fatigueCost > 0 || exhaustionCost > 0 then sprintf " (+%d Fatigue, +%d Exhaustion)" fatigueCost exhaustionCost else " (Effortless)"
          let desc = sprintf "Abjurer projected a ground ward under the attacker mid-swing! The attacker stumbled and fell%s!" costMsg
          events <- CombatEvent.DestabilizingWardTriggered(currentTarget.Id, currentActor.Id, desc, 1.0) :: events

          // Heavy Frustration and kinetic impact damage from tripping on destabilizing ward:
          let frustSpike = Math.Max(35, 40 + (disparity / 15))
          let impactDmg = Math.Max(25, int (float defAcumen * 0.20))
          let actorAfterImpact, impactDmgEvt, wardEvts = applyDamage Mental impactDmg false currentActor
          events <- wardEvts @ (CombatEvent.DamageApplied impactDmgEvt :: CombatEvent.DestabilizingWardTripped(currentTarget.Id, currentActor.Id, impactDmg, frustSpike) :: events)
          currentActor <-
            actorAfterImpact
            |> fun a -> { a with ComboTracker = a.ComboTracker.ResetCombo() }
            |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + frustSpike; Overwhelm = m.Overwhelm + 25; Exhaustion = m.Exhaustion + 15 })
          events <- CombatEvent.ComboReset(currentActor.Id, "Attacker lost footing on destabilizing ground ward.") :: events
          decoyIntercepted <- true
        elif acumenMargin >= 0 && priorDefenses < 5 then
          let fatigueCost =
            if isOverwhelming then 0
            elif disparity >= 200 then 1
            elif disparity >= 30 then 2
            else 4
          currentTarget <- currentTarget |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + fatigueCost })
          let costMsg = if fatigueCost > 0 then sprintf " (+%d Fatigue)" fatigueCost else " (Effortless)"
          let desc = sprintf "Destabilizing ground ward disrupted attacker's stance! Incoming strike glanced (-50%% damage)%s." costMsg
          events <- CombatEvent.DestabilizingWardTriggered(currentTarget.Id, currentActor.Id, desc, 0.5) :: events
          currentActor <- currentActor |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 20 })
          attackDamageMitigation <- 0.50

    // C. Inquisitor: Synaptic Mind-Shock (Intellect vs. Fortitude)
    // Capped at up to 5 flanking spaces; priorDefenses penalty removed, limited by stat minus Exhaustion/Recklessness
    if not decoyIntercepted && currentTarget.Class = CharacterClass.Inquisitor then
      let defIntellect = float (currentTarget.GetStat Intellect)
      let atkFortitude = float (currentActor.GetStat Fortitude)
      let disparity = int defIntellect - int atkFortitude
      let isOverwhelming = (currentTarget.Progression.Level - currentActor.Progression.Level >= 60) || disparity >= 550
      let fatigueCap = if isOverwhelming then 90 else 75
      if currentTarget.Meters.CognitiveFatigue.Value < fatigueCap then
        let ratio = defIntellect / Math.Max(1.0, atkFortitude)
        let fatiguePenalty = float ((currentTarget.Meters.Exhaustion.Value / 4) + (currentTarget.Meters.Recklessness.Value / 4)) * 0.2
        let shockMargin = (ratio * 2.5) - 1.5 - fatiguePenalty + float (roller 1 6 - roller 1 6)
        if shockMargin >= 2.5 && priorDefenses < 5 then
          let fatigueCost =
            if isOverwhelming then 0
            elif disparity >= 200 then 2
            elif disparity >= 30 then 5
            else 10
          currentTarget <- currentTarget |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + fatigueCost })
          events <- CombatEvent.SynapticMindShockDisrupted(currentTarget.Id, currentActor.Id, 30, true) :: events
          currentActor <-
            { currentActor with ComboTracker = currentActor.ComboTracker.ResetCombo() }
            |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + 30; Frustration = m.Frustration + 15 })
          events <- CombatEvent.ComboReset(currentActor.Id, "Attacker's focus shattered by synaptic mind-shock.") :: events
          decoyIntercepted <- true
        elif shockMargin >= 0.0 && priorDefenses < 5 then
          let fatigueCost =
            if isOverwhelming then 0
            elif disparity >= 200 then 1
            elif disparity >= 30 then 2
            else 5
          currentTarget <- currentTarget |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + fatigueCost })
          events <- CombatEvent.SynapticMindShockDisrupted(currentTarget.Id, currentActor.Id, 15, false) :: events
          currentActor <- currentActor |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + 15 })
          attackDamageMitigation <- Math.Min(attackDamageMitigation, 0.70)

    if decoyIntercepted then
      let finalActor = Combatant.evaluateCollapse currentActor
      let finalTarget = Combatant.evaluateCollapse currentTarget
      { Actor = finalActor
        Target = finalTarget
        Events = events
        Contest = None }
    else

    // --- Step B.5: Defender Passive Reactive Defense (Riposte / Disarm) from accumulated Study Stacks ---
    let mutable defenderDisarmedAttacker = false


    if plane = Physical && currentTarget.StudyStacks > 0 then
      let counterChance = Math.Min(70, currentTarget.StudyStacks * 8)
      if roller 1 100 <= counterChance then
        let defProwess = currentTarget.GetStat Prowess
        let atkPoise = currentActor.GetStat Poise
        let isDisarmPossible = defProwess >= int (Math.Round(float atkPoise * 0.75))
        let requiredStacksForDisarm = Math.Max(3, int (Math.Ceiling((float atkPoise / Math.Max(1.0, float defProwess)) * 3.5)))

        if isDisarmPossible && currentTarget.StudyStacks >= requiredStacksForDisarm then
          // Disarm counter: degrades attacker weapon, resets combo, spikes Frustration, defuses attack
          let degradedWeapon = WeaponCondition.degradation currentActor.WeaponCondition
          currentActor <-
            { currentActor with
                WeaponCondition = degradedWeapon
                ComboTracker = currentActor.ComboTracker.ResetCombo() }
            |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 25 })
          events <- CombatEvent.DisarmExecuted(currentTarget.Id, currentActor.Id, "Predicted attack! Reactive disarm deflected the blow and compromised weapon.") :: events
          events <- CombatEvent.WeaponDegraded(currentActor.Id, degradedWeapon) :: events
          defenderDisarmedAttacker <- true
        else
          // Riposte counter: deals reactive damage using defender's Prowess
          let riposteDmg = Math.Max(5, int (float (currentTarget.GetStat Prowess) * 0.75))
          currentActor <-
            { currentActor with
                Health = currentActor.Health.ApplyDelta -riposteDmg }
            |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 15 })
          events <- CombatEvent.RiposteExecuted(currentTarget.Id, currentActor.Id, riposteDmg) :: events

    if defenderDisarmedAttacker then
      let finalActor = Combatant.evaluateCollapse currentActor
      let finalTarget = Combatant.evaluateCollapse currentTarget
      { Actor = finalActor
        Target = finalTarget
        Events = events
        Contest = None }
    else

    // --- Step C: Opposed Resolution via DicePool ---
    let contest =
      DicePool.resolveContestEx
        roller
        vector
        offStat
        currentActor.StudyStacks
        defStat
        currentTarget.StudyStacks
        effectivePriorDefenses

    if contest.EncirclementPenalty > 0 then
      events <- CombatEvent.EncirclementPenalized(currentTarget.Id, effectivePriorDefenses, contest.EncirclementPenalty) :: events

    // --- Step D: Whiff vs. Landed Hit Branching ---
    if contest.IsWhiff then
      // Whiff: 0 damage, clear combo momentum. Clean deflection - no overwhelm inflicted!
      currentActor <-
        { currentActor with
            ComboTracker = currentActor.ComboTracker.ResetCombo() }

      events <-
        CombatEvent.ComboReset(currentActor.Id, "Attack failed to penetrate defenses; combo momentum cleared.")
        :: events

      // Evaluate Indes tempo seizure opportunity (enhanced by Parrying Buckler)
      let updatedDef, updatedAtk, indesEvents, seized =
        IndesResolver.resolveIndesOpportunity currentTarget currentActor contest
      if seized then
        currentTarget <- updatedDef
        currentActor <- updatedAtk
        events <- events @ indesEvents

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
      // Landed Strike: Scaled Tiered NetHits Damage & Weapon Degradation Multiplier
      let baseDamage =
        if plane = Mental then
          // Cognitive and psychic strikes scale superlinearly with raw mental dominance
          int (float offStat * 3.0) + (offStat * offStat / 180)
        else
          offStat * 2
      let tierMult = computeTierMultiplier contest.NetHits
      let gambitMult = if isGambit then 1.5 else 1.0
      let weaponEff = if plane = Mental then 1.0 else WeaponCondition.effectiveness currentActor.WeaponCondition
      let berserkMult = if currentActor.HasActivePreparation PreparationType.BerserkTincture then 1.35 else 1.0

      // If strike landed while defender was encircled, apply flank overwhelm pressure scaled by disparity
      if contest.EncirclementPenalty > 0 then
        let rawRatio = float offStat / Math.Max(1.0, float defStat)
        let effectiveRatio = if rawRatio < 1.0 then Math.Pow(rawRatio, 2.0) else rawRatio
        let rawFlankOverwhelm = int (Math.Round(float (contest.EncirclementPenalty / 2) * effectiveRatio))
        let flankOverwhelm =
          if currentTarget.HasActivePreparation PreparationType.BerserkTincture then
            rawFlankOverwhelm / 2
          else
            rawFlankOverwhelm
        if flankOverwhelm > 0 then
          currentTarget <- Combatant.updateMeters (fun m -> { m with Overwhelm = m.Overwhelm + flankOverwhelm }) currentTarget

      // Agility crit bonus: combo tracker + opponent overwhelm increases crit chance
      let isAgilityAtk = match atk with FinesseCadence _ -> true | _ -> false
      let agilityCritRolled =
        if isAgilityAtk then
          let comboBonus = currentActor.ComboTracker.VitalOpeningBonus
          let overwhelmBonus = currentTarget.Meters.Overwhelm.Value / 2
          let stanceBonus = if currentActor.Stance = CombatStance.AgilityStance then 15 else 0
          let critChance = Math.Min(90, 10 + comboBonus + overwhelmBonus + stanceBonus)
          roller 1 100 <= critChance
        else
          false

      let isCrit = contest.IsCritical || agilityCritRolled
      let critDmgMult = if isCrit && isAgilityAtk then 3.2 elif isCrit then 1.5 else 1.0
      let baseRawDmg =
        if classMult <= 0.0 then 0
        else Math.Max(1, int (float baseDamage * tierMult * gambitMult * classMult * weaponEff * critDmgMult * berserkMult * attackDamageMitigation))

      // Aegis of Retribution: 35% damage reduction applied to recipient
      let rawDmg =
        if currentTarget.HasActivePreparation PreparationType.AegisOfRetribution then
          Math.Max(1, int (Math.Round(float baseRawDmg * 0.65)))
        else
          baseRawDmg

      // Track Recklessness before updates for Neurotoxin check
      let reckBefore = currentTarget.Meters.Recklessness.Value

      // Apply dynamic status meters to target
      currentTarget <- Combatant.updateMeters (meterUpdates isCrit) currentTarget

      let reckAfter = currentTarget.Meters.Recklessness.Value
      let reckDelta = reckAfter - reckBefore

      // Prismatic Flare: detonates when target gains Recklessness, dealing Morale shock and Confusion
      if reckDelta > 0 && (currentTarget.HasActivePreparation PreparationType.PrismaticFlare || currentActor.HasActivePreparationAgainst PreparationType.PrismaticFlare currentTarget.Id) then
        let flareDrain = reckDelta * 2 + (currentActor.GetStat Acuity / 25)
        let flareTarget, flareDmgEvt, _ = applyDamage Mental flareDrain false currentTarget
        currentTarget <-
          flareTarget
          |> Combatant.updateMeters (fun m -> { m with Confusion = m.Confusion + 20 })
        events <- CombatEvent.PrismaticFlareBlinded(currentTarget.Id, reckDelta, flareDrain) :: events
        events <- CombatEvent.DamageApplied flareDmgEvt :: events

      // Apply core pool damage and potential armor shred
      let updatedTarget, dmgEvt, wardEvts = applyDamage plane rawDmg isCrit currentTarget
      currentTarget <- updatedTarget
      events <- wardEvts @ (CombatEvent.DamageApplied dmgEvt :: events)

      // Aegis of Retribution / Retribution Ward: reflects incoming damage back to attacker as radiant retribution + Frustration
      let hasRetributionAegis = currentTarget.HasActivePreparation PreparationType.AegisOfRetribution
      let hasAbjurerWard = (currentTarget.Class = CharacterClass.Abjurer || currentTarget.Class = CharacterClass.Strategist) && (wardEvts |> List.exists (function CombatEvent.ArcaneWardAbsorbed _ -> true | _ -> false))
      if (hasRetributionAegis || hasAbjurerWard) && baseRawDmg > 0 then
        let reflectRatio = if hasRetributionAegis then 0.50 else 0.25
        let reflectDmg = Math.Max(12, int (Math.Round(float baseRawDmg * reflectRatio)))
        let frustSpike = if hasRetributionAegis then 15 else 10
        let actorAfterReflect, reflectDmgEvt, reflectWardEvts = applyDamage Mental reflectDmg false currentActor
        currentActor <-
          actorAfterReflect
          |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + frustSpike })
          |> Combatant.evaluateCollapse
        events <- reflectWardEvts @ (CombatEvent.DamageApplied reflectDmgEvt :: CombatEvent.RetributionReflected(currentTarget.Id, currentActor.Id, reflectDmg, frustSpike) :: events)

      // Severe Mental Stat Disparity: Cranial Hemorrhage (Psychic Bleeding)
      if plane = Mental && rawDmg > 0 then
        let mentalDisparity = offStat - defStat
        if mentalDisparity >= 30 || contest.NetHits >= 4 then
          let bleedStacks =
            if mentalDisparity >= 100 || contest.NetHits >= 6 then 3
            elif mentalDisparity >= 50 || contest.NetHits >= 4 then 2
            else 1
          currentTarget <- currentTarget |> Combatant.addBleed bleedStacks
          events <-
            CombatEvent.BleedApplied(currentTarget.Id, bleedStacks, currentTarget.BleedStacks)
            :: CombatEvent.PsychicHemorrhageInflicted(currentActor.Id, currentTarget.Id, bleedStacks, mentalDisparity)
            :: events

      // Synaptic Brand: critical strikes deal 2x Morale damage and inflict Rupture
      if isCrit && (currentTarget.HasActivePreparation PreparationType.SynapticBrand || currentActor.HasActivePreparationAgainst PreparationType.SynapticBrand currentTarget.Id) then
        let brandMoraleDmg = Math.Max(25, rawDmg)
        let brandTarget, brandDmgEvt, _ = applyDamage Mental brandMoraleDmg true currentTarget
        currentTarget <- brandTarget
        events <- CombatEvent.SynapticBrandTriggered(currentActor.Id, currentTarget.Id, brandMoraleDmg) :: events
        events <- CombatEvent.DamageApplied brandDmgEvt :: events
        events <- CombatEvent.DisparityTriggered(currentActor.Id, currentTarget.Id, CognitiveRupture 35) :: events

      // Disparity trigger if critical
      match disparityFactory isCrit with
      | Some disp -> events <- CombatEvent.DisparityTriggered(currentActor.Id, currentTarget.Id, disp) :: events
      | None -> ()

      // If action was MasterfulDisarm, degrade target weapon and reset target combo
      match atk with
      | MasterfulDisarm _ ->
        let degraded = WeaponCondition.degradation currentTarget.WeaponCondition
        currentTarget <-
          { currentTarget with
              WeaponCondition = degraded
              ComboTracker = currentTarget.ComboTracker.ResetCombo() }
        events <- CombatEvent.DisarmExecuted(currentActor.Id, currentTarget.Id, "Masterful disarm wrested the weapon!") :: events
        events <- CombatEvent.WeaponDegraded(currentTarget.Id, degraded) :: events
      | DisorientingShockwave _ ->
        currentTarget <- { currentTarget with ComboTracker = currentTarget.ComboTracker.ResetCombo() }
        events <- CombatEvent.OpponentDisoriented(currentActor.Id, currentTarget.Id, "Disorienting shockwave shattered stance tempo and balance!") :: events
        events <- CombatEvent.ComboReset(currentTarget.Id, "Disorienting shockwave disrupted posture; combo momentum cleared.") :: events
      | _ -> ()

      // Step E: Evaluate Consecutive Passives (scaled by damage dealt)
      let postPassiveActor, postPassiveTarget, passiveEvents =
        evaluatePassives roller vector plane dmgEvt.Amount currentActor currentTarget

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

  let resolveEx (roller: DiceRoller) (intent: ActionIntent) (actor: Combatant) (target: Combatant) (priorDefenses: int) : ActionResult =
    let upkeepActor, upkeepEvents = applyTurnUpkeep actor
    if upkeepActor.Health.IsDepleted then
      { Actor = upkeepActor
        Target = target
        Events = upkeepEvents
        Contest = None }
    else
      let res =
        match intent with
        | RecoveryAction reset -> resolveRecovery reset upkeepActor target
        | ExecuteStrike plane -> resolveExecute plane upkeepActor target
        | ShiftStance stance -> resolveShiftStance stance upkeepActor target
        | StandardAttack atk -> resolveAttack roller atk upkeepActor target priorDefenses
        | DeployPreparation (prep, targetIdOpt) -> resolveDeployPreparation prep targetIdOpt upkeepActor target

      { res with Events = upkeepEvents @ res.Events }

  let resolve (roller: DiceRoller) (intent: ActionIntent) (actor: Combatant) (target: Combatant) : ActionResult =
    resolveEx roller intent actor target 0

  /// Resolves an action turn against a primary target, and if in Power or Discipline stance against multiple opponents,
  /// resolves secondary cleave or chained attacks according to martial stance rules.
  let resolveGroupTurn
    (roller: DiceRoller)
    (intent: ActionIntent)
    (actor: Combatant)
    (primaryTarget: Combatant)
    (adjacentTargets: Combatant list)
    : GroupActionResult =
    // 1. Resolve action against primary target
    let primaryRes = resolveEx roller intent actor primaryTarget 0
    let mutable currentActor = primaryRes.Actor
    let mutable currentPrimary = primaryRes.Target
    let mutable allEvents = primaryRes.Events

    // 2. Handle group-wide deployment of preparations
    match intent with
    | DeployPreparation (PreparationType.DreadWarhorn, _) when not adjacentTargets.IsEmpty ->
      let sonicDmg = Math.Max(20, int (float (currentActor.GetStat Intellect) * 0.35))
      let mutable dreadEvents = []
      let processedDread =
        adjacentTargets
        |> List.map (fun secTarget ->
          let targetAfterDmg, dmgEvt, wardEvts = applyDamage Mental sonicDmg false secTarget
          let targetAfterHit =
            targetAfterDmg
            |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + 25 })
            |> Combatant.evaluateCollapse

          dreadEvents <-
            dreadEvents
            @ wardEvts
            @ [
              CombatEvent.PreparationDeployed(currentActor.Id, PreparationType.DreadWarhorn, Some secTarget.Id, sprintf "Dread Warhorn echoed across secondary flankers (%d Morale damage, +25 Cognitive Fatigue)!" sonicDmg)
              CombatEvent.DamageApplied dmgEvt
            ]

          if CollapseState.isCollapsed targetAfterHit.Collapse && not (CollapseState.isCollapsed secTarget.Collapse) then
            match targetAfterHit.Collapse with
            | CollapseState.Collapsed reason ->
              dreadEvents <- dreadEvents @ [ CombatEvent.CollapseTriggered(targetAfterHit.Id, reason) ]
            | CollapseState.Stable -> ()

          targetAfterHit
        )
      allEvents <- allEvents @ dreadEvents
      { Actor = currentActor
        PrimaryTarget = currentPrimary
        SecondaryTargets = processedDread
        Events = allEvents }

    | DeployPreparation (PreparationType.HeraldicTreatise, _) when not adjacentTargets.IsEmpty ->
      let bonusStudy = 2 * adjacentTargets.Length
      currentActor <- currentActor |> Combatant.addStudyStacks bonusStudy
      allEvents <- allEvents @ [ CombatEvent.HeraldicTreatiseStudied(currentActor.Id, bonusStudy) ]
      { Actor = currentActor
        PrimaryTarget = currentPrimary
        SecondaryTargets = adjacentTargets
        Events = allEvents }

    | DeployPreparation _ ->
      { Actor = currentActor
        PrimaryTarget = currentPrimary
        SecondaryTargets = adjacentTargets
        Events = allEvents }

    | _ ->
      // 3. Shockwave Slam: surplus NetHits (>= 3) spill over as flat kinetic damage to all engaged flankers
      let mutable currentAdjacent = adjacentTargets
      if currentActor.HasActivePreparation PreparationType.ShockwaveSlam then
        match primaryRes.Contest with
        | Some contest when not contest.IsWhiff && contest.NetHits >= 3 && not adjacentTargets.IsEmpty ->
          let excessHits = contest.NetHits - 2
          let flatDmg = Math.Max(15, excessHits * 12)
          let mutable shockwaveEvents = []
          currentAdjacent <-
            adjacentTargets
            |> List.map (fun secTarget ->
              let targetAfterDmg, dmgEvt, wardEvts = applyDamage Physical flatDmg false secTarget
              let targetAfterHit =
                targetAfterDmg
                |> Combatant.updateMeters (fun m -> { m with Overwhelm = m.Overwhelm + 10 })
                |> Combatant.evaluateCollapse

              shockwaveEvents <-
                shockwaveEvents
                @ wardEvts
                @ [
                  CombatEvent.ShockwaveSurplusDamage(currentActor.Id, secTarget.Id, excessHits, flatDmg)
                  CombatEvent.DamageApplied dmgEvt
                ]

              if CollapseState.isCollapsed targetAfterHit.Collapse && not (CollapseState.isCollapsed secTarget.Collapse) then
                match targetAfterHit.Collapse with
                | CollapseState.Collapsed reason ->
                  shockwaveEvents <- shockwaveEvents @ [ CombatEvent.CollapseTriggered(targetAfterHit.Id, reason) ]
                | CollapseState.Stable -> ()

              targetAfterHit
            )
          allEvents <- allEvents @ shockwaveEvents
        | _ -> ()

      let isLandedPhysicalHit =
        match primaryRes.Contest with
        | Some contest when not contest.IsWhiff ->
          match intent with
          | StandardAttack atk when atk.Plane = Physical -> true
          | _ -> false
        | _ -> false

      let isLandedArcaneCataclysm =
        match primaryRes.Contest with
        | Some contest when not contest.IsWhiff ->
          match intent with
          | StandardAttack (ArcaneCataclysm _) -> true
          | _ -> false
        | _ -> false

      let isLandedDisorientingShockwave =
        match primaryRes.Contest with
        | Some contest when not contest.IsWhiff ->
          match intent with
          | StandardAttack (DisorientingShockwave _) -> true
          | _ -> false
        | _ -> false

      let isLandedMentalHit =
        match primaryRes.Contest with
        | Some contest when not contest.IsWhiff ->
          match intent with
          | StandardAttack atk when atk.Plane = Mental -> true
          | _ -> false
        | _ -> false

      if isLandedArcaneCataclysm && not currentAdjacent.IsEmpty then
        // Arcane Cataclysm: Destructive mental burst splashes to up to 5 adjacent targets (the 5 flanking spaces)
        let splashCandidates = currentAdjacent |> List.truncate 5
        let unengaged = currentAdjacent |> List.skip splashCandidates.Length
        let mutable splashEvents = []

        let primaryDmg =
          primaryRes.Events
          |> List.choose (function CombatEvent.DamageApplied d when d.TargetId = currentPrimary.Id && d.Plane = Mental -> Some d.Amount | _ -> None)
          |> List.tryHead
          |> Option.defaultValue (Math.Max(20, currentActor.GetStat Intellect))

        let rawSplashDmg = Math.Max(15, int (float primaryDmg * 0.80))

        let processedSplash =
          splashCandidates
          |> List.map (fun secTarget ->
            let targetAfterDmg, dmgEvt, wardEvts = applyDamage Mental rawSplashDmg false secTarget
            let targetAfterHit =
              targetAfterDmg
              |> Combatant.updateMeters (fun m -> { m with CognitiveFatigue = m.CognitiveFatigue + 15 })
              |> Combatant.evaluateCollapse

            splashEvents <-
              splashEvents
              @ wardEvts
              @ [
                CombatEvent.CataclysmSplashed(currentActor.Id, secTarget.Id, dmgEvt.Amount)
                CombatEvent.DamageApplied dmgEvt
              ]

            if CollapseState.isCollapsed targetAfterHit.Collapse && not (CollapseState.isCollapsed secTarget.Collapse) then
              match targetAfterHit.Collapse with
              | CollapseState.Collapsed reason ->
                splashEvents <- splashEvents @ [ CombatEvent.CollapseTriggered(targetAfterHit.Id, reason) ]
              | CollapseState.Stable -> ()

            targetAfterHit
          )

        { Actor = currentActor
          PrimaryTarget = currentPrimary
          SecondaryTargets = processedSplash @ unengaged
          Events = allEvents @ splashEvents }

      elif isLandedDisorientingShockwave && not currentAdjacent.IsEmpty then
        // Disorienting Shockwave: Multi-target crowd control pulsing outward across up to 5 adjacent targets
        let shockCandidates = currentAdjacent |> List.truncate 5
        let unengaged = currentAdjacent |> List.skip shockCandidates.Length
        let mutable shockEvents = []

        let primaryDmg =
          primaryRes.Events
          |> List.choose (function CombatEvent.DamageApplied d when d.TargetId = currentPrimary.Id && d.Plane = Mental -> Some d.Amount | _ -> None)
          |> List.tryHead
          |> Option.defaultValue (Math.Max(15, currentActor.GetStat Acumen))

        let rawShockDmg = Math.Max(15, int (float primaryDmg * 0.75))

        let processedShock =
          shockCandidates
          |> List.map (fun secTarget ->
            let targetAfterDmg, dmgEvt, wardEvts = applyDamage Mental rawShockDmg false secTarget
            let targetAfterHit =
              { targetAfterDmg with ComboTracker = targetAfterDmg.ComboTracker.ResetCombo() }
              |> Combatant.updateMeters (fun m -> { m with Confusion = m.Confusion + 15; Provoke = m.Provoke + 15; Frustration = m.Frustration + 25 })
              |> Combatant.evaluateCollapse

            shockEvents <-
              shockEvents
              @ wardEvts
              @ [
                CombatEvent.OpponentDisoriented(currentActor.Id, secTarget.Id, "Resonant shockwave pulse shattered balance across adjacent swarm enemies!")
                CombatEvent.ComboReset(secTarget.Id, "Disorienting pulse disrupted posture; combo momentum cleared.")
                CombatEvent.DamageApplied dmgEvt
              ]

            if CollapseState.isCollapsed targetAfterHit.Collapse && not (CollapseState.isCollapsed secTarget.Collapse) then
              match targetAfterHit.Collapse with
              | CollapseState.Collapsed reason ->
                shockEvents <- shockEvents @ [ CombatEvent.CollapseTriggered(targetAfterHit.Id, reason) ]
              | CollapseState.Stable -> ()

            targetAfterHit
          )

        { Actor = currentActor
          PrimaryTarget = currentPrimary
          SecondaryTargets = processedShock @ unengaged
          Events = allEvents @ shockEvents }

      elif isLandedMentalHit && not currentAdjacent.IsEmpty then
        // Mental attacks with stat disparity cause resonant Area of Effect psychic damage across adjacent flankers
        let splashCandidates = currentAdjacent |> List.truncate 2
        let unengaged = currentAdjacent |> List.skip splashCandidates.Length
        let mutable splashEvents = []

        let primaryDmg =
          primaryRes.Events
          |> List.choose (function CombatEvent.DamageApplied d when d.TargetId = currentPrimary.Id && d.Plane = Mental -> Some d.Amount | _ -> None)
          |> List.tryHead
          |> Option.defaultValue (Math.Max(20, currentActor.GetStat Intellect))

        let rawSplashDmg = Math.Max(10, int (float primaryDmg * 0.50))

        let processedSplash =
          splashCandidates
          |> List.map (fun secTarget ->
            let targetAfterDmg, dmgEvt, wardEvts = applyDamage Mental rawSplashDmg false secTarget
            let offStatSec =
              match currentActor.ArcaneFocus with
              | Power -> currentActor.GetStat Intellect
              | Agility -> currentActor.GetStat Acuity
              | Discipline -> currentActor.GetStat Acumen
            let defStatSec =
              match currentActor.ArcaneFocus with
              | Power -> secTarget.GetStat Resolve
              | Agility -> secTarget.GetStat Intuition
              | Discipline -> secTarget.GetStat Composure
            let secDisparity = offStatSec - defStatSec

            let targetWithMeters =
              targetAfterDmg
              |> Combatant.updateMeters (fun m ->
                let baseSpike = 15
                let dispSpike = Math.Max(0, secDisparity / 20)
                let totalSpike = baseSpike + dispSpike
                match currentActor.ArcaneFocus with
                | Power -> { m with CognitiveFatigue = m.CognitiveFatigue + totalSpike; Recklessness = m.Recklessness + 10 }
                | Agility -> { m with Confusion = m.Confusion + totalSpike; Recklessness = m.Recklessness + totalSpike }
                | Discipline -> { m with Provoke = m.Provoke + totalSpike; Recklessness = m.Recklessness + totalSpike }
              )

            // If severe disparity on secondary target, also inflict 1 bleed stack from resonant shockwave
            let targetWithBleed =
              if secDisparity >= 50 then
                targetWithMeters |> Combatant.addBleed 1
              else
                targetWithMeters

            let targetAfterHit = Combatant.evaluateCollapse targetWithBleed

            splashEvents <-
              splashEvents
              @ wardEvts
              @ [
                CombatEvent.PsychicShockwaveResonated(currentActor.Id, secTarget.Id, dmgEvt.Amount)
                CombatEvent.DamageApplied dmgEvt
              ]
            if secDisparity >= 50 then
              splashEvents <- splashEvents @ [ CombatEvent.BleedApplied(targetAfterHit.Id, 1, targetAfterHit.BleedStacks) ]

            if CollapseState.isCollapsed targetAfterHit.Collapse && not (CollapseState.isCollapsed secTarget.Collapse) then
              match targetAfterHit.Collapse with
              | CollapseState.Collapsed reason ->
                splashEvents <- splashEvents @ [ CombatEvent.CollapseTriggered(targetAfterHit.Id, reason) ]
              | CollapseState.Stable -> ()

            targetAfterHit
          )

        { Actor = currentActor
          PrimaryTarget = currentPrimary
          SecondaryTargets = processedSplash @ unengaged
          Events = allEvents @ splashEvents }

      elif not isLandedPhysicalHit || currentAdjacent.IsEmpty then
        { Actor = currentActor
          PrimaryTarget = currentPrimary
          SecondaryTargets = currentAdjacent
          Events = allEvents }
      else
        let isCleaving =
          currentActor.Class = CharacterClass.Berserker
          || currentActor.Stance = CombatStance.PowerStance

        if isCleaving then
          // Power Stance & Berserker Cleave:
          // Dynamic cleave targets scaling with Force / Tier + preparation bonuses
          let baseCleave =
            if currentActor.Class = CharacterClass.Berserker then
              Math.Min(5, Math.Max(2, currentActor.GetStat Force / 35))
            else
              2

          let prepCleaveBonus =
            let shockwaveBonus = if currentActor.HasActivePreparation PreparationType.ShockwaveSlam then 2 else 0
            let berserkBonus = if currentActor.HasActivePreparation PreparationType.BerserkTincture then 1 else 0
            shockwaveBonus + berserkBonus

          let maxCleave = Math.Min(currentAdjacent.Length, baseCleave + prepCleaveBonus)
          let cleaveCandidates = currentAdjacent |> List.truncate maxCleave
          let unengaged = currentAdjacent |> List.skip cleaveCandidates.Length
          let mutable cleaveEvents = []

          let primaryDmg =
            primaryRes.Events
            |> List.choose (function CombatEvent.DamageApplied d when d.TargetId = currentPrimary.Id && d.Plane = Physical -> Some d.Amount | _ -> None)
            |> List.tryHead
            |> Option.defaultValue (Math.Max(20, currentActor.GetStat Force))

          let hasKineticBuff =
            currentActor.HasActivePreparation PreparationType.ShockwaveSlam
            || currentActor.HasActivePreparation PreparationType.BerserkTincture

          let cleaveRatio = if hasKineticBuff then 0.75 else 0.60
          let rawCleaveDmg = Math.Max(10, int (float primaryDmg * cleaveRatio))

          let processedCleaves =
            cleaveCandidates
            |> List.map (fun secTarget ->
              let offForce = currentActor.GetStat Force
              let defFort = secTarget.GetStat Fortitude

              // Recklessness penalty scaled by disparity ratio:
              let disparityRatio = float defFort / Math.Max(1.0, float offForce)
              let rawReckSpike = Math.Max(3, int (Math.Round(25.0 * disparityRatio)))
              let reckSpike =
                if currentActor.HasActivePreparation PreparationType.BerserkTincture then
                  Math.Max(1, rawReckSpike / 2)
                else
                  rawReckSpike

              // Apply armor mitigation
              let soakedDmg = Math.Max(5, int (Math.Round(float rawCleaveDmg * (1.0 - secTarget.Armor.AbsorptionRatio))))
              let newArmor = secTarget.Armor.Shred 5

              // Apply damage & status meters to secondary target
              let targetAfterHit =
                { secTarget with
                    Health = secTarget.Health.ApplyDelta -soakedDmg
                    Armor = newArmor }
                |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + 10 })
                |> Combatant.evaluateCollapse

              // Attacker incurs disparity-based Recklessness
              currentActor <-
                currentActor
                |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + reckSpike })

              cleaveEvents <-
                cleaveEvents
                @ [
                  CombatEvent.CleaveExecuted(currentActor.Id, secTarget.Id, soakedDmg, reckSpike)
                  CombatEvent.DamageApplied {
                    TargetId = secTarget.Id
                    Plane = Physical
                    Amount = soakedDmg
                    IsCritical = false
                    IsArmorCompromised = newArmor.IsShredded
                  }
                ]

              if CollapseState.isCollapsed targetAfterHit.Collapse && not (CollapseState.isCollapsed secTarget.Collapse) then
                match targetAfterHit.Collapse with
                | CollapseState.Collapsed reason ->
                  cleaveEvents <- cleaveEvents @ [ CombatEvent.CollapseTriggered(targetAfterHit.Id, reason) ]
                | CollapseState.Stable -> ()

              targetAfterHit
            )

          // Evaluate collapse on currentActor in case recklessness reached threshold
          let actorAfterCleaves = Combatant.evaluateCollapse currentActor
          if CollapseState.isCollapsed actorAfterCleaves.Collapse && not (CollapseState.isCollapsed primaryRes.Actor.Collapse) then
            match actorAfterCleaves.Collapse with
            | CollapseState.Collapsed reason ->
              cleaveEvents <- cleaveEvents @ [ CombatEvent.CollapseTriggered(actorAfterCleaves.Id, reason) ]
            | CollapseState.Stable -> ()

          { Actor = actorAfterCleaves
            PrimaryTarget = currentPrimary
            SecondaryTargets = processedCleaves @ unengaged
            Events = allEvents @ cleaveEvents }

        elif currentActor.Stance = CombatStance.DisciplineStance then
          // Discipline Stance: Chain strikes across engaged opponents
          let chainCapacity = Math.Min(3, 1 + (currentActor.StudyStacks / 2))
          let chainCandidates = currentAdjacent |> List.truncate chainCapacity
          let unengaged = currentAdjacent |> List.skip chainCandidates.Length
          let mutable chainEvents = []

          let primaryDmg =
            primaryRes.Events
            |> List.choose (function CombatEvent.DamageApplied d when d.TargetId = currentPrimary.Id && d.Plane = Physical -> Some d.Amount | _ -> None)
            |> List.tryHead
            |> Option.defaultValue (Math.Max(20, currentActor.GetStat Prowess))

          let rawChainDmg = Math.Max(10, int (float primaryDmg * 0.70))

          let mutable step = 1
          let processedChains =
            chainCandidates
            |> List.map (fun secTarget ->
              let soakedDmg = Math.Max(5, int (Math.Round(float rawChainDmg * (1.0 - secTarget.Armor.AbsorptionRatio))))

              let targetAfterHit =
                { secTarget with
                    Health = secTarget.Health.ApplyDelta -soakedDmg }
                |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 12 })
                |> Combatant.evaluateCollapse

              currentActor <- currentActor |> Combatant.addStudyStacks 1

              chainEvents <-
                chainEvents
                @ [
                  CombatEvent.StrikeChained(currentActor.Id, secTarget.Id, step, soakedDmg)
                  CombatEvent.DamageApplied {
                    TargetId = secTarget.Id
                    Plane = Physical
                    Amount = soakedDmg
                    IsCritical = false
                    IsArmorCompromised = secTarget.Armor.IsShredded
                  }
                ]

              if CollapseState.isCollapsed targetAfterHit.Collapse && not (CollapseState.isCollapsed secTarget.Collapse) then
                match targetAfterHit.Collapse with
                | CollapseState.Collapsed reason ->
                  chainEvents <- chainEvents @ [ CombatEvent.CollapseTriggered(targetAfterHit.Id, reason) ]
                | CollapseState.Stable -> ()

              step <- step + 1
              targetAfterHit
            )

          { Actor = currentActor
            PrimaryTarget = currentPrimary
            SecondaryTargets = processedChains @ unengaged
            Events = allEvents @ chainEvents }

        else
          { Actor = currentActor
            PrimaryTarget = currentPrimary
            SecondaryTargets = currentAdjacent
            Events = allEvents }
