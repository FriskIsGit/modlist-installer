using System.Diagnostics;
using System.Net;
using System.Text;
using WebScrapper.scrapper;

namespace modlist_installer.installer;

public class CLI {
    // MAKE MOD LOADER AN ARGUMENT
    private const string MODLOADER = "Forge";
    private const string FAILED_PATH = "failed.html";
    private static FlameAPI flameAPI = new();

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
        if (anchors.Count == 0) {
            return new List<Mod>();
        }

        List<Mod> mods = new List<Mod>(anchors.Count);
        foreach (var anchor in anchors) {
            foreach (var (key, link) in anchor.Attributes) {
                if (key != "href")
                    continue;

                if (link.Contains("minecraft.curseforge")) {
                    string description = html.ExtractText(anchor);
                    var mod = new Mod(description, link);
                    // fill the id field since we're given it
                    string numerical_id = mod.getUrlEnd();
                    try {
                        mod.id = uint.Parse(numerical_id);
                    }
                    catch (Exception) { }
                    mods.Add(mod);
                }
                else {
                    // assume it's the new format
                    string desc = html.ExtractText(anchor);
                    mods.Add(new Mod(desc, link));
                }
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

            uint id = findModId(mod, modCache);
            if (id == 0) {
                failed.Add(mod);
                Console.WriteLine("Mod really couldn't be found, does it even exist?");
                continue;
            }

            // Populate cache if url is not in old format
            if (mod.id == 0) {
                modCache.put(mod.getUrlEnd(), id);
            }

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
            
            string downloadURL = $"{FlameAPI.CF_MODS}/{id}/files/{modInfo.fileId}/download";
            var timer = Stopwatch.StartNew();
            bool success = flameAPI.downloadFile(downloadURL, modInfo.fileName);
            if (success) {
                successes++;
                Console.WriteLine(
                    $"({successes}/{mods.Count}) Downloaded {modInfo.fileName} in {timer.Elapsed.Milliseconds}ms ");
            }
            else {
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

    private static void writeModsToFile(List<Mod> mods, string path) {
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
        
        var modSet1 = new HashSet<string>(mods1.Count);
        var modSet2 = new HashSet<string>(mods2.Count);

        foreach (var mod in mods1) 
            modSet1.Add(mod.name);
        foreach (var mod in mods2) 
            modSet2.Add(mod.name);
        
        modSet1.SymmetricExceptWith(modSet2);
        Console.WriteLine($"Compared by name {mods1.Count} mods with {mods2.Count}");
        if (!modSet1.Any()) {
            Console.WriteLine("No differences found!");
            return;
        }
        
        Console.WriteLine($"{modSet1.Count} differences found. Preparing diff.html");
        // Create a common HashMap
        var namesToMods = new Dictionary<string, Mod>();

        foreach (var mod in mods1)
            namesToMods[mod.name] = mod;
        foreach (var mod in mods2) 
            namesToMods[mod.name] = mod;

        List<Mod> differentMods = new List<Mod>(modSet1.Count);
        foreach (var name in modSet1) {
            differentMods.Add(namesToMods[name]);
        }
        writeModsToFile(differentMods, "diff.html");
    }

    private static uint findModId(Mod mod, ModCache cache) {
        if (mod.id != 0) {
            return mod.id;
        }

        string urlName = mod.getUrlEnd();
        uint id = cache.get(urlName);
        if (id != 0) {
            return id;
        }

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