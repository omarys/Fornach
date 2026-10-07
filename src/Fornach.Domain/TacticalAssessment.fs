namespace Fornach.Domain

open System

/// Insight tier achieved by an observer analyzing a target combatant based on Acumen vs Composure
type TacticalInsightLevel =
  /// Target's composure or discipline conceals precise attributes (Ratio < 0.40)
  | Obscured
  /// Observer discerns the target's primary stat name and approximate tier (0.40 <= Ratio < 0.75)
  | Discerning
  /// Observer reads the exact primary stat and numerical value (0.75 <= Ratio < 1.15)
  | Keen
  /// Observer penetrates all defenses, identifying primary stat, highest offense, and defensive opening (Ratio >= 1.15)
  | Penetrating

/// Structured tactical deduction resulting from comparing observer Acumen to target Composure
type TacticalAssessment =
  { ObserverId: CombatantId
    TargetId: CombatantId
    InsightLevel: TacticalInsightLevel
    EffectiveAcumen: int
    TargetComposure: int
    Ratio: float
    PrimaryStat: StatId
    PrimaryStatValue: int
    PrimaryStatRole: string
    BestOffense: StatId * int
    BestDefense: StatId * int
    WeakestDefense: StatId * int
    Headline: string
    PrimarySummary: string
    VulnerabilitySummary: string option
    StrategicAdvice: string }

module TacticalAssessment =

  /// Resolves the role/orientation description for a stat (e.g. "Agility Defense", "Power Offense")
  let statRole (s: StatId) : string =
    let desc = Attributes.descriptorOf s
    let vectorName = sprintf "%A" desc.Vector
    let orientName = sprintf "%A" desc.Orientation
    sprintf "%s %s" vectorName orientName

  /// Calculates effective scrutiny for an observer:
  /// Acumen is primary (100%), Intuition provides perceptual scrutiny (+25%), and Study Stacks provide tactical familiarity (+15 each)
  let calculateEffectiveScrutiny (observer: Combatant) : int =
    let acumen = observer.GetStat StatId.Acumen
    let intuition = observer.GetStat StatId.Intuition
    let studyBonus = observer.StudyStacks * 15
    Math.Max(1, acumen + (intuition / 4) + studyBonus)

  /// Calculates effective concealing composure for a target
  let calculateEffectiveMasking (target: Combatant) : int =
    Math.Max(1, target.GetStat StatId.Composure)

  /// Identifies the primary (best) stat, highest offense, highest defense, and weakest defense on a combatant's plane
  let analyzeTargetStats (target: Combatant) : (StatId * int) * (StatId * int) * (StatId * int) * (StatId * int) =
    let relevantStats =
      match target.Plane with
      | Physical -> [ StatId.Force; StatId.Fortitude; StatId.Finesse; StatId.Reflex; StatId.Prowess; StatId.Poise ]
      | Mental -> [ StatId.Intellect; StatId.Resolve; StatId.Acuity; StatId.Intuition; StatId.Acumen; StatId.Composure ]

    let statsWithValues =
      relevantStats
      |> List.map (fun s -> s, target.GetStat s)

    let bestStat =
      statsWithValues
      |> List.maxBy snd

    let offensiveStats =
      statsWithValues
      |> List.filter (fun (s, _) -> (Attributes.descriptorOf s).Orientation = Offense)

    let defensiveStats =
      statsWithValues
      |> List.filter (fun (s, _) -> (Attributes.descriptorOf s).Orientation = Defense)

    let bestOffense =
      if offensiveStats.IsEmpty then bestStat
      else offensiveStats |> List.maxBy snd

    let bestDefense =
      if defensiveStats.IsEmpty then bestStat
      else defensiveStats |> List.maxBy snd

    let weakestDefense =
      if defensiveStats.IsEmpty then bestStat
      else defensiveStats |> List.minBy snd

    bestStat, bestOffense, bestDefense, weakestDefense

  /// Generates strategic tactical advice based on the target's stats and insight level
  let generateAdvice (bestStat: StatId * int) (weakestDef: StatId * int) (insight: TacticalInsightLevel) : string =
    match insight with
    | Obscured ->
      "Target maintains stoic composure. Use Discipline strikes to build Study Stacks (+15 Acumen) to decipher their form."
    | Discerning ->
      let (bestS, _) = bestStat
      match bestS with
      | StatId.Reflex -> "Target appears highly evasive. Finesse cadences risk being deflected; consider Cleave or Stance Pressure."
      | StatId.Fortitude -> "Target exhibits heavy structural durability. Direct Force cleaves face steep resistance."
      | StatId.Poise -> "Target holds a rigid center of gravity. Stance Pressure may struggle to break their guard."
      | StatId.Force -> "Target packs immense kinetic striking power. Prepare for heavy physical blows."
      | StatId.Finesse -> "Target attacks with swift multi-strike probing. Guard against rapid cadences."
      | StatId.Prowess -> "Target applies disciplined pressure. Mind your posture against gambits."
      | _ -> "Target relies on psychological discipline. Exploit openings as they present themselves."
    | Keen | Penetrating ->
      let (bestS, bestVal) = bestStat
      let (weakDefS, weakDefVal) = weakestDef
      let counterAdvice =
        match weakDefS with
        | StatId.Fortitude -> sprintf "Exploit lower Fortitude (%d) with Force strikes (Cleave / Wild Blow)." weakDefVal
        | StatId.Reflex -> sprintf "Exploit lower Reflex (%d) with Agility cadences (Finesse Probing / Blitz)." weakDefVal
        | StatId.Poise -> sprintf "Exploit lower Poise (%d) with Discipline pressure (Stance Pressure / Flaw Strike)." weakDefVal
        | StatId.Resolve -> sprintf "Exploit lower Resolve (%d) with Power trauma (Elemental Blast / Mind Fracture)." weakDefVal
        | StatId.Intuition -> sprintf "Exploit lower Intuition (%d) with Agility glamour (Neural Static)." weakDefVal
        | StatId.Composure -> sprintf "Exploit lower Composure (%d) with Acumen interrogation." weakDefVal
        | _ -> "Target their defensive opening."
      sprintf "Primary threat: %A (%d). %s" bestS bestVal counterAdvice

  /// Evaluates an observer's tactical assessment of a target combatant
  let assess (observer: Combatant) (target: Combatant) : TacticalAssessment =
    let effAcumen = calculateEffectiveScrutiny observer
    let effMasking = calculateEffectiveMasking target
    let ratio = float effAcumen / float effMasking

    let insightLevel =
      if ratio >= 1.15 then Penetrating
      elif ratio >= 0.75 then Keen
      elif ratio >= 0.40 then Discerning
      else Obscured

    let (bestStat, bestVal), bestOff, bestDef, weakestDef = analyzeTargetStats target
    let roleStr = statRole bestStat

    let headline, primarySummary, vulnSummary =
      match insightLevel with
      | Penetrating ->
        "PENETRATING INSIGHT",
        sprintf "%A: %d (%s — Apex Strength)" bestStat bestVal roleStr,
        Some (sprintf "%A: %d (%s — Critical Flaw)" (fst weakestDef) (snd weakestDef) (statRole (fst weakestDef)))
      | Keen ->
        "KEEN INSIGHT",
        sprintf "%A: %d (%s)" bestStat bestVal roleStr,
        Some (sprintf "%A: %d (%s Opening)" (fst weakestDef) (snd weakestDef) (statRole (fst weakestDef)))
      | Discerning ->
        let tierDesc =
          if bestVal >= 400 then "Colossal"
          elif bestVal >= 200 then "Formidable"
          elif bestVal >= 100 then "Moderate"
          else "Low"
        "DISCERNING READ",
        sprintf "%A (~%d-%d [%s %s])" bestStat (Math.Max(1, bestVal - 20)) (bestVal + 20) tierDesc roleStr,
        Some (sprintf "%A appears to be their lowest defense" (fst weakestDef))
      | Obscured ->
        let desc = Attributes.descriptorOf bestStat
        "GUARDED POSTURE",
        sprintf "Favors %A-leaning posture (Exact stat masked by Composure)" desc.Vector,
        None

    let advice = generateAdvice (bestStat, bestVal) weakestDef insightLevel

    { ObserverId = observer.Id
      TargetId = target.Id
      InsightLevel = insightLevel
      EffectiveAcumen = effAcumen
      TargetComposure = effMasking
      Ratio = ratio
      PrimaryStat = bestStat
      PrimaryStatValue = bestVal
      PrimaryStatRole = roleStr
      BestOffense = bestOff
      BestDefense = bestDef
      WeakestDefense = weakestDef
      Headline = headline
      PrimarySummary = primarySummary
      VulnerabilitySummary = vulnSummary
      StrategicAdvice = advice }

[<AutoOpen>]
module CombatantTacticalExtensions =
  type Combatant with
    /// Evaluates this combatant's tactical assessment of an opponent based on Acumen vs Composure
    member this.AssessTarget(target: Combatant) : TacticalAssessment =
      TacticalAssessment.assess this target
