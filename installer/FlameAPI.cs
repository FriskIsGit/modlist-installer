using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace modlist_installer.installer;

public class FlameAPI {
    // CF API for acquiring files
    public const string CF_MODS = "https://www.curseforge.com/api/v1/mods";
    private const string CF_WIDGET_AUTHOR = "https://api.cfwidget.com/author/search/";
    public const string CF_MC_MODS = "https://www.curseforge.com/minecraft/mc-mods/";
    
    private readonly HttpClient client = new();
    private string cfbmToken = "";
    private string version = "";

    public FlameAPI() {
        // BaseAddress, Timeout, MaxResponseContentBufferSize are properties that cannot be modified..
        client.Timeout = TimeSpan.FromSeconds(6);
    }
    
    public void setCloudflareToken(string token) {
        cfbmToken = token;
    }
    
    public void setMcVersion(string mc_version) {
        version = mc_version;
    }

    // Using CF WIDGET to acquire project ids by name
    public ModAuthor? fetchAuthor(string author) {
        string url = $"{CF_WIDGET_AUTHOR}{author}";
        var response = fetchJson(url);
        if (response.statusCode != HttpStatusCode.OK) {
            Console.WriteLine($"Status code: {response.statusCode}");
            return null;
        }
        return JsonSerializer.Deserialize<ModAuthor>(response.content);
    }

    private const uint PAGE_SIZE = 500;
    // Fetching large pageSize because gameVersion parameter doesn't do anything conversely to what the documentation says
    // Returns empty string on failure
    public ModFileInfo fetchModFile(uint mod_id) {
        string url = $"{CF_MODS}/{mod_id}/files?pageSize={PAGE_SIZE}&removeAlphas=true";
        var response = fetchJson(url);
        if (response.statusCode != HttpStatusCode.OK) {
            Console.WriteLine($"Status code: {response.statusCode}");
            return ModFileInfo.NotFound();
        }
        var jsonObj = JsonSerializer.Deserialize<JsonNode>(response.content);
        var files_array = jsonObj?["data"]?.AsArray();
        if (files_array is null) {
            return ModFileInfo.NotFound();
        }

        var modFiles = new List<ModFileInfo>();
        // Select all matching versions
        foreach (var modInfoElement in files_array) {
            if (modInfoElement is null) 
                continue;
            
            var versionArray = modInfoElement["gameVersions"]?.AsArray();
            if (versionArray is null) 
                continue;

            bool foundMatch = false;
            foreach (var a_version in versionArray) {
                if (a_version != null && a_version.GetValue<string>() == version) {
                    foundMatch = true;
                    break;
                }
            }
            if (!foundMatch) 
                continue;
            
            var lengthNode = modInfoElement["fileLength"];
            var nameNode = modInfoElement["fileName"];
            var idNode = modInfoElement["id"];
            if (lengthNode is null || nameNode is null || idNode is null)
                continue;
            
            var id = uint.Parse(idNode.ToString());
            var name = nameNode.ToString();
            var length = uint.Parse(lengthNode.ToString());
            
            var modInfo = new ModFileInfo(id, name, length);
            modFiles.Add(modInfo);
        }
        
        // TODO Filter the latest release
        if (modFiles.Count == 0) {
            Console.WriteLine("No matching mod files were found for given ID");
        }
        return modFiles[0];
    }
    
    
    public async Task downloadFile(string url) {
        var urlStream = await client.GetStreamAsync(url);
        var fs = new FileStream("some_mc_mod.jar", FileMode.OpenOrCreate);
        await urlStream.CopyToAsync(fs).ConfigureAwait(false);
    }
    
    public SimpleResponse fetchJson(string url) {
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

public struct ModFileInfo {
    public uint fileId { get; set; }
    public string fileName { get; set; }
    public uint fileLength { get; set; }

    public ModFileInfo(uint fileId, string fileName, uint length) {
        this.fileId = fileId;
        this.fileName = fileName;
        fileLength = length;
    }
    
    public static ModFileInfo NotFound() {
        return new ModFileInfo(0, "", 0);
    }

    public override string ToString() {
        return $"{fileName}, id:{fileId}, length:{fileLength}";
    }
}

