using System.Net;
using System.Net.Http.Headers;

namespace modlist_installer.installer;

public class FlameAPI {
    // CF API for acquiring files
    private const string CF_API = "https://api.curseforge.com/v1/";
    // CF WIDGET for acquiring project ids by name
    private const string CF_WIDGET_AUTHOR = "https://api.cfwidget.com/author/search/";
    
    private readonly HttpClient client = new();
    private string cfbmToken = "";

    public void setCloudflareToken(string token) {
        cfbmToken = token;
    }
    
    public string fetchJson(string url) {
        client.Timeout = TimeSpan.FromSeconds(6);
        var getRequest = new HttpRequestMessage {
            RequestUri = new Uri(url),
            Method = HttpMethod.Get,
        };
        
        getRequest.Headers.Accept.ParseAdd("application/json");
        var response = client.Send(getRequest);
        if (response.StatusCode == HttpStatusCode.OK) {
            string contentOk = response.Content.ReadAsStringAsync().Result;
            return contentOk;
        }

        Console.WriteLine("Response: " + response.StatusCode);
        string content = response.Content.ReadAsStringAsync().Result;
        return content;
    }
    
    public string fetchHtml(string url) {
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
        if (response.StatusCode == HttpStatusCode.OK) {
            string contentOk = response.Content.ReadAsStringAsync().Result;
            return contentOk;
        }

        Console.WriteLine("Response: " + response.StatusCode);
        string content = response.Content.ReadAsStringAsync().Result;
        return content;
    }
}

