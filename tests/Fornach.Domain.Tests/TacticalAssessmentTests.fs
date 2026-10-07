namespace Fornach.Domain.Tests

open System
open Xunit
open Fornach.Domain

module TacticalAssessmentTests =

  [<Fact>]
  let ``Prologue Berserker reads Stone Gargoyle primary stat as Reflex right away with Keen insight`` () =
    let player = StoryBosses.createProloguePlayer "berserker"
    let gargoyle = Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())

    let assess = TacticalAssessment.assess player gargoyle

    // Stone Gargoyle stats: Reflex 240, Fortitude 220, Finesse 230, Poise 200, Force 180, Composure 150
    Assert.Equal(StatId.Reflex, assess.PrimaryStat)
    Assert.Equal(240, assess.PrimaryStatValue)
    Assert.Equal("Agility Defense", assess.PrimaryStatRole)
    Assert.Equal(TacticalInsightLevel.Keen, assess.InsightLevel)
    Assert.Equal(120 + 25, assess.EffectiveAcumen) // Acumen 120 + Intuition 100/4 = 145
    Assert.Equal(150, assess.TargetComposure)
    Assert.True(assess.Ratio >= 0.75 && assess.Ratio < 1.15)
    Assert.Equal((StatId.Poise, 200), assess.WeakestDefense)
    Assert.Equal("KEEN INSIGHT", assess.Headline)
    Assert.True(assess.PrimarySummary.Contains("Reflex: 240"))
    Assert.True(assess.StrategicAdvice.Contains("Primary threat: Reflex (240)"))

  [<Fact>]
  let ``Prologue Inquisitor achieves Penetrating insight on Stone Gargoyle right away`` () =
    let player = StoryBosses.createProloguePlayer "inquisitor"
    let gargoyle = Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())

    let assess = TacticalAssessment.assess player gargoyle

    // Inquisitor has Acumen 160 + Intuition 150/4 = 197 vs Composure 150 (Ratio ~1.31)
    Assert.Equal(TacticalInsightLevel.Penetrating, assess.InsightLevel)
    Assert.Equal("PENETRATING INSIGHT", assess.Headline)
    Assert.Equal(StatId.Reflex, assess.PrimaryStat)
    Assert.Equal(240, assess.PrimaryStatValue)
    match assess.VulnerabilitySummary with
    | Some vuln ->
      Assert.True(vuln.Contains("Poise: 200"))
      Assert.True(vuln.Contains("Critical Flaw"))
    | None -> Assert.True(false, "Expected vulnerability summary for Penetrating insight")

  [<Fact>]
  let ``Prologue classes reveal Slag Hound primary stat as Force and weakness as Reflex`` () =
    let player = StoryBosses.createProloguePlayer "duelist"
    let hound = Bestiary.slagHound.ToCombatant (CombatantId.New())

    let assess = TacticalAssessment.assess player hound

    // Slag Hound stats: Force 105, Fortitude 95, Finesse 80, Reflex 75, Poise 70, Composure 50
    Assert.Equal(StatId.Force, assess.PrimaryStat)
    Assert.Equal(105, assess.PrimaryStatValue)
    Assert.Equal((StatId.Poise, 70), assess.WeakestDefense)
    Assert.Equal(TacticalInsightLevel.Penetrating, assess.InsightLevel)
    Assert.True(assess.StrategicAdvice.Contains("Primary threat: Force (105)"))
    Assert.True(assess.StrategicAdvice.Contains("Exploit lower Poise (70)"))

  [<Fact>]
  let ``Aspect of Guilt primary stat is Force and defensive flaw is Reflex`` () =
    let player = StoryBosses.createProloguePlayer "warden"
    let guilt = StoryBosses.createGuiltAspect ()

    let assess = TacticalAssessment.assess player guilt

    // Aspect of Guilt: Force 210, Fortitude 195, Poise 185, Reflex 95, Composure 150
    Assert.Equal(StatId.Force, assess.PrimaryStat)
    Assert.Equal(210, assess.PrimaryStatValue)
    Assert.Equal((StatId.Reflex, 95), assess.WeakestDefense)
    Assert.True(assess.StrategicAdvice.Contains("Exploit lower Reflex (95)"))

  [<Fact>]
  let ``Study Stacks sharpen tactical insight from Keen to Penetrating`` () =
    let player = StoryBosses.createProloguePlayer "berserker" // Ratio ~0.97 (Keen)
    let gargoyle = Bestiary.stoneGargoyle.ToCombatant (CombatantId.New())

    let initialAssess = TacticalAssessment.assess player gargoyle
    Assert.Equal(TacticalInsightLevel.Keen, initialAssess.InsightLevel)

    // Add 3 study stacks (+45 effective Acumen: 145 + 45 = 190 / 150 = 1.26 -> Penetrating)
    let studiedPlayer = { player with StudyStacks = 3 }
    let studiedAssess = TacticalAssessment.assess studiedPlayer gargoyle

    Assert.Equal(TacticalInsightLevel.Penetrating, studiedAssess.InsightLevel)
    Assert.Equal(190, studiedAssess.EffectiveAcumen)
    Assert.Equal("PENETRATING INSIGHT", studiedAssess.Headline)

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
