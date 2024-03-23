using System.Text;
using WebScrapper.scrapper;

namespace modlist_installer.installer;

public struct Mod {
    public readonly string name;
    public readonly string url;
    public readonly string author;
    // set to 0 for named urls, set to mod id for old urls
    public uint id = 0;
    
    
    public Mod(string description, string url) {
        this.url = url;
        
        name = description;
        var bracket = description.IndexOf('(');
        if (bracket != -1) {
            name = name[..(bracket-1)];
        }
        author = extractAuthor(description);
    }

    private string asListElement() {
        return $"<li><a href=\"{url}\">{name} (by {author})</a></li>";
    }

    public string getUrlEnd() {
        int lastSlash = url.LastIndexOf('/');
        if (lastSlash == -1) {
            return "";
        }
        return url[(lastSlash + 1)..];
    }

    private static string extractAuthor(string modName) {
        var by = modName.IndexOf("by", StringComparison.InvariantCulture);
        if (by == -1) {
            return "";
        }
        if (modName.Length <= by + 3) {
            return "";
        }

        int end = by + 3;
        for (; end < modName.Length; end++) {
            if (modName[end] == ')') {
                break;
            }
        }
        return modName[(by + 3)..end];
    }

    public static List<Mod> parseMods(string path) {
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
    
    public static void writeModsToFile(List<Mod> mods, string path) {
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
}

