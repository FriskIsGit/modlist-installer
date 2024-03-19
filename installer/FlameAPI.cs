using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace modlist_installer.installer;

public class FlameAPI {
    // CF API for acquiring files
    public const string CF_MODS = "https://www.curseforge.com/api/v1/mods";
    public const string CF_MINECRAFT = "https://api.curseforge.com/v1/minecraft/version";
    private const string CF_WIDGET_AUTHOR = "https://api.cfwidget.com/author/search/";
    public const string CF_MC_MODS = "https://www.curseforge.com/minecraft/mc-mods/";
    
    private readonly HttpClient client = new();
    private string cfbmToken = "";
    private string version = "";
    private uint versionId = 0;

    public FlameAPI() {
        // BaseAddress, Timeout, MaxResponseContentBufferSize are properties that cannot be modified..
        client.Timeout = TimeSpan.FromSeconds(10);
    }
    
    public void setCloudflareToken(string token) {
        cfbmToken = token;
    }
    
    public void setMcVersion(string mc_version) {
        version = mc_version;
        // the function below must not fail
        versionId = fetchVersionId(mc_version);
        Console.WriteLine($"gameVersionId: {versionId}");
    }

    // Return 0 on failure
    public uint fetchVersionId(string versionName) {
        var response = fetchJson(CF_MINECRAFT);
        if (response.statusCode != HttpStatusCode.OK) {
            Console.WriteLine($"Status code: {response.statusCode}");
            return 0;
        }

        var jsonObj = JsonNode.Parse(response.content);
        if (jsonObj is null) {
            return 0;
        }

        var minecraft_releases = jsonObj["data"]?.AsArray();
        foreach (var release in minecraft_releases) {
            if (release is null) 
                continue;

            var jsonVersion = release["versionString"];
            if (jsonVersion is null) {
                continue;
            }
            
            if (jsonVersion.ToString() == versionName) {
                var jsonId = release["gameVersionId"];
                if (jsonId is null) {
                    continue;
                }
                uint id = jsonId.GetValue<uint>();
                return id;
            }
        }

        throw new Exception($"gameVersionId wasn't found for given versionString: {versionName}");
    }
    // Using CF WIDGET to acquire project ids by name
    public ModAuthor? fetchAuthor(string author) {
        string url = $"{CF_WIDGET_AUTHOR}{author}";
        var response = fetchJson(url);
        if (response.statusCode != HttpStatusCode.OK) {
            Console.WriteLine($"Status code: {response.statusCode}");
            return null;
        }

        return ModAuthor.Parse(response.content);
    }

    private const uint PAGE_SIZE = 10;
    // Fetching large pageSize because gameVersion parameter doesn't do anything conversely to what the documentation says
    // Returns empty string on failure
    public ModFileInfo fetchModFile(uint mod_id) {
        string url = $"{CF_MODS}/{mod_id}/files?pageSize={PAGE_SIZE}&sort=dateCreated&sortDescending=true&gameVersionId={versionId}&removeAlphas=true";

        string content;
        try {
            var response = fetchJson(url);
            if (response.statusCode != HttpStatusCode.OK) {
                Console.WriteLine($"Status code: {response.statusCode}");
                return ModFileInfo.NotFound();
            }
            content = response.content;
        } catch (Exception e) {
            Console.WriteLine(e.Message);
            return ModFileInfo.TimedOut();
        }
        
        
        var jsonObj = JsonNode.Parse(content);
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
                if (a_version == null) {
                    continue;
                }
                var parsed_version = a_version.GetValue<string>();
                if (version.StartsWith(parsed_version)) {
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
            
            var modInfo = new ModFileInfo(id, name, length, Result.SUCCESS);
            modFiles.Add(modInfo);
        }

        // The endpoint is called with sorting by date which should fetch latest release? Needs checking
        return modFiles.Count == 0 ? ModFileInfo.NotFound() : modFiles[0];
    }
    
    
    public bool downloadFile(string url, string fileName) {
        var webClient = new WebClient();
        webClient.Headers.Add("User-Agent", "Mozilla/5.0 Gecko/20100101");
        try {
            webClient.DownloadFile(url, fileName);
            return true;
        }
        catch (WebException e) {
            Console.WriteLine(e);
            return false;
        }
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
        // getRequest.Headers.Add("Cookie", $"__cf_bm={cfbmToken}");
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
    private ModAuthor(uint id, string username, List<Project> projects) {
        this.id = id;
        this.username = username;
        this.projects = projects;
    }

    public uint id { get; set; }
    public string username { get; set; }
    public List<Project> projects { get; set; }

    public static ModAuthor? Parse(string json) {
        var obj = JsonNode.Parse(json);
        if (obj == null) {
            return null;
        }
        uint id = obj["id"]?.GetValue<uint>() ?? 0;
        string username = obj["username"]?.ToString() ?? "";
        var jsonProjects = obj["projects"]?.AsArray();
        
        if (jsonProjects is null) {
            // API must return an empty array [CS8602]
            return new ModAuthor(id, username, new List<Project>());
        }
        var projects = new List<Project>(jsonProjects.Count);
        foreach (var jsonProj in jsonProjects) {
            if (jsonProj is null) {
                continue;
            }

            var project = Project.Parse(jsonProj);
            projects.Add(project);
        }

        return new ModAuthor(id, username, projects);
    }
}

// sort of like Mods
public struct Project {
    public uint id { get; set; }
    public string name { get; set; }

    public static Project Parse(JsonNode json) {
        uint id = json["id"]?.GetValue<uint>() ?? 0;
        string name = json["name"]?.ToString() ?? "";
        return new Project(id, name);
    }

    private Project(uint id, string name) {
        this.id = id;
        this.name = name;
    }

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

    // can be modloader specific or modloader independent
    public bool matchesKind(string modLoader) {
        var open_bracket = name.IndexOf('(');
        if (open_bracket == -1) {
            return true;
        }

        var close_bracket = name.IndexOf(')');
        if (close_bracket == -1) {
            close_bracket = name.Length;
        }

        string kind = name[(open_bracket+1)..close_bracket].ToLower();
        switch (kind) {
            case "fabric":
                return string.Equals("fabric", modLoader, StringComparison.InvariantCultureIgnoreCase);
            case "forge":
                return string.Equals("forge", modLoader, StringComparison.InvariantCultureIgnoreCase);
            case "neoforge":
                return string.Equals("neoforge", modLoader, StringComparison.InvariantCultureIgnoreCase);
            default:
                // Dunno what it is, might be additional mod description
                return true;
        }
    }
}

public struct ModFileInfo {
    public uint fileId { get; set; }
    public string fileName { get; set; }
    public uint fileLength { get; set; }
    public Result result { get; set; }

    public ModFileInfo(uint fileId, string fileName, uint length, Result result) {
        this.fileId = fileId;
        this.fileName = fileName;
        fileLength = length;
        this.result = result;
    }
    
    public static ModFileInfo NotFound() {
        return new ModFileInfo(0, "", 0, Result.NOT_FOUND);
    }
    public static ModFileInfo TimedOut() {
        return new ModFileInfo(0, "", 0, Result.TIMED_OUT);
    }
    public static ModFileInfo Unknown() {
        return new ModFileInfo(0, "", 0, Result.UNKNOWN);
    }

    public override string ToString() {
        return $"{fileName}, id:{fileId}, length:{fileLength}";
    }
}

public enum Result {
    SUCCESS, TIMED_OUT, NOT_FOUND, UNKNOWN
}
