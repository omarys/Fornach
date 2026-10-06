namespace Fornach.Cli

open System
open System.Threading
open Spectre.Console
open Fornach.Domain

/// Environmental scene specification for the 5 Grief Stages, the Prologue, and the Tower Transition
type EnvironmentScene =
  { Id: string
    StageTitle: string
    LocationName: string
    PsychologicalTheme: string
    BorderColor: Color
    HeaderColor: string
    AccentColor: string
    AsciiArt: string list
    AtmosphereSensory: string list
    WeatherTag: string }

module EnvironmentScenes =

  /// Prologue: The Iron Quarry
  let QuarryScene : EnvironmentScene =
    { Id = "quarry"
      StageTitle = "PROLOGUE: GUILT & HESITATION"
      LocationName = "The Abandoned Iron Quarry"
      PsychologicalTheme = "The suffocating weight of an oath you failed to keep."
      BorderColor = Color(0x62uy, 0x72uy, 0xa4uy) // Theme.Comment Slate
      HeaderColor = Theme.Comment
      AccentColor = Theme.Orange
      AsciiArt =
        [ @"     /\_/\                 /\_/\               "
          @"  __/     \__   [CRANE] __/     \__  [SLAG PIT]"
          @" /           \    | |  /           \    (ooo)  "
          @"|   IRON PIT  |==| |=|   SHALE PIT  |==[=====] "
          @" \___________/   /   \ \___________/    |===|  " ]
      AtmosphereSensory =
        [ "Rainwater hisses as it strikes white-hot slag."
          "The hollow clang of mining picks echoes in the wet dark."
          "Mud pulls at your boots like desperate hands." ]
      WeatherTag = "[grey]Cold Driving Rain ⁝ ⁝ ⁝[/]" }

  /// Stage 1: Denial
  let ShroudedGroveScene : EnvironmentScene =
    { Id = "shrouded_grove"
      StageTitle = "STAGE 1: DENIAL"
      LocationName = "The Shrouded Grove"
      PsychologicalTheme = "Refusal to accept the collision; retreating into protective illusions."
      BorderColor = Color(0x50uy, 0xfauy, 0x7buy) // Theme.Green
      HeaderColor = Theme.Green
      AccentColor = Theme.Cyan
      AsciiArt =
        [ @"     / \         / \         / \               "
          @"    /   \  ░░░  /   \  ░░░  /   \    [MIST]    "
          @"   / / \ \ ░░░ / / \ \ ░░░ / / \ \  ~ ~ ~ ~ ~  "
          @"  / /   \ \   / /   \ \   / /   \ \ (MIRAGE)   "
          @"   |  |        |  |        |  |     [PHONE: OK]" ]
      AtmosphereSensory =
        [ "Dense fog swallows every footstep into unnerving silence."
          "Phantom reflections of an uncracked phone flicker in the gloom."
          "Whispers chorus: 'She's at home waiting for you. Turn around.'" ]
      WeatherTag = "[cyan]Suffocating Mist ~ ~ ~[/]" }

  /// Stage 2: Anger
  let BasaltCalderaScene : EnvironmentScene =
    { Id = "basalt_caldera"
      StageTitle = "STAGE 2: ANGER"
      LocationName = "The Basalt Caldera"
      PsychologicalTheme = "Destructive fury and blame; a screaming engine redlining out of control."
      BorderColor = Color(0xffuy, 0x55uy, 0x55uy) // Theme.Red
      HeaderColor = Theme.Red
      AccentColor = Theme.Yellow
      AsciiArt =
        [ @"       /\              /\             /\       "
          @"      /  \   (SMOKE)  /  \    /\     /  \      "
          @"     / /\ \  (  (  ) / /\ \  /  \   / /\ \     "
          @"====/ /  \ \==(__)==/ /  \ \/ /\ \=/ /  \ \===="
          @"   / /LAVA\ \      / /BASALT\ \  / /MAGMA\ \   " ]
      AtmosphereSensory =
        [ "Sulfur fumes scorch the lining of your lungs."
          "A subterranean roar pulses like a 400-horsepower engine."
          "Basalt plates crack open beneath glowing pools of molten slag." ]
      WeatherTag = "[bold red]Ember Storm * * *[/]" }

  /// Stage 3: Bargaining
  let ShatteredPromontoryScene : EnvironmentScene =
    { Id = "shattered_promontory"
      StageTitle = "STAGE 3: BARGAINING"
      LocationName = "The Shattered Promontory"
      PsychologicalTheme = "Desperate transactions; attempting to bribe fate with impossible promises."
      BorderColor = Color(0x8buy, 0xe9uy, 0xfduy) // Theme.Cyan
      HeaderColor = Theme.Cyan
      AccentColor = Theme.Purple
      AsciiArt =
        [ @"     /\                                        "
          @"    /  \      [CLIFF EDGE]         /\_/\       "
          @"   / /\ \    /            \       /     \      "
          @"  / /  \ \__/  [SCALES: ⚖] \_____/       \     "
          @"~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~/~" ]
      AtmosphereSensory =
        [ "Sideways gale-force spray stings like iron needles."
          "The black ocean roars against jagged needles far below."
          "A whisper begs: 'Take my eyes, take my hands, just reverse the clock.'" ]
      WeatherTag = "[deepskyblue1]Gale Ocean Squall ≋ ≋ ≋[/]" }

  /// Stage 4: Depression
  let SunkenMetropolisScene : EnvironmentScene =
    { Id = "sunken_metropolis"
      StageTitle = "STAGE 4: DEPRESSION"
      LocationName = "The Sunken Metropolis"
      PsychologicalTheme = "Total apathy and paralysis; submerged under the weight of an empty world."
      BorderColor = Color(0xbduy, 0x93uy, 0xf9uy) // Theme.Purple
      HeaderColor = Theme.Purple
      AccentColor = Theme.Comment
      AsciiArt =
        [ @"  |  |  |  |    [DROWNED TOWERS]   |  |  |  |  "
          @"  |  |  |  |         ____          |  |  |  |  "
          @" _|_ |  | _|_       /ARCH\        _|_ |  | _|_ "
          @"|   ||  ||   |=====|      |======|   ||  ||   |"
          @"~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~" ]
      AtmosphereSensory =
        [ "Knee-deep floodwaters lap sluggishly against crumbling concrete."
          "The ruins are hollowed out by a terrifying, soundless stillness."
          "Your limbs feel like lead. The sword weighs a thousand pounds." ]
      WeatherTag = "[dim steelblue]Damp Submerged Murk ░ ░ ░[/]" }

  /// Stage 5: Acceptance
  let WhiteMeadowScene : EnvironmentScene =
    { Id = "white_meadow"
      StageTitle = "STAGE 5: ACCEPTANCE"
      LocationName = "The White Meadow"
      PsychologicalTheme = "Forgiving yourself, releasing the past, and honoring her memory in peace."
      BorderColor = Color(0xf1uy, 0xfauy, 0x8cuy) // Theme.Yellow
      HeaderColor = Theme.Yellow
      AccentColor = Theme.Foreground
      AsciiArt =
        [ @"     \  |  /        (SUNLIGHT)                 "
          @"    -- SUN --      \    |    /     [WHITE LILIES]"
          @"     /  |  \      .  .  .  .  .      ❀   ❀   ❀ "
          @"~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"
          @"    ❀   ❀   ❀      [MEADOW PATH]      ❀   ❀    " ]
      AtmosphereSensory =
        [ "The rain has ceased; warm summer sunshine bathes the hillside."
          "White lilies sway gently to the rhythm of a peaceful breeze."
          "Her voice echoes not in terror, but in a fond, gentle farewell." ]
      WeatherTag = "[bold gold1]Radiant Sunlight ☀ ☀ ☀[/]" }

  /// Stage 6: The Crosswalk & The Tower Transition
  let CrosswalkIntersectionScene : EnvironmentScene =
    { Id = "intersection_tower"
      StageTitle = "THE TRANSITION: THE INTERSECTION"
      LocationName = "Crosswalk Intersection -> The Tower"
      PsychologicalTheme = "The trauma loop breaks. The crosswalk opens into the Infinite Dungeon."
      BorderColor = Color(0xffuy, 0xb8uy, 0x6cuy) // Theme.Orange
      HeaderColor = Theme.Orange
      AccentColor = Theme.Foreground
      AsciiArt =
        [ @"[CAR] ══════▶   |  |  |  |  |  |   ◀══════ [CAR]"
          @" [HEADLIGHTS]  |  |  CROSSWALK |   [HEADLIGHTS] "
          @"               |  |  |  |  |  |                "
          @"           [=== THE MONOLITHIC TOWER ===]      "
          @"               [BRONZE PORTAL: OPEN]           " ]
      AtmosphereSensory =
        [ "The meadow dissolves into rain-slicked asphalt and exhaust fumes."
          "Gridlocked cars on either flank form an impenetrable wall of blazing headlights."
          "Across the white lines of the crosswalk stands the towering monolith of The Tower." ]
      WeatherTag = "[bold yellow]Halogen High Beams ═══[/]" }

  /// Resolves the corresponding scene specification for a boss or environment ID
  let getSceneForEnemy (enemyId: string) : EnvironmentScene =
    match enemyId.ToLowerInvariant() with
    | "guilt_aspect" | "guiltaspect" | "bandit_guilt_aspect" -> QuarryScene
    | "denial_aspect" | "denialaspect" | "denial" -> ShroudedGroveScene
    | "anger_aspect" | "angeraspect" | "anger" -> BasaltCalderaScene
    | "bargaining_aspect" | "bargainingaspect" | "bargaining" -> ShatteredPromontoryScene
    | "depression_aspect" | "depressionaspect" | "depression" -> SunkenMetropolisScene
    | "acceptance_aspect" | "acceptanceaspect" | "acceptance" -> WhiteMeadowScene
    | _ -> QuarryScene

  /// Renders a scene header panel with ASCII landscape and psychological theme
  let renderSceneHeader (scene: EnvironmentScene) : Panel =
    let grid = Grid()
    grid.AddColumn(GridColumn().NoWrap()) |> ignore

    // Subtitle & Psychological Theme
    grid.AddRow(Markup(sprintf "[bold %s]%s[/] — %s" scene.HeaderColor scene.StageTitle scene.WeatherTag)) |> ignore
    grid.AddRow(Markup(sprintf "[italic grey]%s[/]\n" (Markup.Escape scene.PsychologicalTheme))) |> ignore

    // ASCII Artwork Frame
    for line in scene.AsciiArt do
      grid.AddRow(Markup(sprintf "[%s]%s[/]" scene.AccentColor (Markup.Escape line))) |> ignore

    grid.AddRow(Text("")) |> ignore

    // Sensory details
    for sensory in scene.AtmosphereSensory do
      grid.AddRow(Markup(sprintf "[grey]• %s[/]" (Markup.Escape sensory))) |> ignore

    Panel(grid)
      .Header(sprintf "[bold %s] %s [/]" scene.HeaderColor (Markup.Escape scene.LocationName))
      .Border(BoxBorder.Heavy)
      .BorderStyle(Style(foreground = Nullable scene.BorderColor))

  /// Renders a thematic encounter card for the boss manifestation
  let renderBossEncounterCard (boss: Combatant) (scene: EnvironmentScene) : Panel =
    let grid = Grid()
    grid.AddColumn(GridColumn().NoWrap()) |> ignore

    grid.AddRow(Markup(sprintf "[bold %s]Manifestation:[/] [bold %s]%s[/]" scene.HeaderColor Theme.Foreground (Markup.Escape boss.Name))) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]Stance:[/] [bold %s]%A[/]" scene.HeaderColor scene.AccentColor boss.Stance)) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]HP:[/] [bold red]%d/%d[/]   [bold %s]Morale:[/] [bold cyan]%d/%d[/]   [bold %s]Armor:[/] [bold yellow]%d[/]"
      scene.HeaderColor boss.Health.Current boss.Health.Maximum
      scene.HeaderColor boss.Morale.Current boss.Morale.Maximum
      scene.HeaderColor boss.Armor.Current)) |> ignore

    grid.AddRow(Text("")) |> ignore
    grid.AddRow(Markup(sprintf "[bold %s]Equipped Trauma Relics:[/]" scene.HeaderColor)) |> ignore

    for item in boss.EquippedItems do
      grid.AddRow(Markup(sprintf "  [bold %s]▸ %s[/] ([grey]%A[/]): [italic grey]%s[/]"
        scene.AccentColor (Markup.Escape item.Name) item.Slot (Markup.Escape item.Description))) |> ignore

    Panel(grid)
      .Header(sprintf "[bold red] TRAUMA ENCOUNTER [/]")
      .Border(BoxBorder.Rounded)
      .BorderStyle(Style(foreground = Nullable scene.BorderColor))

  /// Plays the atmospheric animated transition from the White Meadow into the Crosswalk & Tower
  let playCrosswalkTowerTransition () =
    AnsiConsole.Clear()
    AnsiConsole.Cursor.Hide()

    let consoleWidth = Math.Max(70, AnsiConsole.Profile.Width)

    // Phase 1: Dissolving the meadow of white lilies into rain and asphalt
    AnsiConsole.MarkupLine("[bold gold1]The radiant white meadow shimmers under the calm summer sun...[/]")
    Thread.Sleep(700)
    AnsiConsole.MarkupLine("[grey]A sudden chill runs through the air. The scent of lilies sharpens into gasoline and wet asphalt.[/]")
    Thread.Sleep(800)
    AnsiConsole.MarkupLine("[bold yellow]A low, vibrating hum rises from all sides—the drone of dozens of idling engines.[/]\n")
    Thread.Sleep(1000)

    // Phase 2: High beam flash animation across the terminal grid
    AnsiConsole.Live(Panel(Text("")))
      .AutoClear(false)
      .Overflow(VerticalOverflow.Crop)
      .Start(fun ctx ->
        let cars = [|
          @"[CAR]  ════════════════════▶   |  |   ◀════════════════════  [CAR]"
          @" [RED LIGHT]                 |  |                 [RED LIGHT] "
          @"                              |  |                              "
          @"             [===== MONOLITHIC BRONZE PORTAL =====]             "
          @"                    [ THE TOWER ENTRANCE ]                      "
        |]

        for step in 1..4 do
          let grid = Grid()
          grid.AddColumn(GridColumn().NoWrap()) |> ignore

          for _ in 1..2 do
            grid.AddRow(Text("")) |> ignore

          let beamColor = if step % 2 = 0 then "bold yellow on black" else "bold white on yellow3"
          let row0 = sprintf "[%s]%s[/]" beamColor (Markup.Escape cars.[0])
          let row1 = sprintf "[bold red]%s[/]" (Markup.Escape cars.[1])
          let row2 = sprintf "[grey]%s[/]" (Markup.Escape cars.[2])
          let row3 = sprintf "[bold cyan]%s[/]" (Markup.Escape cars.[3])
          let row4 = sprintf "[bold gold1]%s[/]" (Markup.Escape cars.[4])

          grid.AddRow(Markup(row0)) |> ignore
          grid.AddRow(Markup(row1)) |> ignore
          grid.AddRow(Markup(row2)) |> ignore
          grid.AddRow(Markup(row3)) |> ignore
          grid.AddRow(Markup(row4)) |> ignore
          grid.AddRow(Markup(sprintf "[grey]%s[/]" (String('=', Math.Min(consoleWidth, 75))))) |> ignore

          ctx.UpdateTarget(
            Panel(grid)
              .Header("[bold yellow] THE CROSSWALK AT THE INTERSECTION [/]")
              .Border(BoxBorder.Double)
              .BorderStyle(Style(foreground = Nullable Color.Yellow))
          )
          Thread.Sleep(300)
      )

    Thread.Sleep(500)
    AnsiConsole.MarkupLine("\n[bold white]Bumper-to-bumper cars block any retreat to the left or right.[/]")
    AnsiConsole.MarkupLine("[bold green]The pedestrian signal clicks white. The path leads forward across the street.[/]")
    AnsiConsole.MarkupLine("[bold cyan]You place your hands against the colossal bronze gates of The Tower...[/]\n")
    Thread.Sleep(1000)
    AnsiConsole.Cursor.Show()
