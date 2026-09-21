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
    let withExertion =
      baseUpdated
      |> Combatant.updateMeters (fun m -> { m with Exhaustion = m.Exhaustion + 1 })

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
        currentActor <- currentActor |> Combatant.addStudyStacks -actualSpend
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
        // 1. Encirclement defense penalty (attention & guard split across multiple attackers)
        let encPenaltyHits = DicePool.computeEncirclementPenalty offStat defStat priorDefenses
        let encReduction = encPenaltyHits * 5
        // 2. Physical limb trauma / severed ligaments
        let limbReduction = currentTarget.LimbDebuff / 2
        // 3. Physical stamina exhaustion / fatigue
        let fatigueReduction = currentTarget.Meters.Exhaustion.Value / 4

        let totalDefensivePenalty = encReduction + limbReduction + fatigueReduction
        let effectiveTriggerChance = Math.Max(0, baseTriggerChance - totalDefensivePenalty)

        if roller 1 100 <= effectiveTriggerChance then

          let aooDmg =
            if vectorName = "Agility" then
              Math.Max(10, int (float defFinesse * 0.65))
            else
              Math.Max(10, int (float defProwess * 0.75))

          currentActor <- { currentActor with Health = currentActor.Health.ApplyDelta -aooDmg }

          if vectorName = "Discipline" then
            currentTarget <- currentTarget |> Combatant.addStudyStacks 1
          elif vectorName = "Agility" then
            currentActor <- currentActor |> Combatant.updateMeters (fun m -> { m with Overwhelm = m.Overwhelm + 10 })

          let isDisrupted =
            currentActor.Health.IsDepleted
            || delta >= 100
            || (delta >= 40 && roller 1 100 <= 50)

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
        priorDefenses

    if contest.EncirclementPenalty > 0 then
      events <- CombatEvent.EncirclementPenalized(currentTarget.Id, priorDefenses, contest.EncirclementPenalty) :: events

    // --- Step D: Whiff vs. Landed Hit Branching ---
    if contest.IsWhiff then
      // Whiff: 0 damage, clear combo momentum. Clean deflection - no overwhelm inflicted!
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
      // Landed Strike: Scaled Tiered NetHits Damage & Weapon Degradation Multiplier
      let baseDamage = offStat * 2
      let tierMult = computeTierMultiplier contest.NetHits
      let gambitMult = if isGambit then 1.5 else 1.0
      let weaponEff = WeaponCondition.effectiveness currentActor.WeaponCondition

      // If strike landed while defender was encircled, apply flank overwhelm pressure scaled by disparity
      if contest.EncirclementPenalty > 0 then
        let rawRatio = float offStat / Math.Max(1.0, float defStat)
        let effectiveRatio = if rawRatio < 1.0 then Math.Pow(rawRatio, 2.0) else rawRatio
        let flankOverwhelm = int (Math.Round(float (contest.EncirclementPenalty / 2) * effectiveRatio))
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
      let rawDmg = Math.Max(1, int (float baseDamage * tierMult * gambitMult * classMult * weaponEff * critDmgMult))

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

    let isLandedPhysicalHit =
      match primaryRes.Contest with
      | Some contest when not contest.IsWhiff ->
        match intent with
        | StandardAttack atk when atk.Plane = Physical -> true
        | _ -> false
      | _ -> false

    if not isLandedPhysicalHit || adjacentTargets.IsEmpty then
      { Actor = currentActor
        PrimaryTarget = currentPrimary
        SecondaryTargets = adjacentTargets
        Events = allEvents }
    else
      match currentActor.Stance with
      | CombatStance.PowerStance ->
        // Power Stance: Cleave up to 2 adjacent targets
        // Cleave incurs a Recklessness penalty based on stat disparity:
        // High disparity (attacker Force >> target Fortitude) mitigates penalty.
        // Low disparity (target Fortitude >= attacker Force) causes a severe Recklessness spike.
        let cleaveCandidates = adjacentTargets |> List.truncate 2
        let unengaged = adjacentTargets |> List.skip cleaveCandidates.Length
        let mutable cleaveEvents = []

        let primaryDmg =
          primaryRes.Events
          |> List.choose (function CombatEvent.DamageApplied d when d.TargetId = currentPrimary.Id && d.Plane = Physical -> Some d.Amount | _ -> None)
          |> List.tryHead
          |> Option.defaultValue (Math.Max(20, currentActor.GetStat Force))

        let rawCleaveDmg = Math.Max(10, int (float primaryDmg * 0.60))

        let processedCleaves =
          cleaveCandidates
          |> List.map (fun secTarget ->
            let offForce = currentActor.GetStat Force
            let defFort = secTarget.GetStat Fortitude

            // Recklessness penalty scaled by disparity ratio:
            let disparityRatio = float defFort / Math.Max(1.0, float offForce)
            let reckSpike = Math.Max(3, int (Math.Round(25.0 * disparityRatio)))

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

      | CombatStance.DisciplineStance ->
        // Discipline Stance: Chain strikes across engaged opponents
        // Max chained targets: 1 base + (StudyStacks / 2), up to 3 targets.
        // No Recklessness penalty! Flowing martial economy and balance.
        // Generates +1 Study Stack per chained target hit.
        let chainCapacity = Math.Min(3, 1 + (currentActor.StudyStacks / 2))
        let chainCandidates = adjacentTargets |> List.truncate chainCapacity
        let unengaged = adjacentTargets |> List.skip chainCandidates.Length
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
            // Apply armor mitigation
            let soakedDmg = Math.Max(5, int (Math.Round(float rawChainDmg * (1.0 - secTarget.Armor.AbsorptionRatio))))

            // Apply damage & status meters to chained target
            let targetAfterHit =
              { secTarget with
                  Health = secTarget.Health.ApplyDelta -soakedDmg }
              |> Combatant.updateMeters (fun m -> { m with Frustration = m.Frustration + 12 })
              |> Combatant.evaluateCollapse

            // Attacker gains Study Stack from fluid martial cadence; 0 Recklessness!
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

      | CombatStance.AgilityStance ->
        // Agility / Finesse Stance: Specialized for 1-on-1 duels!
        // No cleave, no chain. The combatant hyper-focuses on the primary opponent.
        { Actor = currentActor
          PrimaryTarget = currentPrimary
          SecondaryTargets = adjacentTargets
          Events = allEvents }
