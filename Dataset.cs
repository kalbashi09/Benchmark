using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

public static class Dataset
{
    const string Base = "https://snap.stanford.edu/data/";
    // Synthetic "field" values so we have a property to group-by in aggregations.
    static readonly string[] Fields = { "phen", "lat", "gr-qc", "quant-ph", "astro-ph", "cond-mat", "math-ph", "nucl-th" };

    static string SynthField(int id)  // deterministic: same id always gets the same field (reproducible!)
    {
        var h = MD5.HashData(Encoding.UTF8.GetBytes(id.ToString()));
        return Fields[h[0] % Fields.Length];
    }

    static async Task<List<string>> FetchLinesAsync(HttpClient http, string file)
    {
        var bytes = await http.GetByteArrayAsync(Base + file);
        using var gz = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        var lines = new List<string>();
        string? l;
        while ((l = await reader.ReadLineAsync()) != null)
            if (!l.StartsWith('#')) lines.Add(l);  // SNAP files have '#' comment headers
        return lines;
    }

    public static async Task PrepareAsync(string dir = "data")
    {
        Directory.CreateDirectory(dir);
        if (File.Exists($"{dir}/nodes.csv")) { Console.WriteLine("dataset already prepared"); return; }

        using var http = new HttpClient();

        // 1) Real publication years, from SNAP's dates file
        var years = new Dictionary<int, int>();
        foreach (var line in await FetchLinesAsync(http, "cit-HepTh-dates.txt.gz"))
        {
            var p = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 2) continue; // Skip any weird empty lines
            
            // FIX: The date looks like "1992-02-24". Split by '-' and grab the first part (the year).
            var yearStr = p[1].Split('-')[0]; 
            
            years[int.Parse(p[0])] = int.Parse(yearStr);
        }

        // 2) Citation edges (paper A cites paper B)
        var nodes = new Dictionary<int, int>();
        var edges = new HashSet<(int, int)>();
        foreach (var line in await FetchLinesAsync(http, "cit-HepTh.txt.gz"))
        {
            var p = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 2) continue; // Skip empty lines
            
            int a = int.Parse(p[0]), b = int.Parse(p[1]);
            if (a == b) continue;               // drop self-loops
            edges.Add((a, b));
            nodes.TryAdd(a, years.GetValueOrDefault(a, 1997));
            nodes.TryAdd(b, years.GetValueOrDefault(b, 1997));
        }

        using (var w = new StreamWriter($"{dir}/nodes.csv"))
        {
            w.WriteLine("id,year,field");
            foreach (var n in nodes.OrderBy(kv => kv.Key))
                w.WriteLine($"{n.Key},{n.Value},{SynthField(n.Key)}");
        }
        using (var w = new StreamWriter($"{dir}/edges.csv"))
        {
            w.WriteLine("src,dst");
            foreach (var e in edges.OrderBy(t => t))
                w.WriteLine($"{e.Item1},{e.Item2}");
        }
        Console.WriteLine($"dataset ready: {nodes.Count} nodes, {edges.Count} edges");
        // expected: 27,770 nodes / 352,807 edges — inside the 100k–500k requirement
    }

    public static List<Dictionary<string, object>> ReadNodes(string dir = "data") =>
        File.ReadAllLines($"{dir}/nodes.csv").Skip(1).Select(line =>
        {
            var p = line.Split(',');
            return new Dictionary<string, object> { ["id"] = int.Parse(p[0]), ["year"] = int.Parse(p[1]), ["field"] = p[2] };
        }).ToList();

    public static List<Dictionary<string, object>> ReadEdges(string dir = "data") =>
        File.ReadAllLines($"{dir}/edges.csv").Skip(1).Select(line =>
        {
            var p = line.Split(',');
            return new Dictionary<string, object> { ["src"] = int.Parse(p[0]), ["dst"] = int.Parse(p[1]) };
        }).ToList();
}