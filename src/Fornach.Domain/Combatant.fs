namespace Fornach.Domain

open System

/// Represents physical armor durability and passive damage absorption
[<Struct>]
type ArmorIntegrity =
  { Current: int
    Max: int }

  static member Create max =
    let m = Math.Max(0, max)
    { Current = m; Max = m }

  /// Reduces current armor durability, clamped between 0 and Max
  member this.Shred amount =
    { Current = Math.Clamp(this.Current - amount, 0, this.Max)
      Max = this.Max }

  /// Passive mitigation ratio: pristine armor soaks up to 50% of raw physical hits
  member this.AbsorptionRatio =
    if this.Max = 0 then
      0.0
    else
      float this.Current / float this.Max * 0.50

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

/// Aggregate root representing an active participant in physical/mental combat
type Combatant =
  { Id: CombatantId
    Name: string
    Health: Pool
    Morale: Pool
    Stats: StatBlock
    Meters: StatusMeters
    Collapse: CollapseState
    /// Combat ramp: rises on every attack, sharpens Finesse/Reflex, cashed by Agility specials.
    Flow: int
    /// Combat ramp: rises on every attack, sharpens Force/Fortitude, cashed by Power specials.
    Momentum: int
    /// Combat ramp: rises on every attack, sharpens Prowess/Poise, cashed by Discipline specials.
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
    PlayerStyle: bool }

  /// Combatant level derived from progression profile
  member this.Level = this.Progression.Level

  /// Attacks of opportunity the combatant may answer per round: one per flanker, +1 per ten levels
  /// after level 20, capped at 8 (the eight tiles surrounding a fully encircled combatant).
  member this.AttackOfOpportunityCapacity = Math.Clamp((this.Level - 20) / 10, 0, 8)

  /// Spell armor bonus percentage (0.0 to 0.80) for magic classes based on progression level.
  /// Starts at level 50 (5%), increasing by 5% every 10 levels, capping at 80% at level 200.
  member this.SpellArmorBonus : float =
    if this.Plane = Mental then
      if this.Level < 50 then 0.0
      else
        let steps = (this.Level - 40) / 10
        Math.Clamp(Math.Round(float steps * 0.05, 2), 0.05, 0.80)
    else
      0.0

  /// Operative combat plane (Physical or Mental).
  /// Respects character class plane, or detects dominance for untyped combatants.
  member this.Plane : Plane =
    let physTotal = this.Stats.Get Force + this.Stats.Get Finesse + this.Stats.Get Prowess
    let mentTotal = this.Stats.Get Intellect + this.Stats.Get Acuity + this.Stats.Get Acumen
    if this.Class = CharacterClass.Warrior && mentTotal > physTotal then
      Plane.Mental
    else
      this.Class.Plane

  /// Indicates if this combatant has collapsed and is vulnerable to an instant ExecuteStrike
  member this.IsExecuteEligible = CollapseState.isCollapsed this.Collapse

  /// Defenses drop by 75% when in a collapsed state
  member this.EffectiveDefenseMultiplier = if this.IsExecuteEligible then 0.25 else 1.0

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

  /// Retrieves an effective stat value factoring in limb debuffs and collapse penalties
  member this.GetStat(stat: StatId) =
    let rawVal = this.Stats.Get stat
    let penalized =
      if stat = Reflex then
        Math.Max(1, rawVal - this.LimbDebuff)
      else
        rawVal
    int (Math.Round(float penalized * this.EffectiveDefenseMultiplier))

  /// Retrieves a stance-adjusted stat for opposed combat resolution: adopting a stance boosts
  /// the stats of its vector (+25%) and penalizes off-vector offensive stats (-25%) while preserving guard.
  /// Canonical `GetStat` and `Stats` remain stance-neutral; this is the combat-only view used by ActionResolver.
  member this.GetCombatStat(stat: StatId) =
    let baseValue = this.GetStat stat

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

    let stanceAdjusted =
      if descriptor.Plane <> this.Plane then
        baseValue
      elif descriptor.Vector = stanceVector then
        Math.Max(1, int (Math.Round(float baseValue * 1.25)))
      elif descriptor.Orientation = Offense then
        // Trade-off: the plane's other vectors lose offensive sharpness, but keep their guard.
        Math.Max(1, int (Math.Round(float baseValue * 0.75)))
      else
        baseValue

    // Combat ramp: each meter sharpens the stat pair it feeds, so a sustained fight compounds.
    // Flow is deliberately the sharpest (10%/stack vs 1%) — it is the Agility glass cannon meter.
    let ramp =
      match stat with
      | Finesse | Reflex -> 1.0 + 0.10 * float (Math.Clamp(this.Flow, 0, Combatant.StackCap))
      | Force | Fortitude -> 1.0 + 0.01 * float (Math.Clamp(this.Momentum, 0, Combatant.StackCap))
      | Prowess | Poise -> 1.0 + 0.01 * float (Math.Clamp(this.Focus, 0, Combatant.StackCap))
      | _ -> 1.0

    if ramp = 1.0 then
      stanceAdjusted
    else
      Math.Max(1, int (Math.Round(float stanceAdjusted * ramp)))

  /// Guard every defence contest rolls against: the Poise/Composure blend, replaced while the defender
  /// holds DisciplineStance by a pure Poise anchor (1.5x against physical attacks, 1.0x against mental
  /// ones) that drops Composure. `useCombatStats` keeps each call site's existing stat view.
  member this.GetGuard(plane: Plane, useCombatStats: bool) : int =
    if this.Stance = CombatStance.DisciplineStance then
      let stanceGuard = match plane with Physical -> 1.5 | Mental -> 1.0
      int (Math.Round(float (this.GetStat Poise) * stanceGuard))
    else
      let stat = if useCombatStats then this.GetCombatStat else this.GetStat
      (stat Poise + stat Composure) / 2

  /// Dominant arcane vector focus based on mental attribute allocation
  member this.ArcaneFocus : Vector =
    let i = this.GetStat Intellect
    let a = this.GetStat Acuity
    let m = this.GetStat Acumen
    if i >= a && i >= m then Power
    elif a >= i && a >= m then Agility
    else Discipline

  /// Arcane proficiency ratio for a given vector relative to the combatant's highest mental stat (20% - 100%)
  member this.GetArcaneProficiency(vector: Vector) : float =
    let i = float (this.GetStat Intellect)
    let a = float (this.GetStat Acuity)
    let m = float (this.GetStat Acumen)
    let maxMental = Math.Max(1.0, Math.Max(i, Math.Max(a, m)))
    let statVal =
      match vector with
      | Power -> i
      | Agility -> a
      | Discipline -> m
    Math.Clamp(statVal / maxMental, 0.20, 1.0)

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
      PlayerStyle = false }

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
      PlayerStyle = false }

  /// Pure helper to update status meters
  static member updateMeters (updater: StatusMeters -> StatusMeters) (c: Combatant) =
    { c with Meters = updater c.Meters }

  /// Every combat ramp meter shares one ceiling: five stacks, so a full pile cashes at 3.5x.
  static member StackCap = 5

  /// Raises all three ramp meters together. Every attack type feeds all three; only a same-vector
  /// special spends one, so spending always resets that meter alone.
  static member addStacks delta (c: Combatant) =
    { c with
        Flow = Math.Clamp(c.Flow + delta, 0, Combatant.StackCap)
        Momentum = Math.Clamp(c.Momentum + delta, 0, Combatant.StackCap)
        Focus = Math.Clamp(c.Focus + delta, 0, Combatant.StackCap) }

  /// One stack off every meter: the end-of-turn decay when nothing is engaged.
  static member decayStacks (c: Combatant) = Combatant.addStacks -1 c

  /// Stacks currently held in the meter that gates a given attack vector.
  static member stacksIn (vector: Vector) (c: Combatant) =
    match vector with
    | Vector.Agility -> c.Flow
    | Vector.Power -> c.Momentum
    | Vector.Discipline -> c.Focus

  /// Drains one meter by `count`, clamping at zero.
  static member drainStacks (vector: Vector) count (c: Combatant) =
    match vector with
    | Vector.Agility -> { c with Flow = Math.Max(0, c.Flow - count) }
    | Vector.Power -> { c with Momentum = Math.Max(0, c.Momentum - count) }
    | Vector.Discipline -> { c with Focus = Math.Max(0, c.Focus - count) }

  /// Permanent growth on a won encounter, replacing the old Study Stack ramp: the Discipline pair
  /// (Prowess/Poise for everyone but a Mage, which takes Acumen/Composure) and +1 to each of the three
  /// mental defences. No offensive attribute ever grows from a kill — Power and Agility climb through
  /// the combat ramp and gear instead, so Discipline has to be fed on purpose.
  static member growFromEncounter (foes: Combatant list) (c: Combatant) =
    // A fight worth taking teaches more: `max(10 - (bearer level - foe level), 5)`, diminishing as the
    // foe falls behind. The gap is clamped at zero on the other side too — fighting ABOVE your level
    // is not a lever to inflate the gain, it just pays the full ten.
    let foeLevel = foes |> List.fold (fun best (f: Combatant) -> max best f.Level) 0
    let levelGap = Math.Max(c.Level - foeLevel, 0)
    let disciplineGain = Math.Max(10 - levelGap, 5)

    let mentalDefences =
      [ StatId.Resolve, 1; StatId.Intuition, 1; StatId.Composure, 1 ]

    let disciplinePair =
      match c.Class with
      | Mage -> [ StatId.Acumen, disciplineGain; StatId.Composure, disciplineGain ]
      | _ -> [ StatId.Prowess, disciplineGain; StatId.Poise, disciplineGain ]

    { c with Stats = c.Stats.ApplyModifiers(disciplinePair @ mentalDefences) }

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
    { c with ActivePreparations = c.ActivePreparations |> List.filter (fun a -> a.Type <> prepType) }

  /// Spends 1 use of a tactical preparation from the combatant's progression profile
  static member spendPreparation (prepType: PreparationType) (c: Combatant) : Combatant =
    match c.Progression.SpendPreparation prepType with
    | Some updatedProg ->
      { c with Progression = updatedProg; Preparations = updatedProg.Preparations }
    | None -> c

  /// Ticks down turn durations on active preparations, removing expired ones
  static member decrementActivePreparations (c: Combatant) : Combatant =
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
    | Mental, CombatStance.DisciplineStance -> Acumen, Composure

  /// A blow taken hardens the stance that absorbed it: +2 to that stance's defensive attribute.
  static member fortifyStance (c: Combatant) : Combatant =
    let _, defense = c.StanceAttributes
    { c with Stats = c.Stats.Modify(defense, 2) }

  /// Permanently folds an item's stat modifiers into a combatant's base Stats
  /// (each stat clamped to a minimum of 1). Story pickups use this so that an
  /// EquipmentItem's StatPercentBonuses are reflected by GetStat without an equip step.
  static member applyItemBonus (item: EquipmentItem) (c: Combatant) : Combatant =
    if List.isEmpty item.StatPercentBonuses then c
    else { c with Stats = c.Stats.ApplyPercentBonuses item.StatPercentBonuses }

  /// Degrades weapon condition down one progressive stage
  static member degradeWeapon (c: Combatant) =
    { c with WeaponCondition = WeaponCondition.degradation c.WeaponCondition }

  /// Accumulates bleeding trauma stacks
  static member addBleed count (c: Combatant) =
    { c with BleedStacks = Math.Max(0, c.BleedStacks + count) }

  /// Applies crippled limb trauma penalty to Reflex
  static member addLimbDebuff penalty (c: Combatant) =
    { c with LimbDebuff = Math.Min(60, c.LimbDebuff + penalty) }

  /// Evaluates all 7 status meters against the 100% threshold to determine Collapse states
  static member evaluateCollapse(c: Combatant) : Combatant =
    // If already collapsed, preserve existing collapse state
    match c.Collapse with
    | CollapseState.Collapsed _ -> c
    | CollapseState.Stable ->
      let m = c.Meters

      let reasonOpt =
        // Shared Entropy Break (evaluated first)
        let recklessnessCap =
          if c.HasActivePreparation PreparationType.BerserkTincture then
            120
          else
            100
        if m.Recklessness.Value >= recklessnessCap then
          Some RecklessExposure
        // Physical Status Breaks
        elif m.Exhaustion.Value >= 100 then
          Some SomaticAnoxia
        elif m.Overwhelm.Value >= 100 then
          Some Exsanguination
        elif m.Frustration.Value >= 100 then
          Some StanceFailure
        // Mental Status Breaks
        elif m.CognitiveFatigue.Value >= 100 then
          Some CatatonicStupor
        elif m.Confusion.Value >= 100 then
          Some ContradictionLock
        elif m.Provoke.Value >= 100 then
          Some HystericalMeltdown
        else
          None

      match reasonOpt with
      | Some reason ->
        { c with
            Collapse = CollapseState.Collapsed reason }
      | None -> c

  /// Restores a collapsed combatant to Stable if all status meters have dropped below 100%
  static member stabilizeCollapse(c: Combatant) : Combatant =
    match c.Collapse with
    | CollapseState.Collapsed _ ->
      let m = c.Meters
      let hasBreached =
        m.Recklessness.Value >= 100
        || m.Exhaustion.Value >= 100
        || m.Overwhelm.Value >= 100
        || m.Frustration.Value >= 100
        || m.CognitiveFatigue.Value >= 100
        || m.Confusion.Value >= 100
        || m.Provoke.Value >= 100
      if not hasBreached then
        { c with Collapse = CollapseState.Stable }
      else
        c
    | CollapseState.Stable -> c
