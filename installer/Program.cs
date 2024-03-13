using System.Diagnostics;

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
                
                break;
            
            case "test-cache":
                var cache = ModCache.load();
                for (int i = 100; i < 200; i++) {
                    cache.put("mod" + i, (uint)i);
                }
                cache.serialize();
                Console.WriteLine("Finished");
                break;
            case "test-author":
                var timer = Stopwatch.StartNew();
                var mods = new FlameAPI().fetchModsOfAuthor("MysticDrew");
                timer.Stop();
                Console.WriteLine($"FETCHED AUTHOR MODS IN {timer.Elapsed.TotalMilliseconds}ms");
                break;
        }
        
    }
    
    private static void PrintHelp() {
        Console.WriteLine("Modlist installer for CF");
        Console.WriteLine("Usage:");
        Console.WriteLine("modlist show modlist.html");
        Console.WriteLine("modlist install modlist.html");
    }
}
