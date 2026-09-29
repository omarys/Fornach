namespace Fornach.Cli

open System
open System.Threading
open Spectre.Console

module DeathAnimation =

  let private truckFrames =
    [| @"   ______                    "
       @"  /|_||_\`.__                "
       @" (   _    _ _\  [==HONK!==]  "
       @" =`-(_)--(_)-'               " |]

  let private impactFrames =
    [| @"      \  |  /      "
       @"    -- CRASH! --   "
       @"      /  |  \      " |]

  /// Plays the Truck-kun replay sequence across the terminal width
  let playTruckReplay () =
    AnsiConsole.Clear()
    AnsiConsole.Cursor.Hide()

    let consoleWidth = Math.Max(60, AnsiConsole.Profile.Width)
    let playerPos = 8
    let truckWidth = 30
    let totalSteps = consoleWidth - playerPos

    // Phase 1: High-speed approach
    AnsiConsole
      .Live(Panel(Text("")))
      .AutoClear(false)
      .Overflow(VerticalOverflow.Crop)
      .Start(fun ctx ->
        for step in 0..2..totalSteps do
          let currentTruckPos = consoleWidth - step - truckWidth
          let grid = Grid()
          grid.AddColumn(GridColumn().NoWrap()) |> ignore

          // Empty buffer rows to vertically center the road
          for _ in 1..3 do
            grid.AddRow(Text("")) |> ignore

          if currentTruckPos > playerPos + 4 then
            // Truck hurtling towards player
            let padLeft = Math.Max(0, currentTruckPos)
            let playerPad = playerPos

            let row0 =
              sprintf
                "%s[bold red]@_@[/] %s%s"
                (String(' ', playerPad))
                (String(' ', Math.Max(0, padLeft - playerPad - 4)))
                truckFrames.[0]

            let row1 =
              sprintf
                "%s |   %s[bold white on red]%s[/]"
                (String(' ', playerPad))
                (String(' ', Math.Max(0, padLeft - playerPad - 4)))
                truckFrames.[1]

            let row2 =
              sprintf
                "%s/ \\  %s[bold yellow]%s[/]"
                (String(' ', playerPad))
                (String(' ', Math.Max(0, padLeft - playerPad - 4)))
                truckFrames.[2]

            let row3 =
              sprintf
                "%s     %s[bold grey]%s[/]"
                (String(' ', playerPad))
                (String(' ', Math.Max(0, padLeft - playerPad - 4)))
                truckFrames.[3]

            grid.AddRow(Markup row0) |> ignore
            grid.AddRow(Markup row1) |> ignore
            grid.AddRow(Markup row2) |> ignore
            grid.AddRow(Markup row3) |> ignore
            grid.AddRow(Markup(sprintf "[grey]%s[/]" (String('=', consoleWidth))))
            |> ignore

            ctx.UpdateTarget(Panel(grid).Border(BoxBorder.None))
            Thread.Sleep 25
          else
            // Phase 2: Instant Impact
            grid.AddRow(
              Markup(
                sprintf
                  "%s[bold yellow on red]%s[/]"
                  (String(' ', playerPos))
                  impactFrames.[0]
              )
            )
            |> ignore

            grid.AddRow(
              Markup(
                sprintf
                  "%s[bold white on red]%s[/]"
                  (String(' ', playerPos))
                  impactFrames.[1]
              )
            )
            |> ignore

            grid.AddRow(
              Markup(
                sprintf
                  "%s[bold yellow on red]%s[/]"
                  (String(' ', playerPos))
                  impactFrames.[2]
              )
            )
            |> ignore

            grid.AddRow(
              Markup(sprintf "[bold red]%s[/]" (String('=', consoleWidth)))
            )
            |> ignore

            ctx.UpdateTarget(Panel(grid).Border(BoxBorder.None))
            Thread.Sleep 200)

    // Phase 3: Blinding White Screen Flash
    Thread.Sleep 100
    AnsiConsole.Clear()
    let whiteLine = String(' ', consoleWidth)

    for _ in 1..12 do
      AnsiConsole.MarkupLine(sprintf "[on white]%s[/]" whiteLine)

    Thread.Sleep 400

    // Phase 4: Fade to Rain & Charcoal Mist
    AnsiConsole.Clear()
    AnsiConsole.MarkupLine
      "[grey]The blaring horn fades into cold, driving rain...[/]"

    Thread.Sleep 800
    AnsiConsole.MarkupLine
      "[bold red]Truck-kun claims another victim. Rewinding timeline...[/]\n"

    Thread.Sleep 1000
    AnsiConsole.Cursor.Show()
