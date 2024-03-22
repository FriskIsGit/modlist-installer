using System.Diagnostics;
using System.Text;

namespace modlist_installer.installer;

/// <summary>
/// Storage for mapping mod url names to ids. There seems to be no direct API call to retrieve mod id by name.
/// </summary>
public class ModCache {
    private const string CACHE_PATH = "mod.cache";
    private readonly Dictionary<string, uint> mods;

    private ModCache(int capacity) {
        mods = new Dictionary<string, uint>(capacity);
    }
    
    public static ModCache load() {
        if (!File.Exists(CACHE_PATH)) {
            return new ModCache(16);
        }
        string[] entries = File.ReadAllLines(CACHE_PATH);
        var cache = new ModCache(entries.Length);
        foreach (var entry in entries) {
            var pair = parseEntry(entry);
            if (!pair.HasValue) 
                continue;
            
            string modName = pair.Value.Item1;
            if (modName.Length == 0)
                continue;
            
            uint id = pair.Value.Item2;
            cache.mods.Add(modName, id);
        }

        return cache;
    }

    /// <summary>
    /// Serializes all cache entries by overwriting the existing cache. Provided that the existing cache has already
    /// been loaded, it'll appear as if new entries were only appended. <br/>
    /// Streams entries to <c>mod.cache</c> in the current directory, one entry per line; format:
    /// <c>mod_name=id</c>. ID must be a numerical string.
    /// </summary>
    public void serialize() {
        var timer = Stopwatch.StartNew();
        // Use BufferedStream to buffer writes to a FileStream
        using (var file = File.Create(CACHE_PATH)) {
            using (var stream = new BufferedStream(file)) {
                foreach (var (key, value) in mods) {
                    byte[] bytes = Encoding.UTF8.GetBytes($"{key}={value}");
                    stream.Write(bytes);
                    byte[] newLine = Encoding.ASCII.GetBytes(Environment.NewLine);
                    stream.Write(newLine);
                }
            }
        }
        
        timer.Stop();
        Console.WriteLine($"SERIALIZED CACHE IN {timer.Elapsed.TotalMilliseconds}ms");
    }

    public int size() {
        return mods.Count;
    }
    
    public void put(string urlName, uint id) {
        if (urlName.Length == 0) {
            return;
        }
        mods[urlName] = id;
    }
    
    public uint get(string urlName) {
        if (urlName.Length == 0) {
            return 0;
        }
        uint id;
        if (mods.TryGetValue(urlName, out id)) {
            return id;
        }
        return 0;
    }
    
    private static (string, uint)? parseEntry(string entry) {
        int eq = entry.IndexOf('=');
        if (eq == -1) {
            return null;
        }

        string name = entry[..eq];
        uint id = uint.Parse(entry[(eq+1)..entry.Length]);
        return (name, id);
    }
}