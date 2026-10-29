namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain

module TacticalAssessmentTests =

  [<Fact>]
  let ``Prologue Berserker reads Stone Gargoyle primary stat with guarded posture at level 1`` () =
    let player = StoryBosses.createProloguePlayer "berserker"
    let gargoyle = Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())

    let assess = TacticalAssessment.assess player gargoyle

    // Stone Gargoyle stats: Reflex 240, Fortitude 220, Finesse 230, Poise 200, Force 180, Composure 150
    Assert.Equal(1, player.Level)
    Assert.Equal(StatId.Reflex, assess.PrimaryStat)
    Assert.Equal(240, assess.PrimaryStatValue)
    Assert.Equal("Agility Defense", assess.PrimaryStatRole)
    // Level-1 Acumen 19 + Intuition 34/4 (8) = 27 vs Composure 150 -> Obscured
    Assert.Equal(TacticalInsightLevel.Obscured, assess.InsightLevel)
    Assert.Equal(19 + 34 / 4, assess.EffectiveAcumen)
    Assert.Equal(150, assess.TargetComposure)
    Assert.True(assess.Ratio < 0.40)
    Assert.Equal((StatId.Fortitude, 220), assess.WeakestDefense)
    Assert.Equal("GUARDED POSTURE", assess.Headline)
    Assert.True(assess.StrategicAdvice.Contains("build Momentum"))

  [<Fact>]
  let ``Prologue Mage scrutiny still reads Stone Gargoyle as guarded at level 1`` () =
    // The Mage's inquisitor loadout: Acumen 40 + Intuition 40/4 (10) = 50
    let player = StoryBosses.createProloguePlayer "inquisitor"
    let gargoyle = Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())

    let assess = TacticalAssessment.assess player gargoyle

    // Acumen 40 + Intuition 40/4 (10) = 50 vs Composure 150 (Ratio ~0.33)
    Assert.Equal(TacticalInsightLevel.Obscured, assess.InsightLevel)
    Assert.Equal("GUARDED POSTURE", assess.Headline)
    Assert.Equal(40 + 40 / 4, assess.EffectiveAcumen)
    Assert.Equal(StatId.Reflex, assess.PrimaryStat)
    Assert.Equal(240, assess.PrimaryStatValue)
    Assert.True(assess.VulnerabilitySummary.IsNone)

  [<Fact>]
  let ``Prologue classes discern Slag Hound primary stat as Force and weakest defense as Reflex`` () =
    let player = StoryBosses.createProloguePlayer "duelist"
    let hound = Bestiary.slagHound.ToCombatant (CombatantId.New())

    let assess = TacticalAssessment.assess player hound

    // Slag Hound stats: Force 105, Fortitude 95, Finesse 80, Reflex 75 (Poise 0 for non-humanoids)
    Assert.Equal(StatId.Force, assess.PrimaryStat)
    Assert.Equal(105, assess.PrimaryStatValue)
    Assert.Equal((StatId.Reflex, 75), assess.WeakestDefense)
    // Duelist Acumen 15 + Intuition 22/4 (5) = 20 vs Composure 50 -> Discerning (0.40)
    Assert.Equal(TacticalInsightLevel.Discerning, assess.InsightLevel)
    Assert.Equal("DISCERNING READ", assess.Headline)
    Assert.True(assess.StrategicAdvice.Contains("immense kinetic striking power"))

  [<Fact>]
  let ``Aspect of Guilt primary stat is Force and defensive flaw is Reflex`` () =
    let player = StoryBosses.createProloguePlayer "warden"
    let guilt = StoryBosses.createGuiltAspect ()

    let assess = TacticalAssessment.assess player guilt

    // Aspect of Guilt: Force 210, Fortitude 195, Poise 185, Reflex 95, Composure 150
    Assert.Equal(StatId.Force, assess.PrimaryStat)
    Assert.Equal(210, assess.PrimaryStatValue)
    Assert.Equal((StatId.Reflex, 95), assess.WeakestDefense)
    // A level-1 Warden cannot read a Guilt Aspect's exact form yet.
    Assert.Equal(TacticalInsightLevel.Obscured, assess.InsightLevel)
    Assert.True(assess.StrategicAdvice.Contains("build Momentum"))

  [<Fact>]
  let ``Momentum stacks sharpen tactical insight from Obscured to Discerning`` () =
    let player = StoryBosses.createProloguePlayer "berserker" // Acumen 19 + Intuition 34/4 (8) = 27
    let gargoyle = Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())

    let initialAssess = TacticalAssessment.assess player gargoyle
    Assert.Equal(TacticalInsightLevel.Obscured, initialAssess.InsightLevel)

    // Add 3 Momentum stacks (+45 effective Acumen: 27 + 45 = 72 / 150 = 0.48 -> Discerning)
    let studiedPlayer = { player with Stats = player.Stats.ApplyModifiers [ StatId.Acumen, 45 ] }
    let studiedAssess = TacticalAssessment.assess studiedPlayer gargoyle

    Assert.Equal(TacticalInsightLevel.Discerning, studiedAssess.InsightLevel)
    Assert.Equal(72, studiedAssess.EffectiveAcumen)
    Assert.Equal("DISCERNING READ", studiedAssess.Headline)

  [<Fact>]
  let ``Zero composure target like Anger Aspect does not divide by zero and yields Penetrating insight`` () =
    let player = StoryBosses.createProloguePlayer "berserker"
    let anger = StoryBosses.createAngerAspect ()

    // Anger Aspect has Composure = 0
    let assess = TacticalAssessment.assess player anger
    Assert.Equal(TacticalInsightLevel.Penetrating, assess.InsightLevel)
    Assert.True(assess.Ratio > 10.0)

  [<Fact>]
  let ``High composure gap yields Obscured posture with advice to build Momentum`` () =
    let noviceWarrior = TierFactory.createClassLevel CharacterClass.Warrior 1
    let overseer = Bestiary.quarryOverseer.ToCombatant (CombatantId.New()) // Composure 390

    let assess = TacticalAssessment.assess noviceWarrior overseer
    Assert.Equal(TacticalInsightLevel.Obscured, assess.InsightLevel)
    Assert.Equal("GUARDED POSTURE", assess.Headline)
    Assert.True(assess.StrategicAdvice.Contains("build Momentum"))

  [<Fact>]
  let ``Mental plane combatant correctly evaluates mental attributes as primary`` () =
    let player = StoryBosses.createProloguePlayer "duelist"
    let mentalBoss = StoryBosses.createAcceptanceAspect ()

    let assess = TacticalAssessment.assess player mentalBoss
    // Mental boss Plane = Mental; stats: Intellect, Resolve, Acuity, Intuition, Acumen, Composure
    let desc = Attributes.descriptorOf assess.PrimaryStat
    Assert.Equal(Plane.Mental, desc.Plane)

  [<Fact>]
  let ``Tactical assessment threat warning surfaces correctly across insight levels`` () =
    let observer = StoryBosses.createProloguePlayer "berserker" // Base Acumen 19 + Intuition 15/4 (3) = 22
    let hound = Bestiary.slagHound.ToCombatant (CombatantId.New()) // Force 105, Composure 50

    // 1. Obscured insight (high Composure target) -> ThreatWarning is None
    let overseer = Bestiary.quarryOverseer.ToCombatant (CombatantId.New()) // Composure 390
    let obscuredAssess = TacticalAssessment.assess observer overseer
    Assert.Equal(TacticalInsightLevel.Obscured, obscuredAssess.InsightLevel)
    Assert.True(obscuredAssess.ThreatWarning.IsNone, "Obscured insight should not reveal apex threat details")

    // 2. Discerning insight (ratio 22/50 = 0.44 -> Discerning) -> ThreatWarning has approximation
    let discerningAssess = TacticalAssessment.assess observer hound
    Assert.Equal(TacticalInsightLevel.Discerning, discerningAssess.InsightLevel)
    Assert.True(discerningAssess.ThreatWarning.IsSome, "Discerning insight should generate threat warning")
    let warnDiscerning = discerningAssess.ThreatWarning.Value
    Assert.True(warnDiscerning.StartsWith("⚠ THREAT DETECTED"), sprintf "Expected threat detected prefix, got: %s" warnDiscerning)
    Assert.True(warnDiscerning.Contains("Force"), "Should detect Force as the threat stat")

    // 3. Keen insight (elevated Acumen to reach ratio in [0.75, 1.15))
    // Hound Composure 50 -> Need effective Acumen ~45 (ratio = 0.90 -> Keen)
    let keenObserver = { observer with Stats = observer.Stats.ApplyModifiers [ StatId.Acumen, 30 ] } // 22 + 30 = 52 / 50 = 1.04 -> Keen
    let keenAssess = TacticalAssessment.assess keenObserver hound
    Assert.Equal(TacticalInsightLevel.Keen, keenAssess.InsightLevel)
    Assert.True(keenAssess.ThreatWarning.IsSome)
    let warnKeen = keenAssess.ThreatWarning.Value
    Assert.True(warnKeen.StartsWith("⚠ APEX THREAT:"), sprintf "Expected apex threat prefix, got: %s" warnKeen)
    Assert.True(warnKeen.Contains("Colossal Force (105)"), "Should specify exact apex Force value")

    // 4. Penetrating insight (ratio >= 1.15, e.g. Anger Aspect with Composure 0)
    let anger = StoryBosses.createAngerAspect ()
    let penAssess = TacticalAssessment.assess observer anger
    Assert.Equal(TacticalInsightLevel.Penetrating, penAssess.InsightLevel)
    Assert.True(penAssess.ThreatWarning.IsSome)
    let warnPen = penAssess.ThreatWarning.Value
    Assert.True(warnPen.StartsWith("⚠ APEX THREAT (CRITICAL):"), sprintf "Expected critical apex threat prefix, got: %s" warnPen)
