using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetEnv;
using Neo4j.Driver;

// 🪄 THE MAGIC LINE: Loads .env into environment variables
Env.Load();

// Helper methods (Renamed to GetEnv to avoid clashing with DotNetEnv.Env class)
static string GetEnv(string name, string fallback = "") =>
    Environment.GetEnvironmentVariable(name) ?? fallback;

static Dictionary<string, object> P(params object[] kv)   
{
    var d = new Dictionary<string, object>();
    for (int i = 0; i < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
    return d;
}

// Determine which platform to run (defaults to cognodb if no argument is passed)
var platform = args.Length > 0 ? args[0].ToLower() : "cognodb";

// Special command: generate the results report instead of running a benchmark
if (platform == "report")
{
    ReportGenerator.Generate();
    return;
}

Console.WriteLine($"Preparing dataset for {platform}...");
await Dataset.PrepareAsync();

// Route to the correct environment variables based on the platform chosen
var (uri, user, pass, dialect) = platform switch
{
    "cognodb"  => (GetEnv("COGNODB_URI"), "cognodb", GetEnv("COGNODB_PASS"), Dialect.Neo4j),
    "aura"     => (GetEnv("AURA_URI"),    GetEnv("AURA_USER", "neo4j"), GetEnv("AURA_PASS"), Dialect.Neo4j),
    "memgraph" => (GetEnv("MEMGRAPH_URI"), GetEnv("MEMGRAPH_USER"), GetEnv("MEMGRAPH_PASS"), Dialect.Memgraph),
    "docker"   => (GetEnv("DOCKER_URI"),   GetEnv("DOCKER_USER"), GetEnv("DOCKER_PASS"), Dialect.Neo4j), // <-- NEW
    // "falkor"   => (GetEnv("FALKOR_HOST") + ":" + GetEnv("FALKOR_PORT"),   GetEnv("FALKOR_USER"), GetEnv("FALKOR_PASS"), Dialect.Neo4j),
    "sandbox"  => (GetEnv("SANDBOX_URI"),    GetEnv("SANDBOX_USER", "neo4j"), GetEnv("SANDBOX_PASS"), Dialect.Neo4j), // <-- NEW
    _ => throw new ArgumentException($"unknown platform '{platform}'")
};

// Now check if the credentials for the SELECTED platform are missing
if (string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(pass))
{
    Console.WriteLine($"ERROR: Could not find credentials for {platform} in your .env file!");
    Console.WriteLine("Make sure your .env file is in the same folder as your .csproj file.");
    return;
}

var db = new BoltAdapter(platform, uri, user, pass, dialect);
await db.ConnectAsync();
Console.WriteLine($"Successfully connected to {platform}!");

var results = new List<Dictionary<string, object>>();

// 1) INGEST
await db.EnsureSchemaAsync();
var (nps, rps, total) = await db.LoadAsync();
results.Add(new() { ["metric"] = "ingest", ["nodes_per_sec"] = Math.Round(nps, 1),
                    ["rels_per_sec"] = Math.Round(rps, 1), ["total_load_sec"] = Math.Round(total, 1) });
Console.WriteLine($"ingest: {nps:F0} nodes/s | {rps:F0} rels/s | {total:F1}s total");

// 2) Indexes, then identical start-node sample on every platform
await db.CreateSecondaryIndexesAsync();
var starts = await db.SampleStartNodesAsync();
var rnd = new Random(42);

// 3) LOOKUPS
Console.WriteLine("Running Lookups...");
results.Add(await Bench.LatencyAsync("point_lookup", () => db.RunAsync(
    "MATCH (p:Paper {id:$id}) RETURN p.year AS year",
    P("id", starts[rnd.Next(starts.Count)]))));
results.Add(await Bench.LatencyAsync("filtered_lookup", () => db.RunAsync(
    "MATCH (p:Paper) WHERE p.year >= $y1 AND p.year <= $y2 RETURN count(p) AS c",
    P("y1", 1998, "y2", 2001))));

// 4) TRAVERSALS 1/2/3 hops — FIXED SYNTAX
for (int hops = 1; hops <= 3; hops++)
{
    // Build the relationship chain with anonymous intermediate nodes ()
    // 1 hop: "-[:CITES]->"
    // 2 hops: "-[:CITES]->()-[:CITES]->"
    var rel = "-[:CITES]->" + string.Concat(Enumerable.Repeat("()-[:CITES]->", hops - 1));
    var q = $"MATCH (p:Paper {{id:$id}}){rel}(q) RETURN count(q) AS c";
    
    Console.WriteLine($"  Running {hops}-hop traversal...");
    int i = 0;
    results.Add(await Bench.LatencyAsync($"hop{hops}",
        () => db.RunAsync(q, P("id", starts[(i++) % starts.Count]))));
}

// 5) AGGREGATION
Console.WriteLine("Running Aggregations...");
results.Add(await Bench.LatencyAsync("aggregation_by_field", () => db.RunAsync(
    "MATCH (p:Paper) RETURN p.field AS field, count(*) AS c ORDER BY c DESC")));

// 6) MIXED READ/WRITE with concurrency sweep 1 → 10 → 40 clients
Console.WriteLine("Running Mixed Workloads (Concurrency Sweep)...");
int wi = 0;
foreach (var clients in new[] { 1, 10, 40 })
{
    Console.WriteLine($"  Testing {clients} concurrent clients for 30 seconds...");
    results.Add(await Bench.MixedAsync($"mixed_{clients}_clients",
        read:  () => db.RunAsync("MATCH (p:Paper {id:$id}) RETURN p.year AS year",
                                 P("id", starts[Random.Shared.Next(starts.Count)])),
        write: () => db.RunAsync("MATCH (p:Paper {id:$id}) SET p.seen = true",
                                 P("id", starts[(wi++) % starts.Count])),
        clients));
}

// 7) FOOTPRINT
Console.WriteLine("Calculating Footprint...");
results.Add(await db.FootprintAsync());

Bench.Save(platform, results);
await db.DisposeAsync();
Console.WriteLine("done ✔");