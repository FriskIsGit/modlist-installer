using System.Diagnostics;
using System.Net;
using System.Text;

namespace modlist_installer.installer;

public class ModlistHandler {
    // MAKE MOD LOADER AN ARGUMENT
    private const string MODLOADER = "Forge";
    private const string FAILED_HTML = "failed.html";
    private const string FAILED_JSON = "failed.json";
    private const string DOWNLOADS = "mods";
    private static FlameAPI flameAPI = new();

    public static void displayModlist(string path) {
        Console.WriteLine("Parsing mods..");
        var mods = Mod.parseMods(path);
        if (mods.Count == 0) {
            return;
        }

        foreach (var mod in mods) {
            Console.WriteLine(modToString(mod));
        }

        Console.WriteLine($"Mods found: {mods.Count}");
    }

    private static string modToString(Mod mod) {
        var padded_name = pad('[' + mod.name + ']', 24);
        var padded_author = pad('"' + mod.author + '"', 12);
        return $"{padded_name} {padded_author} {mod.url}";
    }
    
    public static void displayManifest(string path) {
        Console.WriteLine("Parsing manifest..");
        var manifest = Manifest.parseManifest(path);
        if (manifest is null) {
            Console.WriteLine("Invalid manifest!");
            return;
        }
        if (manifest.files.Count == 0) {
            return;
        }

        
        var format = formatManifestFiles(manifest.files);
        Console.WriteLine(format);
        printManifestLabels(true, true);
        printManifestInfo(manifest, true);
    }

    private const int SYMBOL_LEN = 10;
    private const int PROJECT_ID_LEN = 12;
    private const int FILE_ID_LEN = 9;
    
    private static StringBuilder formatManifestFiles(List<ManifestFile> modFiles) {
        var format = new StringBuilder();
        format.Append("| REQUIRED | PROJECT_ID | FILE_ID |");
        format.AppendLine();
        format.Append("-----------------------------------");
        format.AppendLine();
        uint countRequired = 0;
        foreach (var mod in modFiles) {
            if (mod.required)
                countRequired++;
            
            string symbol = mod.required ? "[+]" : "[-]";
            format.Append('|');
            format.Append(centerPad(symbol, SYMBOL_LEN));
            format.Append('|');
            format.Append(centerPad(mod.projectID.ToString(), PROJECT_ID_LEN));
            format.Append('|');
            format.Append(centerPad(mod.fileID.ToString(), FILE_ID_LEN));
            format.Append('|');
            format.AppendLine();
        }
        format.Append($"Required mods: {countRequired}");
        return format;
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
    public static void installModlist(string path, string version) {
        flameAPI.setMcVersion(version);

        var mods = Mod.parseMods(path);
        if (mods.Count == 0) {
            return;
        }

        Console.WriteLine($"Mods parsed: {mods.Count}");
        var modCache = ModCache.load();
        Console.WriteLine($"Loaded cache size: {modCache.size()}");
        Directory.CreateDirectory(DOWNLOADS);
        Console.WriteLine($"Downloading to '{DOWNLOADS}' directory");
        
        int successes = 0;
        var failed = new List<Mod>();
        for (int m = 0; m < mods.Count && m < LIMIT; m++) {
            var mod = mods[m];
            Console.WriteLine(modToString(mod));

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

            Task<DownloadInfo> downloadTask = flameAPI.downloadFile(downloadURL, DOWNLOADS);
            try {
                downloadTask.Wait();
                var info = downloadTask.Result;
                successes++;
                Console.WriteLine(
                    $"({successes}/{mods.Count}) Downloaded {info.fileName} in {timer.Elapsed.Milliseconds}ms ");
            } catch (AggregateException e) {
                Console.WriteLine(e.Message);
                failed.Add(mod);
                Console.WriteLine($"Failed on {downloadURL}");
            }
        }

        Console.WriteLine($"Writing {failed.Count} failed downloads to {FAILED_HTML}");
        if (mods.Count > 0) {
            Mod.writeModsToFile(failed, FAILED_HTML);
        }

        Console.WriteLine($"Serializing cache of {modCache.size()} entries");
        modCache.serialize();
    }

    public static void installManifest(string path) {
        Manifest? manifest = Manifest.parseManifest(path);
        if (manifest is null) {
            return;
        }
        Directory.CreateDirectory(manifest.name);
        Console.WriteLine($"Downloading to '{manifest.name}' directory");
        
        var failed = new List<ManifestFile>();
        uint successes = 0;
        var allMods = manifest.files.Count;
        foreach (var mod in manifest.files) {
            uint modId = mod.projectID;
            uint fileId = mod.fileID;
            string downloadURL = $"{FlameAPI.CF_MODS}/{modId}/files/{fileId}/download";
            
            var timer = Stopwatch.StartNew();
            Task<DownloadInfo> downloadTask = flameAPI.downloadFile(downloadURL, manifest.name);
            try {
                downloadTask.Wait();
                var info = downloadTask.Result;
                successes++;
                Console.WriteLine(
                    $"({successes}/{allMods}) Downloaded {info.fileName} in {timer.Elapsed.Milliseconds}ms ");
            } catch (AggregateException e) {
                Console.WriteLine(e.Message);
                failed.Add(mod);
                Console.WriteLine($"Failed on {downloadURL}");
            }
        }
        var failedFormat = formatManifestFiles(failed);
        Console.WriteLine("FAILED:");
        Console.WriteLine(failedFormat);
        var failedManifest = new Manifest(manifest.name, "failed", manifest.author, failed.Count);
        failedManifest.fill(failed);
        Manifest.serializeManifest(failedManifest, FAILED_JSON);
        Console.WriteLine($"Serialized to {FAILED_JSON}");
    }
    

    public static void createModlistDifference(string path1, string path2) {
        List<Mod> mods1 = Mod.parseMods(path1);
        if (mods1.Count == 0) {
            Console.WriteLine("No mods contained in 1st list");
            return;
        }
        
        List<Mod> mods2 = Mod.parseMods(path2);
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
        Mod.writeModsToFile(differentMods, "diff.html");
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

    private const int NAME_LEN = 22;
    private const int AUTHOR_LEN = 15;
    private const int VERSION_LEN = 10;
    private const int FILES_LEN = 5;
    
    public static void createManifestDifference(string path1, string path2) {
        var manifest1 = Manifest.parseManifest(path1);
        if (manifest1 is null) {
            Console.WriteLine("First manifest is invalid");
            return;
        }
        var manifest2 = Manifest.parseManifest(path2);
        if (manifest2 is null) {
            Console.WriteLine("First manifest is invalid");
            return;
        }

        printManifestLabels(true, true);
        printManifestInfo(manifest1, false);
        printManifestInfo(manifest2, true);
        
        var modSet1 = new HashSet<uint>(manifest1.files.Count);
        var modSet2 = new HashSet<uint>(manifest2.files.Count);

        foreach (var mod in manifest1.files) 
            modSet1.Add(mod.fileID);
        foreach (var mod in manifest2.files) 
            modSet2.Add(mod.fileID);
        
        modSet1.SymmetricExceptWith(modSet2);
        Console.WriteLine("File ID differences: " + modSet1.Count);
        Manifest diff = new Manifest(manifest1.name, "difference", manifest1.author, modSet1.Count);
        foreach (var fileId in modSet1) {
            bool found = false;
            foreach (var modFile in manifest1.files) {
                if (modFile.fileID == fileId) {
                    diff.files.Add(modFile);
                    found = true;
                    break;
                }
            }
            if (found) {
                continue;
            }
            foreach (var modFile in manifest2.files) {
                if (modFile.fileID == fileId) {
                    diff.files.Add(modFile);
                    break;
                }
            }
        }
        Manifest.serializeManifest(diff, "diff.json");
        Console.WriteLine("Serialized manifest!");
    }

    private static void printManifestLabels(bool startLineSeparator, bool endLineSeparator) {
        var line = characterLine('-', NAME_LEN + AUTHOR_LEN + VERSION_LEN + FILES_LEN + 5);
        var nameLabel = centerPad("NAME", NAME_LEN);
        var authorLabel = centerPad("AUTHOR", AUTHOR_LEN);
        var versionLabel = centerPad("VERSION", VERSION_LEN);
        var filesLabel = centerPad("FILES", FILES_LEN);
        if (startLineSeparator) {
            Console.WriteLine(line);
        }
        Console.WriteLine($"|{nameLabel}|{authorLabel}|{versionLabel}|{filesLabel}|");
        if (endLineSeparator) {
            Console.WriteLine(line);
        }
    }


    private static void printManifestInfo(Manifest manifest, bool endLineSeparator) {
        Console.Write("|");
        Console.Write(centerPad(manifest.name, NAME_LEN));
        Console.Write("|");
        Console.Write(centerPad(manifest.author, AUTHOR_LEN));
        Console.Write("|");
        Console.Write(centerPad(manifest.version, VERSION_LEN));
        Console.Write("|");
        Console.Write(centerPad(manifest.files.Count.ToString(), FILES_LEN));
        Console.WriteLine("|");
        if (endLineSeparator) {
            Console.WriteLine(characterLine('-', NAME_LEN + AUTHOR_LEN + VERSION_LEN + FILES_LEN + 5));
        }
    }

    private static string centerPad(string str, int minLength) {
        var builder = new StringBuilder(str);
        var padding = minLength - str.Length;
        var half = padding / 2;
        for (int i = 0; i < half; i++) {
            builder.Insert(0, ' ');
        }
        for (int i = half; i < padding; i++) {
            builder.Append(' ');
        }

        return builder.ToString();
    }

    private static string characterLine(char chr, int length) {
        var builder = new StringBuilder(length);
        for (int i = 0; i < length; i++) {
            builder.Append(chr);
        }
        return builder.ToString();
    }
    
    private static string pad(string str, int minLength) {
        var builder = new StringBuilder(str);
        var padding = minLength - str.Length;
        for (int i = 0; i < padding; i++) {
            builder.Append(' ');
        }

        return builder.ToString();
    }

    public static void compareDirectories(string path1, string path2) {
        if (!Directory.Exists(path1)) {
            Console.WriteLine($"{path1} not found!");
            return;
        }
        if (!Directory.Exists(path2)) {
            Console.WriteLine($"{path2} not found!");
            return;
        }
        
        string[] files1 = Directory.GetFiles(path1);
        List<Jar> jars1 = convertToJars(files1);
        string[] files2 = Directory.GetFiles(path2);
        List<Jar> jars2 = convertToJars(files1);
        convertToJars(files2);
        foreach (var jar1 in jars1) {
            Jar? candidate = null;
            bool equal = false;
            foreach (var jar2 in jars2) {
                if (!jar1.modName.StartsWith(jar2.modName)) 
                    continue;
                
                if (jar1.modName == jar2.modName && jar1.version == jar2.version) {
                    equal = true;
                    break;
                }
                candidate = jar2;
            }

            if (equal) {
                continue;
            }

            if (candidate is null) {
                printDirectoryEntries(jar1.modName + ' ' + jar1.version, "MISSING");
            } else {
                var match = candidate.Value;
                printDirectoryEntries(jar1.modName + ' ' + jar1.version, match.modName + ' ' + match.version);
            }
        }
    }

    private const int JAR_NAME_LEN = 50;
    private static void printDirectoryEntries(string name1, string name2) {
        var str = new StringBuilder();
        str.Append('|');
        str.Append(centerPad(name1, JAR_NAME_LEN));
        str.Append('|');
        str.Append(centerPad(name2, JAR_NAME_LEN));
        str.Append('|');
        Console.WriteLine(str);
    }

    private static List<Jar> convertToJars(string[] files) {
        var jars = new List<Jar>();
        for (int i = 0; i< files.Length; i++) {
            var file = files[i];
            if (!file.EndsWith(".jar")) {
                continue;
            }
            var slash = file.LastIndexOf('/');
            if (slash == -1) {
                slash = file.LastIndexOf('\\');
            }
            if (slash == -1) {
                continue;
            }

            files[i] = file[(slash + 1)..];
            var jar = new Jar(files[i]);
            jars.Add(jar);
        }

        return jars;
    }
}

struct Jar {
    public string modName;
    public string version;

    public Jar(string filename) {
        filename = filename.ToLower();
        var jar = filename.LastIndexOf(".jar", StringComparison.Ordinal);
        string extensionlessName = filename[..jar];
        var nameSeparator = extensionlessName.IndexOf('-');
        if (nameSeparator == -1) {
            nameSeparator = extensionlessName.IndexOf(' ');
        }
        if (nameSeparator == -1) {
            nameSeparator = extensionlessName.IndexOf('_');
        }
        if (nameSeparator == -1) {
            Console.WriteLine($"I don't know how to parse name & version: {extensionlessName}");
            modName = extensionlessName;
            version = extensionlessName;
            return;
        }
        modName = extensionlessName[..nameSeparator];
        version = extensionlessName[(nameSeparator+1)..];
    }
}
