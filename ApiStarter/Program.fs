open System
open System.Net.Http
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers
open FSharp.Data

// Helper to send JSON responses
let jsonResponse o =
    let text = System.Text.Json.JsonSerializer.Serialize(o)
    OK text >=> setHeader "Content-Type" "application/json"

// Function to fetch data from a URL
let httpGet (url: string) = async {
    use client = new HttpClient()
    client.Timeout <- TimeSpan.FromSeconds 10.0
    let! resp = client.GetAsync(url) |> Async.AwaitTask
    let! body = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
    if resp.IsSuccessStatusCode then return Ok body
    else return Error (sprintf "HTTP %d: %s" (int resp.StatusCode) body)
}

// Parse Kraken JSON response to extract currency pairs
let parseKrakenPairs (raw: string) =
    let root = JsonValue.Parse raw
    match root.TryGetProperty "result" with
    | Some result ->
        result.Properties()
        |> Seq.choose (fun (_, v) ->
            match v.TryGetProperty "wsname" with
            | Some w ->
                match w.AsString().Split('/') with
                | [| a; b |] -> Some $"{a}-{b}"
                | _ -> None
            | None -> None)
        |> Seq.distinct
        |> Seq.toArray
    | None ->
        [||]

// Function to call Kraken API and parse response
let getKrakenPairs () = async {
    let url = "https://api.kraken.com/0/public/AssetPairs"
    let! res = httpGet url
    return
        match res with
        | Ok body -> Ok (parseKrakenPairs body)
        | Error e -> Error e
}

[<EntryPoint>]
let main _ =
    // Check connectivity on startup
    printfn "Connecting to Kraken..."
    let result = getKrakenPairs () |> Async.RunSynchronously
    match result with
    | Ok pairs when pairs.Length > 0 ->
        printfn "Kraken connected successfully. Found %d pairs. First 5:" pairs.Length
        pairs |> Array.truncate 5 |> Array.iter (printfn " - %s")
    | Ok _ ->
        printfn "No pairs found from Kraken."
    | Error e ->
        printfn "Failed to connect to Kraken: %s" e

    // Define REST endpoints
    let app =
        choose [
            GET >=> path "/healthz" >=> OK "ok"
            GET >=> path "/pairs/kraken" >=> fun ctx ->
                async {
                    let! res = getKrakenPairs ()
                    match res with
                    | Ok pairs -> return! jsonResponse {| exchange = "kraken"; count = pairs.Length; pairs = pairs |} ctx
                    | Error e -> return! ServerErrors.SERVICE_UNAVAILABLE e ctx
                }
        ]

    printfn "Server running at http://localhost:8080"
    startWebServer { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 8080 ] } app
    0