namespace Fornach.Engine

open System
open Fornach.Domain

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

  /// Computes the bounded dice pool size (between 4 and 20 dice) based on stat ratio
  let computePoolSize (statValue: int) (opposingStat: int) : int =
    let safeOpponent = Math.Max(1.0, float opposingStat)
    let ratio = float statValue / safeOpponent
    let scaled = int (Math.Round(6.0 + 4.0 * ratio))
    Math.Clamp(scaled, 4, 20)

  /// Deterministic floor hits: guarantees 1 hit per 15 stat points
  let computeFloorHits (statValue: int) : int =
    statValue / 15

  /// Power Vector: TN 5+ (5 counts as 1 hit; 6 counts as 2 hits)
  let private evaluatePower (dice: int list) : int list * int =
    let hits =
      dice
      |> List.sumBy (fun roll ->
        if roll = 6 then 2
        elif roll = 5 then 1
        else 0)

    dice, hits

  /// Agility Vector: TN 4+ (4, 5, 6 each count as 1 hit)
  let private evaluateAgility (dice: int list) : int list * int =
    let hits =
      dice
      |> List.sumBy (fun roll -> if roll >= 4 then 1 else 0)

    dice, hits

  /// Discipline Vector: TN 4+ (4, 5, 6 each count as 1 hit).
  /// If studyStacks > 0, rerolls up to studyStacks dice that rolled 1.
  let private evaluateDiscipline (roller: int -> int -> int) (studyStacks: int) (dice: int list) : int list * int =
    let mutable rerollsLeft = Math.Max(0, studyStacks)

    let finalDice =
      dice
      |> List.map (fun roll ->
        if roll = 1 && rerollsLeft > 0 then
          rerollsLeft <- rerollsLeft - 1
          roller 1 6
        else
          roll)

    let hits =
      finalDice
      |> List.sumBy (fun roll -> if roll >= 4 then 1 else 0)

    finalDice, hits

  /// Evaluates an individual participant's dice pool for a designated Vector
  let evaluatePool
    (roller: int -> int -> int)
    (vector: Vector)
    (statValue: int)
    (studyStacks: int)
    (poolSize: int)
    : DicePoolResult =
    let rawRolls = List.init poolSize (fun _ -> roller 1 6)

    let finalRolls, rolledHits =
      match vector with
      | Power -> evaluatePower rawRolls
      | Agility -> evaluateAgility rawRolls
      | Discipline -> evaluateDiscipline roller studyStacks rawRolls

    let floorHits = computeFloorHits statValue
    let totalHits = floorHits + rolledHits
    let onesCount = finalRolls |> List.filter (fun r -> r = 1) |> List.length
    let isGlitch = onesCount > (finalRolls.Length / 2)

    { StatValue = statValue
      FloorHits = floorHits
      RolledHits = rolledHits
      TotalHits = totalHits
      RawRolls = finalRolls
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
