using System.Text;
using System.Text.Json.Nodes;

public static class ReportGenerator
{
    // Holds one platform's parsed JSON
    record PlatformData(string Name, JsonNode Root);

    public static void Generate(string resultsDir = "results", string outputFile = "RESULTS.md")
    {
        if (!Directory.Exists(resultsDir))
        {
            Console.WriteLine($"No '{resultsDir}' folder found. Run some benchmarks first!");
            return;
        }

        var files = Directory.GetFiles(resultsDir, "*.json");
        if (files.Length == 0)
        {
            Console.WriteLine("No JSON result files found. Run a benchmark first!");
            return;
        }

        // Load every platform's JSON
        var platforms = new List<PlatformData>();
        foreach (var f in files)
        {
            try
            {
                var root = JsonNode.Parse(File.ReadAllText(f));
                var name = root?["platform"]?.GetValue<string>() ?? Path.GetFileNameWithoutExtension(f);
                platforms.Add(new PlatformData(name, root!));
            }
            catch (Exception e)
            {
                Console.WriteLine($"  Warning: could not parse {f}: {e.Message}");
            }
        }

        // Put CognoDB first (it's the subject), then sort the rest alphabetically
        platforms = platforms
            .OrderByDescending(p => p.Name == "cognodb")
            .ThenBy(p => p.Name)
            .ToList();

        Console.WriteLine($"Found {platforms.Count} platforms: {string.Join(", ", platforms.Select(p => p.Name))}");

        var sb = new StringBuilder();
        sb.AppendLine("# 📊 Benchmark Results Matrix");
        sb.AppendLine();
        sb.AppendLine($"_Auto-generated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC from {platforms.Count} platform result files._");
        sb.AppendLine();

        AppendIngestTable(sb, platforms);
        AppendTraversalTable(sb, platforms);
        AppendLookupTable(sb, platforms);
        AppendAggregationTable(sb, platforms);
        AppendMixedTable(sb, platforms);
        AppendFootprintTable(sb, platforms);

        File.WriteAllText(outputFile, sb.ToString());
        Console.WriteLine($"\n✅ Report written to {outputFile}");
        Console.WriteLine("Copy its contents into your README.md results section.");
    }

    // ---------- Table builders ----------

    static void AppendIngestTable(StringBuilder sb, List<PlatformData> platforms)
    {
        sb.AppendLine("## 1. Data Loading (Ingest Throughput)");
        sb.AppendLine();
        sb.AppendLine("| Platform | Nodes/sec | Relationships/sec | Total Load Time (s) |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var p in platforms)
        {
            var m = FindMetric(p, "ingest");
            sb.AppendLine($"| {p.Name} | {Num(m, "nodes_per_sec")} | {Num(m, "rels_per_sec")} | {Num(m, "total_load_sec")} |");
        }
        sb.AppendLine();
    }

    static void AppendTraversalTable(StringBuilder sb, List<PlatformData> platforms)
    {
        sb.AppendLine("## 2. Traversal Latency (ms)");
        sb.AppendLine();
        sb.AppendLine("| Platform | 1-hop p50 | 1-hop p95 | 2-hop p50 | 2-hop p95 | 3-hop p50 | 3-hop p95 |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (var p in platforms)
        {
            var h1 = FindMetric(p, "hop1");
            var h2 = FindMetric(p, "hop2");
            var h3 = FindMetric(p, "hop3");
            sb.AppendLine($"| {p.Name} | {Num(h1, "p50_ms")} | {Num(h1, "p95_ms")} | {Num(h2, "p50_ms")} | {Num(h2, "p95_ms")} | {Num(h3, "p50_ms")} | {Num(h3, "p95_ms")} |");
        }
        sb.AppendLine();
    }

    static void AppendLookupTable(StringBuilder sb, List<PlatformData> platforms)
    {
        sb.AppendLine("## 3. Lookup Latency (ms)");
        sb.AppendLine();
        sb.AppendLine("| Platform | Point p50 | Point p95 | Filtered p50 | Filtered p95 |");
        sb.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var p in platforms)
        {
            var pt = FindMetric(p, "point_lookup");
            var fl = FindMetric(p, "filtered_lookup");
            sb.AppendLine($"| {p.Name} | {Num(pt, "p50_ms")} | {Num(pt, "p95_ms")} | {Num(fl, "p50_ms")} | {Num(fl, "p95_ms")} |");
        }
        sb.AppendLine();
    }

    static void AppendAggregationTable(StringBuilder sb, List<PlatformData> platforms)
    {
        sb.AppendLine("## 4. Aggregation Latency (ms)");
        sb.AppendLine();
        sb.AppendLine("| Platform | Group-by p50 | Group-by p95 |");
        sb.AppendLine("|---|---:|---:|");
        foreach (var p in platforms)
        {
            var m = FindMetric(p, "aggregation_by_field");
            sb.AppendLine($"| {p.Name} | {Num(m, "p50_ms")} | {Num(m, "p95_ms")} |");
        }
        sb.AppendLine();
    }

    static void AppendMixedTable(StringBuilder sb, List<PlatformData> platforms)
    {
        sb.AppendLine("## 5. Mixed Read/Write Workload (Concurrency Sweep)");
        sb.AppendLine();
        sb.AppendLine("| Platform | 1 client QPS | 10 clients QPS | 40 clients QPS | 40-client errors |");
        sb.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var p in platforms)
        {
            var c1 = FindMetric(p, "mixed_1_clients");
            var c10 = FindMetric(p, "mixed_10_clients");
            var c40 = FindMetric(p, "mixed_40_clients");
            sb.AppendLine($"| {p.Name} | {Num(c1, "qps")} | {Num(c10, "qps")} | {Num(c40, "qps")} | {Num(c40, "errors")} |");
        }
        sb.AppendLine();
        sb.AppendLine("_Read/write mix: 80% reads / 20% writes. QPS = total completed operations per second._");
        sb.AppendLine();
    }

    static void AppendFootprintTable(StringBuilder sb, List<PlatformData> platforms)
    {
        sb.AppendLine("## 6. Resource Footprint");
        sb.AppendLine();
        sb.AppendLine("| Platform | Node Count | Relationship Count | Notes |");
        sb.AppendLine("|---|---:|---:|---|");
        foreach (var p in platforms)
        {
            // Footprint is the result entry WITHOUT a standard "metric" like the others,
            // but it carries node_count / rel_count keys.
            var fp = FindNodeWithKey(p, "node_count");
            var notes = CollectStorageNotes(fp);
            sb.AppendLine($"| {p.Name} | {Num(fp, "node_count")} | {Num(fp, "rel_count")} | {notes} |");
        }
        sb.AppendLine();
    }

    // ---------- Helpers ----------

    // Find a result object whose "metric" field matches the given name
    static JsonNode? FindMetric(PlatformData p, string metricName)
    {
        var results = p.Root["results"]?.AsArray();
        if (results == null) return null;
        foreach (var r in results)
        {
            if (r?["metric"]?.GetValue<string>() == metricName) return r;
        }
        return null;
    }

    // Find the first result object that contains a specific key (used for footprint)
    static JsonNode? FindNodeWithKey(PlatformData p, string key)
    {
        var results = p.Root["results"]?.AsArray();
        if (results == null) return null;
        foreach (var r in results)
        {
            if (r is JsonObject obj && obj.ContainsKey(key)) return r;
        }
        return null;
    }

    // Safely read a numeric field, returning "—" if missing
    static string Num(JsonNode? node, string field)
    {
        var val = node?[field];
        return val?.ToString() ?? "—";
    }

    // Gather any "storage.*" keys from the footprint into a readable note
    static string CollectStorageNotes(JsonNode? fp)
    {
        if (fp is not JsonObject obj) return "—";
        var notes = new List<string>();
        foreach (var kv in obj)
        {
            if (kv.Key.StartsWith("storage."))
                notes.Add($"{kv.Key.Replace("storage.", "")}: {kv.Value}");
        }
        return notes.Count > 0 ? string.Join("; ", notes) : "—";
    }
}