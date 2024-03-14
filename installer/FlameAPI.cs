using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace modlist_installer.installer;

public class FlameAPI {
    // CF API for acquiring files
    private const string CF_API = "https://api.curseforge.com/v1/";
    private const string CF_WIDGET_AUTHOR = "https://api.cfwidget.com/author/search/";
    public const string CF_MC_MODS = "https://www.curseforge.com/minecraft/mc-mods/";
    
    private readonly HttpClient client = new();
    private string cfbmToken = "";

    public void setCloudflareToken(string token) {
        cfbmToken = token;
    }

    // Using CF WIDGET to acquire project ids by name
    public ModAuthor? fetchAuthor(string author) {
        var response = fetchJson($"{CF_WIDGET_AUTHOR}{author}");
        if (response.statusCode != HttpStatusCode.OK) {
            Console.WriteLine($"Status code: {response.statusCode}");
            return null;
        }
        return JsonSerializer.Deserialize<ModAuthor>(response.content);
    }
    
    private SimpleResponse fetchJson(string url) {
        client.Timeout = TimeSpan.FromSeconds(6);
        var getRequest = new HttpRequestMessage {
            RequestUri = new Uri(url),
            Method = HttpMethod.Get,
        };
        
        getRequest.Headers.Accept.ParseAdd("application/json");
        var response = client.Send(getRequest);
        HttpStatusCode code = response.StatusCode;
        string content = response.Content.ReadAsStringAsync().Result;
        return new SimpleResponse(code, content);
    }
    
    public SimpleResponse fetchHtml(string url) {
        client.Timeout = TimeSpan.FromSeconds(6);
        var getRequest = new HttpRequestMessage {
            RequestUri = new Uri(url),
            Method = HttpMethod.Get,
        };
        getRequest.Headers.UserAgent.ParseAdd("Mozilla/5.0 Gecko/20100101");
        getRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        getRequest.Headers.AcceptLanguage.ParseAdd("en-US;q=0.7");
        getRequest.Headers.Add("Cookie", $"__cf_bm={cfbmToken}");
        getRequest.Headers.Add("Set-GPC", "1");
        var response = client.Send(getRequest);
        HttpStatusCode code = response.StatusCode;
        string content = response.Content.ReadAsStringAsync().Result;
        return new SimpleResponse(code, content);
    }
    
}

public struct SimpleResponse {
    public readonly HttpStatusCode statusCode;
    public readonly string content;
    public SimpleResponse(HttpStatusCode code, string content) {
        statusCode = code;
        this.content = content;
    }
}


public struct ModAuthor {
    public uint id { get; set; }
    public string username { get; set; }
    public Project[] projects { get; set; }
}

// sort of like Mods
public struct Project {
    public uint id { get; set; }
    public string name { get; set; }

    // since .NET 5
    public string convertToURL() {
        var urlName = new StringBuilder();
        foreach (var chr in name) {
            switch (chr) {
                case >= 'a' and <= 'z':
                    urlName.Append(chr);
                    break;
                case >= 'A' and <= 'Z':
                    urlName.Append(char.ToLower(chr));
                    break;
                case '-':
                case '/':
                case ' ':
                    urlName.Append('-');
                    break;
            }
        }

        return $"{FlameAPI.CF_MC_MODS}{urlName}";
    }
}