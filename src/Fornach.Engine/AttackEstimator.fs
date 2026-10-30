namespace Fornach.Engine

open System
open Fornach.Domain

/// Predicts how hard an attack will land against an opponent, for menu ranking and automated play.
module AttackEstimator =

  /// Offensive stat and the (one or two) defensive stats an attack is contested against.
  /// Mirrors the stat selection in ActionResolver.resolveAttack.
  let statPair (attack: AttackClassification) : StatId * StatId list =
    match attack with
    | ForceStrike _
    | ForceInsightStrike
    | TraumaAttack BasaltEruption -> Force, [ Fortitude; Resolve ]
    | FinesseCadence _
    | FinesseInsightStrike
    | TraumaAttack DenialPhaseShift -> Finesse, [ Reflex; Intuition ]
    | ProwessStrike _
    | ShieldBash _
    | CalculatedFlawStrike
    | MasterfulDisarm _ -> Prowess, [ Poise; Composure ]
    | AuthorityDecree _
    | ArcaneCataclysm _ -> Intellect, [ Fortitude; Resolve ]
    | GuileDeception _
    | SynapticGlamour _
    | MirrorIllusion _ -> Acuity, [ Reflex; Intuition ]
    | AcumenInterrogation _
    | RunicWardTrap _
    | DisorientingShockwave _
    | StaffSiphon
    | TraumaAttack CoerciveBargain -> Acumen, [ Poise; Composure ]
    | TraumaAttack ApathyDoldrums -> Fortitude, [ Poise; Composure ]
    | TraumaAttack SereneResolution -> Composure, [ Fortitude; Resolve ]

  /// The combatant a strike is actually contested as. `ActionResolver.applyFluidTransition` runs *before*
  /// the contest, so a physical strike always lands in the stance its vector selects and a mental one in
  /// the Complex Form its attack selects. Reading the actor's held stance instead understates every strike
  /// that is not already in it (0.75x offensive) and credits the meter's ramp only to the stance already
  /// held — which is exactly what made a Discipline build's Focus look worthless in the ranking.
  /// ShieldBash is deliberately absent from the transition table and keeps the held stance.
  let contestedAs (actor: Combatant) (attack: AttackClassification) : Combatant =
    if
      not actor.PlayerStyle
      && not actor.Class.IsPlayerClass
      && actor.MonsterFamily.IsNone
    then
      // Plain humanoid opposition never shifts discipline mid-fight (mirrors applyFluidTransition).
      actor
    elif actor.Plane = Physical then
      match attack with
      | ForceStrike _
      | ForceInsightStrike
      | TraumaAttack BasaltEruption -> { actor with Stance = CombatStance.PowerStance }
      | FinesseCadence _
      | FinesseInsightStrike
      | TraumaAttack DenialPhaseShift -> { actor with Stance = CombatStance.AgilityStance }
      | ProwessStrike _
      | CalculatedFlawStrike
      | MasterfulDisarm _ -> { actor with Stance = CombatStance.DisciplineStance }
      | _ -> actor
    else
      match attack with
      | ArcaneCataclysm _
      | AuthorityDecree _ ->
        { actor with
            ComplexForm = Some ComplexForm.ResonanceSpike
            Stance = CombatStance.PowerStance }
      | SynapticGlamour _
      | MirrorIllusion _
      | GuileDeception _
      | TraumaAttack DenialPhaseShift ->
        { actor with
            ComplexForm = Some ComplexForm.PhantasmalDiffusion
            Stance = CombatStance.AgilityStance }
      | RunicWardTrap _
      | DisorientingShockwave _
      | StaffSiphon
      | AcumenInterrogation _
      | TraumaAttack (CoerciveBargain | ApathyDoldrums | SereneResolution) ->
        { actor with
            ComplexForm = Some ComplexForm.AegisLattice
            Stance = CombatStance.DisciplineStance }
      | _ -> actor

  /// The contest reads stance-adjusted stats, so the estimate must too: an actor standing in the wrong
  /// stance strikes at 0.75x, which a blanket "always in vector stance" bonus would miss entirely.
  let private offenseValue (actor: Combatant) (stat: StatId) : float = float (actor.GetCombatStat stat)

  /// Expected hits from one dice pool (every vector averages 0.5 hits per die; Discipline stacks reroll
  /// 1s). `minPool` mirrors the floor DicePool.resolveContestEx puts on the defender's pool.
  let private expectedHits (vector: Vector) (studyStacks: int) (stat: int) (opposingStat: int) (minPool: int) : float =
    let pool = Math.Max(minPool, DicePool.computePoolSize stat opposingStat)
    let rerollGain = if vector = Discipline then float (Math.Min(studyStacks, pool)) / 12.0 else 0.0
    float (DicePool.computeFloorHits stat) + float pool * 0.5 + rerollGain

  /// The defence an attack is actually contested against, mirroring ActionResolver's selection: a
  /// DisciplineStance guard blends Poise and Composure at 1.5x, other pairs average, and the mental
  /// plane passes through the caster's defence conversion.
  let defenseValue (actor: Combatant) (defender: Combatant) (attack: AttackClassification) : int =
    let _, defenseStats = statPair attack

    let raw =
      match defenseStats with
      | [ single ] -> defender.GetCombatStat single
      | [ first; second ] when first = Poise && second = Composure -> defender.GetGuard(attack.Plane, true)
      | [ first; second ] -> (defender.GetCombatStat first + defender.GetCombatStat second) / 2
      | _ -> defender.GetCombatStat (List.head defenseStats)

    if attack.Plane = Mental then
      ActionResolver.resolveMentalDefenseFor actor defender raw
    else
      raw

  /// The whole contest in one number, and both halves are read on the attack's own vector: how far the
  /// actor's attack outclasses the defence it faces, plus how far the actor's guard against that same
  /// vector outclasses the counter-threat the target's matching attribute represents. Ranking on offence
  /// alone recommends the strike that hits hardest and loses the exchange; the second term is what makes
  /// "best overall outcome" mean survive-and-hit rather than hit-hardest.
  let contestMargin (actor: Combatant) (target: Combatant) (attack: AttackClassification) : int =
    let striker = contestedAs actor attack
    let offenseStat, _ = statPair attack
    let offense = striker.GetCombatStat offenseStat - defenseValue actor target attack
    let guard = defenseValue actor striker attack - target.GetCombatStat offenseStat
    offense + guard

  /// Estimated damage of an attack against a target; 0.0 when the strike is expected to whiff.
  let estimate (actor: Combatant) (target: Combatant) (attack: AttackClassification) : float =
    let striker = contestedAs actor attack
    let offenseStat, _ = statPair attack
    let offValue = int (Math.Round(offenseValue striker offenseStat))
    let defValue = defenseValue actor target attack

    // Study-Stack strikes pay more for spending, so the estimate has to weigh the stacks the
    // strike would actually burn — same helper the resolver uses, so the two cannot drift.
    // The spender attacks cash in every stack held (see ActionResolver step A), and MasterfulDisarm
    // pays a requirement rather than the pile — mirror both so the estimate cannot drift.
    let stackSpend =
      match attack with
      | CalculatedFlawStrike
      | ForceInsightStrike
      | FinesseInsightStrike -> Combatant.stacksIn attack.Vector actor
      | MasterfulDisarm _ -> Math.Min(Combatant.StackCap, Combatant.stacksIn attack.Vector actor)
      | _ -> 0

    let netHits =
      // The attacker's pool has no floor in DicePool.resolveContestEx; only the defender's does (4).
      expectedHits attack.Vector (Combatant.stacksIn attack.Vector actor) offValue defValue 0
      - expectedHits attack.Vector (Combatant.stacksIn attack.Vector target) defValue offValue 4

    let tierMult = ActionResolver.computeTierMultiplier (int (Math.Round netHits))
    let planeFactor = match attack.Plane with Physical -> 1.50 | Mental -> 1.15
    float offValue
    * planeFactor
    * tierMult
    * ActionResolver.strikeMultiplier attack stackSpend striker.Stance
    * ActionResolver.masteryMultiplier striker target
