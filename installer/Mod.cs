namespace modlist_installer.installer;

public struct Mod {
    public readonly string name;
    public readonly string url;
    public readonly string author;
    
    
    public Mod(string name, string url) {
        this.name = name;
        this.url = url;
        author = extractAuthor(name);
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