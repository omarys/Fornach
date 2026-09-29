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
    Seed: int }

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

  /// Initializes a new Tower run session starting at a given floor
  let initSession (player: Combatant) (seed: int) (startFloor: int) : TowerRunState =
    let floor = TowerGenerator.generateFloor seed startFloor
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
        Seed = seed }

    updateFov initial

  /// Advances to the next floor via the Stairway Door
  let ascendFloor (state: TowerRunState) : TowerRunState =
    let nextFloorNum = state.CurrentFloor.FloorNumber + 1
    let nextFloor = TowerGenerator.generateFloor state.Seed nextFloorNum
    let nextState =
      { state with
          CurrentFloor = nextFloor
          PlayerPosition = nextFloor.SpawnLocation
          Facing = Some Direction.North
          Locomotion = LocomotionState.Walking
          MessageLog = sprintf "Ascended to Floor %d: %s!" nextFloorNum nextFloor.Theme.Name :: state.MessageLog }

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
        events <- [ TowerEvent.CombatTriggered enemy ]
        let logMsg = sprintf "Encountered %s! Initiating duel..." enemy.Name
        { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

      | Some (EntityNpc npc) ->
        let questAccepted = npc.Quest.IsSome
        events <- [ TowerEvent.NpcInteracted(npc, questAccepted) ]
        let logMsg = sprintf "Spoke with %s: \"%s\"" npc.Name (List.head npc.Dialogue)
        { nextState with MessageLog = logMsg :: nextState.MessageLog }, events

      | Some (EntityChest chest) when not chest.IsOpen ->
        let updatedChest = { chest with IsOpen = true }
        let updatedEntities = Map.add targetPt (EntityChest updatedChest) state.CurrentFloor.Entities
        let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }

        let mutable newKeys = state.CollectedKeys
        let mutable newItems = state.InventoryItems
        let mutable rewardMsg = "The chest is empty."

        match chest.LootKeyId with
        | Some kId ->
          newKeys <- newKeys.Add kId
          rewardMsg <- sprintf "Obtained key: %s!" kId
        | None -> ()

        match chest.ItemReward with
        | Some item ->
          newItems <- item :: newItems
          rewardMsg <- sprintf "Obtained relic: %s!" item.Name
        | None -> ()

        events <- [ TowerEvent.ChestOpened(chest, chest.ItemReward, chest.LootKeyId) ]
        let updatedState =
          { nextState with
              CurrentFloor = updatedFloor
              CollectedKeys = newKeys
              InventoryItems = newItems
              MessageLog = rewardMsg :: nextState.MessageLog }

        updatedState, events

      | Some (EntityShrine shrine) when not shrine.IsUsed ->
        let updatedShrine = { shrine with IsUsed = true }
        let updatedEntities = Map.add targetPt (EntityShrine updatedShrine) state.CurrentFloor.Entities
        let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }

        // Blessing: restore 40 morale, reset recklessness to 0
        let blessedPlayer =
          { state.Player with
              Morale = state.Player.Morale.ApplyDelta 40
              Meters = { state.Player.Meters with Recklessness = Meter.Zero } }

        events <- [ TowerEvent.ShrineActivated shrine ]
        let logMsg = sprintf "Communed with %s: %s" shrine.Name shrine.BlessingDescription
        let updatedState =
          { nextState with
              Player = blessedPlayer
              CurrentFloor = updatedFloor
              MessageLog = logMsg :: nextState.MessageLog }

        updatedState, events

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

  /// Marks a defeated enemy on the floor and awards its dropped key if any
  let resolveEnemyDefeat (enemyId: string) (state: TowerRunState) : TowerRunState =
    let mutable droppedKeyOpt = None
    let updatedEntities =
      state.CurrentFloor.Entities
      |> Map.map (fun _ ent ->
        match ent with
        | EntityEnemy e when e.Id = enemyId ->
          droppedKeyOpt <- e.DropsKeyId
          EntityEnemy { e with IsDefeated = true }
        | other -> other)

    let mutable newKeys = state.CollectedKeys
    let mutable logMessages = state.MessageLog

    match droppedKeyOpt with
    | Some kId ->
      newKeys <- newKeys.Add kId
      logMessages <- sprintf "The guardian dropped key: %s!" kId :: logMessages
    | None -> ()

    let updatedFloor = { state.CurrentFloor with Entities = updatedEntities }
    { state with
        CurrentFloor = updatedFloor
        CollectedKeys = newKeys
        MessageLog = logMessages }

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
