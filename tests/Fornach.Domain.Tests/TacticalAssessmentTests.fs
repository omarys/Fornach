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
    // Level-1 Acumen 19 + Intuition 15/4 (3) = 22 vs Composure 150 -> Obscured
    Assert.Equal(TacticalInsightLevel.Obscured, assess.InsightLevel)
    Assert.Equal(19 + 15 / 4, assess.EffectiveAcumen)
    Assert.Equal(150, assess.TargetComposure)
    Assert.True(assess.Ratio < 0.40)
    Assert.Equal((StatId.Poise, 200), assess.WeakestDefense)
    Assert.Equal("GUARDED POSTURE", assess.Headline)
    Assert.True(assess.StrategicAdvice.Contains("build Study Stacks"))

  [<Fact>]
  let ``Prologue Inquisitor leads all classes in scrutiny yet still reads Stone Gargoyle as guarded at level 1`` () =
    let player = StoryBosses.createProloguePlayer "inquisitor"
    let gargoyle = Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())

    let assess = TacticalAssessment.assess player gargoyle

    // Inquisitor Acumen 30 + Intuition 30/4 (7) = 37 vs Composure 150 (Ratio ~0.25)
    Assert.Equal(TacticalInsightLevel.Obscured, assess.InsightLevel)
    Assert.Equal("GUARDED POSTURE", assess.Headline)
    Assert.Equal(30 + 30 / 4, assess.EffectiveAcumen)
    Assert.Equal(StatId.Reflex, assess.PrimaryStat)
    Assert.Equal(240, assess.PrimaryStatValue)
    Assert.True(assess.VulnerabilitySummary.IsNone)

  [<Fact>]
  let ``Prologue classes discern Slag Hound primary stat as Force and weakest defense as Poise`` () =
    let player = StoryBosses.createProloguePlayer "duelist"
    let hound = Bestiary.slagHound.ToCombatant (CombatantId.New())

    let assess = TacticalAssessment.assess player hound

    // Slag Hound stats: Force 105, Fortitude 95, Finesse 80, Reflex 75, Poise 70, Composure 50
    Assert.Equal(StatId.Force, assess.PrimaryStat)
    Assert.Equal(105, assess.PrimaryStatValue)
    Assert.Equal((StatId.Poise, 70), assess.WeakestDefense)
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
    Assert.True(assess.StrategicAdvice.Contains("build Study Stacks"))

  [<Fact>]
  let ``Study Stacks sharpen tactical insight from Obscured to Discerning`` () =
    let player = StoryBosses.createProloguePlayer "berserker" // Acumen 19 + Intuition 15/4 (3) = 22
    let gargoyle = Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())

    let initialAssess = TacticalAssessment.assess player gargoyle
    Assert.Equal(TacticalInsightLevel.Obscured, initialAssess.InsightLevel)

    // Add 3 study stacks (+45 effective Acumen: 22 + 45 = 67 / 150 = 0.44 -> Discerning)
    let studiedPlayer = { player with StudyStacks = 3 }
    let studiedAssess = TacticalAssessment.assess studiedPlayer gargoyle

    Assert.Equal(TacticalInsightLevel.Discerning, studiedAssess.InsightLevel)
    Assert.Equal(67, studiedAssess.EffectiveAcumen)
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
  let ``High composure gap yields Obscured posture with advice to build Study Stacks`` () =
    let noviceWarrior = TierFactory.createClassLevel CharacterClass.Warrior 1
    let overseer = Bestiary.quarryOverseer.ToCombatant (CombatantId.New()) // Composure 390

    let assess = TacticalAssessment.assess noviceWarrior overseer
    Assert.Equal(TacticalInsightLevel.Obscured, assess.InsightLevel)
    Assert.Equal("GUARDED POSTURE", assess.Headline)
    Assert.True(assess.StrategicAdvice.Contains("build Study Stacks"))

  [<Fact>]
  let ``Mental plane combatant correctly evaluates mental attributes as primary`` () =
    let player = StoryBosses.createProloguePlayer "duelist"
    let mentalBoss = StoryBosses.createAcceptanceAspect ()

    let assess = TacticalAssessment.assess player mentalBoss
    // Mental boss Plane = Mental; stats: Intellect, Resolve, Acuity, Intuition, Acumen, Composure
    let desc = Attributes.descriptorOf assess.PrimaryStat
    Assert.Equal(Plane.Mental, desc.Plane)
