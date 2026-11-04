namespace Fornach.Cli

open System
open System.Threading
open Spectre.Console

module DeathAnimation =

  /// Heavy cab-over box truck ("Truck-kun") driving LEFT.
  /// Kept free of raw single brackets (`[` / `]`) so Spectre.Console Markup never misinterprets art as style tags;
  /// literal brackets use doubled `[[` and `]]`.
  let truckBodyFrames (step: int) : string array =
    let wheel = if step % 4 < 2 then "(O)" else "(@)"
    let horn =
      if step % 4 < 2 then "[[==HONK!!==]]  ~ ░"
      else "[[==BLAAARE!==]] ░▒"

    [| @"      ___________________________            "
       @"   __/   |  [[FORNACH EXPRESS]]  |           "
       @"  |[[  ]]|   HEAVY FREIGHT       |  " + horn
       @"  |______|_______________________|           "
       sprintf @"  '-%s---%s-----------%s---%s'   ~ ░▒▓   " wheel wheel wheel wheel |]

  /// Generates a diagonal rain line across the sky for the given animation frame
  let private rainLine (width: int) (step: int) (rowSalt: int) : string =
    let usable = Math.Max(10, width - 4)
    let chars =
      Array.init usable (fun col ->
        let h = (col * 7 + step * 3 + rowSalt * 13) % 19
        if h = 0 then '/'
        elif h = 5 then '\''
        elif h = 11 then '.'
        else ' ')
    sprintf "[#6272a4]%s[/]" (String(chars))

  /// Two-lane wet asphalt road surface with dashed center divider
  let private roadSurface (consoleWidth: int) (step: int) : string * string =
    let w = Math.Max(10, consoleWidth - 4)
    let divider =
      Array.init w (fun i -> if ((i + step) % 6) < 3 then '-' else ' ')
      |> String
    let asphalt = String('═', w)
    sprintf "[bold #f1fa8c]%s[/]" divider, sprintf "[#6272a4]%s[/]" asphalt

  /// Multi-frame impact and windshield shatter sequence
  let impactSequenceFrames : (string * string * string * string) array =
    [| ( @"        \   *   |   *   /        ",
         @"    * ── [[ C R A S H ! ! ]] ── * ",
         @"        /   *   |   *   \        ",
         "bold #f1fa8c on #ff5555" )
       ( @"   ✦  \  .  *  / \  *  .  /  ✦   ",
         @"  ── ✦ [[ SHATTERED GLASS ]] ✦ ── ",
         @"   ✦  /  '  *  \ /  *  '  \  ✦   ",
         "bold white on #ff5555" )
       ( @"  .  *    /     |     \    *  .  ",
         @"     ((  TRAUMA IMPACT!!  ))     ",
         @"  '  *    \     |     /    *  '  ",
         "bold #ffb86c on #44475a" ) |]

  /// Plays the Truck-kun replay sequence across the terminal width
  let playTruckReplay () =
    AnsiConsole.Clear()
    AnsiConsole.Cursor.Hide()

    let consoleWidth = Math.Max(70, AnsiConsole.Profile.Width)
    let playerPos = 8
    let truckWidth = 46
    let startPos = Math.Max(playerPos + 6, consoleWidth - truckWidth - 2)

    // Phase 1 & 2: High-speed storm approach + multi-frame impact shatter
    AnsiConsole
      .Live(Panel(Text("")))
      .AutoClear(false)
      .Overflow(VerticalOverflow.Crop)
      .Start(fun ctx ->
        let mutable currentTruckPos = startPos
        let mutable step = 0

        while currentTruckPos > playerPos + 4 do
          let grid = Grid()
          grid.AddColumn(GridColumn().NoWrap()) |> ignore

          // Atmospheric storm sky
          grid.AddRow(Markup(rainLine consoleWidth step 1)) |> ignore
          grid.AddRow(Markup(rainLine consoleWidth step 2)) |> ignore

          let frames = truckBodyFrames step
          let gap = Math.Max(0, currentTruckPos - playerPos - 5)
          let playerPad = String(' ', playerPos)
          let isClose = gap < 18

          // Player reactions & halogen headlight cone
          let playerHead = if isClose then "[bold #ff5555]@_@[/]" else "[bold #f1fa8c] o [/]"
          let playerTorso = if isClose then "[bold #f1fa8c]\\|/[/]" else "[bold #8be9fd]/|\\[/]"
          let playerLegs = "[bold #f8f8f2]/ \\[/]"

          let beamLen = Math.Min(gap, 16)
          let darkGap = Math.Max(0, gap - beamLen)
          let upperBeam =
            if beamLen > 0 then
              sprintf "%s[bold #f1fa8c]%s[/]" (String(' ', darkGap)) (String('/', beamLen))
            else
              String(' ', gap)
          let mainBeam =
            if beamLen > 0 then
              let beamColor = if isClose then "bold #282a36 on #f1fa8c" else "bold #f1fa8c"
              sprintf "%s[%s]%s[/]" (String(' ', darkGap)) beamColor (String('═', beamLen))
            else
              String(' ', gap)
          let lowerBeam =
            if beamLen > 0 then
              sprintf "%s[bold #ffb86c]%s[/]" (String(' ', darkGap)) (String('\\', beamLen))
            else
              String(' ', gap)

          let row0 = sprintf "%s    %s[bold #f8f8f2]%s[/]" playerPad (String(' ', gap)) frames.[0]
          let row1 = sprintf "%s    %s[bold #ff5555]%s[/]" playerPad upperBeam frames.[1]
          let row2 = sprintf "%s%s %s[bold #f8f8f2 on #ff5555]%s[/]" playerPad playerHead mainBeam frames.[2]
          let row3 = sprintf "%s%s %s[bold #ff5555]%s[/]" playerPad playerTorso lowerBeam frames.[3]
          let row4 = sprintf "%s%s %s[bold #6272a4]%s[/]" playerPad playerLegs (String(' ', gap)) frames.[4]

          grid.AddRow(Markup row0) |> ignore
          grid.AddRow(Markup row1) |> ignore
          grid.AddRow(Markup row2) |> ignore
          grid.AddRow(Markup row3) |> ignore
          grid.AddRow(Markup row4) |> ignore

          let divider, asphalt = roadSurface consoleWidth step
          grid.AddRow(Markup divider) |> ignore
          grid.AddRow(Markup asphalt) |> ignore

          let headerText =
            if isClose then "[bold #ff5555] ⚠ SCREEEEECH!! HIGH BEAMS IN YOUR LANE!! ⚠ [/]"
            else "[bold #8be9fd] 🌧️ RAIN-SLICKED CROSSING ── ONCOMING HEADLIGHTS [/]"

          ctx.UpdateTarget(
            Panel(grid)
              .Header(headerText)
              .Border(BoxBorder.Heavy)
              .BorderColor(if isClose then Theme.ColorRed else Theme.ColorComment)
          )

          Thread.Sleep 28
          // Truck accelerates as it closes in
          let stride = if isClose then 4 else 3
          currentTruckPos <- currentTruckPos - stride
          step <- step + 1

        // Phase 2: Multi-frame Impact & Shattered Windshield
        for i in 0 .. impactSequenceFrames.Length - 1 do
          let f0, f1, f2, styleTag = impactSequenceFrames.[i]
          let grid = Grid()
          grid.AddColumn(GridColumn().NoWrap()) |> ignore

          grid.AddRow(Markup(rainLine consoleWidth (step + i) 3)) |> ignore
          grid.AddRow(Text("")) |> ignore

          let pad = String(' ', Math.Max(2, playerPos - 2))
          grid.AddRow(Markup(sprintf "%s[%s]%s[/]" pad styleTag f0)) |> ignore
          grid.AddRow(Markup(sprintf "%s[%s]%s[/]" pad styleTag f1)) |> ignore
          grid.AddRow(Markup(sprintf "%s[%s]%s[/]" pad styleTag f2)) |> ignore
          grid.AddRow(Text("")) |> ignore

          let _, asphalt = roadSurface consoleWidth (step + i)
          grid.AddRow(Markup(sprintf "[bold #ff5555]%s[/]" (String('═', Math.Max(10, consoleWidth - 4))))) |> ignore
          grid.AddRow(Markup asphalt) |> ignore

          ctx.UpdateTarget(
            Panel(grid)
              .Header("[bold #f8f8f2 on #ff5555] ☠ FATAL COLLISION ── WINDSHIELD SHATTERED ☠ [/]")
              .Border(BoxBorder.Double)
              .BorderColor(Theme.ColorRed)
          )
          Thread.Sleep 180)

    // Phase 3: Blinding Halogen Whiteout Flash
    Thread.Sleep 80
    AnsiConsole.Clear()
    let whiteLine = String(' ', Math.Max(20, consoleWidth - 2))
    for _ in 1..10 do
      AnsiConsole.MarkupLine(sprintf "[on white]%s[/]" whiteLine)
    Thread.Sleep 220

    // Phase 4: ICU Monitor Flatline & Trauma Loop Rewind
    AnsiConsole.Clear()
    AnsiConsole.WriteLine()
    AnsiConsole.MarkupLine(sprintf "  [bold #50fa7b]ICU MONITOR:[/] [bold #ff5555]───/\\────/\\────────────────────────── [[ FLATLINE ]][/]")
    Thread.Sleep 450
    AnsiConsole.MarkupLine(sprintf "  [italic #6272a4]The blaring horn dissolves into cold, driving rain and shattering glass...[/]")
    Thread.Sleep 550
    AnsiConsole.MarkupLine(sprintf "  [bold #bd93f9]⏪ TRAUMA LOOP ACTIVATED:[/] [bold #f1fa8c]Truck-kun claims another victim. Rewinding timeline...[/]\n")
    Thread.Sleep 850
    AnsiConsole.Cursor.Show()
