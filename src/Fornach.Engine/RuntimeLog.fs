namespace Fornach.Engine

open System
open System.IO
open System.Text

/// Verbose runtime logging for troubleshooting. Every call is a cheap no-op unless
/// `enabled` has been set by `begin`, so instrumentation can live permanently on hot
/// paths without changing gameplay when the run was not launched with --verbose/--log.
module RuntimeLog =

  /// Master switch, set once by `begin`.
  let mutable enabled = false

  /// Path of the active session log (logs/runtime_<stamp>.log), or "" before `begin`.
  let mutable path = ""

  /// Path of the always-overwritten mirror (logs/runtime_latest.log), or "" before `begin`.
  let mutable private mirrorPath = ""

  let mutable private stream : StreamWriter option = None
  let mutable private streamPath = ""

  let private stamp () = DateTime.Now.ToString("HH:mm:ss.fff")

  /// Returns the append-mode writer for the current `path`, reopening it whenever the
  /// active session file changes. AutoFlush keeps the newest line on disk even if the
  /// process is killed mid-run (the freeze this tooling is meant to diagnose).
  let private sessionStream () =
    if path = "" then
      None
    else
      if streamPath <> path then
        match stream with
        | Some s -> s.Dispose()
        | None -> ()
        let s = new StreamWriter(path, append = true, encoding = Encoding.UTF8)
        s.AutoFlush <- true
        stream <- Some s
        streamPath <- path
      stream

  /// Appends one timestamped line to the session log and the latest mirror.
  /// No-op unless logging is enabled.
  let write (msg: string) : unit =
    if enabled then
      let line = sprintf "[%s] %s" (stamp ()) msg
      match sessionStream () with
      | Some s -> s.WriteLine line
      | None -> ()
      if mirrorPath <> "" then
        try File.AppendAllText(mirrorPath, line + Environment.NewLine)
        with _ -> ()

  /// Writes a banner line marking the start of a logical section.
  let section (title: string) : unit =
    write (sprintf "── %s ──" title)

  /// Writes an exception with its message and full stack trace.
  let exn (ctx: string) (e: exn) : unit =
    write (sprintf "%s :: %s: %s" ctx (e.GetType().FullName) e.Message)
    if not (isNull e.StackTrace) then
      write e.StackTrace

  /// Opens a fresh session log under ./logs, mirrors the header into
  /// logs/runtime_latest.log and returns the session log path.
  let ``begin`` (args: string array) : string =
    let dir = Path.Combine(Environment.CurrentDirectory, "logs")
    Directory.CreateDirectory dir |> ignore
    let sessionPath =
      Path.Combine(dir, sprintf "runtime_%s.log" (DateTime.Now.ToString("yyyyMMdd_HHmmss")))
    path <- sessionPath
    mirrorPath <- Path.Combine(dir, "runtime_latest.log")
    enabled <- true
    // Drop any writer from a previous session so this run starts a fresh file.
    match stream with
    | Some s -> s.Dispose()
    | None -> ()
    stream <- None
    streamPath <- ""
    let header =
      [ "=== FORNACH RUNTIME LOG ==="
        sprintf "Started: %s" (DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
        sprintf "Args: %s" (String.Join(" ", args))
        sprintf "Cwd: %s" Environment.CurrentDirectory
        "===========================" ]
      |> String.concat Environment.NewLine
    File.WriteAllText(sessionPath, header + Environment.NewLine)
    try File.WriteAllText(mirrorPath, header + Environment.NewLine) with _ -> ()
    sessionPath
