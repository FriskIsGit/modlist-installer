namespace modlist_installer.installer;

public struct Mod {
    public readonly string name;
    public readonly string url;
    public readonly string author;
    // id by default zero - not fetched
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
    
    public override string ToString() {
        // TODO add spaced format
        return $"[{url}] {name} {author}";
    }  
}

