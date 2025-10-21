open System
open MongoDB.Bson
open MongoDB.Driver

// Connect directly to your MongoDB Atlas cluster
let connectionString = "mongodb+srv://db_user:dbUserPassword@cluster0.z1ihkte.mongodb.net/?retryWrites=true&w=majority&appName=Cluster0"
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
        BsonElement("insertedAt", BsonDateTime(DateTime.UtcNow))
    ])

// CRUD operations
let insertPair pair =
    try
        collection.InsertOne(makeDocument pair)
        printfn "Inserted %s" (normalize pair)
    with
    | :? MongoWriteException as ex when ex.WriteError.Category = ServerErrorCategory.DuplicateKey ->
        printfn "%s already exists" (normalize pair)
    | e -> printfn "Insert error: %s" e.Message

let readAll () =
    collection.Find(FilterDefinition<BsonDocument>.Empty).ToList()
    |> Seq.map (fun d -> d.GetValue("pair").AsString)
    |> Seq.toList

let updatePair pair =
    let filter = Builders<BsonDocument>.Filter.Eq("pair", BsonString(normalize pair))
    let update =
        Builders<BsonDocument>.Update
            .Set("updatedAt", BsonDateTime(DateTime.UtcNow))
    collection.UpdateOne(filter, update) |> ignore
    printfn "Updated %s" (normalize pair)

let deletePair pair =
    let filter = Builders<BsonDocument>.Filter.Eq("pair", BsonString(normalize pair))
    let result = collection.DeleteOne(filter)
    if result.DeletedCount = 1L then
        printfn "Deleted %s" (normalize pair)
    else
        printfn "No record found for %s" (normalize pair)

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