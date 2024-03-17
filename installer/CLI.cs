using System.Diagnostics;
using WebScrapper.scrapper;

namespace modlist_installer.installer;

public class CLI {
    // MAKE MOD LOADER AN ARGUMENT
    private const string MODLOADER = "Forge";
    private static FlameAPI flameAPI = new ();

    public static void displayMods(string path) {
        Console.WriteLine("Parsing mods..");
        var mods = parseMods(path);
        if (mods.Count == 0) {
            return;
        }
        foreach (var mod in mods) {
            Console.WriteLine(mod);
        }

        Console.WriteLine($"Mods found: {mods.Count}");
    }

    private static List<Mod> parseMods(string path) {
        if (!File.Exists(path)) {
            Console.WriteLine("File does not exist. Exiting.");
            return new List<Mod>();
        }

        string contents = File.ReadAllText(path);
        HtmlDoc html = new(contents);
        List<Tag> anchors = html.FindAll("a");
        List<Mod> mods = new List<Mod>(anchors.Count);
        foreach (var anchor in anchors) {
            foreach (var (key, link) in anchor.Attributes) {
                if (key != "href")
                    continue;

                string name = html.ExtractText(anchor);
                var mod = new Mod(name, link);
                mods.Add(mod);
            }
        }

        return mods;
    }

    // const to limit the input size for testing purposes
    private const int LIMIT = 500;
    /// <summary>
    /// Steps taken: <br/>
    /// 1. Parse mods from modlist.html <br/>
    /// 2. Attempt to use mod_name to mod_id cache if successful skip step 3. and step 4 <br/>
    /// 3. Retrieve mod authors <br/>
    /// 4. Search for mod id by name in author's projects <br/>
    /// 5. Call CF mods/{mod_id}/files endpoint with mod id and search for user's desired version, selecting latest release <br/>
    /// </summary>
    public static void installMods(string path, string version) {
        flameAPI.setMcVersion(version);
        var mods = parseMods(path);
        if (mods.Count == 0) {
            return;
        }
        Console.WriteLine($"Mods parsed: {mods.Count}");
        var modCache = ModCache.load();
        Console.WriteLine($"Loaded cache size: {modCache.size()}");
        
        int failures = 0;
        for (int m = 0; m < mods.Count && m < LIMIT; m++) {
            var mod = mods[m];
            Console.WriteLine(mod);
            uint id = modCache.get(mod.name);
            

            if (id == 0) {
                if (mod.author.Length == 0) {
                    Console.WriteLine("Author name is empty and mod has no known name-to-id mapping. Skipping!");
                    continue;
                }
                id = reverseSearch(mod.name, mod.author);
                if (id == 0) {
                    failures += 1;
                    Console.WriteLine($"Mod {mod} - cannot be downloaded, include its name in cache?");
                    continue;
                }
                // Populate cache, TODO - cache every project that's retrieved for good measure
                modCache.put(mod.name, id);
            }
            // Fill the field, could be useful
            mod.id = id;
            
            ModFileInfo modInfo = flameAPI.fetchModFile(id);
            switch (modInfo.result) {
                case Result.SUCCESS:
                    break;
                case Result.NOT_FOUND:
                    Console.WriteLine("Download URL not found, are you sure this mod was released for your version?");
                    failures += 1;
                    continue;
                case Result.TIMED_OUT:
                    Console.WriteLine("Download timed out, likely because of too many releases.");
                    failures += 1;
                    continue;
            }
            
            // progress with cursor move?
            string downloadURL = $"{FlameAPI.CF_MODS}/{id}/files/{modInfo.fileId}/download";
            Console.WriteLine($"DOWNLOAD URL {downloadURL}");
            var timer = Stopwatch.StartNew();
            bool success = flameAPI.downloadFile(downloadURL, modInfo.fileName);
            if (success) {
                Console.WriteLine($"Downloaded {modInfo.fileName} in {timer.Elapsed.Milliseconds}ms");
            } else {
                failures++;
                Console.WriteLine($"Failed on {modInfo.fileName}");
            }
        }
        Console.WriteLine($"Failures: {failures}");
        Console.WriteLine($"Serializing cache of {modCache.size()} entries");
        modCache.serialize();

    }

    private static uint reverseSearch(string modName, string authorName) {
        ModAuthor? maybeAuthor = flameAPI.fetchAuthor(authorName);
        if (maybeAuthor == null) {
            Console.WriteLine("Author not found!");
            return 0;
        }

        ModAuthor author = maybeAuthor.Value;
        if (author.projects is null) {
            Console.WriteLine("It can be false according to trimming.");
            return 0;
        }
        foreach (var proj in author.projects) {
            if (proj.name.StartsWith(modName) && proj.matchesKind(MODLOADER)) {
                // it must not contain the MODLOADER
                return proj.id;
            }
        }

        return 0;
    }

    public static void displayAuthor(string authorName) {
        ModAuthor? maybeAuthor = flameAPI.fetchAuthor(authorName);
        if (maybeAuthor == null) {
            Console.WriteLine("Author not found!");
            return;
        }

        ModAuthor author = maybeAuthor.Value;
        Console.WriteLine("MOD_NAME | ID | INFERRED_URL");
        Console.WriteLine("----------------------------");
        foreach (var proj in author.projects) {
            Console.WriteLine($"{proj.name} | {proj.id} | {proj.convertToURL()}");
        }

        Console.WriteLine("Found " + author.projects.Length + " projects");
    }
}