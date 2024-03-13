using System.Net;
using System.Net.Http.Headers;

namespace modlist_installer.installer;

public class FlameAPI {
    // CF API for acquiring files
    private const string CF_API = "https://api.curseforge.com/v1/";
    private const string CF_WIDGET_AUTHOR = "https://api.cfwidget.com/author/search/";
    
    private readonly HttpClient client = new();
    private string cfbmToken = "";

    public void setCloudflareToken(string token) {
        cfbmToken = token;
    }

    // CF WIDGET for acquiring project ids by name
    public List<Mod> fetchModsOfAuthor(string author) {
        var response = fetchJson($"{CF_WIDGET_AUTHOR}{author}");
        Console.WriteLine("code: " + response.statusCode);
        Console.WriteLine(response.content);
        return new List<Mod>();
    }
    
    public SimpleResponse fetchJson(string url) {
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
    public HttpStatusCode statusCode;
    public string content;
    public SimpleResponse(HttpStatusCode code, string content) {
        statusCode = code;
        this.content = content;
    }
}

