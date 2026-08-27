using System.Diagnostics;
using Neo4j.Driver;

public enum Dialect { Neo4j, Memgraph }   // Cypher dialects differ slightly for indexes

public class BoltAdapter : IAsyncDisposable
{
    readonly IDriver _driver;
    public string Name { get; }
    public Dialect Dialect { get; }

    public BoltAdapter(string name, string uri, string user, string pass, Dialect dialect)
    {
        Name = name; Dialect = dialect;
        _driver = string.IsNullOrEmpty(user)
            ? GraphDatabase.Driver(uri)
            : GraphDatabase.Driver(uri, AuthTokens.Basic(user, pass));
    }

    public Task ConnectAsync() => _driver.VerifyConnectivityAsync();

    public async Task RunAsync(string query, Dictionary<string, object>? parameters = null) =>
        await _driver.ExecutableQuery(query)
                     .WithParameters(parameters ?? new Dictionary<string, object>())
                     .ExecuteAsync();

    async Task TryRun(string q)
    {
        try { await RunAsync(q); }
        catch (Exception e) { Console.WriteLine($"  note: {e.Message.Split('\n')[0]}"); }
    }

        public async Task EnsureSchemaAsync()
    {
        await RunAsync("MATCH (n) DETACH DELETE n");  // clean slate every run

        if (Dialect == Dialect.Neo4j)
        {
            await TryRun("CREATE CONSTRAINT paper_id IF NOT EXISTS FOR (p:Paper) REQUIRE p.id IS UNIQUE");
        }
        else if (Dialect == Dialect.Memgraph)
        {
            // Memgraph requires "implicit" (auto-commit) transactions for schema changes.
            var session = _driver.AsyncSession();
            try
            {
                await session.RunAsync("CREATE INDEX ON :Paper(id);");
            }
            catch (Exception e) { Console.WriteLine($"  note: {e.Message.Split('\n')[0]}"); }
            finally
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task CreateSecondaryIndexesAsync()
    {
        if (Dialect == Dialect.Neo4j)
        {
            await TryRun("CREATE INDEX paper_year IF NOT EXISTS FOR (p:Paper) ON (p.year)");
            await Task.Delay(3000); 
        }
        else if (Dialect == Dialect.Memgraph)
        {
            var session = _driver.AsyncSession();
            try
            {
                await session.RunAsync("CREATE INDEX ON :Paper(year);");
            }
            catch (Exception e) { Console.WriteLine($"  note: {e.Message.Split('\n')[0]}"); }
            finally
            {
                await session.CloseAsync();
            }
        }
    }

    public async Task<(double nodesPerSec, double relsPerSec, double totalSec)> LoadAsync()
    {
        var nodes = Dataset.ReadNodes();
        var edges = Dataset.ReadEdges();
        var sw = Stopwatch.StartNew();

        // Batched UNWIND: 2,000 rows per request instead of 1 round-trip per node
        foreach (var batch in nodes.Chunk(2000))
            await RunAsync("UNWIND $rows AS r CREATE (p:Paper {id: r.id, year: r.year, field: r.field})",
                           new() { ["rows"] = batch });
        double nodeSecs = sw.Elapsed.TotalSeconds;
        Console.WriteLine($"  {nodes.Count} nodes in {nodeSecs:F1}s");

        foreach (var batch in edges.Chunk(2000))
            await RunAsync("UNWIND $rows AS r MATCH (a:Paper {id:r.src}), (b:Paper {id:r.dst}) CREATE (a)-[:CITES]->(b)",
                           new() { ["rows"] = batch });
        sw.Stop();
        double edgeSecs = sw.Elapsed.TotalSeconds - nodeSecs;
        return (nodes.Count / nodeSecs, edges.Count / edgeSecs, sw.Elapsed.TotalSeconds);
    }

    // Same random start nodes on every platform (fixed seed = fairness!)
    public async Task<List<int>> SampleStartNodesAsync(int count = 50, int seed = 42)
    {
        var res = await _driver.ExecutableQuery("MATCH (p:Paper) RETURN p.id AS id").ExecuteAsync();
        var ids = res.Result.Select(r => r["id"].As<int>()).ToList();
        var rng = new Random(seed);
        return ids.OrderBy(_ => rng.Next()).Take(count).ToList();
    }

    public async Task<Dictionary<string, object>> FootprintAsync()
    {
        var fp = new Dictionary<string, object>();
        var n = await _driver.ExecutableQuery("MATCH (n) RETURN count(n) AS c").ExecuteAsync();
        var r = await _driver.ExecutableQuery("MATCH ()-[x]->() RETURN count(x) AS c").ExecuteAsync();
        fp["node_count"] = n.Result[0]["c"].As<long>(); // <--- FIXED (Result instead of Records)
        fp["rel_count"] = r.Result[0]["c"].As<long>(); 
        if (Dialect == Dialect.Memgraph)
            try
            {
                var si = await _driver.ExecutableQuery("SHOW STORAGE INFO").ExecuteAsync();
                foreach (var rec in si.Result)
                    fp[$"storage.{rec["key"]}"] = rec["value"]?.ToString() ?? "";
            }
            catch { fp["storage"] = "not observable via query"; }
        return fp;
    }

    public ValueTask DisposeAsync() => new(_driver.DisposeAsync().AsTask());
}