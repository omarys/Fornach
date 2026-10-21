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
    | ForceInsightStrike _
    | TraumaAttack BasaltEruption -> Force, [ Fortitude; Resolve ]
    | FinesseCadence _
    | FinesseInsightStrike _
    | TraumaAttack DenialPhaseShift -> Finesse, [ Reflex; Intuition ]
    | ProwessStrike _
    | ShieldBash _
    | CalculatedFlawStrike _
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

  /// The contest reads stance-adjusted stats, so the estimate must too: an actor standing in the wrong
  /// stance strikes at 0.75x, which a blanket "always in vector stance" bonus would miss entirely.
  let private offenseValue (actor: Combatant) (stat: StatId) : float = float (actor.GetCombatStat stat)

  /// Expected hits from one dice pool (every vector averages 0.5 hits per die; Discipline stacks reroll
  /// 1s). `minPool` mirrors the floor DicePool.resolveContestEx puts on the defender's pool.
  let private expectedHits (vector: Vector) (studyStacks: int) (stat: int) (opposingStat: int) (minPool: int) : float =
    let pool = Math.Max(minPool, DicePool.computePoolSize stat opposingStat)
    let rerollGain = if vector = Discipline then float (Math.Min(studyStacks, pool)) / 12.0 else 0.0
    float (DicePool.computeFloorHits stat) + float pool * 0.5 + rerollGain

  /// Estimated damage of an attack against a target; 0.0 when the strike is expected to whiff.
  let estimate (actor: Combatant) (target: Combatant) (attack: AttackClassification) : float =
    let offenseStat, defenseStats = statPair attack
    let offValue = int (Math.Round(offenseValue actor offenseStat))
    let defValue =
      let raw =
        match defenseStats with
        | [ single ] -> target.GetCombatStat single
        // A DisciplineStance defender's guard is 1.5x Poise, not the Poise/Composure blend.
        | [ first; second ] when first = Poise && second = Composure -> target.GetGuard(attack.Plane, true)
        | [ first; second ] -> (target.GetCombatStat first + target.GetCombatStat second) / 2
        | _ -> target.GetCombatStat (List.head defenseStats)

      if attack.Plane = Mental then
        ActionResolver.resolveMentalDefenseFor target raw
      else
        raw

    // Study-Stack strikes pay more for spending, so the estimate has to weigh the stacks the
    // strike would actually burn — same helper the resolver uses, so the two cannot drift.
    let requestedSpend =
      match attack with
      | CalculatedFlawStrike spend
      | ForceInsightStrike spend
      | FinesseInsightStrike spend
      | MasterfulDisarm spend -> spend
      | _ -> 0

    let stackSpend = Math.Min(actor.StudyStacks, Math.Max(2, requestedSpend))

    let netHits =
      // The attacker's pool has no floor in DicePool.resolveContestEx; only the defender's does (4).
      expectedHits attack.Vector actor.StudyStacks offValue defValue 0
      - expectedHits attack.Vector target.StudyStacks defValue offValue 4

    let tierMult = ActionResolver.computeTierMultiplier (int (Math.Round netHits))
    let planeFactor = match attack.Plane with Physical -> 1.50 | Mental -> 1.15
    float offValue
    * planeFactor
    * tierMult
    * ActionResolver.strikeMultiplier attack actor.StudyStacks stackSpend actor.Stance
    * ActionResolver.masteryMultiplier actor target
