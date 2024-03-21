using System.Text;

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

    public string asListElement() {
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

    private const int MINIMUM_NAME_LENGTH = 24;
    private const int MINIMUM_AUTHOR_LENGTH = 14;

    private static string pad(string str, int minLength) {
        var builder = new StringBuilder(str);
        var padding = minLength - str.Length;
        for (int i = 0; i < padding; i++) {
            builder.Append(' ');
        }

        return builder.ToString();
    }
    public override string ToString() {
        var padded_name = pad('[' + name + ']', MINIMUM_NAME_LENGTH);
        var padded_author = pad('"' + author + '"', MINIMUM_AUTHOR_LENGTH);
        return $"{padded_name} {padded_author} {url}";
    } 
}

