module Fornach.Domain.Tests.BossTuningTests

open Xunit
open Fornach.Domain

[<Fact>]
let ``neutral tuning leaves the aspect untouched`` () =
  let guilt = StoryBosses.createGuiltAspect ()
  let same = StoryBosses.applyTuning StoryBosses.neutralTuning guilt

  Assert.Equal(guilt.Health.Maximum, same.Health.Maximum)
  Assert.Equal(guilt.Morale.Maximum, same.Morale.Maximum)
  Assert.Equal(guilt.Stats.Get StatId.Force, same.Stats.Get StatId.Force)

[<Fact>]
let ``pool factor scales both pools and nothing else`` () =
  let guilt = StoryBosses.createGuiltAspect ()

  let tuned =
    StoryBosses.applyTuning { PoolFactor = 2.0; PotencyFactor = 1.0 } guilt

  Assert.Equal(guilt.Health.Maximum * 2, tuned.Health.Maximum)
  Assert.Equal(guilt.Morale.Maximum * 2, tuned.Morale.Maximum)
  Assert.Equal(guilt.Stats.Get StatId.Force, tuned.Stats.Get StatId.Force)

[<Fact>]
let ``potency factor scales the stat block and leaves pools alone`` () =
  let guilt = StoryBosses.createGuiltAspect ()

  let tuned =
    StoryBosses.applyTuning { PoolFactor = 1.0; PotencyFactor = 1.5 } guilt

  Assert.Equal(guilt.Stats.Get StatId.Force * 3 / 2, tuned.Stats.Get StatId.Force)
  Assert.Equal(guilt.Health.Maximum, tuned.Health.Maximum)

[<Fact>]
let ``tuning survives stage scaling`` () =
  // scaleToStage recomputes the stat block from the authored one; a tuning applied first must still
  // be present afterwards, otherwise every per-aspect dial would be silently erased at runtime.
  let plain = StoryBosses.createGuiltAspect () |> StoryBosses.scaleToStage 2

  let tuned =
    StoryBosses.createGuiltAspect ()
    |> StoryBosses.applyTuning { PoolFactor = 2.0; PotencyFactor = 1.0 }
    |> StoryBosses.scaleToStage 2

  Assert.Equal(plain.Health.Maximum * 2, tuned.Health.Maximum)
  Assert.True(tuned.Stats.Get StatId.Force >= plain.Stats.Get StatId.Force)

[<Fact>]
let ``potency below one reduces the stat block`` () =
  // StatBonus.percentOf clamps to `max 1`, so routing a reduction through ApplyPercentBonuses turns
  // -40% into +1 point. The dial has to scale directly or "weaker" is silently "very slightly stronger".
  let guilt = StoryBosses.createGuiltAspect ()

  let tuned =
    StoryBosses.applyTuning { PoolFactor = 1.0; PotencyFactor = 0.5 } guilt

  Assert.Equal(guilt.Stats.Get StatId.Force / 2, tuned.Stats.Get StatId.Force)
