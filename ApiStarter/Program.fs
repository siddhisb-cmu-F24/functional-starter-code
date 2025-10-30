open System
open System.Net.Http
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers

let krakenAssetPairsUrl =
    match Environment.GetEnvironmentVariable "KRAKEN_ASSET_PAIRS_URL" with
    | null
    | "" ->
        // TODO: inject via config/env for production deployments
        "https://api.kraken.com/0/public/AssetPairs"
    | url -> url

// Function to fetch data from a URL
// Note: no JSON libraries here; we return the raw Kraken payload
let httpGet (url: string) = async {
    use client = new HttpClient()
    client.Timeout <- TimeSpan.FromSeconds 10.0
    // sends the HTTP request, waits for network response headers
    let! resp = client.GetAsync(url) |> Async.AwaitTask
    // waits for full body stream of the response and reads it
    let! body = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
    return
        match resp.IsSuccessStatusCode with
        | true -> Ok body
        | false -> Error (sprintf "HTTP %d: %s" (int resp.StatusCode) body)
}

[<EntryPoint>]
let main _ =
    let app =
        choose [
        // Compose WebParts sequentially: >=> is Suave's equivalent of the >> operator for functions
            GET >=> path "/healthz" >=> OK "ok"
            GET >=> path "/pairs/kraken" >=> fun ctx ->
                async {
                    let! outcome = httpGet krakenAssetPairsUrl
                    match outcome with
                    | Ok body ->
                        return!
                            (OK body
                             >=> setHeader "Content-Type" "application/json") ctx
                    | Error error ->
                        return! ServerErrors.SERVICE_UNAVAILABLE error ctx
                }
        ]

    printfn "Server running at http://localhost:8080"
    startWebServer { defaultConfig with bindings = [ HttpBinding.createSimple HTTP "0.0.0.0" 8080 ] } app
    0
