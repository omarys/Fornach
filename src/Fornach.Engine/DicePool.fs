namespace Fornach.Engine

open System
open Fornach.Domain

/// Function signature for rolling dice: (min -> max -> int)
type DiceRoller = int -> int -> int

/// Result of evaluating an individual combatant's dice pool
type DicePoolResult =
  { StatValue: int
    FloorHits: int
    RolledHits: int
    TotalHits: int
    RawRolls: int list
    IsGlitch: bool }

/// Result of an opposed dice pool contest between attacker and defender
type ContestResult =
  { Attacker: DicePoolResult
    Defender: DicePoolResult
    NetHits: int
    IsCritical: bool
    IsWhiff: bool
    EncirclementPenalty: int }

module DicePool =

  /// Computes the bounded dice pool size (between 4 and 20 dice) based on stat ratio.
  /// The curve itself lives in `ProgressionScale.poolFor`: the tier boundaries are derived from it, so it
  /// has a single definition rather than one here and one in the domain.
  let computePoolSize (statValue: int) (opposingStat: int) : int =
    ProgressionScale.poolFor statValue opposingStat

  /// Deterministic floor hits: guarantees 1 hit per 15 stat points
  let computeFloorHits (statValue: int) : int =
    ProgressionScale.floorFor statValue

  /// The extremes a pool can actually produce for a vector: floor hits are guaranteed whatever lands,
  /// and rolled hits run from nothing up to the vector's ceiling (a Power die scores double on a six,
  /// every other vector scores at most one hit per die). `fst` is the worst roll, `snd` the best.
  let hitBounds (vector: Vector) (statValue: int) (opposingStat: int) : int * int =
    ProgressionScale.hitBounds vector statValue opposingStat

  /// True when the lesser combatant's best possible roll still cannot reach the greater's worst: the
  /// contest is decided before the dice. The cutoff is arithmetic and nothing else -- while any overlap
  /// remains the win is real, and a lucky novice that beats a master is awarded the win.
  let cannotBeat (vector: Vector) (greaterStat: int) (lesserStat: int) : bool =
    let _, lesserBest = hitBounds vector lesserStat greaterStat
    let greaterWorst, _ = hitBounds vector greaterStat lesserStat
    lesserBest < greaterWorst

  /// Evaluates an individual participant's dice pool for a designated Vector.
  /// Preserves exact RNG draw order: initial poolSize rolls first, then Discipline rerolls in index order.
  let evaluatePool
    (roller: int -> int -> int)
    (vector: Vector)
    (statValue: int)
    (studyStacks: int)
    (poolSize: int)
    : DicePoolResult =
    let rolls = Array.zeroCreate<int> poolSize
    for i = 0 to poolSize - 1 do
      rolls.[i] <- roller 1 6

    let mutable rolledHits = 0
    let mutable onesCount = 0

    match vector with
    | Power ->
      for i = 0 to poolSize - 1 do
        let r = rolls.[i]
        if r = 6 then rolledHits <- rolledHits + 2
        elif r = 5 then rolledHits <- rolledHits + 1
        elif r = 1 then onesCount <- onesCount + 1
    | Agility ->
      for i = 0 to poolSize - 1 do
        let r = rolls.[i]
        if r >= 4 then rolledHits <- rolledHits + 1
        elif r = 1 then onesCount <- onesCount + 1
    | Discipline ->
      let mutable rerollsLeft = Math.Max(0, studyStacks)
      for i = 0 to poolSize - 1 do
        let mutable r = rolls.[i]
        if r = 1 && rerollsLeft > 0 then
          rerollsLeft <- rerollsLeft - 1
          r <- roller 1 6
          rolls.[i] <- r
        if r >= 4 then rolledHits <- rolledHits + 1
        elif r = 1 then onesCount <- onesCount + 1

    let floorHits = computeFloorHits statValue
    let totalHits = floorHits + rolledHits
    let isGlitch = onesCount > (poolSize / 2)

    { StatValue = statValue
      FloorHits = floorHits
      RolledHits = rolledHits
      TotalHits = totalHits
      RawRolls = Array.toList rolls
      IsGlitch = isGlitch }

  /// Computes compounding successive defense penalty based on stat disparity.
  /// When defender outclasses attacker, pressure drops non-linearly (quadratic ratio)
  /// so that novices cannot coordinate to penetrate high-mastery defenses.
  let computeEncirclementPenalty (attackerStat: int) (defenderStat: int) (priorDefenses: int) : int =
    if priorDefenses <= 0 then 0
    else
      let rawRatio = float attackerStat / Math.Max(1.0, float defenderStat)
      let pressureRatio = if rawRatio < 1.0 then Math.Pow(rawRatio, 2.0) else rawRatio
      let compoundFactor = float (priorDefenses * (priorDefenses + 1)) / 2.0
      int (Math.Round(compoundFactor * pressureRatio * 3.5))


  /// Opposed contest resolution between attacker and defender with successive encirclement defense penalty
  let resolveContestEx
    (roller: int -> int -> int)
    (vector: Vector)
    (attackerStat: int)
    (attackerStudy: int)
    (defenderStat: int)
    (defenderStudy: int)
    (priorDefenses: int)
    : ContestResult =
    let penalty = computeEncirclementPenalty attackerStat defenderStat priorDefenses
    let attackerPoolSize = computePoolSize attackerStat defenderStat
    let defenderPoolSize = Math.Max(4, (computePoolSize defenderStat attackerStat) - (penalty / 2))

    let attackerRes = evaluatePool roller vector attackerStat attackerStudy attackerPoolSize
    let defenderResRaw = evaluatePool roller vector defenderStat defenderStudy defenderPoolSize

    let effectiveDefTotalHits = Math.Max(0, defenderResRaw.TotalHits - penalty)
    let defenderRes = { defenderResRaw with TotalHits = effectiveDefTotalHits }

    let netHits = attackerRes.TotalHits - effectiveDefTotalHits
    let isCritical = netHits >= 5
    let isWhiff = netHits <= 0

    { Attacker = attackerRes
      Defender = defenderRes
      NetHits = netHits
      IsCritical = isCritical
      IsWhiff = isWhiff
      EncirclementPenalty = penalty }

  /// Opposed contest resolution between attacker and defender (default 0 prior defenses)
  let resolveContest
    (roller: int -> int -> int)
    (vector: Vector)
    (attackerStat: int)
    (attackerStudy: int)
    (defenderStat: int)
    (defenderStudy: int)
    : ContestResult =
    resolveContestEx roller vector attackerStat attackerStudy defenderStat defenderStudy 0
