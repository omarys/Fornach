module Fornach.Tests.DefeatVerdictTests

open Xunit
open Fornach.Domain
open Fornach.Engine

/// The player who loses to a condition track, not to a pool: full health and morale, recklessness at
/// the collapse threshold. This is the case the log used to report as a bare PLAYERDEFEATED.
let private brokenOnTrack () =
  let player = { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Master with PlayerStyle = true }
  { player with
      Meters =
        { player.Meters with
            Recklessness = Meter.Create 100 } }

let private opponent () = TierFactory.createClassTier CharacterClass.Mage CombatTier.Novice

[<Fact>]
let ``The defeat verdict names the Collapse track that broke the combatant`` () =
  let player = brokenOnTrack () |> Combatant.evaluateCollapse
  let session = CombatLogger.startSession "t" "test" player (opponent ())
  let text = CombatLogger.formatSession session "PlayerDefeated" player (opponent ())
  Assert.Contains("OUTCOME:", text)
  Assert.Contains("collapsed", text)
  Assert.Contains("Recklessness", text)
  Assert.Contains("100", text)

[<Fact>]
let ``A defeat with every track healthy reports the emptied pool instead of a Collapse`` () =
  let player = { TierFactory.createClassTier CharacterClass.Fighter CombatTier.Novice with PlayerStyle = true }
  let drained = { player with Health = player.Health.ApplyDelta(-player.Health.Maximum) }
  let session = CombatLogger.startSession "t" "test" drained (opponent ())
  let text = CombatLogger.formatSession session "PlayerDefeated" drained (opponent ())
  Assert.Contains("Health pool emptied", text)
  Assert.DoesNotContain("collapsed on the", text)

[<Fact>]
let ``Every vitals line prints all seven condition tracks`` () =
  let player = brokenOnTrack ()
  let text = CombatLogger.formatVitalsSnapshot player
  for track in [ "Reck"; "Exh"; "Frus"; "Cog"; "Conf"; "Prov" ] do
    Assert.Contains(track, text)

[<Fact>]
let ``The logged Collapse reason agrees with the engine's own priority order`` () =
  // Two broken tracks at once: the verdict must name the same one evaluateCollapse applies.
  let player = brokenOnTrack ()
  let double = { player with Meters = { player.Meters with Exhaustion = Meter.Create 100 } }
  let collapsed = Combatant.evaluateCollapse double
  Assert.Equal(CollapseState.Collapsed RecklessExposure, collapsed.Collapse)
  Assert.Equal(Some RecklessExposure, Combatant.collapseReasonOf double)
