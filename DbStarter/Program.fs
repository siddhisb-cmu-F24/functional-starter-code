open System
open MongoDB.Bson
open MongoDB.Driver

// Connect directly to your MongoDB Atlas cluster
let connectionString =
    match Environment.GetEnvironmentVariable("MONGODB_URI") with
    | null | "" -> "mongodb+srv://db_user:dbUserPassword@cluster0.z1ihkte.mongodb.net/?retryWrites=true&w=majority&appName=Cluster0" // TODO: replace
    | s -> s
    
let client = new MongoClient(connectionString)
let db = client.GetDatabase("arbitragegainer")
let collection = db.GetCollection<BsonDocument>("cross_traded_pairs")

// Ensure a unique index on the "pair" field
let ensureUniqueIndex () =
    let keys = Builders<BsonDocument>.IndexKeys.Ascending("pair")
    let options = CreateIndexOptions(Name = "uniq_pair", Unique = Nullable true)
    collection.Indexes.CreateOne(CreateIndexModel<BsonDocument>(keys, options)) |> ignore

// Helper functions
let normalize (s: string) = s.Trim().ToUpperInvariant()
let makeDocument (pair: string) =
    BsonDocument([
        BsonElement("pair", BsonString(normalize pair))
        BsonElement("createdAt", BsonDateTime(DateTime.UtcNow))
    ])

// CRUD operations
let insertPair pair =
    try
    //For production, recommended switch to InsertOneAsync/UpdateOneAsync + F# task/async
        collection.InsertOne(makeDocument pair)
        true
    with
    | :? MongoWriteException as ex when ex.WriteError.Category = ServerErrorCategory.DuplicateKey -> false

let readAll () =
    collection.Find(FilterDefinition<BsonDocument>.Empty).ToList()
    |> Seq.map (fun d -> d.GetValue("pair").AsString)
    |> Seq.toList

let updatePair pair =
    let filter = Builders<BsonDocument>.Filter.Eq("pair", normalize pair)
    let update =
        Builders<BsonDocument>.Update
            .Set("updatedAt", BsonDateTime(DateTime.UtcNow))
    let res = collection.UpdateOne(filter, update)
    res.MatchedCount > 0L

let deletePair pair =
    let filter = Builders<BsonDocument>.Filter.Eq("pair", normalize pair)
    let res = collection.DeleteOne(filter)
    res.DeletedCount = 1L

// Demo
[<EntryPoint>]
let main _ =
    printfn "Connecting to MongoDB Atlas..."
    ensureUniqueIndex()

    insertPair "BTC-USD"
    insertPair "ETH-USD"
    insertPair "CHZ-USD"

    printfn "\nAll pairs in DB:"
    readAll () |> List.iter (printfn " - %s")

    updatePair "BTC-USD"
    deletePair "ETH-USD"

    printfn "\nRemaining pairs:"
    readAll () |> List.iter (printfn " - %s")


    0
