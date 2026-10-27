namespace Fornach.Engine

open System
open Fornach.Domain
open Fornach.Spatial

/// Discrete events emitted during the Tower dungeon crawl turn loop
[<RequireQualifiedAccess>]
type TowerEvent =
  | Moved of Point
  | StairwayAscended of nextFloor: int
  | DoorLockedMessage of message: string
  | CombatTriggered of enemy: TowerEnemy
  | NpcInteracted of npc: TowerNpc * questCompleted: bool
  | ChestOpened of chest: TowerChest * item: EquipmentItem option * key: string option
  | ShrineActivated of shrine: TowerShrine
  | HazardStepped of hazard: HazardType * message: string
  | AltarEncountered of altar: AltarChoice * point: Point
  | TraderEncountered of merchant: SpectralMerchant * point: Point
  | VaultEncountered of vault: VaultData * point: Point
  | TrapTriggered of trap: TrapGauntletData * message: string
  | TrapDisarmed of trap: TrapGauntletData * message: string
  | EchoDiscovered of echo: MemoryEchoData
  | AmbushTriggered of ambush: AmbushData * spawnedEnemies: TowerEnemy list
  | GroupCombatTriggered of enemies: TowerEnemy list

/// The running aggregate state of an active Tower dungeon ascent
type TowerRunState =
  { Player: Combatant
    CurrentFloor: TowerFloor
    PlayerPosition: Point
    Facing: Direction option
    Locomotion: LocomotionState
    CollectedKeys: Set<string>
    CompletedQuests: Set<string>
    InventoryItems: EquipmentItem list
    MessageLog: string list
    Seed: int
    Souls: int
    Trophies: (AlchemicalTrophy * int) list
    /// Single-use items carried, by kind and count.
    Consumables: (ConsumableKind * int) list
    /// Kinds found but not yet revealed by a shrine. Unidentified kinds cannot be used.
    UnidentifiedConsumables: Set<ConsumableKind>
    DiscoveredEchoes: Set<string>
    IsStoryMode: bool
    /// Safe steps taken since the last hone; travel hones a weapon every fourth step.
    SafeStepsSinceHone: int }

module TowerSession =

  /// Recalculates field-of-view and updates explored memory
  let updateFov (state: TowerRunState) : TowerRunState =
    let cone =
      VisionResolver.resolve
        state.Player
        state.PlayerPosition
        state.Facing
        state.Locomotion
        EnvironmentalFactors.Default

    let visibleTiles = Fov.compute state.CurrentFloor.IsOpaque cone
    let updatedExplored = Set.union state.CurrentFloor.Explored visibleTiles

    let updatedFloor =
      { state.CurrentFloor with
          Visible = visibleTiles
          Explored = updatedExplored }

    { state with CurrentFloor = updatedFloor }

  /// Chebyshev radius within which a living enemy takes notice of the player and gives chase
  let aggroRadius = 8

  /// Resting and waiting top out here. Only a shrine restores Health, Morale, Armor or a weapon
  /// to full, so a long rest can never substitute for finding one.
  let restRecoveryFraction = 0.80

  /// Number of consecutive waits/steps taken as the player's current focus depth.
  let private focusDepth (locomotion: LocomotionState) =
    match locomotion with
    | LocomotionState.Stationary turns -> Math.Max(0, turns)
    | _ -> 0

  /// Waiting widens the view one step (VisionResolver dilates the arc per Stationary turn).
  /// Entering a floor with the whole floor in view: 3 focus turns is 120 + 3*90, i.e. the full circle.
  let private entryLocomotion = LocomotionState.Stationary 3

  let focusOnWait (locomotion: LocomotionState) : LocomotionState =
    LocomotionState.Stationary(focusDepth locomotion + 1)

  /// Walking it back narrows the arc a step at a time, at the same rate it widened.
  let focusOnMove (locomotion: LocomotionState) : LocomotionState =
    match focusDepth locomotion with
    | depth when depth > 1 -> LocomotionState.Stationary(depth - 1)
    | _ -> LocomotionState.Walking

  let private restCap (maximum: int) = max 1 (int (float maximum * restRecoveryFraction))

  /// Heals toward the rest cap, never past it, and never downward.
  let restRecoverPool (pool: Pool) (amount: int) =
    let cap = restCap pool.Maximum
    if pool.Current >= cap then pool else pool.ApplyDelta(min amount (cap - pool.Current))

  /// Repairs toward the rest cap, never past it.
  let restRepairArmor (armor: ArmorIntegrity) (amount: int) =
    let cap = restCap armor.Max

    if armor.Current >= cap then armor
    else { armor with Current = min cap (armor.Current + amount) }

  /// A turn of movement closes one bleed stack, a turn of waiting five.
  let bleedClosedPerStep = 1
  let bleedClosedPerRest = 5

  /// Closes this turn's worth of bleeding, floored at zero.
  let closeBleed (bleedStacks: int) (closed: int) = Math.Max(0, bleedStacks - closed)

  /// Health and Morale cannot regenerate while the wound is open; the pool comes back untouched so
  /// the caller can say why.
  let restRecoverPoolWhenWoundClosed (bleedStacks: int) (pool: Pool) (amount: int) =
    if bleedStacks > 0 then pool else restRecoverPool pool amount

  /// One turn of out-of-combat recovery. Bleeding closes first, and Health and Morale only
  /// regenerate once that has stopped the wound. Armor is tended either way — gear is maintained
  /// regardless of what is bleeding. The combat ramp decays one stack per turn here: this function
  /// only runs when nothing is engaged, which is what keeps the meters a within-fight ramp.
  let recoverOutOfCombat (player: Combatant) (bleedClosed: int) (heal: int) (morale: int) (armorRepair: int) =
    let bleed = closeBleed player.BleedStacks bleedClosed

    { Combatant.decayStacks player with
        BleedStacks = bleed
        Health = restRecoverPoolWhenWoundClosed bleed player.Health heal
        Morale = restRecoverPoolWhenWoundClosed bleed player.Morale morale
        Armor = restRepairArmor player.Armor armorRepair }

  /// One step short of Pristine: resting hones a ruined weapon but cannot restore it.
  let restHoneWeapon (weapon: WeaponCondition) =
    match WeaponCondition.repair weapon with
    | WeaponCondition.Pristine -> weapon
    | honed -> honed

  /// Recovery on the move: every fourth safe step (the same condition armor repair uses) hones a
  /// weapon one notch, and never past Notched. The counter keeps counting while the weapon is at the
  /// cap — the fourth step is spent, not banked.
  let honeOnSafeStep (state: TowerRunState) : TowerRunState =
    let steps = state.SafeStepsSinceHone + 1

    if steps < 4 then
      { state with SafeStepsSinceHone = steps }
    else
      { state with
          SafeStepsSinceHone = 0
          Player = { state.Player with WeaponCondition = restHoneWeapon state.Player.WeaponCondition } }

  /// Uses one identified consumable. An unread pack cannot be used — only a shrine reveals it — and
  /// an empty row is refused rather than silently ignored.
  let useConsumable (kind: ConsumableKind) (state: TowerRunState) : Result<TowerRunState, string> =
    if state.UnidentifiedConsumables.Contains kind then
      Error(sprintf "The %s is still unidentified. A shrine can reveal what it is." kind.Name)
    elif Consumables.count kind state.Consumables = 0 then
      Error(sprintf "No %s in the pack." kind.Name)
    else
      Ok
        { state with
            Player = Consumables.apply kind state.Player
            Consumables = Consumables.remove kind state.Consumables }

  /// The pair a shrine sharpens for a given plane — `StatBoon.pairOf`, kept here so session callers
  /// read the same class lock the altars use.
  let shrineBoonPair (plane: Plane) (boon: StatBoon) : StatId * StatId = StatBoon.pairOf plane boon

  /// Percentage each stat of the pair gains on a shrine's first commune.
  let shrineBoonPct = 10

  /// A shrine's blessing, in order: the full repair, then every carried kind is revealed, then every
  /// carried rare material is worked into the player and spent. Repeat visits are harmless: the pack
  /// is already revealed and the materials are already gone. Returns the lines the caller chronicles.
  let shrineBlessing (shrine: TowerShrine) (pt: Point) (state: TowerRunState) : TowerRunState * string list =
    let repaired =
      { Combatant.clearStacks state.Player with
          Health = Pool.Create state.Player.Health.Maximum
          Morale = Pool.Create state.Player.Morale.Maximum
          Armor = ArmorIntegrity.Create state.Player.Armor.Max
          WeaponCondition = WeaponCondition.Pristine
          BleedStacks = 0
          LimbDebuff = 0
          Meters = StatusMeters.Zero }

    let revealed = state.UnidentifiedConsumables

    let mutable player = repaired
    let mutable bag = state.Consumables
    let mutable spent = []

    for kind in [ WeaponMaterial; MentalMaterial ] do
      let carried = Consumables.count kind bag

      if carried > 0 then
        for _ in 1..carried do
          player <- Consumables.apply kind player

        bag <- Consumables.clear kind bag
        spent <- (kind, carried) :: spent

    let summary =
      [ if not revealed.IsEmpty then
          sprintf "The shrine reveals: %s." (revealed |> Seq.map (fun k -> k.Name) |> String.concat ", ")
        for (kind, carried) in List.rev spent do
          let stat = Consumables.materialStat kind player.Class

          match stat with
          | Some s -> sprintf "The shrine works %d %s into your gear (+%d%% %A each)." carried kind.Name Consumables.materialUpgrade s
          | None -> sprintf "The shrine spends %d %s." carried kind.Name ]

    // The stat blessing is one-time, so a shrine cannot be farmed for attributes; the restorative
    // half above repeats on every visit.
    let boonLine, blessedPlayer =
      if shrine.IsBlessed then
        [], player
      else
        let first, second = shrineBoonPair player.Plane shrine.Boon

        let sharpened =
          [ first; second ]
          |> List.fold (fun (c: Combatant) (stat: StatId) ->
            { c with Stats = c.Stats.Modify(stat, StatBonus.percentOf (c.Stats.Get stat) shrineBoonPct) }) player

        [ sprintf "The monolith sharpens %A and %A by %d%% each." first second shrineBoonPct ], sharpened

    let blessedShrine = { shrine with IsBlessed = true }
    let blessedEntities = Map.add pt (EntityShrine blessedShrine) state.CurrentFloor.Entities

    { state with
        Player = blessedPlayer
        CurrentFloor = { state.CurrentFloor with Entities = blessedEntities }
        Consumables = bag
        UnidentifiedConsumables = Set.empty },
    summary @ boonLine

  /// Foes scale to the player the moment they engage, so a guardian authored for tower floor 6
  /// (level 185) is not fielded against a level 36 player. The authored state is untouched;
  /// only the combatant handed to the duel is scaled.
  let private engagedAtPlayerLevel (state: TowerRunState) (e: TowerEnemy) : TowerEnemy =
    { e with Combatant = TierFactory.clampEngagedToPlayerLevel e.Combatant state.Player }

  /// Living enemies within the aggro radius that have an unobstructed line of sight to the player
  let enemiesAggroed (state: TowerRunState) : TowerEnemy list =
    state.CurrentFloor.Entities
    |> Map.toList
    |> List.choose (fun (pt, ent) ->
      match ent with
      | EntityEnemy e when not e.IsDefeated -> Some(pt, e)
      | _ -> None)
    |> List.filter (fun (pt, _) ->
      Distance.chebyshev pt state.PlayerPosition <= aggroRadius
      && Los.check state.CurrentFloor.IsOpaque pt state.PlayerPosition)
    |> List.map (fun (_, e) -> engagedAtPlayerLevel state e)

  /// Living enemies adjacent (Chebyshev distance 1) to the player
  let adjacentEnemies (state: TowerRunState) : TowerEnemy list =
    state.CurrentFloor.Entities
    |> Map.toList
    |> List.choose (fun (pt, ent) ->
      match ent with
      | EntityEnemy e when not e.IsDefeated && Distance.chebyshev pt state.PlayerPosition = 1 -> Some(engagedAtPlayerLevel state e)
      | _ -> None)

  /// True when at least one neighbouring tile is walkable and free of blocking entities,
  /// i.e. the player is not completely encircled and may disengage
  let canEscape (state: TowerRunState) : bool =
    Point.allNeighbors state.PlayerPosition
    |> List.exists (fun p ->
      let walkable =
        match Map.tryFind p state.CurrentFloor.Tiles with
        | Some t -> t.IsWalkable
        | None -> false
      let blocked =
        match Map.tryFind p state.CurrentFloor.Entities with
        | Some ent -> ent.BlocksMovement
        | None -> false
      walkable && not blocked)

  /// True when no enemy is adjacent or actively pursuing the player, so out-of-combat recovery is allowed
  let isSafe (state: TowerRunState) : bool =
    (adjacentEnemies state).IsEmpty && (enemiesAggroed state).IsEmpty

  /// Snapshot a combatant's armor and weapon condition before a fight so the wear taken during
  /// it can be discarded when combat ends: armor penetration and weapon damage are combat debuffs.
  let combatCondition (c: Combatant) : ArmorIntegrity * WeaponCondition =
    c.Armor, c.WeaponCondition

  /// Puts a combatant's armor and weapon condition back to a captured pair.
  let restoreCombatCondition (armor: ArmorIntegrity) (weapon: WeaponCondition) (c: Combatant) : Combatant =
    { c with Armor = armor; WeaponCondition = weapon }

  /// Writes an enemy record back into the current floor's entity map by Id
  let updateEnemy (enemy: TowerEnemy) (state: TowerRunState) : TowerRunState =
    let updatedEntities =
      state.CurrentFloor.Entities
      |> Map.map (fun _ ent ->
        match ent with
        | EntityEnemy e when e.Id = enemy.Id -> EntityEnemy enemy
        | other -> other)
    { state with CurrentFloor = { state.CurrentFloor with Entities = updatedEntities } }

  /// A pursuer's tiles per turn relative to the player, from the Finesse gap: at parity the chase
  /// keeps its historical one tile per turn, a nimbler player outpaces it, a slower one is run down.
  let paceOf (playerFinesse: int) (enemyFinesse: int) =
    Math.Clamp(1.0 + float (enemyFinesse - playerFinesse) / 200.0, 0.4, 1.6)

  /// A pursuer may bank up to two steps' worth, so a fast one can close with two tiles in a turn
  /// while a long spell with nowhere to step still cannot buy more than that.
  let private maxChaseCharge = 2.0

  /// The enemy phase of a turn: aggroed living enemies pursue the player at the pace their Finesse
  /// gap buys in deterministic order (nearest first, ties by Id), then any enemies adjacent to the
  /// player raise a group engagement. Bosses and defeated enemies never move; enemies never stack
  /// or step onto the player's tile.
  let stepEnemies (state: TowerRunState) : TowerRunState * TowerEvent list =
    let orderedAggro =
      state.CurrentFloor.Entities
      |> Map.toList
      |> List.choose (fun (pt, ent) ->
        match ent with
        | EntityEnemy e when not e.IsDefeated -> Some(pt, e)
        | _ -> None)
      |> List.filter (fun (pt, _) ->
        Distance.chebyshev pt state.PlayerPosition <= aggroRadius
        && Los.check state.CurrentFloor.IsOpaque pt state.PlayerPosition)
      |> List.sortBy (fun (pt, e) -> Distance.chebyshev pt state.PlayerPosition, e.Id)

    let playerFinesse = state.Player.Stats.Get StatId.Finesse

    let mutable workingState = state

    for (_, enemy) in orderedAggro do
      // Re-read the enemy's position: an earlier move this phase may have overwritten it.
      let currentPt =
        workingState.CurrentFloor.Entities
        |> Map.toList
        |> List.tryPick (fun (p, ent) -> match ent with EntityEnemy e when e.Id = enemy.Id -> Some p | _ -> None)

      match currentPt with
      | Some pt ->
        let pace = paceOf playerFinesse (enemy.Combatant.Stats.Get StatId.Finesse)
        let mutable charge = min maxChaseCharge (enemy.ChaseCharge + pace)
        let mutable here = pt
        let mutable blocked = false

        if enemy.Id.StartsWith("boss_") then
          RuntimeLog.write (sprintf "enemy %s: holds at %A (boss)" enemy.Id pt)
        elif Distance.chebyshev pt workingState.PlayerPosition = 1 then
          // Pinned against the player it is already engaged: the chase is over, so the charge it
          // arrived with is spent rather than banked for a burst the moment the player breaks away.
          workingState <- updateEnemy { enemy with ChaseCharge = 0.0 } workingState
          RuntimeLog.write (sprintf "enemy %s: holds at %A (adjacent)" enemy.Id pt)
        else
          // Spend the banked charge one tile at a time. Two steps is the most a turn can hold, so a
          // pace above 1.0 buys real ground instead of being flattened to one tile by the bank cap.
          let mutable stepsLeft = 2

          while not blocked
                && stepsLeft > 0
                && charge >= 1.0
                && Distance.chebyshev here workingState.PlayerPosition > 1 do
            let isPassable (p: Point) =
              p = workingState.PlayerPosition
              || ((match Map.tryFind p workingState.CurrentFloor.Tiles with
                   | Some t -> t.IsWalkable
                   | None -> false)
                  && (match Map.tryFind p workingState.CurrentFloor.Entities with
                      | Some ent -> not ent.BlocksMovement
                      | None -> true))

            match Pathfinding.aStar isPassable here workingState.PlayerPosition with
            | Some (_ :: step :: _) when
                (match Map.tryFind step workingState.CurrentFloor.Entities with
                 | Some ent -> not ent.BlocksMovement
                 | None -> true) ->
              let moved = { enemy with ChaseCharge = charge - 1.0 }
              let vacated = { workingState.CurrentFloor with Entities = Map.remove here workingState.CurrentFloor.Entities }
              workingState <- { workingState with CurrentFloor = { vacated with Entities = Map.add step (EntityEnemy moved) vacated.Entities } }
              RuntimeLog.write (sprintf "enemy %s: %A -> %A (pursuit)" enemy.Id here step)
              charge <- charge - 1.0
              stepsLeft <- stepsLeft - 1
              here <- step
            | _ ->
              blocked <- true
              RuntimeLog.write (sprintf "enemy %s: holds at %A (no path)" enemy.Id here)

          if here = pt then
            // Nowhere to step: keep what it has banked for the turn the way clears.
            workingState <- updateEnemy { enemy with ChaseCharge = charge } workingState
            if not blocked then
              RuntimeLog.write (sprintf "enemy %s: holds at %A (charging %.2f)" enemy.Id pt charge)
      | None ->
        RuntimeLog.write (sprintf "enemy %s: position not found" enemy.Id)

    let adjacent = adjacentEnemies workingState

    let events =
      if List.isEmpty adjacent then []
      else [ TowerEvent.GroupCombatTriggered adjacent ]

    workingState, events

  /// One enemy phase taken from inside an ongoing group fight: pursuers keep closing and whoever
  /// ends up adjacent joins that fight. The events are discarded on purpose -- a
  /// GroupCombatTriggered raised here would re-enter the fight handler from within itself. Ids in
  /// alreadyEngaged are already in the fight and are not reported twice; the returned enemies are
  /// level-clamped by adjacentEnemies, exactly like the opening engagement.
  let reinforceFromPursuit (state: TowerRunState) (alreadyEngaged: string list) : TowerRunState * TowerEnemy list =
    let advanced, _ = stepEnemies state

    let joined =
      adjacentEnemies advanced
      |> List.filter (fun e -> not (List.contains e.Id alreadyEngaged))

    advanced, joined

  /// Completes an active quest on the floor and awards its reward. The payout is read from the
  /// quest's own `Reward` record, so the completion card cannot promise one thing and pay another.
  let completeQuest (questId: string) (state: TowerRunState) : TowerRunState =
    let quest = state.CurrentFloor.ActiveQuests |> List.tryFind (fun q -> q.Id = questId)

    let questTitle =
      quest |> Option.map (fun q -> q.Title) |> Option.defaultValue questId

    let updatedQuests =
      state.CurrentFloor.ActiveQuests
      |> List.map (fun q -> if q.Id = questId then { q with IsCompleted = true } else q)

    let updatedFloor =
      { state.CurrentFloor with
          ActiveQuests = updatedQuests }
      |> (fun f -> f.UnlockStairway())

    let rewardedPlayer, souls, keys =
      match quest with
      | None -> state.Player, state.Souls, state.CollectedKeys
      | Some q ->
        let restored =
          if q.Reward.FullVitality then
            { state.Player with
                Health = Pool.Create state.Player.Health.Maximum
                Morale = Pool.Create state.Player.Morale.Maximum }
          else
            state.Player

        let player =
          { restored with
              Health = restored.Health.ApplyDelta q.Reward.Health
              Morale = restored.Morale.ApplyDelta q.Reward.Morale }

        let keys =
          match q.Reward.KeyId with
          | Some keyId -> state.CollectedKeys.Add keyId
          | None -> state.CollectedKeys

        player, state.Souls + q.Reward.Souls, keys

    { state with
        CurrentFloor = updatedFloor
        Player = rewardedPlayer
        Souls = souls
        CollectedKeys = keys
        CompletedQuests = state.CompletedQuests.Add questId
        MessageLog =
          (match quest with
           | Some q ->
             sprintf "Quest complete: %s — %s The Ascension Portal is unlocked." questTitle q.RewardDescription
           | None -> sprintf "Quest complete: %s — the Ascension Portal is unlocked." questTitle)
          :: state.MessageLog }

  /// The level spread a floor is allowed to field, as `(mobCap, bossOffset)` measured from the player's
  /// level. The story's first floor is the tutorial: its mobs answer at the protagonist's own level,
  /// because a level-1 protagonist has nothing to grow into yet and the intro has no way out. The
  /// guardian keeps its `+10`, which is what makes it the strongest thing on the floor.
  /// Exported because the minimap rewind re-seats pristine mobs and must land them on the same levels.
  let floorScalingFor (isStory: bool) (floorNumber: int) : int * int =
    if isStory && floorNumber <= 1 then 0, 10 else 2 + floorNumber / 2, 10

  let rescaleFloorEnemiesWith (depthCap: int) (bossOffset: int) (player: Combatant) (floor: TowerFloor) : TowerFloor =
    let targetLevel (e: TowerEnemy) =
      if e.IsBoss then player.Level + bossOffset
      else
        let salt = e.Id |> Seq.sumBy int
        player.Level + min (salt % 6) depthCap

    let rescaled (e: TowerEnemy) =
      if e.IsDefeated then e
      else
        let level = max 1 (targetLevel e)
        { e with
            Combatant =
              { TierFactory.scaleCombatantToLevel level e.Combatant with
                  Name = sprintf "Lv.%d %s" level e.Name } }

    { floor with
        Entities =
          floor.Entities
          |> Map.map (fun _ ent ->
            match ent with
            | EntityEnemy e -> EntityEnemy(rescaled e)
            | other -> other) }

  /// Enemy levels track the protagonist. A floor's mobs land at the player's level up to five above,
  /// and its boss ten above, measured once at floor entry: the authored bestiary levels follow the
  /// biome, not the run, so without this a thorough player walked into floors they had outgrown and a
  /// hurried one into floors that outclassed them. The spread is derived from the enemy id, so the
  /// same floor always fields the same mix.
  /// The spread is capped by depth: floor 1 answers at +0..+2, floor 4 at +3, and from floor 8 the
  /// full +0..+5 is live. Uncapped, a level-1 player opened the tower against four enemies of up to
  /// level 6 (--run-sim: 55% floor-1 clear, 33% by floor 3) with no room to grow into it.
  /// The tower curve; story floors go through `floorScalingFor` instead.
  let rescaleFloorEnemies (player: Combatant) (floor: TowerFloor) : TowerFloor =
    let depthCap, bossOffset = floorScalingFor false floor.FloorNumber
    rescaleFloorEnemiesWith depthCap bossOffset player floor

  /// Initializes a new Tower run session starting at a given floor with specified story mode
  let initSessionWithMode (player: Combatant) (seed: int) (startFloor: int) (isStory: bool) : TowerRunState =
    let depthCap, bossOffset = floorScalingFor isStory startFloor

    let floor =
      TowerGenerator.generateFloorWithMode seed startFloor isStory
      |> rescaleFloorEnemiesWith depthCap bossOffset player
    let initial =
      { Player = player
        CurrentFloor = floor
        PlayerPosition = floor.SpawnLocation
        Facing = Some Direction.North
        Locomotion = entryLocomotion
        CollectedKeys = Set.empty
        CompletedQuests = Set.empty
        InventoryItems = player.EquippedItems
        MessageLog = [ sprintf "Ascended to Floor %d: %s." startFloor floor.Theme.Name ]
        Seed = seed
        Souls = 0
        Trophies = []
        Consumables = []
        UnidentifiedConsumables = Set.empty
        DiscoveredEchoes = Set.empty
        IsStoryMode = isStory
        SafeStepsSinceHone = 0 }

    updateFov initial

  /// Initializes a new Tower run session starting at a given floor
  let initSession (player: Combatant) (seed: int) (startFloor: int) : TowerRunState =
    initSessionWithMode player seed startFloor false

  /// Advances to the next floor via the Stairway Door
  let ascendFloor (state: TowerRunState) : TowerRunState =
    let nextFloorNum = state.CurrentFloor.FloorNumber + 1
    let depthCap, bossOffset = floorScalingFor state.IsStoryMode nextFloorNum

    let nextFloor =
      TowerGenerator.generateFloorWithMode state.Seed nextFloorNum state.IsStoryMode
      |> rescaleFloorEnemiesWith depthCap bossOffset state.Player
    let nextState =
      { state with
          Player = { Combatant.clearStacks state.Player with Meters = StatusMeters.Zero }
          CurrentFloor = nextFloor
          PlayerPosition = nextFloor.SpawnLocation
          Facing = Some Direction.North
          Locomotion = entryLocomotion
          MessageLog = sprintf "Ascended to Floor %d: %s!" nextFloorNum nextFloor.Theme.Name :: state.MessageLog }

    RuntimeLog.section (sprintf "FLOOR %d" nextFloorNum)
    RuntimeLog.write (
      sprintf "floor transition: %d -> %d (seed=%d theme=%s size=%dx%d spawn=%A)"
        state.CurrentFloor.FloorNumber nextFloorNum state.Seed nextFloor.Theme.Name
        nextFloor.Width nextFloor.Height nextFloor.SpawnLocation)

    updateFov nextState

  /// Resolves a single turn of player movement or interaction
  let stepPlayer (dir: Direction) (state: TowerRunState) : TowerRunState * TowerEvent list =
    let targetPt = state.PlayerPosition + Direction.toDelta dir
    let mutable events = []
    let mutable nextState =
      { state with
          Facing = Some dir
          Locomotion = focusOnMove state.Locomotion }

    match Map.tryFind targetPt state.CurrentFloor.Tiles with
    | None ->
      // Out of bounds
      nextState, events

    | Some tile when not tile.IsWalkable ->
      // Blocked by Chasm or Pillar
      let obstacleName = match tile with Chasm -> "an endless chasm" | Pillar -> "a stone pillar" | _ -> "an obstacle"
      let logMsg = sprintf "Path blocked by %s." obstacleName
      { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

    | Some tile ->
      // Check for entities occupying the target point
      match Map.tryFind targetPt state.CurrentFloor.Entities with
      | Some (EntityEnemy enemy) when not enemy.IsDefeated ->
        let engaged = engagedAtPlayerLevel state enemy
        events <- [ TowerEvent.CombatTriggered engaged ]
        let logMsg = sprintf "Encountered %s! Initiating duel..." enemy.Name
        { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

      | Some (EntityNpc npc) ->
        // Check if quest can be completed (e.g. at least one enemy was defeated on this floor)
        let defeatedEnemies =
          state.CurrentFloor.Entities
          |> Map.values
          |> Seq.filter (function EntityEnemy e -> e.IsDefeated | _ -> false)
          |> Seq.length
        // A quest may pin its completion to a named enemy (e.g. the tutorial Bandit Leader).
        let requiredEnemyDefeated =
          match npc.Quest |> Option.bind (fun q -> q.RequiredEnemyName) with
          | None -> true
          | Some requiredName ->
            state.CurrentFloor.Entities
            |> Map.exists (fun _ ent ->
              match ent with
              | EntityEnemy e -> e.IsDefeated && e.Name = requiredName
              | _ -> false)
        let questAlreadyCompleted =
          npc.Quest |> Option.exists (fun q -> state.CompletedQuests.Contains q.Id)
        if npc.Quest.IsSome && not npc.HasGivenReward && defeatedEnemies > 0 && requiredEnemyDefeated && not questAlreadyCompleted then
          let q = npc.Quest.Value
          let updatedNpc = { npc with HasGivenReward = true; Quest = Some { q with IsCompleted = true } }
          let updatedEntities = Map.add targetPt (EntityNpc updatedNpc) state.CurrentFloor.Entities
          let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
          let completedState = completeQuest q.Id { nextState with CurrentFloor = updatedFloor }
          events <- [ TowerEvent.NpcInteracted(updatedNpc, true) ]
          completedState, events
        else
          events <- [ TowerEvent.NpcInteracted(npc, false) ]
          let logMsg = sprintf "Spoke with %s: \"%s\"" npc.Name (List.head npc.Dialogue)
          { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

      | Some (EntityChest chest) when not chest.IsOpen ->
        let updatedChest = { chest with IsOpen = true }
        let updatedEntities = Map.add targetPt (EntityChest updatedChest) state.CurrentFloor.Entities
        let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }

        let mutable newKeys = state.CollectedKeys
        let mutable newItems = state.InventoryItems
        let mutable newPlayer = state.Player
        let mutable rewardMsg = "The chest is empty."

        match chest.LootKeyId with
        | Some kId ->
          newKeys <- newKeys.Add kId
          rewardMsg <- sprintf "Obtained key: %s!" kId
        | None -> ()

        match chest.ItemReward with
        | Some item ->
          newItems <- item :: newItems
          newPlayer <- Combatant.applyItemBonus item newPlayer
          rewardMsg <- sprintf "Obtained relic: %s!" item.Name
        | None -> ()

        events <- [ TowerEvent.ChestOpened(chest, chest.ItemReward, chest.LootKeyId) ]
        let updatedState =
          { nextState with
              Player = newPlayer
              CurrentFloor = updatedFloor
              CollectedKeys = newKeys
              InventoryItems = newItems
              MessageLog = rewardMsg :: nextState.MessageLog }

        updatedState, events

      // A shrine is never consumed: the tile stays on the map and blesses every visit, reveal and
      // rare-material work included.
      | Some (EntityShrine shrine) ->
        let blessedState, blessingSummary = shrineBlessing shrine targetPt nextState
        let logMsg = sprintf "Communed with %s: %s" shrine.Name shrine.BlessingDescription

        events <- [ TowerEvent.ShrineActivated shrine ]

        { blessedState with
            MessageLog = blessingSummary @ (logMsg :: nextState.MessageLog) },
        events

      // --- Dynamic Encounters (ADR 0004 Phase 3) ---
      | Some (EntityEncounter enc) ->
        match enc with
        | SacrificialAltar altar when not altar.IsUsed ->
          events <- [ TowerEvent.AltarEncountered(altar, targetPt) ]
          let logMsg = sprintf "Approached %s. [Cost: %s -> Reward: %s]" altar.Name altar.Cost.Description (altar.Reward.ResolvedFor state.Player.Plane).Description
          { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

        | SacrificialAltar _ ->
          let logMsg = "The sacrificial altar is cold and spent."
          { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

        | WanderingTrader merchant ->
          events <- [ TowerEvent.TraderEncountered(merchant, targetPt) ]
          let logMsg = sprintf "Spoke with %s (%s): \"%s\"" merchant.Name merchant.Title (List.head merchant.Dialogue)
          { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

        | TreasureVault vault when not vault.IsOpen ->
          events <- [ TowerEvent.VaultEncountered(vault, targetPt) ]
          let puzzleDesc =
            match vault.Puzzle with
            | KeyholeLock (_, name, hint) -> sprintf "Keyhole sealed ([%s] — %s)" name hint
            | StatCheck (stat, req, _) -> sprintf "Sealed by %A lock (Requires %d %A)" stat req stat
            | MemoryCipher (riddle, _) -> sprintf "Inscribed cipher: \"%s\"" riddle
          let logMsg = sprintf "%s bars the way: %s" vault.Name puzzleDesc
          { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

        | TreasureVault _ ->
          let logMsg = "The vault chamber stands open and emptied."
          { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

        | AmbushLair ambush when not ambush.IsTriggered ->
          // Ambush springs!
          let updatedAmbush = { ambush with IsTriggered = true }
          let mutable updatedEntities = Map.add targetPt (EntityEncounter (AmbushLair updatedAmbush)) state.CurrentFloor.Entities

          // Spawn enemies on open adjacent walkable tiles
          let adjacentDeltas = [ (0, 1); (1, 0); (0, -1); (-1, 0); (1, 1); (-1, -1); (1, -1); (-1, 1) ]
          let mutable spawnCandidates =
            adjacentDeltas
            |> List.map (fun (dx, dy) -> { X = targetPt.X + dx; Y = targetPt.Y + dy })
            |> List.filter (fun p ->
              match Map.tryFind p state.CurrentFloor.Tiles with
              | Some t when t.IsWalkable -> not (Map.containsKey p updatedEntities) && p <> state.PlayerPosition
              | _ -> false)

          let mutable spawnedEnemies = []
          // 1. Leader
          let leaderCombatant =
            Bestiary.createMonster ambush.Pack.Leader
            |> fun c -> TierFactory.clampEngagedToPlayerLevel c state.Player
          let leaderEnemy =
            { Id = sprintf "%s_leader" ambush.Id
              Name = ambush.Pack.Leader.Name
              Combatant = leaderCombatant
              DropsKeyId = None
              IsDefeated = false
              ChaseCharge = 0.0 }
          match spawnCandidates with
          | spawnPos :: rest ->
            updatedEntities <- Map.add spawnPos (EntityEnemy leaderEnemy) updatedEntities
            spawnedEnemies <- leaderEnemy :: spawnedEnemies
            spawnCandidates <- rest
          | [] -> ()

          // 2. Minions
          for i, minionTemplate in List.indexed ambush.Pack.Minions do
            match spawnCandidates with
            | spawnPos :: rest ->
              let minionCombatant =
                Bestiary.createMonster minionTemplate
                |> fun c -> TierFactory.clampEngagedToPlayerLevel c state.Player
              let minionEnemy =
                { Id = sprintf "%s_minion_%d" ambush.Id i
                  Name = minionTemplate.Name
                  Combatant = minionCombatant
                  DropsKeyId = None
                  IsDefeated = false
                  ChaseCharge = 0.0 }
              updatedEntities <- Map.add spawnPos (EntityEnemy minionEnemy) updatedEntities
              spawnedEnemies <- minionEnemy :: spawnedEnemies
              spawnCandidates <- rest
            | [] -> ()

          let logMsg = sprintf "AMBUSH TRIGGERED! %s %s" ambush.Name ambush.TriggerDescription
          events <- [ TowerEvent.AmbushTriggered(ambush, spawnedEnemies); TowerEvent.Moved targetPt ]
          let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
          let movedState =
            { nextState with
                CurrentFloor = updatedFloor
                PlayerPosition = targetPt
                MessageLog = logMsg :: nextState.MessageLog }
          updateFov movedState, events

        | AmbushLair _ ->
          // Ambush already cleared / triggered - just normal movement
          events <- [ TowerEvent.Moved targetPt ]
          let movedState = { nextState with PlayerPosition = targetPt }
          updateFov movedState, events

        | MechanicalTrapGauntlet trap when not trap.IsDisarmed ->
          // Check player disarm stat vs threshold
          let playerStatVal = state.Player.Stats.Get trap.DisarmStat
          if playerStatVal >= trap.DisarmThreshold then
            // Disarmed!
            let updatedTrap = { trap with IsDisarmed = true }
            let updatedEntities = Map.add targetPt (EntityEncounter (MechanicalTrapGauntlet updatedTrap)) state.CurrentFloor.Entities
            let logMsg = sprintf "Spotted and safely disarmed %s! (%A check: %d >= %d)" trap.Name trap.DisarmStat playerStatVal trap.DisarmThreshold
            events <- [ TowerEvent.TrapDisarmed(trap, logMsg); TowerEvent.Moved targetPt ]
            let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
            let movedState =
              { nextState with
                  CurrentFloor = updatedFloor
                  PlayerPosition = targetPt
                  MessageLog = logMsg :: nextState.MessageLog }
            updateFov movedState, events
          else
            // Trap sprung!
            let updatedTrap = { trap with IsTriggered = true }
            let updatedEntities = Map.add targetPt (EntityEncounter (MechanicalTrapGauntlet updatedTrap)) state.CurrentFloor.Entities
            let effectMsg, damagedPlayer =
              match trap.TrapType with
              | FloorSpikes dmg ->
                sprintf "Punctured by hidden spikes for -%d Health!" dmg,
                { state.Player with Health = state.Player.Health.ApplyDelta -dmg }
              | DartVolley (shred, dmg) ->
                sprintf "Sprayed by poison darts for -%d Armor and -%d Health!" shred dmg,
                { state.Player with Armor = state.Player.Armor.Shred shred; Health = state.Player.Health.ApplyDelta -dmg }
              | HallucinogenicGas (mor, exh) ->
                sprintf "Engulfed in gas! -%d Morale, +%d Exhaustion!" mor exh,
                { state.Player with Morale = state.Player.Morale.ApplyDelta -mor; Meters = { state.Player.Meters with Exhaustion = state.Player.Meters.Exhaustion + exh } }
              | ArcaneDischarge dmg ->
                sprintf "Blasted by arcane discharge for -%d Health!" dmg,
                { state.Player with Health = state.Player.Health.ApplyDelta -dmg }

            let logMsg = sprintf "TRAP SPRUNG: %s! %s" trap.Name effectMsg
            events <- [ TowerEvent.TrapTriggered(trap, logMsg); TowerEvent.Moved targetPt ]
            let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
            let movedState =
              { nextState with
                  Player = damagedPlayer
                  CurrentFloor = updatedFloor
                  PlayerPosition = targetPt
                  MessageLog = logMsg :: nextState.MessageLog }
            updateFov movedState, events

        | MechanicalTrapGauntlet _ ->
          // Already disarmed/triggered
          events <- [ TowerEvent.Moved targetPt ]
          let movedState = { nextState with PlayerPosition = targetPt }
          updateFov movedState, events

        | MemoryEchoFragment echo when not echo.IsCommuned ->
          let updatedEcho = { echo with IsCommuned = true }
          let updatedEntities = Map.add targetPt (EntityEncounter (MemoryEchoFragment updatedEcho)) state.CurrentFloor.Entities
          let restoredPlayer = { state.Player with Morale = state.Player.Morale.ApplyDelta echo.MoraleRecovery }
          let logMsg = sprintf "󰔱 Discovered Memory Echo: [%s]. %s (+%d Morale)" echo.Title echo.SensoryDetail echo.MoraleRecovery
          events <- [ TowerEvent.EchoDiscovered echo; TowerEvent.Moved targetPt ]
          let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
          let movedState =
            { nextState with
                Player = restoredPlayer
                CurrentFloor = updatedFloor
                PlayerPosition = targetPt
                DiscoveredEchoes = state.DiscoveredEchoes.Add echo.Id
                MessageLog = logMsg :: nextState.MessageLog }
          updateFov movedState, events

        | MemoryEchoFragment _ ->
          // Already communed
          events <- [ TowerEvent.Moved targetPt ]
          let movedState = { nextState with PlayerPosition = targetPt }
          updateFov movedState, events

      | _ ->
        // No blocking entity: move to tile
        match tile with
        | StairwayPortal doorState ->
          match doorState with
          | Open ->
            events <- [ TowerEvent.StairwayAscended (state.CurrentFloor.FloorNumber + 1) ]
            let ascended = ascendFloor nextState
            ascended, events

          | LockedByKey(keyId, keyName, hint) ->
            if state.CollectedKeys.Contains keyId then
              // Unlock the door
              let unlockedFloor = state.CurrentFloor.UnlockStairway()
              let logMsg = sprintf "Unlocked the Ascension Door with the %s!" keyName
              events <- [ TowerEvent.DoorLockedMessage logMsg ]
              { nextState with
                  CurrentFloor = unlockedFloor
                  MessageLog = logMsg :: nextState.MessageLog }, events
            else
              let logMsg = sprintf "The Ascension Portal is sealed. Requires: [%s]. (%s)" keyName hint
              events <- [ TowerEvent.DoorLockedMessage logMsg ]
              { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

          | LockedByQuest(questId, questTitle, req) ->
            if state.CompletedQuests.Contains questId then
              let unlockedFloor = state.CurrentFloor.UnlockStairway()
              let logMsg = sprintf "The trial is complete. The Ascension Portal swings open!"
              events <- [ TowerEvent.DoorLockedMessage logMsg ]
              { nextState with
                  CurrentFloor = unlockedFloor
                  MessageLog = logMsg :: nextState.MessageLog }, events
            else
              let logMsg = sprintf "The Ascension Portal is barred by decree: [%s]. (%s)" questTitle req
              events <- [ TowerEvent.DoorLockedMessage logMsg ]
              { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

        | Hazard hazard ->
          let hazardMsg, damagedPlayer =
            match hazard with
            | LavaRift ->
              "Stepped on a molten lava rift! Sizzling heat singes your armor.",
              { state.Player with Health = state.Player.Health.ApplyDelta -15 }
            | AcidSlag ->
              "Stepped on corrosive slag! Armor integrity degrades.",
              { state.Player with Armor = state.Player.Armor.Shred 15 }
            | DeepCurrent ->
              "Waded through deep water currents. Limbs feel heavy.",
              { state.Player with Meters = { state.Player.Meters with Exhaustion = state.Player.Meters.Exhaustion + 15 } }
            | CalmingSpores ->
              "Inhaled calming spores. Accumulated recklessness fades.",
              { state.Player with Meters = { state.Player.Meters with Recklessness = Meter.Zero } }

          events <- [ TowerEvent.HazardStepped(hazard, hazardMsg); TowerEvent.Moved targetPt ]
          let movedState =
            { nextState with
                Player = damagedPlayer
                PlayerPosition = targetPt
                MessageLog = hazardMsg :: nextState.MessageLog }

          updateFov movedState, events

        | _ ->
          // Normal walkable floor or archway
          events <- [ TowerEvent.Moved targetPt ]
          let movedState = { nextState with PlayerPosition = targetPt }
          updateFov movedState, events

  /// Resolves an altar sacrifice: pays cost, grants reward, marks altar used
  let applyAltarSacrifice (altarPt: Point) (state: TowerRunState) : Result<TowerRunState, string> =
    match Map.tryFind altarPt state.CurrentFloor.Entities with
    | Some (EntityEncounter (SacrificialAltar altar)) when not altar.IsUsed ->
      // 1. Check & Apply Cost. The amount is a percentage of the maximum, read off the player at
      // interaction time: a flat toll against thousand-point pools was free, and could never bite past
      // the eighty percent rest cap.
      let percentOf (maximum: int) (pct: int) =
        int (Math.Round(float maximum * float pct / 100.0, MidpointRounding.AwayFromZero))

      let costPlayer =
        match altar.Cost with
        | SacrificeHealth pct ->
          let cost = percentOf state.Player.Health.Maximum pct
          // A sacrifice wounds but never kills: at least one point of Health is left standing.
          { state.Player with
              Health =
                { state.Player.Health with
                    Current = Math.Max(1, state.Player.Health.Current - cost) } }
        | SacrificeMorale pct ->
          { state.Player with
              Morale = state.Player.Morale.ApplyDelta -(percentOf state.Player.Morale.Maximum pct) }
        | ShredArmor pct ->
          { state.Player with
              Armor = state.Player.Armor.Shred(percentOf state.Player.Armor.Max pct) }

      // 2. Apply Reward. Every blessing is a percentage of the protagonist's own values, read here
      // for the same reason the toll is: it has to keep pace as attributes and pools run to thousands.
      let mutable rewardedPlayer = costPlayer
      let mutable newKeys = state.CollectedKeys
      let mutable newItems = state.InventoryItems

      // What the altar hands over is resolved against the bearer's plane, so a Fighter is never
      // granted a mental attribute and a Mage never a physical one.
      let reward = altar.Reward.ResolvedFor state.Player.Plane

      match reward with
      | StatBuff (stats, bonus) ->
        // Flat points, not a percentage: the pair is resolved against the bearer's plane, so the
        // number has to mean the same thing for a level-1 delver and a level-185 guardian.
        let updatedStats = stats |> List.fold (fun (s: StatBlock) stat -> s.Modify(stat, bonus)) costPlayer.Stats
        rewardedPlayer <- { costPlayer with Stats = updatedStats }
      | PairBuff _ ->
        // Unreachable: `ResolvedFor` above turns every authored pair buff into concrete stats.
        rewardedPlayer <- costPlayer
      | VitalitySurge (hpPct, morPct) ->
        // ApplyDelta clamps at the pool maximum, so a surge gifted at full health is not banked.
        rewardedPlayer <-
          { costPlayer with
              Health = costPlayer.Health.ApplyDelta (percentOf costPlayer.Health.Maximum hpPct)
              Morale = costPlayer.Morale.ApplyDelta (percentOf costPlayer.Morale.Maximum morPct) }
      | KeyReward (kId, _) ->
        newKeys <- newKeys.Add kId
      | RelicReward item ->
        // No scaling here. The relic's bonuses are already percentages and `applyItemBonus` resolves
        // them against the bearer, exactly as it does for every other piece of equipment.
        newItems <- item :: newItems
        rewardedPlayer <- Combatant.applyItemBonus item rewardedPlayer

      let updatedAltar = { altar with IsUsed = true }
      let updatedEntities = Map.add altarPt (EntityEncounter (SacrificialAltar updatedAltar)) state.CurrentFloor.Entities
      let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
      let logMsg = sprintf "Communed with %s: Paid [%s], Received [%s]!" altar.Name altar.Cost.Description reward.Description

      Ok { state with
             Player = rewardedPlayer
             CurrentFloor = updatedFloor
             CollectedKeys = newKeys
             InventoryItems = newItems
             MessageLog = logMsg :: state.MessageLog }
    | Some (EntityEncounter (SacrificialAltar _)) ->
      Error "This altar has already been used."
    | _ ->
      Error "No active altar found at this position."

  /// Purchases a ware from a spectral merchant using souls and optional alchemical trophies
  let buyFromTrader (merchantPt: Point) (wareIndex: int) (state: TowerRunState) : Result<TowerRunState, string> =
    match Map.tryFind merchantPt state.CurrentFloor.Entities with
    | Some (EntityEncounter (WanderingTrader merchant)) ->
      if wareIndex < 0 || wareIndex >= merchant.Wares.Length then
        Error "Invalid merchandise index."
      else
        let ware = merchant.Wares.[wareIndex]
        if ware.IsPurchased then
          Error "This item has already been purchased."
        elif state.Souls < ware.CostSouls then
          Error (sprintf "Insufficient Souls! Requires %d Souls (you have %d)." ware.CostSouls state.Souls)
        else
          // Check trophy if required
          let hasRequiredTrophy, updatedTrophies =
            match ware.RequiredTrophy with
            | None -> true, state.Trophies
            | Some (reqTrophy, countNeeded) ->
              match List.tryFind (fun (t: AlchemicalTrophy, cnt) -> t.Id = reqTrophy.Id && cnt >= countNeeded) state.Trophies with
              | Some (t, cnt) ->
                let remaining =
                  state.Trophies
                  |> List.choose (fun (curT, curCnt) ->
                    if curT.Id = reqTrophy.Id then
                      let left = curCnt - countNeeded
                      if left > 0 then Some (curT, left) else None
                    else Some (curT, curCnt))
                true, remaining
              | None -> false, state.Trophies

          if not hasRequiredTrophy then
            let trophyName = match ware.RequiredTrophy with Some (t, c) -> sprintf "%d %s" c t.Name | None -> ""
            Error (sprintf "Missing required alchemical trophy: %s!" trophyName)
          else
            let updatedWare = { ware with IsPurchased = true }
            let updatedWares =
              merchant.Wares
              |> List.mapi (fun i w -> if i = wareIndex then updatedWare else w)
            let updatedMerchant = { merchant with Wares = updatedWares; HasTraded = true }
            let updatedEntities = Map.add merchantPt (EntityEncounter (WanderingTrader updatedMerchant)) state.CurrentFloor.Entities
            let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
            let logMsg = sprintf "Purchased %s from %s for %d Souls!" ware.Item.Name merchant.Name ware.CostSouls

            Ok { state with
                   CurrentFloor = updatedFloor
                   Souls = state.Souls - ware.CostSouls
                   Trophies = updatedTrophies
                   Player = Combatant.applyItemBonus ware.Item state.Player
                   InventoryItems = ware.Item :: state.InventoryItems
                   MessageLog = logMsg :: state.MessageLog }
    | _ -> Error "No merchant found at this location."

  /// Attempts to unlock and loot a sealed treasure vault
  let attemptOpenVault (vaultPt: Point) (state: TowerRunState) : Result<TowerRunState, string> =
    match Map.tryFind vaultPt state.CurrentFloor.Entities with
    | Some (EntityEncounter (TreasureVault vault)) ->
      if vault.IsOpen then
        Error "This vault has already been opened."
      else
        let canOpen, reason =
          match vault.Puzzle with
          | KeyholeLock (kId, kName, hint) ->
            if state.CollectedKeys.Contains kId then true, ""
            else false, sprintf "Sealed by [%s]! (%s)" kName hint
          | StatCheck (stat, reqVal, desc) ->
            let currentVal = state.Player.Stats.Get stat
            if currentVal >= reqVal then true, ""
            else false, sprintf "Requires %d %A (current: %d). %s" reqVal stat currentVal desc
          | MemoryCipher _ ->
            // Memory cipher unlocks with contemplation or automatic trial
            true, ""

        if not canOpen then
          Error reason
        else
          let updatedVault = { vault with IsOpen = true }
          let updatedEntities = Map.add vaultPt (EntityEncounter (TreasureVault updatedVault)) state.CurrentFloor.Entities
          let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
          let logMsg = sprintf "Unlocked %s! Recovered +%d Souls and %d rare relic(s)!" vault.Name vault.BonusSouls vault.Relics.Length

          Ok { state with
                 CurrentFloor = updatedFloor
                 Souls = state.Souls + vault.BonusSouls
                 Player = vault.Relics |> List.fold (fun p it -> Combatant.applyItemBonus it p) state.Player
                 InventoryItems = vault.Relics @ state.InventoryItems
                 MessageLog = logMsg :: state.MessageLog }
    | _ -> Error "No vault found at this location."

  /// Applies combat experience to the run's player, rescaling stats on level-up.
  /// Returns the updated player and chronicle notification lines (newest first).
  let applyExperience (player: Combatant) (xp: int) : Combatant * string list =
    let previousLevel = player.Level
    let profile, levelsGained = player.Progression.GrantXP xp
    let withProfile = { player with Progression = profile }

    if levelsGained = 0 then
      withProfile,
      [ sprintf "Gained %d XP. (%d / %d toward Level %d)" xp profile.CurrentXP profile.ExperienceToNext (profile.Level + 1) ]
    else
      // rescaleToLevel treats c.Progression.Level as the old level, so pass the
      // pre-level-up player, then reattach the XP profile (which carries CurrentXP).
      let leveled = TierFactory.rescaleToLevel player profile.Level

      let leveled =
        { leveled with
            Progression =
              { profile with
                  PrimaryStat = leveled.Progression.PrimaryStat } }
      let previousTier = ProgressionScale.levelToTier previousLevel
      let currentTier = ProgressionScale.levelToTier profile.Level

      let rankLine =
        if currentTier <> previousTier then
          sprintf " You have attained the rank of %s!" (currentTier.ToString())
        else
          ""

      leveled,
      [ sprintf "LEVEL UP! You are now Level %d (from %d). All attributes, Health, Morale, and Armor scale to your new mastery.%s"
          profile.Level previousLevel rankLine ]

  /// Marks a defeated enemy on the floor and awards its dropped key, souls, and alchemical trophies
  let resolveEnemyDefeat (enemyId: string) (state: TowerRunState) : TowerRunState =
    let mutable droppedKeyOpt = None
    let mutable defeatedEnemyOpt = None
    let updatedEntities =
      state.CurrentFloor.Entities
      |> Map.map (fun _ ent ->
        match ent with
        | EntityEnemy e when e.Id = enemyId ->
          droppedKeyOpt <- e.DropsKeyId
          defeatedEnemyOpt <- Some e
          EntityEnemy { e with IsDefeated = true }
        | other -> other)

    let mutable newKeys = state.CollectedKeys
    let mutable newSouls = state.Souls
    let mutable newTrophies = state.Trophies
    let mutable newConsumables = state.Consumables
    let mutable newUnidentified = state.UnidentifiedConsumables
    let mutable newXp = 0
    let mutable logMessages = state.MessageLog

    match defeatedEnemyOpt with
    | Some e ->
      let baseXp = e.Combatant.Level * 10 + 25
      // The stage guardian carries the keystone; beating it opens the gate and hands the protagonist
      // the strength the next biome assumes. Ordinary kills keep the normal per-kill value, except
      // on the tutorial floor, where every kill accelerates leveling.
      //
      // The top-up is measured from the level the PREVIOUS stage handed over, so optional kills are
      // never swallowed: a player who clears every fight arrives at the gate carrying that many levels
      // of lead, and keeps it for the rest of the ascent.
      let stageEntryLevel =
        let floorNum = state.CurrentFloor.FloorNumber
        if floorNum <= 1 then 1 else ProgressionScale.storyGuardianLevel (floorNum - 1)
      let carriedLead =
        // Capped at three levels: the tutorial pays 100x the normal per-kill XP, so an uncapped lead
        // would hand a completionist twenty levels by stage two and flatten the rest of the ascent.
        min 3 (max 0 (state.Player.Level - stageEntryLevel))
      let storyGateLevel =
        if state.IsStoryMode && state.CurrentFloor.FloorNumber <= 7 && e.DropsKeyId.IsSome then
          Some (ProgressionScale.storyGuardianLevel state.CurrentFloor.FloorNumber + carriedLead)
        else
          None

      match storyGateLevel with
      | Some targetLevel ->
        let profile = state.Player.Progression
        let mutable needed = -profile.CurrentXP
        for lvl in profile.Level .. targetLevel - 1 do
          needed <- needed + ProgressionProfile.ExperienceForNextLevel lvl
        newXp <- baseXp + max 0 needed
      | None when state.IsStoryMode && state.CurrentFloor.FloorNumber = 1 ->
        newXp <- baseXp + 2500
      | None ->
        newXp <- baseXp
      // Look up monster loot in Bestiary
      let monsterTemplateOpt =
        Bestiary.allMonsters
        |> List.tryFind (fun m -> m.Name.Equals(e.Name, StringComparison.OrdinalIgnoreCase))

      match monsterTemplateOpt with
      | Some template ->
        let rng = Random(state.Seed + state.CurrentFloor.FloorNumber * 31 + newSouls)
        let roll = rng.Next(template.Loot.MinSouls, template.Loot.MaxSouls + 1)
        let soulGain = template.Loot.SoulsFor roll
        newSouls <- newSouls + soulGain
        let soulMsg = sprintf "Harvested %d Souls from %s!" soulGain e.Name

        let trophyMsg =
          match template.Loot.Trophy with
          | Some trophy when rng.Next(100) < template.Loot.TrophyDropChancePct ->
            let updatedList =
              match List.tryFind (fun (t: AlchemicalTrophy, _) -> t.Id = trophy.Id) newTrophies with
              | Some (_, cnt) ->
                newTrophies |> List.map (fun (t, c) -> if t.Id = trophy.Id then (t, c + 1) else (t, c))
              | None ->
                (trophy, 1) :: newTrophies
            newTrophies <- updatedList
            sprintf " Acquired rare trophy: [%s]!" trophy.Name
          | _ -> ""

        let consumableMsg =
          match Consumables.rollDrop (rng.Next 100) template.Loot.Category with
          | Some kind ->
            newConsumables <- Consumables.add kind newConsumables
            newUnidentified <- newUnidentified.Add kind
            sprintf " Salvaged a %s." kind.Name
          | None -> ""

        logMessages <- (soulMsg + trophyMsg + consumableMsg) :: logMessages
      | None ->
        // Humanoid opposition built from a class (bandits, scouts) carries no bestiary entry, so its
        // loot category is not authored anywhere: it is always Humanoid.
        let rng = Random(state.Seed + state.CurrentFloor.FloorNumber * 31 + newSouls)
        let standardSouls = e.Combatant.Level * 15
        newSouls <- newSouls + standardSouls

        let consumableMsg =
          match Consumables.rollDrop (rng.Next 100) LootCategory.Humanoid with
          | Some kind ->
            newConsumables <- Consumables.add kind newConsumables
            newUnidentified <- newUnidentified.Add kind
            sprintf " Salvaged a %s." kind.Name
          | None -> ""

        logMessages <- sprintf "Acquired %d Souls from the fallen foe.%s" standardSouls consumableMsg :: logMessages
    | None -> ()

    match droppedKeyOpt with
    | Some kId ->
      newKeys <- newKeys.Add kId
      logMessages <- sprintf "The guardian dropped key: %s!" kId :: logMessages
    | None -> ()

    let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }

    let leveledPlayer, xpMessages =
      if newXp > 0 then applyExperience state.Player newXp
      else state.Player, []

    // Every kill of a species sharpens the player against the next one.
    let masteredPlayer =
      match defeatedEnemyOpt with
      | Some e ->
        let species = e.Combatant.Name
        let profile = leveledPlayer.Progression
        let kills = (profile.Mastery |> Map.tryFind species |> Option.defaultValue 0) + 1
        if kills <= 10 then
          logMessages <-
            sprintf "Mastery of %s: %d kills (+%d%% damage dealt, -%d%% damage taken)."
              species kills (kills * 2) kills :: logMessages
        { leveledPlayer with
            Progression = { profile with Mastery = profile.Mastery.Add(species, kills) } }
      | None -> leveledPlayer

    { state with
        Player = masteredPlayer
        CurrentFloor = updatedFloor
        CollectedKeys = newKeys
        Souls = newSouls
        Trophies = newTrophies
        Consumables = newConsumables
        UnidentifiedConsumables = newUnidentified
        MessageLog = xpMessages @ logMessages }

  /// Resolves the fleeing behavior of a critically wounded lone beast (ADR 0009)
  let resolveBeastFlee (enemyId: string) (isSpared: bool) (state: TowerRunState) : TowerRunState =
    let mutable targetEnemyOpt = None
    let updatedEntities =
      state.CurrentFloor.Entities
      |> Map.map (fun _ ent ->
        match ent with
        | EntityEnemy e when e.Id = enemyId ->
          targetEnemyOpt <- Some e
          EntityEnemy { e with IsDefeated = true }
        | other -> other)

    match targetEnemyOpt with
    | None -> state
    | Some e ->
      if isSpared then
        let updatedPlayer =
          state.Player
          |> Combatant.updateMeters (fun m -> { m with Recklessness = Meter.Zero })
          |> (fun p -> { p with Morale = p.Morale.ApplyDelta 20 })
          |> Combatant.growFromEncounter [ e.Combatant ]

        let msg = sprintf "The wounded %s scrambled into the shadows and vanished. You spared it, steadying your nerves and gaining martial insight." e.Name
        { state with
            CurrentFloor = { state.CurrentFloor with Entities = updatedEntities }
            Player = updatedPlayer
            MessageLog = msg :: state.MessageLog }
      else
        // Executed: resolve standard defeat (drops loot/trophy) + 15 Recklessness
        let stateAfterDefeat = resolveEnemyDefeat enemyId state
        let updatedPlayer =
          stateAfterDefeat.Player
          |> Combatant.updateMeters (fun m -> { m with Recklessness = m.Recklessness + 15 })
          |> Combatant.growFromEncounter [ e.Combatant ]

        let msg = sprintf "Delivered a merciless coup de grâce to the fleeing %s (+15 Recklessness)." e.Name
        { stateAfterDefeat with
            Player = updatedPlayer
            MessageLog = msg :: stateAfterDefeat.MessageLog }
