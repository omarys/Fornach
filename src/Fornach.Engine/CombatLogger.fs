namespace Fornach.Engine

open System
open System.IO
open System.Text
open Fornach.Domain

/// Structured combat logging facility for recording round-by-round tactical contests,
/// dice pool evaluations, damage calculations, and combatant state snapshots.
module CombatLogger =

  type CombatTurnRecord =
    { Round: int
      TurnNumber: int
      ActorName: string
      TargetName: string
      ActionName: string
      Contest: ContestResult option
      Events: CombatEvent list
      ActorVitalsAfter: string
      TargetVitalsAfter: string }

  type CombatLogSession =
    { SessionId: string
      Title: string
      EncounterType: string
      Timestamp: DateTimeOffset
      InitialPlayer: Combatant
      InitialOpponent: Combatant
      Turns: ResizeArray<CombatTurnRecord>
      LogDirectory: string }

  /// Formats attributes and vital statistics of a combatant into a readable block
  let formatCombatantSummary (prefix: string) (c: Combatant) : string =
    let sb = StringBuilder()
    let planeStr = sprintf "%A" c.Plane
    let stanceStr = sprintf "%A" c.Stance
    let familyStr = c.MonsterFamily |> Option.map (fun f -> sprintf "%A" f) |> Option.defaultValue c.Class.Name
    let absPct = c.Armor.AbsorptionRatio * 100.0

    sb.AppendLine(sprintf "%s: %s | %s (Level %d)" prefix c.Name familyStr c.Level) |> ignore
    sb.AppendLine(sprintf "  Plane: %s | Stance: %s" planeStr stanceStr) |> ignore
    sb.AppendLine(sprintf "  Health: %d/%d (%.1f%%) | Morale: %d/%d (%.1f%%) | Armor: %d/%d (Absorb: %.1f%%)"
      c.Health.Current c.Health.Maximum (float c.Health.Current / Math.Max(1.0, float c.Health.Maximum) * 100.0)
      c.Morale.Current c.Morale.Maximum (float c.Morale.Current / Math.Max(1.0, float c.Morale.Maximum) * 100.0)
      c.Armor.Current c.Armor.Max absPct) |> ignore
    sb.AppendLine(sprintf "  Attributes: Force=%d, Fortitude=%d, Finesse=%d, Reflex=%d, Prowess=%d, Poise=%d"
      (c.GetStat Force) (c.GetStat Fortitude) (c.GetStat Finesse) (c.GetStat Reflex) (c.GetStat Prowess) (c.GetStat Poise)) |> ignore
    sb.AppendLine(sprintf "              Intellect=%d, Resolve=%d, Acuity=%d, Intuition=%d, Acumen=%d, Composure=%d"
      (c.GetStat Intellect) (c.GetStat Resolve) (c.GetStat Acuity) (c.GetStat Intuition) (c.GetStat Acumen) (c.GetStat Composure)) |> ignore
    if not c.EquippedItems.IsEmpty then
      let items = c.EquippedItems |> List.map (fun i -> i.Name) |> String.concat ", "
      sb.AppendLine(sprintf "  Equipped Gear: %s" items) |> ignore
    if not c.MonsterTraits.IsEmpty then
      let traits = c.MonsterTraits |> List.map (fun t -> sprintf "%A" t) |> String.concat ", "
      sb.AppendLine(sprintf "  Monster Traits: %s" traits) |> ignore
    if c.BleedStacks > 0 || c.LimbDebuff > 0 || c.ArcaneWard > 0 || c.MirrorClones > 0 then
      sb.AppendLine(sprintf "  Debuffs/Wards: Bleed=%d, LimbDebuff=%d, ArcaneWard=%d, MirrorClones=%d"
        c.BleedStacks c.LimbDebuff c.ArcaneWard c.MirrorClones) |> ignore
    sb.ToString().TrimEnd()

  /// Formats a domain CombatEvent into a concise, human-readable log string
  let formatCombatEvent (evt: CombatEvent) : string =
    match evt with
    | CombatEvent.DamageApplied d ->
      let crit = if d.IsCritical then " [CRITICAL]" else ""
      let comp = if d.IsArmorCompromised then " [ARMOR COMPROMISED]" else ""
      sprintf "Damage Dealt: %d %A%s%s" d.Amount d.Plane crit comp
    | CombatEvent.DisparityTriggered (_, _, outcome) ->
      sprintf "Stat Disparity: %A" outcome
    | CombatEvent.PassiveProcTriggered (_, _, outcome) ->
      sprintf "Passive Proc: %A" outcome
    | CombatEvent.MonsterTraitTriggered (_, name, desc) ->
      sprintf "Monster Trait [%s]: %s" name desc
    | CombatEvent.AcidicArmorCorroded (_, corrosion) ->
      sprintf "Acidic Blood: corroded %d Armor durability!" corrosion
    | CombatEvent.MoltenBurnInflicted (_, burn) ->
      sprintf "Molten Aura: scorched attacker for %d physical burn damage!" burn
    | CombatEvent.GambitDeclared (_, name, cost) ->
      sprintf "Gambit Declared: %s (+%d Recklessness)" name cost
    | CombatEvent.GambitPunished (_, reason) ->
      sprintf "Gambit Punished: %s" reason
    | CombatEvent.FormStabilized (_, drained, gained) ->
      sprintf "Form Stabilized: drained %d Recklessness, gained %d Study Stacks" drained gained
    | CombatEvent.BreathStabilized (_, summary, morale) ->
      sprintf "Steady Breathing: %s (+%d Morale)" summary morale
    | CombatEvent.CollapseRecovered _ ->
      "Collapse Recovered: All strain meters vented below critical thresholds; collapsed state cleared."
    | CombatEvent.ComboReset (_, reason) ->
      sprintf "Combo Reset: %s" reason
    | CombatEvent.CollapseTriggered (_, reason) ->
      sprintf "THRESHOLD COLLAPSE: %A" reason
    | CombatEvent.Executed (_, _, plane) ->
      sprintf "EXECUTED: Fatal strike delivered (%A)" plane
    | CombatEvent.EquipmentProcTriggered (name, _, desc) ->
      sprintf "Equipment Proc [%s]: %s" name desc
    | CombatEvent.WeaponDegraded (_, cond) ->
      sprintf "Weapon Degraded: %A" cond
    | CombatEvent.StanceShifted (_, oldS, newS) ->
      sprintf "Stance Shifted: %A -> %A" oldS newS
    | CombatEvent.RiposteExecuted (_, _, dmg) ->
      sprintf "Riposte Counter: %d damage" dmg
    | CombatEvent.DisarmExecuted (_, _, reason) ->
      sprintf "Disarmed: %s" reason
    | CombatEvent.BleedTicked (_, dmg, rem) ->
      sprintf "Bleed Tick: %d damage (Remaining Stacks: %d)" dmg rem
    | CombatEvent.BleedApplied (_, added, tot) ->
      sprintf "Bleed Inflicted: +%d stacks (Total: %d)" added tot
    | CombatEvent.LimbDisabled (_, pen) ->
      sprintf "Limb Disabled: -%d Reflex" pen
    | CombatEvent.ArcaneWardErected (_, added, tot) ->
      sprintf "Arcane Ward Erected: +%d barrier (Total: %d)" added tot
    | CombatEvent.ArcaneWardAbsorbed (_, soaked, rem) ->
      sprintf "Arcane Ward Absorbed: soaked %d damage (Remaining: %d)" soaked rem
    | CombatEvent.MirrorClonesConjured (_, added, tot) ->
      sprintf "Mirror Clones Conjured: +%d (Total: %d)" added tot
    | CombatEvent.MirrorCloneDecoyed (_, _, rem) ->
      sprintf "Mirror Clone Decoyed: strike absorbed by illusion decoy (Remaining: %d)" rem
    | other ->
      sprintf "%A" other

  /// Formats the detailed dice pool and contest resolution
  let formatContestDetails (actionName: string) (actorName: string) (contest: ContestResult) : string =
    let sb = StringBuilder()
    let atk = contest.Attacker
    let def = contest.Defender
    let tierMult = ActionResolver.computeTierMultiplier contest.NetHits

    let atkRolls = atk.RawRolls |> List.map string |> String.concat ", "
    let defRolls = def.RawRolls |> List.map string |> String.concat ", "

    sb.AppendLine(sprintf "  Action: %s" actionName) |> ignore
    sb.AppendLine(sprintf "  Attacker (%s): Stat=%d | Pool=%d dice | Rolls=[%s] | FloorHits=+%d | RolledHits=%d | TotalHits=%d%s"
      actorName atk.StatValue atk.RawRolls.Length atkRolls atk.FloorHits atk.RolledHits atk.TotalHits (if atk.IsGlitch then " [GLITCH]" else "")) |> ignore
    sb.AppendLine(sprintf "  Defender: Stat=%d | Pool=%d dice | Rolls=[%s] | FloorHits=+%d | RolledHits=%d | TotalHits=%d%s"
      def.StatValue def.RawRolls.Length defRolls def.FloorHits def.RolledHits def.TotalHits (if def.IsGlitch then " [GLITCH]" else "")) |> ignore
    if contest.EncirclementPenalty > 0 then
      sb.AppendLine(sprintf "  Encirclement Defense Penalty: -%d defense hits" contest.EncirclementPenalty) |> ignore
    let outcomeTag =
      if contest.IsWhiff then "WHIFF (Deflected / 0.00x Damage)"
      else
        let crit = if contest.IsCritical then " [CRITICAL]" else ""
        sprintf "PENETRATING HIT: %+d Net Hits -> Tier Multiplier: %.2fx%s" contest.NetHits tierMult crit
    sb.AppendLine(sprintf "  Contest Outcome: %s" outcomeTag) |> ignore
    sb.ToString().TrimEnd()

  /// Formats current vitals for quick state tracking
  let formatVitalsSnapshot (c: Combatant) : string =
    sprintf "%s: HP %d/%d (%.1f%%) | Morale %d/%d (%.1f%%) | Armor %d/%d | Recklessness %d/%d | Bleed %d"
      c.Name c.Health.Current c.Health.Maximum
      (float c.Health.Current / Math.Max(1.0, float c.Health.Maximum) * 100.0)
      c.Morale.Current c.Morale.Maximum
      (float c.Morale.Current / Math.Max(1.0, float c.Morale.Maximum) * 100.0)
      c.Armor.Current c.Armor.Max
      c.Meters.Recklessness.Value 100
      c.BleedStacks

  /// Initializes a new combat logging session
  let startSession (title: string) (encounterType: string) (player: Combatant) (opponent: Combatant) : CombatLogSession =
    let defaultLogDir =
      try
        Path.Combine(Environment.CurrentDirectory, "logs")
      with _ ->
        "logs"

    { SessionId = Guid.NewGuid().ToString("N")
      Title = title
      EncounterType = encounterType
      Timestamp = DateTimeOffset.UtcNow
      InitialPlayer = player
      InitialOpponent = opponent
      Turns = ResizeArray<CombatTurnRecord>()
      LogDirectory = defaultLogDir }

  /// Records an individual combat turn within a round
  let recordTurn
    (session: CombatLogSession)
    (round: int)
    (turnNum: int)
    (actionName: string)
    (actorBefore: Combatant)
    (targetBefore: Combatant)
    (result: ActionResult) : unit =
    let turnRecord =
      { Round = round
        TurnNumber = turnNum
        ActorName = actorBefore.Name
        TargetName = targetBefore.Name
        ActionName = actionName
        Contest = result.Contest
        Events = result.Events
        ActorVitalsAfter = formatVitalsSnapshot result.Actor
        TargetVitalsAfter = formatVitalsSnapshot result.Target }
    session.Turns.Add turnRecord

  /// Generates the full formatted string representation of the combat session
  let formatSession (session: CombatLogSession) (outcome: string) (finalPlayer: Combatant) (finalOpponent: Combatant) : string =
    let sb = StringBuilder()
    sb.AppendLine("================================================================================") |> ignore
    sb.AppendLine("FORNACH COMBAT LOG - TACTICAL DUEL RECORD") |> ignore
    sb.AppendLine(sprintf "Session: %s" session.Title) |> ignore
    sb.AppendLine(sprintf "Encounter Type: %s" session.EncounterType) |> ignore
    sb.AppendLine(sprintf "Timestamp: %s" (session.Timestamp.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"))) |> ignore
    sb.AppendLine("--------------------------------------------------------------------------------") |> ignore
    sb.AppendLine(formatCombatantSummary "PLAYER" session.InitialPlayer) |> ignore
    sb.AppendLine() |> ignore
    sb.AppendLine(formatCombatantSummary "OPPONENT" session.InitialOpponent) |> ignore
    sb.AppendLine("================================================================================") |> ignore
    sb.AppendLine() |> ignore

    let rounds = session.Turns |> Seq.groupBy (fun t -> t.Round) |> Seq.sortBy fst
    for (roundNum, turns) in rounds do
      sb.AppendLine("--------------------------------------------------------------------------------") |> ignore
      sb.AppendLine(sprintf "ROUND %d" roundNum) |> ignore
      sb.AppendLine("--------------------------------------------------------------------------------") |> ignore
      for turn in turns do
        sb.AppendLine(sprintf "[Turn %d] %s executes %s against %s" turn.TurnNumber turn.ActorName turn.ActionName turn.TargetName) |> ignore
        match turn.Contest with
        | Some contest ->
          sb.AppendLine("  Contest Resolution:") |> ignore
          sb.AppendLine(formatContestDetails turn.ActionName turn.ActorName contest) |> ignore
        | None ->
          sb.AppendLine(sprintf "  Action: %s (No opposed contest required)" turn.ActionName) |> ignore

        if not turn.Events.IsEmpty then
          sb.AppendLine("  Events Emitted:") |> ignore
          for evt in turn.Events do
            sb.AppendLine(sprintf "    • %s" (formatCombatEvent evt)) |> ignore

        sb.AppendLine("  Post-Turn Vitals:") |> ignore
        sb.AppendLine(sprintf "    %s" turn.ActorVitalsAfter) |> ignore
        sb.AppendLine(sprintf "    %s" turn.TargetVitalsAfter) |> ignore
        sb.AppendLine() |> ignore

    sb.AppendLine("================================================================================") |> ignore
    sb.AppendLine(sprintf "COMBAT RESOLUTION: %s" (outcome.ToUpperInvariant())) |> ignore
    let totalRounds = if session.Turns.Count = 0 then 0 else (session.Turns |> Seq.map (fun t -> t.Round) |> Seq.max)
    sb.AppendLine(sprintf "Total Rounds: %d | Total Actions Resolved: %d" totalRounds session.Turns.Count) |> ignore
    sb.AppendLine() |> ignore
    sb.AppendLine("FINAL VITALS:") |> ignore
    sb.AppendLine(sprintf "  %s" (formatVitalsSnapshot finalPlayer)) |> ignore
    sb.AppendLine(sprintf "  %s" (formatVitalsSnapshot finalOpponent)) |> ignore
    sb.AppendLine("================================================================================") |> ignore
    sb.ToString()

  /// Writes combat session log to disk:
  /// - Overwrites `logs/combat_latest.log` for immediate debugging
  /// - Appends to `logs/combat_history.log` for long-term historical inspection
  let writeSession (session: CombatLogSession) (outcome: string) (finalPlayer: Combatant) (finalOpponent: Combatant) : string * string =
    let logText = formatSession session outcome finalPlayer finalOpponent
    let dir =
      try
        if not (Directory.Exists session.LogDirectory) then
          Directory.CreateDirectory session.LogDirectory |> ignore
        session.LogDirectory
      with _ ->
        let fallback = Path.Combine(AppContext.BaseDirectory, "logs")
        if not (Directory.Exists fallback) then
          Directory.CreateDirectory fallback |> ignore
        fallback

    let latestFile = Path.Combine(dir, "combat_latest.log")
    let historyFile = Path.Combine(dir, "combat_history.log")

    try
      File.WriteAllText(latestFile, logText, Encoding.UTF8)
      File.AppendAllText(historyFile, "\n\n" + logText, Encoding.UTF8)
    with ex ->
      Console.Error.WriteLine(sprintf "[CombatLogger Warning] Failed to write combat log file: %s" ex.Message)

    latestFile, historyFile
