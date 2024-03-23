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
    private Version version;

    public FlameAPI() {
        // BaseAddress, Timeout, MaxResponseContentBufferSize are properties that cannot be modified..
        client.Timeout = Timeout.InfiniteTimeSpan;
        //client.Timeout = TimeSpan.FromSeconds(300);
    }

    public void setMcVersion(string mc_version) {
        // the function below must not fail
        version = fetchVersion(mc_version);
        Console.WriteLine($"gameVersionId: {version.versionId}; baseVersionId: {version.baseVersionId}");
    }

    public Version fetchVersion(string versionName) {
        var response = fetchJson(CF_MINECRAFT);
        if (response.statusCode != HttpStatusCode.OK) {
            Console.WriteLine($"Status code: {response.statusCode}");
            throw new Exception("Cannot access versions endpoints");
        }

        var jsonObj = JsonNode.Parse(response.content);
        if (jsonObj is null) {
            throw new NullReferenceException("Rare: Content is null");
        }
        var minecraft_releases = jsonObj["data"]?.AsArray();
        if (minecraft_releases is null) {
            throw new NullReferenceException("Rare: data releases is null");
        }

        string baseVersion = Version.getBaseVersion(versionName);

        uint versionId = 0;
        uint baseVersionId = 0;
        foreach (var release in minecraft_releases) {
            if (release is null) 
                continue;

            var jsonVersion = release["versionString"];
            if (jsonVersion is null) {
                continue;
            }

            string releaseVersion = jsonVersion.ToString();
            if (releaseVersion == versionName) {
                var jsonId = release["gameVersionId"];
                if (jsonId is null) {
                    continue;
                }
                versionId = jsonId.GetValue<uint>();
            } else if (releaseVersion == baseVersion) {
                var jsonId = release["gameVersionId"];
                if (jsonId is null) {
                    continue;
                }
                baseVersionId = jsonId.GetValue<uint>();
            }
        }

        if (versionId == 0) {
            throw new Exception($"gameVersionId wasn't found for given versionString: {versionName}");
        }

        return new Version(versionId, baseVersionId);
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

    private const uint PAGE_SIZE = 5;

    public ModFileInfo fetchModFile(uint mod_id) {
        return fetchModFile(mod_id, true);
    }

    private ModFileInfo fetchModFile(uint mod_id, bool firstAttempt) {
        uint gameVersion = firstAttempt ? version.versionId : version.baseVersionId;
        string url = $"{CF_MODS}/{mod_id}/files?pageSize={PAGE_SIZE}&sort=dateCreated&sortDescending=true&gameVersionId={gameVersion}&removeAlphas=true";

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

        if (files_array.Count == 0) {
            if (!firstAttempt || version.isBaseVersion) {
                return ModFileInfo.NotFound();
            }
            
            // recursive call to attempt to retrieve by base version id, shouldn't happen often
            Console.WriteLine("Not found, falling back to the base version of the mod");
            return fetchModFile(mod_id, false);
        }

        var latestMod = files_array[0];
        if (latestMod is null) {
            return ModFileInfo.Unknown();
        }
        
        var lengthNode = latestMod["fileLength"];
        var nameNode = latestMod["fileName"];
        var idNode = latestMod["id"];
        if (lengthNode is null || nameNode is null || idNode is null)
            return ModFileInfo.Unknown();
            
        var id = uint.Parse(idNode.ToString());
        var name = nameNode.ToString();
        var length = uint.Parse(lengthNode.ToString());
            
        return new ModFileInfo(id, name, length, Result.SUCCESS);
    }

    public List<Dependency> _fetchDependencies(uint modId) {
        string url = $"{CF_MODS}/{modId}/dependencies?index=0&pageSize=20";
        string content;
        try {
            var response = fetchJson(url);
            if (response.statusCode != HttpStatusCode.OK) {
                Console.WriteLine($"Status code: {response.statusCode}");
                return new List<Dependency>();
            }
            content = response.content;
        } catch (Exception e) {
            Console.WriteLine(e.Message);
            return new List<Dependency>();
        }
        
        var jsonObj = JsonNode.Parse(content);
        var dependenciesArr = jsonObj?["data"]?.AsArray();
        if (dependenciesArr is null) {
            return new List<Dependency>();
        }
        
        return new List<Dependency>();
    }
    
    
    public async Task<DownloadInfo> downloadFile(string url, string dir) {
        HttpResponseMessage response = await client.GetAsync(url);
        if (response.RequestMessage?.RequestUri is null) {
            // Should never be here executed
            return DownloadInfo.Failed();
        }
        string fileName = extractName(response.RequestMessage.RequestUri);
        long contentLength = response.Content.Headers.ContentLength ?? 0;
        await using var stream = await client.GetStreamAsync(response.RequestMessage.RequestUri);
        await using var fs = new FileStream(Path.Combine(dir, fileName), FileMode.OpenOrCreate);
        await stream.CopyToAsync(fs);
        return DownloadInfo.Ok(fileName, contentLength);
    }

    private static string extractName(Uri requestUri) {
        int lastSlash = requestUri.LocalPath.LastIndexOf('/');
        if (lastSlash == -1) {
            return requestUri.LocalPath;
        }
        return requestUri.LocalPath[(lastSlash+1)..];
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
            case "quilt":
                return string.Equals("quilt", modLoader, StringComparison.InvariantCultureIgnoreCase);
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

public struct Version {
    public readonly uint versionId;
    public readonly uint baseVersionId;
    public readonly bool isBaseVersion;

    public Version(uint versionId, uint baseVersionId) {
        this.versionId = versionId;
        this.baseVersionId = baseVersionId;
        isBaseVersion = versionId == baseVersionId;
    }

    public static string getBaseVersion(string version) {
        int lastDot = version.LastIndexOf('.');
        return lastDot == -1 ? "" : version[..lastDot];
    }
}

public struct DownloadInfo {
    public string fileName { get; set; }
    public long contentLength { get; set; }

    private DownloadInfo(string fileName, long contentLength) {
        this.fileName = fileName;
        this.contentLength = contentLength;
    }

    public static DownloadInfo Ok(string fileName, long contentLength) {
        return new DownloadInfo(fileName, contentLength);
    }
    public static DownloadInfo Failed() {
        return new DownloadInfo("", 0);
    }
}

public struct Dependency {
    public uint modId { get; set; }

    // urlName
    public string urlName { get; set; }
    public string author { get; set; }
}