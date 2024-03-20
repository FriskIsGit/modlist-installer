﻿
using System.Net;

namespace modlist_installer.installer;

class Program {
    public static readonly string VERSION = "1.0.0";
    private static void Main(string[] args) {
        if (args.Length == 0) {
            PrintHelp();
            return;
        }
        
        switch (args[0]) {
            case "show":
                if (args.Length == 1) {
                    Console.WriteLine("Path to modlist is required.");
                    return;
                }
                CLI.displayMods(args[1]);
                break;
            case "install":
                switch (args.Length) {
                    case 1:
                        Console.WriteLine("Path to modlist is required.");
                        return;
                    case 2:
                        Console.WriteLine("Version not specified, defaulting to 1.12.2");
                        CLI.installMods(args[1], "1.12.2");
                        return;
                    case 3:
                        string version = args[2].Trim();
                        Console.WriteLine($"Version: {version}");
                        CLI.installMods(args[1], version);
                        return;
                }
                break;
            case "author":
                if (args.Length == 1) {
                    Console.WriteLine("Author name required");
                    return;
                }

                CLI.displayAuthor(args[1]);
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
            case "diff":
                if (args.Length < 3) {
                    Console.WriteLine("Two mod lists must be specified");
                    return;
                }
                CLI.createModDifference(args[1], args[2]);
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
        Console.WriteLine("---- Usage ----");
        
        Console.WriteLine("1. Enumerate the list of mods (OFFLINE).");
        Console.WriteLine("modlist show <modlist.html>");
        Console.WriteLine("2. Install specified modlist (CF) targeting the latest release of specified version");
        Console.WriteLine("modlist install <modlist.html> <version>");
        Console.WriteLine("3. Fetch author by name, displaying their projects (CFWidget)");
        Console.WriteLine("modlist author <name>");
        Console.WriteLine("4. Generate a modlist that's the difference of two mod lists (OFFLINE)");
        Console.WriteLine("modlist diff <list1.html> <list2.html>");
        Console.WriteLine("5. Scrape mod id by name");
        Console.WriteLine("modlist id <mod_name>");
    }
}
