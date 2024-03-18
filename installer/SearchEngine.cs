using System.Text;
using WebScrapper.scrapper;

namespace modlist_installer.installer; 

public class SearchEngine {
    public const string DUCKDUCKGO = "https://html.duckduckgo.com/html?q=";
    
    public static string createSearchURL(string modName) {
        return $"{DUCKDUCKGO}{modName} files Project ID site:curseforge.com";
    }

    private const int MAX_SEARCHABLE_TAGS = 3;
    
    /// <summary>Uses a search engine to fetch project ID. Acts as a fallback and an alternative to
    /// reverse searching projects from authors using cfwidget.</summary>
    /// <returns>Project ID if scraping was successful, otherwise 0</returns>
    public static uint scrapeProjectID(string duckHtml) {
        var html = new HtmlDoc(duckHtml);

        Tag? resultsTag = html.Find("div", ("id", "links"), ("class", "results"));
        if (resultsTag is null) {
            Console.WriteLine("There were no results for this name");
            return 0;
        }

        List<Tag> results = html.FindAllFrom("div", resultsTag.StartOffset, false, 
            ("class", "links_main links_deep result__body"));
        
        for (int i = 0; i < results.Count && i < MAX_SEARCHABLE_TAGS; i++) {
            Tag tag = results[i];
            string text = html.ExtractText(tag);
            // TODO Maybe ensure "Project" is found first? 
            int id_index = text.IndexOf("ID", StringComparison.InvariantCulture);
            if (id_index == -1) {
                continue;
            }

            bool parsingNumber = false;
            // Parse numerical ID which should occur right after "ID"
            StringBuilder id = new StringBuilder(10);
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
}