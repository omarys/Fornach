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
    StudyStacks: int
    Armor: ArmorIntegrity
    WeaponCondition: WeaponCondition
    Stance: CombatStance
    BleedStacks: int
    LimbDebuff: int
    ArcaneWard: int
    MirrorClones: int
    ComboTracker: ConsecutiveComboTracker
    EquippedItems: EquipmentItem list }

  /// Indicates if this combatant has collapsed and is vulnerable to an instant ExecuteStrike
  member this.IsExecuteEligible = CollapseState.isCollapsed this.Collapse

  /// Defenses drop by 75% when in a collapsed state
  member this.EffectiveDefenseMultiplier = if this.IsExecuteEligible then 0.25 else 1.0

  /// Retrieves an effective stat value factoring in limb debuffs and collapse penalties
  member this.GetStat(stat: StatId) =
    let rawVal = this.Stats.Get stat
    let penalized =
      if stat = Reflex then
        Math.Max(1, rawVal - this.LimbDebuff)
      else
        rawVal
    int (Math.Round(float penalized * this.EffectiveDefenseMultiplier))

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

  /// Factory for creating a base combatant with default baseline pools and meters
  static member create id name maxHealth maxMorale stats =
    { Id = id
      Name = name
      Health = Pool.Create maxHealth
      Morale = Pool.Create maxMorale
      Stats = stats
      Meters = StatusMeters.Zero
      Collapse = CollapseState.Stable
      StudyStacks = 0
      Armor = ArmorIntegrity.Create 50
      WeaponCondition = WeaponCondition.Pristine
      Stance = CombatStance.PowerStance
      BleedStacks = 0
      LimbDebuff = 0
      ArcaneWard = 0
      MirrorClones = 0
      ComboTracker = ConsecutiveComboTracker.Zero
      EquippedItems = [] }

  /// Pure helper to update status meters
  static member updateMeters (updater: StatusMeters -> StatusMeters) (c: Combatant) =
    { c with Meters = updater c.Meters }

  /// Adds or drains study/insight stacks, bounded at zero
  static member addStudyStacks delta (c: Combatant) =
    { c with
        StudyStacks = Math.Max(0, c.StudyStacks + delta) }

  /// Modifies active Arcane Ward barrier absorption pool
  static member addWard delta (c: Combatant) =
    { c with ArcaneWard = Math.Max(0, c.ArcaneWard + delta) }

  /// Adds or consumes active Mirror Clone decoys
  static member addClones delta (c: Combatant) =
    { c with MirrorClones = Math.Max(0, c.MirrorClones + delta) }

  /// Shifts active tactical stance
  static member setStance stance (c: Combatant) =
    { c with Stance = stance }

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
        if m.Recklessness.Value >= 100 then
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
