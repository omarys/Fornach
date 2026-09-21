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

  /// Opponent weapon structural integrity degraded by heavy power impact or disarm
  | WeaponDegraded of combatantId: CombatantId * newCondition: WeaponCondition

  /// Combatant shifted their active tactical stance
  | StanceShifted of combatantId: CombatantId * oldStance: CombatStance * newStance: CombatStance

  /// Reactive counter-strike triggered by accumulated Study Stacks in Discipline stance
  | RiposteExecuted of defenderId: CombatantId * attackerId: CombatantId * counterDamage: int

  /// Weapon or stance disarmed by a tactical maneuver or Study Stacks counter
  | DisarmExecuted of defenderId: CombatantId * attackerId: CombatantId * reason: string

  /// Periodic somatic bleed trauma applied at start of turn
  | BleedTicked of combatantId: CombatantId * damageDealt: int * remainingStacks: int

  /// Critical finesse opening inflicted stacking bleed trauma
  | BleedApplied of targetId: CombatantId * stacksAdded: int * totalStacks: int

  /// Vital ligament or tendon severed, crippling opponent reflex
  | LimbDisabled of targetId: CombatantId * reflexPenalty: int

  /// Dedicated Discipline gambit executed by consuming Study Stacks without Recklessness spike
  | DisciplineGambitExecuted of actorId: CombatantId * gambitName: string * stacksSpent: int

  /// Defender suffered a compounding successive defense penalty from being surrounded
  | EncirclementPenalized of defenderId: CombatantId * priorDefenses: int * penaltyHits: int

  /// Preemptive Attack of Opportunity triggered against an opponent attempting to flank / surround
  | AttackOfOpportunityTriggered of defenderId: CombatantId * attackerId: CombatantId * vectorName: string * damageDealt: int * attackDisrupted: bool

  /// Sweeping cleave struck a secondary target in Power Stance, incurring disparity-scaled recklessness
  | CleaveExecuted of attackerId: CombatantId * secondaryTargetId: CombatantId * cleaveDamage: int * recklessnessIncurred: int

  /// Fluid martial cadence chained from target to target in Discipline Stance without reckless exposure
  | StrikeChained of attackerId: CombatantId * chainedTargetId: CombatantId * chainStep: int * chainDamage: int

  /// Caster struggled with an off-specialization spell, incurring Cognitive Fatigue strain
  | ArcaneStrainIncurred of casterId: CombatantId * spellName: string * strain: int * proficiencyPct: int

  /// Guile illusionist conjured mirror clones/decoys to misdirect incoming strikes
  | MirrorClonesConjured of casterId: CombatantId * countAdded: int * totalClones: int

  /// Attacker's strike was deceived and absorbed by an illusionary mirror decoy clone
  | MirrorCloneDecoyed of defenderId: CombatantId * attackerId: CombatantId * remainingClones: int

  /// Abjuration caster raised or reinforced an active Arcane Ward barrier
  | ArcaneWardErected of casterId: CombatantId * barrierAdded: int * totalWard: int

  /// Active Arcane Ward absorbed incoming damage before reaching health/morale
  | ArcaneWardAbsorbed of defenderId: CombatantId * damageSoaked: int * remainingWard: int

  /// Disorienting shockwave disrupted enemy balance, resetting tempo and inflicting confusion
  | OpponentDisoriented of casterId: CombatantId * targetId: CombatantId * reason: string

  /// Overwhelming cataclysmic blast splashed destructive energy to adjacent swarm targets
  | CataclysmSplashed of casterId: CombatantId * secondaryTargetId: CombatantId * splashDamage: int
