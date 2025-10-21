This repository contains two independent starter files:
	•	DbStarter: demonstrates how to connect to a MongoDB database, perform simple insert/read/update/delete operations, and print results to the console.
	•	ApiStarter: demonstrates how to make HTTP requests, parse JSON, and expose simple REST endpoints using the Suave web framework.

Under DbStarter and ApiStarter use dotnet run to run the Program.fs file.

Packages:

dotnet new console -lang "F#" -n DbStarter
cd DbStarter
dotnet add package MongoDB.Driver

dotnet new console -lang "F#" -n ApiStarter
cd ApiStarter
dotnet add package Suave
dotnet add package FSharp.Data
