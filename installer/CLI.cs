using WebScrapper.scrapper;

namespace modlist_installer.installer;

public class CLI {
    public static void displayMods(string path) {
        Console.WriteLine("Parsing mods..");
        var mods = parseMods(path);
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
        HtmlDoc html = new (contents);
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
}