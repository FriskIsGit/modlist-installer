using System.Text;
using WebScrapper.scrapper;

namespace modlist_installer.installer; 

public class SearchEngine {
    public const string DUCKDUCKGO = "https://html.duckduckgo.com/html?q=";
    
    public static string createSearchURL(string modName) {
        return $"{DUCKDUCKGO}{modName} Project ID files site:curseforge.com";
    }

    private const int MAX_SEARCHABLE_TAGS = 6;
    
    /// <summary>Uses a search engine to fetch project ID. Acts as a fallback and an alternative to
    /// reverse searching projects from authors using cfwidget.</summary>
    /// <returns>Project ID if scraping was successful, otherwise 0</returns>
    public static uint scrapeProjectID(string duckHtml, string urlName) {
        var html = new HtmlDoc(duckHtml);

        Tag? resultsTag = html.Find("div", ("id", "links"), ("class", "results"));
        if (resultsTag is null) {
            Console.WriteLine("There were no results for this name");
            return 0;
        }

        List<Tag> results = html.FindAllFrom("div", resultsTag.StartOffset, false, 
            ("class", "links_main links_deep result__body"));
        
        for (int i = 0; i < results.Count && i < MAX_SEARCHABLE_TAGS; i++) {
            Tag resultTag = results[i];
            
            Tag? extraResults = html.FindFrom("div", resultTag.StartOffset, false, 
                ("class", "result__extras__url"));

            if (extraResults is null) {
                continue;
            }
            
            Tag? anchor = html.FindFrom("a", extraResults.StartOffset+1, true);
            if (anchor is null) {
                continue;
            }
            // first anchor is for the icon
            if (!anchor.ContainsAttribute(("class", "result__url"))) {
                // minimum tag content size 300
                anchor = html.FindFrom("a", extraResults.StartOffset+300, true);
            }
            
            if (anchor is null || !anchor.ContainsAttribute(("class", "result__url"))) {
                continue;
            }
                
            string cfUrl = html.ExtractText(anchor).Trim();
            // Console.WriteLine($"CF_URL: {cfUrl}");
            string extractedName = extractModName(cfUrl);
            if (urlName != extractedName) {
                continue;
            }

            string text = html.ExtractText(resultTag);
            // TODO Maybe ensure "Project" is found first? 
            int id_index = text.IndexOf("ID", StringComparison.InvariantCulture);
            if (id_index == -1) {
                continue;
            }

            bool parsingNumber = false;
            // Parse numerical ID which should occur right after "ID"
            var id = new StringBuilder(10);
            for (int s = id_index+2; s < text.Length; s++) {
                char chr = text[s];
                switch (chr) {
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                    case '0':
                        parsingNumber = true;
                        id.Append(chr);
                        break;
                    default:
                        if (parsingNumber) {
                            return uint.Parse(id.ToString());
                        }
                        break;
                }
            }
        }
        return 0;
    }
    
    private static string extractModName(string cfUrl) {
        var end = cfUrl.Length;
        var argSeparator = cfUrl.LastIndexOf('?');
        if (argSeparator != -1) {
            end = argSeparator;
        }
        string[] parts = cfUrl[..end].Split('/');
        for (int i = parts.Length-1; i > -1; i--) {
            string part = parts[i];
            if (part.Length == 0) {
                continue;
            }
            switch (part) {
                case "files":
                case "all":
                    continue;
            }

            // make sure it's not the id
            if (containsLetter(part)) {
                return part;
            }
        }

        return "";
    }

    private static bool containsLetter(string str) {
        foreach (var chr in str) {
            switch (chr) {
                case < 'z' and > 'a':
                    return true;
                case < 'Z' and > 'A':
                    return true;
            }
        }

        return false;
    }
}