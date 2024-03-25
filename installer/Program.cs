using System.Net;

namespace modlist_installer.installer;

class Program {
    public const string VERSION = "2.0.1";

    private static void Main(string[] args) {
        if (args.Length == 0) {
            PrintHelp();
            return;
        }
        
        switch (args[0]) {
            case "ls":
            case "list":
            case "show":
            case "print":
                if (args.Length == 1) {
                    Console.WriteLine("Path to modlist is required. Displaying possibilities..");
                    string[] files = Directory.GetFileSystemEntries(".");
                    foreach (var file in files) {
                        if (file.EndsWith(".json") || file.EndsWith(".html")) {
                            Console.WriteLine(file);
                        }
                    }
                    return;
                }
                if (args[1].EndsWith(".html")) {
                    ModlistHandler.displayModlist(args[1]);
                } else if (args[1].EndsWith(".json")) {
                    ModlistHandler.displayManifest(args[1]);
                } else {
                    Console.WriteLine("Unrecognized format?");
                }
                break;
            case "download":
            case "install":
                switch (args.Length) {
                    case 1:
                        Console.WriteLine("Path to modlist is required.");
                        return;
                    case 2:
                        string path = args[1];
                        if (path.EndsWith(".json")) {
                            ModlistHandler.installManifest(path);
                        } else if (path.EndsWith(".html")) {
                            Console.WriteLine("Version not specified, defaulting to 1.12.2");
                            ModlistHandler.installModlist(args[1], "1.12.2");
                        } else {
                            Console.WriteLine("Unrecognized format?");
                        }
                        return;
                    case 3:
                        if (args[1].EndsWith(".json")) {
                            ModlistHandler.installManifest(args[1]);
                        } else if(args[1].EndsWith(".html")) {
                            string version = args[2].Trim();
                            Console.WriteLine($"Version: {version}");
                            ModlistHandler.installModlist(args[1], version);
                        } else {
                            Console.WriteLine("Unrecognized format?");
                        }
                        return;
                }
                break;
            case "author":
                if (args.Length == 1) {
                    Console.WriteLine("Author name required");
                    return;
                }

                ModlistHandler.displayAuthor(args[1]);
                break;
            case "id":
                if (args.Length == 1) {
                    Console.WriteLine("Mod name required");
                    return;
                }
                var flameApi = new FlameAPI();
                string url = SearchEngine.createSearchURL(args[1]);
                Console.WriteLine($"URL: {url}");
                var response = flameApi.fetchHtml(url);
                if (response.statusCode != HttpStatusCode.OK) {
                    Console.WriteLine(response.statusCode);
                    return;
                }

                uint id = SearchEngine.scrapeProjectID(response.content, args[1]);
                Console.WriteLine($"Id: {id}");
                break;
            case "compare":
                if (args.Length < 3) {
                    Console.WriteLine("Two paths to directories are required");
                    return;
                }

                ModlistHandler.compareDirectories(args[1], args[2]);
                break;
            case "diff":
            case "difference":
                if (args.Length < 3) {
                    Console.WriteLine("Two mod lists must be specified");
                    return;
                }
                var arg1 = args[1];
                var arg2 = args[2];

                if (arg1.EndsWith(".json") && arg2.EndsWith(".json")) {
                    ModlistHandler.createManifestDifference(arg1, arg2);
                } else if (arg1.EndsWith(".html") && arg2.EndsWith(".html")) {
                    ModlistHandler.createModlistDifference(arg1, arg2);
                } else {
                    Console.WriteLine("Incompatible formats");
                }
                
                break;
            default:
                string arg0 = args[0];
                if (arg0.StartsWith("-v") || arg0.StartsWith("--v")) {
                    Console.WriteLine($"v{VERSION}");
                    return;
                }

                if (arg0.StartsWith("-h") || arg0.StartsWith("--h")) {
                    PrintHelp();
                    return;
                }
                break;
        }
    }
    
    private static void PrintHelp() {
        Console.WriteLine("Modlist installer for CF");
        Console.WriteLine("Usage: modlist [COMMAND] [params]...");
        Console.WriteLine("Commands:");
        
        Console.WriteLine("  show <modlist.html>, show <manifest.json>");
        Console.WriteLine("  ls <modlist.html>, ls <manifest.json>");
        Console.WriteLine("        Enumerate the list of mods - modlists and manifests (OFFLINE)");
        Console.WriteLine("  install <modlist.html> <version>");
        Console.WriteLine("        Install specified modlist (CF) targeting the latest release of specified version");
        Console.WriteLine("  diff <list1.html> <list2.html>");
        Console.WriteLine("        Generate a modlist that's the difference of two mod lists (OFFLINE)");
        Console.WriteLine("  diff <manifest1.json> <manifest2.json>");
        Console.WriteLine("        Generate a manifest that's the difference of two manifests (OFFLINE)");
        Console.WriteLine("  author <name>");
        Console.WriteLine("        Fetch author by name, displaying their projects (CFWidget)");
        Console.WriteLine("  id <mod_name>");
        Console.WriteLine("        Scrape mod id by name");
        Console.WriteLine("  compare <dir_path1> <dir_path2>");
        Console.WriteLine("        Compare jar files between two directories");
    }
}
