using System.Diagnostics;
using System.Net;
using System.Text;
using WebScrapper.scrapper;

namespace modlist_installer.installer;

public class CLI {
    // MAKE MOD LOADER AN ARGUMENT
    private const string MODLOADER = "Forge";
    private const string FAILED_PATH = "failed.html";
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
    private const int LIMIT = 5000;
    /// <summary>
    /// Functionality overview: <br/>
    /// 1. Parse mods from modlist.html <br/>
    /// 2. Attempt to use mod_name to mod_id cache if successful skip steps: 3, 4 and 5 <br/>
    /// 3. Retrieve mod authors <br/>
    /// 4. Search for project ID by name in author's projects <br/>
    /// 5. If unsuccessful attempt to scrape the ID from search engines using a crafted query <br/>
    /// 6. Call CF mods/{mod_id}/files endpoint with mod_id and gameVersionId, selecting latest release <br/>
    /// 7. Serialize all failed mods to a separate modlist <br/>
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
        
        int successes = 0;
        var failed = new List<Mod>();
        for (int m = 0; m < mods.Count && m < LIMIT; m++) {
            var mod = mods[m];
            Console.WriteLine(mod);
            if (mod.name.Length == 0) {
                Console.WriteLine("Mod has no name! Why?");
                continue;
            }

            uint id = findModId(mod, modCache);
            if (id == 0) {
                failed.Add(mod);
                Console.WriteLine("Mod really couldn't be found, does it even exist?");
                continue;
            }

            // Populate cache, TODO - cache every project that's retrieved for good measure
            modCache.put(mod.name, id);
            // Fill the field, could be useful
            mod.id = id;
            
            ModFileInfo modInfo = flameAPI.fetchModFile(id);
            switch (modInfo.result) {
                case Result.SUCCESS:
                    break;
                case Result.NOT_FOUND:
                    Console.WriteLine($"No valid mod file was found for version {version} [id:{id}]");
                    failed.Add(mod);
                    continue;
                case Result.TIMED_OUT:
                    Console.WriteLine("Download timed out, likely because of too many releases.");
                    failed.Add(mod);
                    continue;
                case Result.UNKNOWN:
                    Console.WriteLine("Unexpected error");
                    failed.Add(mod);
                    continue;
            }
            
            // progress with cursor move?
            string downloadURL = $"{FlameAPI.CF_MODS}/{id}/files/{modInfo.fileId}/download";
            var timer = Stopwatch.StartNew();
            bool success = flameAPI.downloadFile(downloadURL, modInfo.fileName);
            if (success) {
                successes++;
                Console.WriteLine($"({successes}/{mods.Count}) Downloaded {modInfo.fileName} in {timer.Elapsed.Milliseconds}ms ");
            } else {
                failed.Add(mod);
                Console.WriteLine($"Failed on {modInfo.fileName}");
            }
        }
        Console.WriteLine($"Writing {failed.Count} failed downloads to {FAILED_PATH}");
        if (mods.Count > 0) {
            writeModsToFile(failed, FAILED_PATH);
        }
        Console.WriteLine($"Serializing cache of {modCache.size()} entries");
        modCache.serialize();
    }

    private static void writeModsToFile(IEnumerable<Mod> mods, string path) {
        using var file = File.Create(path);
        using var stream = new BufferedStream(file);
        stream.Write("<ul>"u8);
        stream.Write(Encoding.ASCII.GetBytes(Environment.NewLine));
        foreach (var mod in mods) {
            byte[] bytes = Encoding.UTF8.GetBytes(mod.asListElement());
            stream.Write(bytes);
            stream.Write(Encoding.ASCII.GetBytes(Environment.NewLine));
        }
        stream.Write("</ul>"u8);
    }
    
    public static void createModDifference(string path1, string path2) {
        List<Mod> mods1 = parseMods(path1);
        if (mods1.Count == 0) {
            Console.WriteLine("No mods contained in 1st list");
            return;
        }
        List<Mod> mods2 = parseMods(path2);
        if (mods2.Count == 0) {
            Console.WriteLine("No mods contained in 2nd list");
            return;
        }
        
        var modSet1 = mods1.ToHashSet();
        var modSet2 = mods2.ToHashSet();

        modSet1.SymmetricExceptWith(modSet2);
        if (!modSet1.Any()) {
            Console.WriteLine("No differences found!");
            return;
        }
        Console.WriteLine($"{modSet1.Count} differences found. Writing to diff.html");
        writeModsToFile(modSet1, "diff.html");
    }

    private static uint findModId(Mod mod, ModCache cache) {
        uint id = cache.get(mod.name);
        if (id != 0) {
            return id;
        }
        
        string urlName = mod.getURLName();
        if (mod.author.Length != 0) {
            id = reverseSearch(mod.name, mod.author);
        }
        if (id != 0) {
            return id;
        }
        Console.WriteLine("Mod not found in cfwidget, falling back to scraping!");
        string url = SearchEngine.createSearchURL(urlName);
        // Console.WriteLine($"URL:{url}");
        var resp = flameAPI.fetchHtml(url);
        if (resp.statusCode != HttpStatusCode.OK) {
            Console.WriteLine(resp.statusCode);
            return 0;
        }
        id = SearchEngine.scrapeProjectID(resp.content, urlName);
        return id;
    }
    
    
    private static uint reverseSearch(string modName, string authorName) {
        ModAuthor? maybeAuthor = flameAPI.fetchAuthor(authorName);
        if (maybeAuthor == null) {
            Console.WriteLine("Author not found!");
            return 0;
        }
        
        ModAuthor author = maybeAuthor.Value;
        
        List<Project> similarProjects = new();
        foreach (var proj in author.projects) {
            if (proj.name.StartsWith(modName) && proj.matchesKind(MODLOADER)) {
                similarProjects.Add(proj);
            }
        }

        switch (similarProjects.Count) {
            case 0:
                return 0;
            case 1:
                return similarProjects[0].id;
            default:
                // Prioritize exact matches over order
                foreach (var proj in similarProjects) {
                    if (proj.name == modName) {
                        return proj.id;
                    }
                }
                return similarProjects[0].id;
        }
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

        Console.WriteLine("Found " + author.projects.Count + " projects");
    }
}