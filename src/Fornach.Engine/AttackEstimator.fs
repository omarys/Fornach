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
    | TraumaAttack CoerciveBargain -> Acumen, [ Poise; Composure ]
    | TraumaAttack ApathyDoldrums -> Fortitude, [ Poise; Composure ]
    | TraumaAttack SereneResolution -> Composure, [ Fortitude; Resolve ]

  /// Strikes fluidly shift the actor into their vector's stance before resolving, so player-style
  /// actors and monsters always strike with the 1.25x vector bonus.
  let private offenseValue (actor: Combatant) (stat: StatId) : float =
    if actor.PlayerStyle || actor.MonsterFamily.IsSome then
      Math.Max(1.0, Math.Round(float (actor.GetStat stat) * 1.25))
    else
      float (actor.GetCombatStat stat)

  /// Expected hits from one dice pool (every vector averages 0.5 hits per die; Discipline stacks reroll 1s)
  let private expectedHits (vector: Vector) (studyStacks: int) (stat: int) (opposingStat: int) : float =
    let pool = DicePool.computePoolSize stat opposingStat
    let rerollGain = if vector = Discipline then float (Math.Min(studyStacks, pool)) / 12.0 else 0.0
    float (DicePool.computeFloorHits stat) + float pool * 0.5 + rerollGain

  /// Estimated damage of an attack against a target; 0.0 when the strike is expected to whiff.
  let estimate (actor: Combatant) (target: Combatant) (attack: AttackClassification) : float =
    let offenseStat, defenseStats = statPair attack
    let offValue = int (Math.Round(offenseValue actor offenseStat))
    let defValue =
      match defenseStats with
      | [ single ] -> target.GetCombatStat single
      // A DisciplineStance defender's guard is 1.5x Poise, not the Poise/Composure blend.
      | [ first; second ] when first = Poise && second = Composure -> target.GetGuard(attack.Plane, true)
      | [ first; second ] -> (target.GetCombatStat first + target.GetCombatStat second) / 2
      | _ -> target.GetCombatStat (List.head defenseStats)

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
      expectedHits attack.Vector actor.StudyStacks offValue defValue
      - expectedHits attack.Vector target.StudyStacks defValue offValue

    let tierMult = ActionResolver.computeTierMultiplier (int (Math.Round netHits))
    let planeFactor = match attack.Plane with Physical -> 1.50 | Mental -> 1.15
    float offValue
    * planeFactor
    * tierMult
    * ActionResolver.strikeMultiplier attack actor.StudyStacks stackSpend
    * ActionResolver.masteryMultiplier actor target
