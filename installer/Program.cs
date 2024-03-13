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
        }
        
    }
    
    private static void PrintHelp() {
        Console.WriteLine("Modlist installer for CF");
        Console.WriteLine("Usage:");
        Console.WriteLine("modlist show modlist.html");
        Console.WriteLine("modlist install modlist.html");
    }
}
