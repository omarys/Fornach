module Fornach.Tests.MenuCancelTests

open System
open System.Threading
open System.Threading.Tasks
open Xunit
open Spectre.Console
open Fornach.Cli

/// Feeds one canned key, then reports "no key" forever.
type private StubInput(key: ConsoleKeyInfo) =
  let mutable spent = false

  interface IAnsiConsoleInput with
    member _.IsKeyAvailable() = not spent
    member _.ReadKey(_intercept: bool) =
      spent <- true
      Nullable key
    member _.ReadKeyAsync(_intercept: bool, _ct: CancellationToken) =
      spent <- true
      Task.FromResult(Nullable key)

let private escape = ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false)

[<Fact>]
let ``escape aborts a selection prompt by raising MenuCancelled`` () =
  let input = StubInput escape :> IAnsiConsoleInput
  let vim = VimInput(input) :> IAnsiConsoleInput

  Assert.Throws<MenuCancelled>(fun () -> vim.ReadKey(true) |> ignore) |> ignore

[<Fact>]
let ``escape aborts the async read path too`` () =
  let input = StubInput escape :> IAnsiConsoleInput
  let vim = VimInput(input) :> IAnsiConsoleInput

  let read () =
    (vim.ReadKeyAsync(true, CancellationToken.None)).GetAwaiter().GetResult() |> ignore

  Assert.Throws<MenuCancelled>(read) |> ignore

[<Fact>]
let ``typed j and k survive when vim navigation is off`` () =
  // The custom combatant builder feeds TextPrompt through the same adapter; translating j/k
  // into arrows there would make it impossible to type a name containing them.
  let j = ConsoleKeyInfo('j', ConsoleKey.J, false, false, false)
  let plain = VimInput(StubInput j :> IAnsiConsoleInput, false, false) :> IAnsiConsoleInput

  Assert.Equal(j, plain.ReadKey(true).Value)

[<Fact>]
let ``j still navigates down when vim navigation is on`` () =
  let j = ConsoleKeyInfo('j', ConsoleKey.J, false, false, false)
  let vim = VimInput(StubInput j :> IAnsiConsoleInput, false, true) :> IAnsiConsoleInput

  Assert.Equal(ConsoleKey.DownArrow, vim.ReadKey(true).Value.Key)
