﻿
namespace modlist_installer.installer;

class Program {
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
                CLI.installMods(args[1]);
                break;
            
            case "test-cache":
                var cache = ModCache.load();
                for (int i = 100; i < 200; i++) {
                    cache.put("mod" + i, (uint)i);
                }
                cache.serialize();
                Console.WriteLine("Finished");
                break;
            case "author":
                if (args.Length == 1) {
                    Console.WriteLine("Author name required");
                    return;
                }

                CLI.displayAuthor(args[1]);
                break;
        }
        
    }
    
    private static void PrintHelp() {
        Console.WriteLine("Modlist installer for CF");
        Console.WriteLine("---- Usage ----");
        
        Console.WriteLine("1. Enumerate the list of mods (OFFLINE).");
        Console.WriteLine("modlist show modlist.html");
        Console.WriteLine("2. Install specified modlist (CF)");
        Console.WriteLine("modlist install modlist.html");
        Console.WriteLine("3. Fetch author by name, displaying their projects (CFWidget)");
        Console.WriteLine("modlist author <name>");
    }
}
