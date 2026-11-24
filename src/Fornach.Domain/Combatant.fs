namespace Fornach.Domain

open System

/// Represents physical armor durability, passive damage absorption, and temporary shield plating
[<Struct>]
type ArmorIntegrity =
  { Current: int
    Max: int
    /// Shield plating held up by Raise Shield: it counts toward soak and eats armor shred before real
    /// durability, but it is not armor -- kits and shrines repair Current, never Boost, and it comes
    /// down the moment its bearer commits to a new intent.
    Boost: int }

  static member Create max =
    let m = Math.Max(0, max)
    { Current = m; Max = m; Boost = 0 }

  /// Reduces armor durability, clamped between 0 and Max. Shield plating takes the beating first:
  /// a blow against a raised shield chews Boost before it reaches the armor underneath.
  member this.Shred amount =
    let boostHit = Math.Min(this.Boost, amount)
    { Current = Math.Clamp(this.Current - (amount - boostHit), 0, this.Max)
      Max = this.Max
      Boost = this.Boost - boostHit }

  /// Repairs current armor durability, clamped between 0 and Max. Boost is untouched: plating is
  /// never repaired, only re-erected by holding the shield up again.
  member this.Repair amount =
    { Current = Math.Clamp(this.Current + amount, 0, this.Max)
      Max = this.Max
      Boost = this.Boost }

  /// Adds temporary shield plating on top of whatever durability the armor already has.
  member this.BoostBy amount =
    { Current = this.Current
      Max = this.Max
      Boost = this.Boost + amount }

  /// Passive mitigation ratio: pristine armor soaks up to 50% of raw physical hits. Plating held up
  /// in front of it raises the ratio, capped at 75% -- a shield stops most of a blow, never all of it.
  member this.AbsorptionRatio =
    if this.Max = 0 then
      0.0
    else
      Math.Min(0.75, float (this.Current + this.Boost) / float this.Max * 0.50)

  member this.IsShredded = this.Current <= 0

/// Tracks consecutive landed strikes, power escalations, and vital opening bonuses
[<Struct>]
type ConsecutiveComboTracker =
  { ConsecutiveHits: int
    ConsecutivePowerHits: int
    VitalOpeningBonus: int } // Scaled crit / rupture chance, capped at 60%

  static member Zero =
    { ConsecutiveHits = 0
      ConsecutivePowerHits = 0
      VitalOpeningBonus = 0 }

  /// Increments hit momentum and builds vital opening vulnerability
  member this.RegisterHit() =
    { ConsecutiveHits = this.ConsecutiveHits + 1
      ConsecutivePowerHits = this.ConsecutivePowerHits + 1
      VitalOpeningBonus = Math.Min(60, this.VitalOpeningBonus + 15) }

  /// Resets all offensive momentum (triggered on recovery actions or misses)
  member this.ResetCombo() = ConsecutiveComboTracker.Zero

/// Trigger contexts for rule-bending combat modifiers and relics
[<RequireQualifiedAccess>]
type RelicHookTrigger =
  | OnStrikeLanded of isCritical: bool * netHits: int
  | OnWhiff of isAttacker: bool
  | OnTempoShifted of seizedVor: bool
  | OnIndesResolved of won: bool
  | OnStrainThreshold of strainType: string

type Combatant =
  { Id: CombatantId
    Name: string
    Health: Pool
    Morale: Pool
    Stats: StatBlock
    Meters: StatusMeters
    Collapse: CollapseState
    /// Combat ramp: rises on every attack, sharpens Finesse/Reflex and Acuity/Intuition,
    /// cashed by Agility specials.
    Flow: int
    /// Combat ramp: rises on every attack, sharpens Force/Fortitude and the Discipline stats,
    /// cashed by Power and Discipline specials.
    Momentum: int
    /// Guard ramp: rises only on an action taken from the Nach, and sharpens the physical defensive
    /// attributes (Fortitude/Poise) at a steeper rate than Momentum does.
    Inertia: int
    /// Guard ramp: rises only on an action taken from the Nach, and sharpens the mental defensive
    /// attributes (Resolve/Poise) at a steeper rate than Momentum does.
    Focus: int
    Armor: ArmorIntegrity
    WeaponCondition: WeaponCondition
    Stance: CombatStance
    ComplexForm: ComplexForm option
    BleedStacks: int
    LimbDebuff: int
    ArcaneWard: int
    MirrorClones: int
    ComboTracker: ConsecutiveComboTracker
    EquippedItems: EquipmentItem list
    Class: CharacterClass
    Progression: ProgressionProfile
    Preparations: PreparationSlot list
    ActivePreparations: ActivePreparation list
    MonsterFamily: MonsterFamily option
    MonsterTraits: MonsterTrait list
    /// True for combatants with player-style discipline: fluid stance / Complex Form transitions,
    /// tactical preparations, and a varied attack kit. Plain opposition leaves this false.
    PlayerStyle: bool
    /// How many foes the player faces, stamped at the boundary that owns the pack. A defence taken from
    /// the Nach pays one guard stack per foe, so a duel pays 1 and a four-strong pack pays 4. Zero means
    /// "not stated" and pays 1, which is what every non-CLI path and every test gets.
    EnemiesFaced: int
    /// Tactical tempo in the Vor / Nach paradigm
    Tempo: TacticalTiming
    /// True when staggered by Pain Shock, losing the next action/turn (ADR 0009)
    IsStaggered: bool
    /// True while the combatant's last action was a held round (`ActionIntent.Wait`). A held round is the
    /// spent action behind a reactive strike: `ActionResolver` counts defences already answered this round
    /// (`priorDefenses`), and a combatant who waited answered none, so this flag supplies that credit for the
    /// attack of opportunity until the combatant acts again and clears it.
    HeldRound: bool
    /// Named behaviour the owning ruleset supplies for this combatant. The engine looks the id up in the
    /// table its caller passes to `AI.chooseIntentUsing` and keeps its own generic discipline when the id
    /// is absent or unregistered, so this is the seam a new setting uses to give its combatants their own
    /// signature moves without teaching the engine what those moves are. `None` is the common case: every
    /// plain opposition combatant behaves on the engine's generic profile.
    Policy: PolicyId option
    /// Active rule-bending relic modifiers carried by this combatant
    RelicModifiers: RelicModifier list } with

    /// Tactical tempo state helpers
    member this.IsInVor = this.Tempo = TacticalTiming.Vor
    member this.IsInNach = this.Tempo = TacticalTiming.Nach

and RelicModifier = {
  Id: string
  Name: string
  Description: string
  Apply: RelicHookTrigger -> Combatant -> Combatant option -> (Combatant * Combatant option * CombatEvent list)
}

type Combatant with

  /// True for humanoids (characters without a non-humanoid MonsterFamily)
  member this.IsHumanoid = this.MonsterFamily.IsNone

  /// True for wild predatory fauna
  member this.IsBeast = this.MonsterFamily = Some MonsterFamily.Beast

  /// Combatant level derived from progression profile
  member this.Level = this.Progression.Level

  /// How many bodies can bring a strike to bear on this combatant in one round. Poise is how well a
  /// fighter holds the space around itself, so it closes one approach lane per 100 points: a raw Novice
  /// is surrounded by all eight tiles, a GrandMaster Fighter is reachable by one attacker at a time. The
  /// floor of 1 keeps even a champion reachable — frontage decides the crowd, never immunity.
  member this.Frontage = Math.Clamp(8 - (this.Stats.Get Poise / 100), 1, 8)

  /// Attacks of opportunity the combatant may answer per round: Prowess is the reach and speed behind a
  /// reactive strike, so it buys one interception per 20 points above the 40 a raw Novice starts with,
  /// capped at 8 (the eight tiles around a fully encircled combatant). A Novice therefore answers none —
  /// the same shape the old level curve gave — while a Veteran lands on the full eight. Was keyed on level,
  /// which made every class equally good at punishing an approach regardless of the stat that does it.
  member this.AttackOfOpportunityCapacity = Math.Clamp((this.Stats.Get Prowess - 40) / 20, 0, 8)

  // `SpellArmorBonus` used to live here as a second copy of `ProgressionScale.spellArmorBonus`, gated on
  // `Level < 50 -> 0.0`. Nothing in the game read it — `TierFactory` already bakes the share into the
  // caster's Fortitude and Reflex — so the copy only served to drift out of step with the curve.

  /// Operative combat plane (Physical or Mental).
  /// Respects character class plane, or detects dominance for untyped combatants.
  member this.Plane : Plane =
    let physTotal = this.Stats.Get Force + this.Stats.Get Finesse + this.Stats.Get Prowess
    let mentTotal = this.Stats.Get Intellect + this.Stats.Get Acuity + this.Stats.Get Prowess
    if this.Class = CharacterClass.Warrior && mentTotal > physTotal then
      Plane.Mental
    else
      this.Class.Plane

  /// Indicates if this combatant has collapsed and is vulnerable to an instant ExecuteStrike
  member this.IsExecuteEligible = CollapseState.isCollapsed this.Collapse

  /// Defenses drop by 75% when in a collapsed state
  member this.EffectiveDefenseMultiplier = if this.IsExecuteEligible then 0.25 else 1.0

  /// Fraction of incoming status-meter pressure this combatant actually takes. Collapse makes a
  /// target executable and execution ignores pools, so an undamped Exhaustion clock decides fights
  /// that were authored to run on their pools. `StoicResistance` damps it for such targets.
  member this.MeterGainFactor =
    this.MonsterTraits
    |> List.fold
        (fun acc monsterTrait ->
            match monsterTrait with
            | MonsterTrait.StoicResistance pct -> acc * (1.0 - float pct / 100.0)
            | _ -> acc)
        1.0

  /// Checks if a preparation is currently active and unexpired
  member this.HasActivePreparation(prepType: PreparationType) : bool =
    this.ActivePreparations
    |> List.exists (fun a -> a.Type = prepType && not a.IsExpired)

  /// Retrieves an active preparation if present and unexpired
  member this.GetActivePreparation(prepType: PreparationType) : ActivePreparation option =
    this.ActivePreparations
    |> List.tryFind (fun a -> a.Type = prepType && not a.IsExpired)

  /// Checks if an active preparation is targeted specifically against a given combatant
  member this.HasActivePreparationAgainst (prepType: PreparationType) (targetId: CombatantId) : bool =
    this.ActivePreparations
    |> List.exists (fun a -> a.Type = prepType && a.TargetId = Some targetId && not a.IsExpired)

  /// Retrieves an effective stat value factoring in limb debuffs and collapse penalties.
  ///
  /// Prowess and Poise are the trained discipline pair, and only a trained combatant has it. The player
  /// classes carry it, and so does any humanoid opposition authored without a `MonsterFamily` — a Knight
  /// or a Wizard is a `Combatant` with no family, so it reads the pair like the player does. Beasts,
  /// constructs, wraiths, aberrations and grief manifestations do not: their sheet may still carry values
  /// where Acumen/Composure used to sit, and those are ignored. The player is also the only combatant that
  /// *grows* the pair, out of a stressed victory or a defeat (`growFromStressedVictory`, `growFromDefeat`).
  member this.GetStat(stat: StatId) =
    if not this.IsHumanoid && (stat = Prowess || stat = Poise) then
      0
    else
      let rawVal = this.Stats.Get stat
      let penalized =
        if stat = Reflex then
          Math.Max(1, rawVal - this.LimbDebuff)
        else
          rawVal

      int (Math.Round(float penalized * this.EffectiveDefenseMultiplier))

  /// Retrieves a stance-adjusted stat for opposed combat resolution: adopting a stance boosts
  /// the stats of its vector (+25%) and penalizes off-vector offensive stats (-25%) while preserving guard.
  /// Applies strain threshold debuffs (>=75) (ADR 0009).
  /// Canonical `GetStat` and `Stats` remain stance-neutral; this is the combat-only view used by ActionResolver.
  member this.GetCombatStat(stat: StatId) =
    let baseValue = this.GetStat stat

    let isDefenseStat =
      stat = Fortitude || stat = Reflex || stat = Resolve || stat = Intuition || stat = Poise || stat = Poise

    // Strain debuffs at >= 75 (ADR 0009):
    let baseAdjusted =
      if (stat = Force || stat = Fortitude) && this.Meters.Exhaustion.Value >= 75 then
        int (Math.Round(float baseValue * 0.75))
      elif (stat = Finesse || stat = Reflex) && this.Meters.Frustration.Value >= 75 then
        int (Math.Round(float baseValue * 0.75))
      elif this.Meters.CognitiveFatigue.Value >= 75 && (stat = Intellect || stat = Acuity || (this.Plane = Mental && (stat = Fortitude || stat = Reflex || stat = Resolve || stat = Intuition))) then
        int (Math.Round(float baseValue * 0.75))
      else
        baseValue

    let strainAdjusted =
      if this.Meters.Recklessness.Value >= 75 && isDefenseStat then
        int (Math.Round(float baseAdjusted * 0.75))
      else
        baseAdjusted

    let stanceVector =
      if this.Plane = Mental then
        match this.ComplexForm with
        | Some form -> form.Vector
        | None ->
          match this.Stance with
          | CombatStance.PowerStance -> Vector.Power
          | CombatStance.AgilityStance -> Vector.Agility
          | CombatStance.DisciplineStance -> Vector.Discipline
      else
        match this.Stance with
        | CombatStance.PowerStance -> Vector.Power
        | CombatStance.AgilityStance -> Vector.Agility
        | CombatStance.DisciplineStance -> Vector.Discipline

    let descriptor = Attributes.descriptorOf stat

    // Prowess and Poise are the shared discipline pair: they belong to whichever plane their bearer
    // fights on, so the stance read is against the bearer's own plane rather than the descriptor's.
    // Without this a Mage would take no stance multiplier on the one stat both classes train.
    let statPlane =
      if descriptor.Vector = Vector.Discipline then
        this.Plane
      else
        descriptor.Plane

    let stanceAdjusted =
      if statPlane <> this.Plane then
        strainAdjusted
      elif descriptor.Vector = stanceVector then
        Math.Max(1, int (Math.Round(float strainAdjusted * 1.25)))
      elif descriptor.Orientation = Offense then
        // Trade-off: the plane's other vectors lose offensive sharpness, but keep their guard.
        Math.Max(1, int (Math.Round(float strainAdjusted * 0.75)))
      else
        strainAdjusted

    // Combat ramp: each meter sharpens the physical stats it feeds, so a sustained fight compounds.
    // Flow carries the Agility stats (Finesse/Reflex, Acuity/Intuition); Momentum carries the Power
    // stats (Force/Intellect) and the guard meters buy defence (Fortitude/Resolve). Discipline
    // (Prowess/Poise/Prowess/Poise) never ramps in combat - it is earned permanently through
    // stress growth (`growFromStressedVictory` / `growFromDefeat`), not banked by standing in a stance.
    // Flow is deliberately the sharpest (6%/stack vs 1%) — it is the Agility glass cannon meter. At
    // 10% it was ~1.9x the Momentum meter once Finesse's 2.5x crit was counted in.
    let momentumRamp = 1.0 + 0.01 * float (Math.Clamp(this.Momentum, 0, Combatant.StackCap))
    // The guard meters pay 2.5%/stack against the offensive ramp's 1%: acting from the Nach is a losing
    // exchange on the attack, so it buys defence instead. The rate is unchanged; the guard is simply
    // allowed to bank 25 stacks instead of 5, so its ceiling is 1.625x rather than 1.125x.
    let guardRamp (stacks: int) = 1.0 + 0.025 * float (Math.Clamp(stacks, 0, Combatant.GuardStackCap))

    let ramp =
      match stat with
      | Finesse | Reflex | Acuity | Intuition -> 1.0 + 0.06 * float (Math.Clamp(this.Flow, 0, Combatant.StackCap))
      | Fortitude -> momentumRamp * guardRamp this.Inertia
      | Resolve -> momentumRamp * guardRamp this.Focus
      | Force | Intellect -> momentumRamp
      | Prowess | Poise | Prowess | Poise -> 1.0

    if ramp = 1.0 then
      stanceAdjusted
    else
      Math.Max(1, int (Math.Round(float stanceAdjusted * ramp)))

  /// Guard every defence contest rolls against: the Poise/Poise blend, replaced while the defender
  /// holds DisciplineStance by a pure Poise anchor (1.5x against physical attacks, 1.0x against mental
  /// ones) that drops Poise. Non-humanoids use Fortitude fallback (ADR 0009).
  member this.GetGuard(plane: Plane, useCombatStats: bool) : int =
    if not this.IsHumanoid then
      let stat = if useCombatStats then this.GetCombatStat else this.GetStat
      stat Fortitude
    elif this.Stance = CombatStance.DisciplineStance then
      let stanceGuard = match plane with Physical -> 1.5 | Mental -> 1.0
      int (Math.Round(float (this.GetStat Poise) * stanceGuard))
    else
      let stat = if useCombatStats then this.GetCombatStat else this.GetStat
      stat Poise

  /// Dominant arcane vector focus based on mental attribute allocation
  member this.ArcaneFocus : Vector =
    let i = this.GetStat Intellect
    let a = this.GetStat Acuity
    let m = this.GetStat Prowess
    if i >= a && i >= m then Power
    elif a >= i && a >= m then Agility
    else Discipline

  /// Factory for creating a base combatant with default baseline pools, meters, and generic Warrior class profile
  static member create id name maxHealth maxMorale stats =
    let defaultProg = ProgressionProfile.create CharacterClass.Warrior 1
    { Id = id
      Name = name
      Health = Pool.Create maxHealth
      Morale = Pool.Create maxMorale
      Stats = stats
      Meters = StatusMeters.Zero
      Collapse = CollapseState.Stable
      Flow = 0
      Momentum = 0
      Inertia = 0
      Focus = 0
      Armor = ArmorIntegrity.Create 50
      WeaponCondition = WeaponCondition.Pristine
      Stance = CombatStance.PowerStance
      ComplexForm = None
      BleedStacks = 0
      LimbDebuff = 0
      ArcaneWard = 0
      MirrorClones = 0
      ComboTracker = ConsecutiveComboTracker.Zero
      EquippedItems = []
      Class = CharacterClass.Warrior
      Progression = defaultProg
      Preparations = defaultProg.Preparations
      ActivePreparations = []
      MonsterFamily = None
      MonsterTraits = []
      PlayerStyle = false
      EnemiesFaced = 0
      Tempo = TacticalTiming.Vor
      IsStaggered = false
      HeldRound = false
      Policy = None
      RelicModifiers = [] }

  /// Factory for creating a combatant with explicit class archetype and progression level
  static member createWithClass id name maxHealth maxMorale (stats: StatBlock) (cls: CharacterClass) (level: int) =
    let primStat = stats.Get cls.PrimaryStat
    let prog = ProgressionProfile.createWithStat cls level primStat
    let defaultStance =
      match cls.Vector with
      | Power -> CombatStance.PowerStance
      | Agility -> CombatStance.AgilityStance
      | Discipline -> CombatStance.DisciplineStance
    let defaultForm =
      if cls.Plane = Mental then
        match cls.Vector with
        | Power -> Some ComplexForm.ResonanceSpike
        | Agility -> Some ComplexForm.PhantasmalDiffusion
        | Discipline -> Some ComplexForm.AegisLattice
      else None
    { Id = id
      Name = name
      Health = Pool.Create maxHealth
      Morale = Pool.Create maxMorale
      Stats = stats
      Meters = StatusMeters.Zero
      Collapse = CollapseState.Stable
      Flow = 0
      Momentum = 0
      Inertia = 0
      Focus = 0
      Armor = ArmorIntegrity.Create 50
      WeaponCondition = WeaponCondition.Pristine
      Stance = defaultStance
      ComplexForm = defaultForm
      BleedStacks = 0
      LimbDebuff = 0
      ArcaneWard = 0
      MirrorClones = 0
      ComboTracker = ConsecutiveComboTracker.Zero
      EquippedItems = []
      Class = cls
      Progression = prog
      Preparations = prog.Preparations
      ActivePreparations = []
      MonsterFamily = None
      MonsterTraits = []
      PlayerStyle = false
      EnemiesFaced = 0
      Tempo = TacticalTiming.Vor
      IsStaggered = false
      HeldRound = false
      Policy = None
      RelicModifiers = [] }

  /// Updates the combatant's tactical tempo (Vor / Nach)
  static member withTempo (tempo: TacticalTiming) (c: Combatant) = { c with Tempo = tempo }

  /// Advances the combatant into the Vor (proactive initiative)
  static member seizeVor (c: Combatant) = { c with Tempo = TacticalTiming.Vor }

  /// Yields the Vor, moving the combatant into the Nach (reactive defense)
  static member yieldVor (c: Combatant) = { c with Tempo = TacticalTiming.Nach }

  /// Staggers combatant from Pain Shock, causing them to lose their next action (ADR 0009)
  static member stagger (c: Combatant) = { c with IsStaggered = true }

  /// Clears stagger after lost action
  static member clearStagger (c: Combatant) = { c with IsStaggered = false }

  /// Marks the combatant as having spent its round holding (a pass), which is the spent action an attack of
  /// opportunity needs
  static member holdRound (c: Combatant) = { c with HeldRound = true }

  /// Clears the held round once the combatant acts again, so the credit cannot outlive the round it was spent in
  static member clearHeldRound (c: Combatant) =
    if c.HeldRound then { c with HeldRound = false } else c

  /// Pure helper to update status meters.
  static member updateMeters (updater: StatusMeters -> StatusMeters) (c: Combatant) =
    { c with Meters = updater c.Meters }

  /// The offense ramp's ceiling: five stacks. Flow tops out at 1.30x (6%/stack), Momentum at 1.05x.
  static member StackCap = 5

  /// The guard meters' own ceiling, deliberately above the offense ramp's: a defence taken from the Nach
  /// is a losing exchange on the attack, so the defence it buys is allowed to bank deeper. At the same
  /// 2.5%/stack the rate never changed, 25 stacks cash at 1.625x instead of 1.125x.
  static member GuardStackCap = 25

  /// Raises both ramp meters together. Only the player compounds: `PlayerStyle` marks the player's kit,
  /// and the ramp is part of it, so opposition never leaves the baseline it spawned with.
  static member addStacks delta (c: Combatant) =
    if not c.PlayerStyle then
      c
    else
    { c with
        Flow = Math.Clamp(c.Flow + delta, 0, Combatant.StackCap)
        Momentum = Math.Clamp(c.Momentum + delta, 0, Combatant.StackCap) }

  /// Raises both guard meters together: they accrue only on an action taken from the Nach, and only for
  /// the player, for the same reason the ramp does.
  static member addGuardStacks delta (c: Combatant) =
    if not c.PlayerStyle then
      c
    else
    { c with
        Inertia = Math.Clamp(c.Inertia + delta, 0, Combatant.GuardStackCap)
        Focus = Math.Clamp(c.Focus + delta, 0, Combatant.GuardStackCap) }

  /// One stack off every meter: the end-of-turn decay when nothing is engaged.
  static member decayStacks (c: Combatant) =
    { Combatant.addStacks -1 c with
        Inertia = Math.Max(0, c.Inertia - 1)
        Focus = Math.Max(0, c.Focus - 1) }

  /// Wipes active ramp meters. Recovery breaks the ramp outright.
  static member clearStacks (c: Combatant) =
    { c with
        Flow = 0
        Momentum = 0
        Inertia = 0
        Focus = 0 }

  /// Stacks currently held in the meter that gates a given attack vector.
  static member stacksIn (vector: Vector) (c: Combatant) =
    match vector with
    | Vector.Agility -> c.Flow
    | Vector.Power -> c.Momentum
    | Vector.Discipline -> c.Momentum

  /// Drains one meter by `count`, clamping at zero.
  static member drainStacks (vector: Vector) count (c: Combatant) =
    match vector with
    | Vector.Agility -> { c with Flow = Math.Max(0, c.Flow - count) }
    | Vector.Power -> { c with Momentum = Math.Max(0, c.Momentum - count) }
    | Vector.Discipline -> { c with Momentum = Math.Max(0, c.Momentum - count) }

  /// Encounter bookkeeping (ADR 0009, stress-growth rework): records species familiarity in
  /// ProgressionProfile.BestiaryKnowledge (+1 per encounter with the species) and keeps the
  /// sharpness stipend on the perception stats. The flat discipline grant is gone -
  /// Prowess/Poise/Prowess/Poise now only grow through `growFromStressedVictory` /
  /// `growFromDefeat`.
  static member growFromEncounter (foes: Combatant list) (c: Combatant) =
    let mentalDefences =
      [ StatId.Resolve, 1; StatId.Intuition, 1 ]

    let updatedProg =
      foes
      |> List.fold (fun (p: ProgressionProfile) foe ->
          let pNext, _ = p.RecordBestiaryEncounter foe.Name
          pNext) c.Progression

    { c with
        Stats = c.Stats.ApplyModifiers mentalDefences
        Progression = updatedProg }

  /// Victory discipline growth (ADR 0009 + clean mastery reward):
  /// - Stressed victory (blood, morale, or status): +5 to Prowess/Poise (+1 more above half Recklessness).
  /// - Clean mastery victory (Collapse Execution or Indes Seizure even at 100% HP/Morale): +3 to Prowess/Poise.
  /// - Flawless non-mastery victory: +0 (still clears Collapse).
  static member growFromVictory (cleanMasteryVictory: bool) (c: Combatant) : Combatant =
    let stressed =
      c.Health.Current < c.Health.Maximum
      || c.Morale.Current < c.Morale.Maximum
      || c.Meters.HasAnyStatus

    let gain =
      if stressed then
        5 + (if c.Meters.Recklessness.Value > 50 then 1 else 0)
      elif cleanMasteryVictory then
        3
      else
        0

    if gain = 0 then { c with Collapse = CollapseState.Stable }
    else
      { c with
          Stats = c.Stats.ApplyModifiers [ StatId.Prowess, gain; StatId.Poise, gain ]
          Collapse = CollapseState.Stable }

  /// Stress-gated discipline growth (ADR 0009): delegates to `growFromVictory false`.
  static member growFromStressedVictory (c: Combatant) =
    Combatant.growFromVictory false c

  /// Defeat growth (ADR 0009): collapse or death teaches hardest - +10 to the class discipline
  /// pair. The trauma reset that follows restores pools and clears collapse but keeps this
  /// growth; the scars are the point.
  static member growFromDefeat (c: Combatant) =
    let disciplinePair = [ StatId.Prowess, 10; StatId.Poise, 10 ]
    { c with Stats = c.Stats.ApplyModifiers disciplinePair }

  /// Awards milestone Poise bonus for defeating a Floor Boss / Guardian without collapsing (ADR 0009)
  static member grantBossPoise (c: Combatant) =
    let bonus = [ StatId.Poise, 3 ]
    { c with Stats = c.Stats.ApplyModifiers bonus }

  /// Modifies active Arcane Ward barrier absorption pool
  static member addWard delta (c: Combatant) =
    { c with ArcaneWard = Math.Max(0, c.ArcaneWard + delta) }

  /// Adds or consumes active Mirror Clone decoys
  static member addClones delta (c: Combatant) =
    { c with MirrorClones = Math.Max(0, c.MirrorClones + delta) }

  /// Adds or updates an active preparation on the combatant
  static member addActivePreparation (active: ActivePreparation) (c: Combatant) =
    let filtered =
      c.ActivePreparations
      |> List.filter (fun a ->
        not (a.Type = active.Type && a.TargetId = active.TargetId))
    { c with ActivePreparations = active :: filtered }

  /// Removes an active preparation by type
  static member removeActivePreparation (prepType: PreparationType) (c: Combatant) =
    if c.ActivePreparations.IsEmpty then c
    else { c with ActivePreparations = c.ActivePreparations |> List.filter (fun a -> a.Type <> prepType) }

  /// Spends 1 use of a tactical preparation from the combatant's progression profile
  static member spendPreparation (prepType: PreparationType) (c: Combatant) : Combatant =
    match c.Progression.SpendPreparation prepType with
    | Some updatedProg ->
      { c with Progression = updatedProg; Preparations = updatedProg.Preparations }
    | None -> c

  /// Ticks down turn durations on active preparations, removing expired ones
  static member decrementActivePreparations (c: Combatant) : Combatant =
    if c.ActivePreparations.IsEmpty then c
    else
      let updated =
        c.ActivePreparations
        |> List.choose (fun a ->
          if a.DurationTurns <= 1 then None
          else Some (a.DecrementTurn()))
      { c with ActivePreparations = updated }

  /// Shifts active tactical stance
  static member setStance stance (c: Combatant) =
    { c with Stance = stance }

  /// Shifts active mental Complex Form
  static member setComplexForm form (c: Combatant) =
    { c with ComplexForm = form }

  /// The offensive and defensive attributes a stance governs on this combatant's plane. Landing a kill
  /// sharpens the first; taking a blow hardens the second. Physical stances read the physical triad,
  /// mental ones the mental triad.
  member this.StanceAttributes : StatId * StatId =
    match this.Plane, this.Stance with
    | Physical, CombatStance.PowerStance -> Force, Fortitude
    | Physical, CombatStance.AgilityStance -> Finesse, Reflex
    | Physical, CombatStance.DisciplineStance -> Prowess, Poise
    | Mental, CombatStance.PowerStance -> Intellect, Resolve
    | Mental, CombatStance.AgilityStance -> Acuity, Intuition
    | Mental, CombatStance.DisciplineStance -> Prowess, Poise

  /// Permanently folds an item's bonuses into a combatant: stat percentages, the flat health and morale
  /// the item grants, and its opening Recklessness. Story pickups use this so a pickup is reflected without
  /// an equip step.
  static member applyItemBonus (item: EquipmentItem) (c: Combatant) : Combatant =
    // Every field an item declares has to land here, not just the percentages: a kit whose only bonus is
    // health was skipped whole by the old `List.isEmpty item.StatPercentBonuses` guard, so the shield's
    // +200 health, the robes' +300 morale and the caster weapons' health had never applied at all.
    { c with
        Stats = c.Stats.ApplyPercentBonuses item.StatPercentBonuses
        Health = c.Health.RaiseMaximum item.HealthBonus
        Morale = c.Morale.RaiseMaximum item.MoraleBonus
        Meters =
          { c.Meters with
              Recklessness = c.Meters.Recklessness + item.StartingRecklessnessDelta } }

  /// Degrades weapon condition down one progressive stage
  static member degradeWeapon (c: Combatant) =
    { c with WeaponCondition = WeaponCondition.degradation c.WeaponCondition }

  /// Accumulates bleeding trauma stacks
  static member addBleed count (c: Combatant) =
    { c with BleedStacks = Math.Max(0, c.BleedStacks + count) }

  /// Applies crippled limb trauma penalty to Reflex
  static member addLimbDebuff penalty (c: Combatant) =
    { c with LimbDebuff = Math.Min(60, c.LimbDebuff + penalty) }

  /// The status meter that has broken this combatant, in break priority order: Recklessness first, then
  /// the physical meters, then the mental ones. Single source for both `evaluateCollapse` and the combat
  /// log's death verdict, so a logged cause can never disagree with the collapse the engine applied.
  static member collapseReasonOf(c: Combatant) : CollapseReason option =
    let m = c.Meters
    let recklessnessCap =
      if c.HasActivePreparation PreparationType.BerserkTincture then
        120
      else
        100
    if m.Recklessness.Value >= recklessnessCap then
      Some RecklessExposure
    elif m.Exhaustion.Value >= 100 then
      Some SomaticAnoxia
    elif m.Frustration.Value >= 100 then
      Some Exsanguination
    elif m.CognitiveFatigue.Value >= 100 then
      Some CatatonicStupor
    elif m.Confusion.Value >= 100 then
      Some ContradictionLock
    elif m.Provoke.Value >= 100 then
      Some HystericalMeltdown
    else
      None

  /// Evaluates all 7 status meters against the 100% threshold to determine Collapse states
  static member evaluateCollapse(c: Combatant) : Combatant =
    // If already collapsed, preserve existing collapse state
    match c.Collapse with
    | CollapseState.Collapsed _ -> c
    | CollapseState.Stable ->
      let reasonOpt = Combatant.collapseReasonOf c

      match reasonOpt with
      | Some reason ->
        // A combatant that collapses drops whatever tempo it was holding: it takes no further action, so
        // it cannot insist on the Vor either. Without this a collapsed foe stayed in the Vor and kept
        // swinging, which made reaching 100% on any status meter do nothing at all.
        { c with
            Collapse = CollapseState.Collapsed reason
            Tempo = TacticalTiming.Nach }
      | None -> c

  /// Restores a collapsed combatant to Stable if all status meters have dropped below 100%
  static member stabilizeCollapse(c: Combatant) : Combatant =
    match c.Collapse with
    | CollapseState.Collapsed _ ->
      let m = c.Meters
      let hasBreached =
        m.Recklessness.Value >= 100
        || m.Exhaustion.Value >= 100
        || m.Frustration.Value >= 100
        || m.CognitiveFatigue.Value >= 100
        || m.Confusion.Value >= 100
        || m.Provoke.Value >= 100
      if not hasBreached then
        { c with Collapse = CollapseState.Stable }
      else
        c
    | CollapseState.Stable -> c
