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
  | NpcInteracted of npc: TowerNpc * questAccepted: bool
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
    DiscoveredEchoes: Set<string>
    IsStoryMode: bool }

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

  /// Restore a combatant's pre-fight armor and weapon condition, clearing any in-combat wear.
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

  /// The enemy phase of a turn: aggroed living enemies pursue the player one tile at a time in
  /// deterministic order (nearest first, ties by Id), then any enemies adjacent to the player
  /// raise a group engagement. Bosses and defeated enemies never move; enemies never stack or
  /// step onto the player's tile.
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

    let mutable workingState = state

    for (_, enemy) in orderedAggro do
      // Re-read the enemy's position: an earlier move this phase may have overwritten it.
      let currentPt =
        workingState.CurrentFloor.Entities
        |> Map.toList
        |> List.tryPick (fun (p, ent) -> match ent with EntityEnemy e when e.Id = enemy.Id -> Some p | _ -> None)

      match currentPt with
      | Some pt when Distance.chebyshev pt workingState.PlayerPosition > 1 && not (enemy.Id.StartsWith("boss_")) ->
        let isPassable (p: Point) =
          p = workingState.PlayerPosition
          || ((match Map.tryFind p workingState.CurrentFloor.Tiles with
               | Some t -> t.IsWalkable
               | None -> false)
              && (match Map.tryFind p workingState.CurrentFloor.Entities with
                  | Some ent -> not ent.BlocksMovement
                  | None -> true))

        match Pathfinding.aStar isPassable pt workingState.PlayerPosition with
        | Some (_ :: step :: _) when
            (match Map.tryFind step workingState.CurrentFloor.Entities with
             | Some ent -> not ent.BlocksMovement
             | None -> true) ->
          let vacated = { workingState.CurrentFloor with Entities = Map.remove pt workingState.CurrentFloor.Entities }
          let movedFloor = { vacated with Entities = Map.add step (EntityEnemy enemy) vacated.Entities }
          workingState <- { workingState with CurrentFloor = movedFloor }
          RuntimeLog.write (sprintf "enemy %s: %A -> %A (pursuit)" enemy.Id pt step)
        | _ ->
          RuntimeLog.write (sprintf "enemy %s: holds at %A (no path)" enemy.Id pt)
      | Some pt ->
        RuntimeLog.write (sprintf "enemy %s: holds at %A (adjacent or boss)" enemy.Id pt)
      | None ->
        RuntimeLog.write (sprintf "enemy %s: position not found" enemy.Id)

    let adjacent = adjacentEnemies workingState

    let events =
      if List.isEmpty adjacent then []
      else [ TowerEvent.GroupCombatTriggered adjacent ]

    workingState, events

  /// Completes an active quest on the floor and awards its reward
  let completeQuest (questId: string) (state: TowerRunState) : TowerRunState =
    let updatedQuests =
      state.CurrentFloor.ActiveQuests
      |> List.map (fun q -> if q.Id = questId then { q with IsCompleted = true } else q)

    let updatedFloor =
      { state.CurrentFloor with
          ActiveQuests = updatedQuests }
      |> (fun f -> f.UnlockStairway())

    { state with
        CurrentFloor = updatedFloor
        CompletedQuests = state.CompletedQuests.Add questId
        MessageLog = sprintf "Quest '%s' completed! The Ascension Portal is unlocked." questId :: state.MessageLog }

  /// Initializes a new Tower run session starting at a given floor with specified story mode
  let initSessionWithMode (player: Combatant) (seed: int) (startFloor: int) (isStory: bool) : TowerRunState =
    let floor = TowerGenerator.generateFloorWithMode seed startFloor isStory
    let initial =
      { Player = player
        CurrentFloor = floor
        PlayerPosition = floor.SpawnLocation
        Facing = Some Direction.North
        Locomotion = LocomotionState.Walking
        CollectedKeys = Set.empty
        CompletedQuests = Set.empty
        InventoryItems = player.EquippedItems
        MessageLog = [ sprintf "Ascended to Floor %d: %s." startFloor floor.Theme.Name ]
        Seed = seed
        Souls = 0
        Trophies = []
        DiscoveredEchoes = Set.empty
        IsStoryMode = isStory }

    updateFov initial

  /// Initializes a new Tower run session starting at a given floor
  let initSession (player: Combatant) (seed: int) (startFloor: int) : TowerRunState =
    initSessionWithMode player seed startFloor false

  /// Advances to the next floor via the Stairway Door
  let ascendFloor (state: TowerRunState) : TowerRunState =
    let nextFloorNum = state.CurrentFloor.FloorNumber + 1
    let nextFloor = TowerGenerator.generateFloorWithMode state.Seed nextFloorNum state.IsStoryMode
    let nextState =
      { state with
          Player = { state.Player with Meters = StatusMeters.Zero }
          CurrentFloor = nextFloor
          PlayerPosition = nextFloor.SpawnLocation
          Facing = Some Direction.North
          Locomotion = LocomotionState.Walking
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
    let mutable nextState = { state with Facing = Some dir }

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
        let questAccepted = npc.Quest.IsSome
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
          let rewardMoralePlayer = { completedState.Player with Morale = completedState.Player.Morale.ApplyDelta 40 }
          let rewardedState =
            { completedState with
                Player = rewardMoralePlayer
                Souls = completedState.Souls + 50
                MessageLog = sprintf "Trial complete! %s thanks you: %s (+50 Souls, +40 Morale)" npc.Name q.RewardDescription :: completedState.MessageLog }
          events <- [ TowerEvent.NpcInteracted(updatedNpc, true) ]
          rewardedState, events
        else
          events <- [ TowerEvent.NpcInteracted(npc, questAccepted) ]
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

      | Some (EntityShrine shrine) when not shrine.IsUsed ->
        let updatedShrine = { shrine with IsUsed = true }
        let updatedEntities = Map.add targetPt (EntityShrine updatedShrine) state.CurrentFloor.Entities
        let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }

        // Blessing: fully restore health and morale, repair armor, restore weapon, purge debuffs, reset recklessness to 0
        let blessedPlayer =
          { state.Player with
              Health = Pool.Create state.Player.Health.Maximum
              Morale = Pool.Create state.Player.Morale.Maximum
              Armor = ArmorIntegrity.Create state.Player.Armor.Max
              WeaponCondition = WeaponCondition.Pristine
              BleedStacks = 0
              LimbDebuff = 0
              Meters = StatusMeters.Zero }

        events <- [ TowerEvent.ShrineActivated shrine ]
        let logMsg = sprintf "Communed with %s: %s" shrine.Name shrine.BlessingDescription
        let updatedState =
          { nextState with
              Player = blessedPlayer
              CurrentFloor = updatedFloor
              MessageLog = logMsg :: nextState.MessageLog }

        updatedState, events

      // --- Dynamic Encounters (ADR 0004 Phase 3) ---
      | Some (EntityEncounter enc) ->
        match enc with
        | SacrificialAltar altar when not altar.IsUsed ->
          events <- [ TowerEvent.AltarEncountered(altar, targetPt) ]
          let logMsg = sprintf "Approached %s. [Cost: %s -> Reward: %s]" altar.Name altar.Cost.Description altar.Reward.Description
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
              IsDefeated = false }
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
                  IsDefeated = false }
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
      // 1. Check & Apply Cost
      let costPlayer =
        match altar.Cost with
        | SacrificeHealth amt ->
          { state.Player with Health = state.Player.Health.ApplyDelta -amt }
        | SacrificeMorale amt ->
          { state.Player with Morale = state.Player.Morale.ApplyDelta -amt }
        | ShredArmor amt ->
          { state.Player with Armor = state.Player.Armor.Shred amt }

      // 2. Apply Reward
      let mutable rewardedPlayer = costPlayer
      let mutable newKeys = state.CollectedKeys
      let mutable newItems = state.InventoryItems

      match altar.Reward with
      | StatBuff (stat, bonus) ->
        let updatedStats = costPlayer.Stats.Modify(stat, bonus)
        rewardedPlayer <- { costPlayer with Stats = updatedStats }
      | VitalitySurge (hp, mor) ->
        rewardedPlayer <- { costPlayer with Health = costPlayer.Health.ApplyDelta hp; Morale = costPlayer.Morale.ApplyDelta mor }
      | KeyReward (kId, _) ->
        newKeys <- newKeys.Add kId
      | RelicReward item ->
        newItems <- item :: newItems
        rewardedPlayer <- Combatant.applyItemBonus item rewardedPlayer

      let updatedAltar = { altar with IsUsed = true }
      let updatedEntities = Map.add altarPt (EntityEncounter (SacrificialAltar updatedAltar)) state.CurrentFloor.Entities
      let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
      let logMsg = sprintf "Communed with %s: Paid [%s], Received [%s]!" altar.Name altar.Cost.Description altar.Reward.Description

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
        let soulGain = rng.Next(template.Loot.MinSouls, template.Loot.MaxSouls + 1)
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

        logMessages <- (soulMsg + trophyMsg) :: logMessages
      | None ->
        let standardSouls = e.Combatant.Level * 15
        newSouls <- newSouls + standardSouls
        logMessages <- sprintf "Acquired %d Souls from the fallen foe." standardSouls :: logMessages
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
        MessageLog = xpMessages @ logMessages }
