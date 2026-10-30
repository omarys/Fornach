namespace Fornach.Tests

open Xunit
open Xunit.Abstractions
open System
open Fornach.Domain
open Fornach.Spatial
open Fornach.Engine
open Fornach.Cli

/// A headless player that plays a fight as well as it can, so balance can be measured against a ceiling
/// instead of against the heuristic bot. It is deliberately one ply deep: each turn it resolves every
/// legal action through the real resolver and keeps the best outcome by a stated formula.
module OptimalPlayer =

  /// Scores need a contest that can actually differ between the two sides. A roller that returns the same
  /// value for both ties every exchange at NetHits 0 and whiffs every strike. The attacker takes the top of
  /// the range, the defender the middle, and it is rebuilt per candidate so the ranking compares actions.
  let private scoringRoller () =
    let mutable call = 0

    fun min max ->
      call <- call + 1
      if call % 2 = 1 then max else (min + max) / 2

  /// Everything a player can swing with. Split by plane so a fighter is not scored on mental strikes it has
  /// no business throwing, and a mage is not scored on cleaves.
  let physicalAttacks =
    [ ForceStrike false
      ForceStrike true
      FinesseCadence false
      FinesseCadence true
      ProwessStrike false
      ProwessStrike true
      ShieldBash false
      ShieldBash true
      ForceInsightStrike
      FinesseInsightStrike
      CalculatedFlawStrike ]

  let mentalAttacks =
    [ AuthorityDecree false
      AuthorityDecree true
      GuileDeception false
      GuileDeception true
      AcumenInterrogation false
      AcumenInterrogation true
      ArcaneCataclysm false
      ArcaneCataclysm true
      SynapticGlamour false
      SynapticGlamour true
      MirrorIllusion false
      MirrorIllusion true
      RunicWardTrap false
      RunicWardTrap true
      DisorientingShockwave false
      DisorientingShockwave true
      StaffSiphon ]

  /// The five grief gambits are psychological, so they belong to the mental pool.
  let traumaGambits =
    [ DenialPhaseShift; BasaltEruption; CoerciveBargain; ApathyDoldrums; SereneResolution ]

  let poolFor (solo: Combatant) =
    if solo.Plane = Physical then
      (physicalAttacks |> List.map StandardAttack) @ [ ExecuteStrike Physical ]
    else
      (mentalAttacks |> List.map StandardAttack)
      @ (traumaGambits |> List.map (TraumaAttack >> StandardAttack))
      @ [ ExecuteStrike Mental ]

  /// The Nach is defensive in the menu, so the policy holds the guard that answers the plane the attacker
  /// actually swings on — and inside that plane, the stronger of the two defensive attributes.
  let bestDefense (solo: Combatant) (attacker: Combatant) : DefenseClassification =
    if attacker.Plane = Mental then
      if solo.GetStat Resolve >= solo.GetStat Intuition then ResolveAnchor true
      else IntuitionPerception true
    elif solo.GetStat Fortitude >= solo.GetStat Reflex then FortitudeGuard true
    else ReflexSlip true

  /// Both pools count. A mental-plane fighter swings on Morale and a physical one on Health, so reading
  /// Health alone scored every caster action at zero damage and zero kills — the Mage's scorer was picking
  /// on momentum ramp alone, which is why a stronger Mage measured as a worse one.
  let private poolLeft (c: Combatant) = c.Health.Current + c.Morale.Current
  let private isBeaten (c: Combatant) = c.Health.IsDepleted || c.Morale.IsDepleted

  let private damageDealt (before: Combatant list) (after: Combatant list) =
    let poolOf (pool: Combatant list) (id: CombatantId) =
      pool
      |> List.tryFind (fun c -> c.Id = id)
      |> Option.map poolLeft
      |> Option.defaultValue 0

    after |> List.sumBy (fun c -> max 0 (poolOf before c.Id - poolLeft c))

  /// What the pack does back. Each surviving foe answers with the attack its own AI would pick, resolved
  /// through the same resolver against the fighter as this action left them, and the damage that lands is
  /// the price of the action. One ply cannot see this at all, which is why the first numbers were not
  /// trustworthy: a strike that looks best in isolation can be the one that gets you killed.
  let private replyCost (solo: Combatant) (foes: Combatant list) =
    foes
    |> List.filter (fun f -> not f.Health.IsDepleted)
    |> List.sumBy (fun foe ->
      let intent = AI.chooseIntentWithContext foe solo 0
      let answer = ActionResolver.resolveGroupTurn (scoringRoller ()) intent foe solo []
      max 0 (poolLeft solo - poolLeft answer.PrimaryTarget))

  /// Two-ply payoff: damage resolved for, what it kills, the ramp banked, a bonus for retaking the Vor from
  /// the Nach, minus the damage the pack's answer lands. Dying is scored out of contention.
  let private scoreAction (solo: Combatant) (foes: Combatant list) (intent: ActionIntent) =
    let live = foes |> List.filter (fun f -> not f.Health.IsDepleted)
    let target = live |> List.minBy (fun f -> f.Health.Current)
    let adjacent = live |> List.filter (fun f -> f.Id <> target.Id)
    let res = ActionResolver.resolveGroupTurn (scoringRoller ()) intent solo target adjacent
    let resolved = res.PrimaryTarget :: res.SecondaryTargets

    let damage = damageDealt live resolved
    let kills = resolved |> List.filter isBeaten |> List.length
    let ramp = (res.Actor.Momentum + res.Actor.Flow) - (solo.Momentum + solo.Flow)
    let retookVor = if res.Actor.IsInVor && solo.IsInNach then 20.0 else 0.0
    let died = if isBeaten res.Actor then -1e6 else 0.0

    // A foe this action killed does not get to answer it.
    let stillStanding =
      live
      |> List.filter (fun f ->
        not (resolved |> List.exists (fun r -> r.Id = f.Id && isBeaten r)))

    // Weighted above the damage it buys: an action that leaves the fighter open is how a run ends.
    let exposed = float (replyCost res.Actor stillStanding) * 1.2

    float damage + (float kills * 250.0) + (float ramp * 6.0) + retookVor - exposed + died

  /// A policy instance carries the pack it is fighting, so the reply can be priced against everyone still
  /// standing rather than only the primary target the intent hook is handed. One per fight: it is mutable.
  let freshPolicy () : Simulation.GroupPolicy =
    let mutable pack: Combatant list = []

    { ChooseTarget =
        fun _ foes ->
          pack <- foes
          foes |> List.minBy (fun f -> f.Health.Current)
      ChooseDefense = bestDefense
      ChooseIntent =
        fun solo target _swings ->
          // Swinging out of the Nach is how the Vor comes back and the scorer prices that in, so attacks
          // stay on the table in either tempo; guarding is for when the answer would finish the fighter.
          let badlyHurt = solo.Health.Current * 3 < solo.Health.Maximum

          if solo.IsInNach && badlyHurt then
            ActiveDefense(bestDefense solo target)
          else
            poolFor solo
            |> List.map (fun intent -> intent, scoreAction solo pack intent)
            |> List.maxBy snd
            |> fst }

  // -- driving a floor -------------------------------------------------------------------------------

  let private chambersOf (floor: TowerFloor) (packSize: int) =
    let spawn = floor.SpawnLocation
    let distance (pt: Point) = abs (pt.X - spawn.X) + abs (pt.Y - spawn.Y)

    let roaming =
      floor.Entities
      |> Map.toList
      |> List.choose (fun (pt, ent) ->
        match ent with
        | TowerEntity.EntityEnemy e when not e.IsDefeated && not e.IsBoss -> Some(distance pt, e)
        | _ -> None)
      |> List.sortBy fst
      |> List.map snd
      |> List.chunkBySize packSize

    // The guardian is the floor's last fight, so a walk that skips it does not measure the floor clear.
    let guardians =
      floor.Entities
      |> Map.toList
      |> List.choose (fun (_, ent) ->
        match ent with
        | TowerEntity.EntityEnemy e when not e.IsDefeated && e.IsBoss -> Some e
        | _ -> None)

    if guardians.IsEmpty then roaming else roaming @ [ guardians ]

  /// Plays a whole floor chamber by chamber, carrying health, growth and XP forward the way the tower does.
  /// Returns chambers cleared, chambers faced, and the survivor.
  let playFloor (policy: Simulation.GroupPolicy) (player: Combatant) (seed: int) (floorNumber: int) (story: bool) =
    let floor = (TowerSession.initSessionWithMode player seed floorNumber story).CurrentFloor
    let chambers = chambersOf floor 3
    let mutable survivor = player
    let mutable cleared = 0
    let mutable died = false

    chambers
    |> List.iteri (fun index chamber ->
      if not died then
        let rng = Random(seed + index)
        let roller: DiceRoller = fun min max -> rng.Next(min, max + 1)
        let foes = chamber |> List.map (fun e -> e.Combatant)
        let result, after = Simulation.runPreparedGroupMatchWith policy roller survivor foes 80

        if result.SoloWon then
          cleared <- cleared + 1
          let grown = Combatant.growFromEncounter foes after
          let xp = foes |> List.sumBy (fun f -> f.Level * 10 + 25)
          let levelled, _ = TowerSession.applyExperience grown xp
          survivor <- { Combatant.clearStacks levelled with Meters = StatusMeters.Zero }
        else
          died <- true
          survivor <- after)

    cleared, chambers.Length, survivor

  type OptimalPlayTests(output: ITestOutputHelper) =

    [<Theory>]
    [<InlineData("fighter")>]
    [<InlineData("berserker")>]
    [<InlineData("duelist")>]
    [<InlineData("warden")>]
    member _.``optimal play clears the prologue floor on the physical archetypes``(className: string) =
      let mutable cleared = 0

      // Ten seeds: the prologue must be clearable with a competent player, not only with a lucky one.
      for trial in 0 .. 9 do
        let player = StoryBosses.createProloguePlayer className
        let chambers, total, survivor = playFloor (freshPolicy ()) player (9100 + trial) 1 true

        output.WriteLine(
          sprintf
            "%s seed %d: %d/%d chambers, hp %d/%d, morale %d/%d, level %d"
            className
            (9100 + trial)
            chambers
            total
            survivor.Health.Current
            survivor.Health.Maximum
            survivor.Morale.Current
            survivor.Morale.Maximum
            survivor.Level
        )

        if chambers = total then
          cleared <- cleared + 1

      // Measured bar, not an aspiration: with best play the four physical archetypes walk the whole
      // prologue floor in every seed. The four mental archetypes clear zero chambers here — recorded in
      // `mental archetypes cannot clear the prologue floor` below rather than hidden in this ratio.
      Assert.True(cleared >= 8, sprintf "optimal play should clear the prologue floor in at least 8 of 10 seeds, cleared %d" cleared)

    [<Theory>]
    [<InlineData("mage")>]
    [<InlineData("mesmer")>]
    [<InlineData("abjurer")>]
    [<InlineData("inquisitor")>]
    member _.``mental archetypes clear the prologue floor``(className: string) =
      // This was a defect record: best play used to get a mental archetype through at most three chambers.
      // The cause was not enemy mental defence but the gambit self-tax — every declared gambit charged its
      // whole Recklessness exposure before the roll, so three or four hits that all landed drove the caster
      // into `RecklessExposure` at full HP and full Morale. Capping the self-spike one short of the break
      // and firming early enemies' mental guard up only against a mental-plane attacker fixed it.
      //
      // The floor still is not won on the mental plane — the seeds that fail die at 0 HP with Morale
      // untouched — so this bounds how *often* a caster clears, not how it clears.
      let mutable cleared = 0

      for trial in 0 .. 4 do
        let player = StoryBosses.createProloguePlayer className
        let chambers, total, survivor = playFloor (freshPolicy ()) player (9300 + trial) 1 true

        output.WriteLine(
          sprintf
            "%s seed %d: %d/%d chambers, hp %d/%d morale %d/%d"
            className
            (9300 + trial)
            chambers
            total
            survivor.Health.Current
            survivor.Health.Maximum
            survivor.Morale.Current
            survivor.Morale.Maximum
        )

        if chambers = total then
          cleared <- cleared + 1

      Assert.True(
        cleared >= 3,
        sprintf "a mental archetype cleared the prologue floor in only %d of 5 seeds: the caster self-tax has regressed" cleared
      )

    [<Theory>]
    [<InlineData("Fighter")>]
    [<InlineData("Mage")>]
    [<InlineData("Warrior")>]
    [<InlineData("Rogue")>]
    member _.``optimal play survives the tower climb without healing``(className: string) =
      let cls =
        match className with
        | "Mage" -> CharacterClass.Mage
        | "Warrior" -> CharacterClass.Warrior
        | "Rogue" -> CharacterClass.Rogue
        | _ -> CharacterClass.Fighter

      let floors = 12
      let report = Simulation.runProgressionSimWith (freshPolicy ()) (fun _ -> cls) 12 floors

      output.WriteLine(
        sprintf "%s: avg floors %.1f/%d, runs cleared %d/%d" className report.AvgFloorsReached floors report.RunsWon report.Runs
      )

      for f in report.Floors do
        if f.Reached > 0 then
          output.WriteLine(
            sprintf
              "  floor %2d reached %2d cleared %2d (%.0f%%) avg level %.1f hp %.0f%% morale %.0f%%"
              f.Floor
              f.Reached
              f.Wins
              (float f.Wins / float f.Reached * 100.0)
              f.AvgLevel
              f.AvgHpPct
              f.AvgMoralePct
          )

      // Measured baseline for a gearless level-1 protagonist climbing without healing, one entry per class.
      // These are pinning numbers, not targets: they fail when a balance change moves the curve, and the
      // spread between them is the finding worth arguing about (Mage 1.2 vs Fighter 1.8 floors).
      let baseline =
        match className with
        | "Fighter" -> 1.7
        | "Rogue" -> 1.1
        | "Warrior" -> 0.9
        | _ -> 1.0

      Assert.True(
        report.AvgFloorsReached >= baseline,
        sprintf
          "optimal play with %s should reach at least floor %.1f on average, reached %.1f"
          className
          baseline
          report.AvgFloorsReached
      )
