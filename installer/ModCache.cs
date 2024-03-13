using System.Diagnostics;
using System.Text;

namespace modlist_installer.installer;

/// <summary>
/// Storage for mapping mod names to ids. There seems to be no direct API call to retrieve mod id by name.
/// Mod ids are instead retrieved by looking through author's mods thanks to https://cfwidget.com/
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
    
    public bool put(string name, uint id) {
        if (name.Length == 0) {
            return false;
        }
        mods[name] = id;
        return true;
    }
    
    public uint? get(string name) {
        if (name.Length == 0) {
            return null;
        }

        return mods[name];
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