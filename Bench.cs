using System.Diagnostics;
using System.Text.Json;

public static class Bench
{
    // Linear-interpolated percentile. p50 = typical case, p95 = "bad day" case.
    public static double Percentile(List<double> xs, double p)
    {
        var s = xs.OrderBy(x => x).ToList();
        double k = (s.Count - 1) * p / 100.0;
        int lo = (int)Math.Floor(k), hi = Math.Min(lo + 1, s.Count - 1);
        return s[lo] + (s[hi] - s[lo]) * (k - lo);
    }

    // Runs one workload: warm-up (NOT measured) + iters measured runs → percentiles.
    public static async Task<Dictionary<string, object>> LatencyAsync(
        string name, Func<Task> fn, int warmup = 20, int iters = 100)
    {
        for (int i = 0; i < warmup; i++) await fn();   // warm caches & connections first
        var xs = new List<double>(iters);
        var sw = new Stopwatch();
        for (int i = 0; i < iters; i++)
        {
            sw.Restart();
            await fn();
            sw.Stop();
            xs.Add(sw.Elapsed.TotalMilliseconds);
        }
        return new Dictionary<string, object>
        {
            ["metric"] = name,
            ["p50_ms"] = Math.Round(Percentile(xs, 50), 3),
            ["p95_ms"] = Math.Round(Percentile(xs, 95), 3),
            ["min_ms"] = Math.Round(xs.Min(), 3),
            ["max_ms"] = Math.Round(xs.Max(), 3),
            ["iters"] = iters
        };
    }

    // Concurrency test: N clients hammer reads+writes for `seconds`; we count what finishes → QPS.
    public static async Task<Dictionary<string, object>> MixedAsync(
        string name, Func<Task> read, Func<Task> write,
        int clients, int seconds = 30, double writeRatio = 0.2)
    {
        long reads = 0, writes = 0, errors = 0;
        var stopAt = DateTime.UtcNow.AddSeconds(seconds);
        var tasks = Enumerable.Range(0, clients).Select(_ => Task.Run(async () =>
        {
            var rng = new Random(Environment.CurrentManagedThreadId);
            while (DateTime.UtcNow < stopAt)
            {
                try
                {
                    if (rng.NextDouble() < writeRatio) { await write(); Interlocked.Increment(ref writes); }
                    else                               { await read();  Interlocked.Increment(ref reads); }
                }
                catch { Interlocked.Increment(ref errors); }   // honest error counting = graded!
            }
        })).ToArray();

        var sw = Stopwatch.StartNew();
        await Task.WhenAll(tasks);
        sw.Stop();

        return new Dictionary<string, object>
        {
            ["metric"] = name, ["clients"] = clients,
            ["seconds"] = Math.Round(sw.Elapsed.TotalSeconds, 1),
            ["reads"] = reads, ["writes"] = writes, ["errors"] = errors,
            ["qps"] = Math.Round((reads + writes) / sw.Elapsed.TotalSeconds, 1),
            ["write_ratio"] = writeRatio
        };
    }

    public static void Save(string platform, List<Dictionary<string, object>> rows)
    {
        Directory.CreateDirectory("results");
        File.WriteAllText($"results/{platform}.json", JsonSerializer.Serialize(
            new { platform, generated_utc = DateTime.UtcNow, results = rows },
            new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"saved results/{platform}.json");
    }
}