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

  /// Magic combatant threaded or shifted their active mental Complex Form
  | ComplexFormThreaded of combatantId: CombatantId * oldForm: ComplexForm option * newForm: ComplexForm

  /// Magic combatant suffered somatic Fading / drain from channeling high-resonance forms
  | FadingDrainSuffered of combatantId: CombatantId * formName: string * fatigueDrain: int * recklessnessSpike: int

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

  /// Tactical preparation deployed by combatant
  | PreparationDeployed of actorId: CombatantId * prepType: PreparationType * targetId: CombatantId option * description: string

  /// Surplus NetHits from Shockwave Slam spilled over to engaged flanker
  | ShockwaveSurplusDamage of attackerId: CombatantId * secondaryTargetId: CombatantId * surplusNetHits: int * flatDamage: int

  /// Critical strike triggered Synaptic Brand on marked target for double Morale damage and Rupture
  | SynapticBrandTriggered of attackerId: CombatantId * targetId: CombatantId * bonusMoraleDamage: int

  /// Concealed boot blade quick-drawn from the Nach to counter-puncture incoming attacker
  | ConcealedBladeCounter of defenderId: CombatantId * attackerId: CombatantId * damage: int * attackDisrupted: bool

  /// Prismatic Flare fireworks detonated due to Recklessness gain, blinding target for Morale shock and Confusion
  | PrismaticFlareBlinded of targetId: CombatantId * recklessnessSpike: int * moraleDrain: int

  /// Secondary flanker struck a Mirror Mirage phantasm, suffering Confusion and missing turn
  | MirrorMirageDeceived of defenderId: CombatantId * flankerId: CombatantId * confusionInflicted: int

  /// Defender seized the Vor from the Nach via an Indes counter-interception
  | IndesSeized of defenderId: CombatantId * attackerId: CombatantId * threshold: int * margin: int

  /// Bastion Zone Control planted, restricting multi-opponent engagement to 3 simultaneous attackers
  | BastionZoneErected of actorId: CombatantId

  /// Socratic Dossier exposed hypocrisies, converting Recklessness directly to Morale damage
  | SocraticDossierExecuted of actorId: CombatantId * targetId: CombatantId * recklessnessConverted: int * moraleDamage: int

  /// Heraldic Treatise reviewed, granting immediate Study Stacks across all engaged foes
  | HeraldicTreatiseStudied of actorId: CombatantId * studyStacksGranted: int

  /// Severe mental stat disparity caused cranial hemorrhage (bleeding)
  | PsychicHemorrhageInflicted of casterId: CombatantId * targetId: CombatantId * bleedStacks: int * disparity: int

  /// Severe mental stat disparity resonated as an Area of Effect psychic shockwave to adjacent flanker
  | PsychicShockwaveResonated of casterId: CombatantId * secondaryTargetId: CombatantId * splashDamage: int

  /// Mesmer actively swapped places with a mirror decoy clone in the Nach/Indes
  | PhantasmalSwapExecuted of defenderId: CombatantId * attackerId: CombatantId * success: bool * note: string

  /// Strategist projected a tactical ground rune under an incoming attacker mid-swing
  | DestabilizingWardTriggered of defenderId: CombatantId * attackerId: CombatantId * outcome: string * damageMitigation: float

  /// Inquisitor projected a synaptic mind-shock to disrupt the attacker's focus
  | SynapticMindShockDisrupted of defenderId: CombatantId * attackerId: CombatantId * fatigueInflicted: int * attackDisrupted: bool

  /// Passive class ability generated clones, wards, or psychological dread
  | PassiveGenerationTriggered of combatantId: CombatantId * description: string

  /// Mirror decoy clone was struck and shattered upon contact, inflicting retaliatory feedback damage
  | MirrorCloneShattered of defenderId: CombatantId * attackerId: CombatantId * blastDamage: int * remainingClones: int

  /// Aegis of Retribution / Retribution Ward reflected damage back onto attacker, inflicting damage and Frustration
  | RetributionReflected of defenderId: CombatantId * attackerId: CombatantId * damageReflected: int * frustrationInflicted: int

  /// Attacker stumbled and fell onto a destabilizing ground ward, suffering kinetic impact damage and severe Frustration
  | DestabilizingWardTripped of defenderId: CombatantId * attackerId: CombatantId * impactDamage: int * frustrationInflicted: int

  /// Enraged Berserker deadened pain receptors, shrugging off incoming physical damage
  | EnrageDamageShrugged of defenderId: CombatantId * damageIgnored: int

  /// Enraged Berserker executed an adrenaline-fueled Frenzy bonus attack against the swarm
  | FrenzyStrikeExecuted of actorId: CombatantId * targetId: CombatantId
