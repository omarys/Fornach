namespace Fornach.Domain

/// Payload detailing direct pool damage applied to a target
type DamageEvent =
  { TargetId: CombatantId
    Plane: Plane
    Amount: int
    IsCritical: bool
    IsArmorCompromised: bool }

/// Disparity threshold outcomes triggered by significant stat deltas
type DisparityOutcome =
  // Physical Disparities
  | CrushingBlow of bonusExhaustion: int
  | ArterialRupture of bonusOverwhelm: int
  | DisarmOrLimbDisable
  // Mental / Social / Arcane Disparities
  | CognitiveRupture of bonusFatigue: int
  | DialecticalParalysis
  | StrippedCredibility of bonusProvoke: int

/// Passive escalation outcomes from consecutive landed strikes
type PassiveProcOutcome =
  | ArmorSundered of shredAmount: int * remainingArmor: int
  | FocusShattered of focusSpike: int
  | VitalOpeningTriggered of bonusDamage: int * isPhysical: bool
  | StudyStackGenerated of currentTotal: int

/// Pure Domain Events emitted during action resolution
[<RequireQualifiedAccess>]
type CombatEvent =
  /// Damage applied to Health or Morale pools
  | DamageApplied of DamageEvent

  /// Stat disparity exceeded operational thresholds
  | DisparityTriggered of attackerId: CombatantId * targetId: CombatantId * outcome: DisparityOutcome

  /// High-risk gambit maneuver declared by an actor
  | GambitDeclared of actorId: CombatantId * gambitName: string * recklessnessCost: int

  /// A gambit was baited or countered by an opponent
  | GambitPunished of actorId: CombatantId * reason: string

  /// Defensive recovery stabilized posture and drained entropy
  | FormStabilized of actorId: CombatantId * drainedRecklessness: int * gainedStudyStacks: int

  /// Reactive or offensive equipment hook executed
  | EquipmentProcTriggered of itemName: string * sourceId: CombatantId * description: string

  /// Passive escalation (Sunder, Vital Opening, Prescience) executed
  | PassiveProcTriggered of actorId: CombatantId * targetId: CombatantId * outcome: PassiveProcOutcome

  /// Offensive momentum was cleared (recovery action or whiff)
  | ComboReset of actorId: CombatantId * reason: string

  /// Target status meter crossed 100%, causing a Collapse state
  | CollapseTriggered of targetId: CombatantId * reason: CollapseReason

  /// Fatal execution strike delivered to a Collapsed combatant
  | Executed of actorId: CombatantId * targetId: CombatantId * plane: Plane
