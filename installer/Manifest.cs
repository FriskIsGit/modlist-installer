using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;

namespace modlist_installer.installer;

public class Manifest {
    public readonly string name;
    public readonly string version;
    public readonly string author;
    // set to 0 for named urls, set to mod id for old urls
    public List<ManifestFile> files;
    
    public Manifest(string name, string version, string author, int fileCapacity) {
        this.name = name;
        this.version = version;
        this.author = author;
        files = new List<ManifestFile>(fileCapacity);
    }
    
    public static Manifest? parseManifest(string path) {
        if (!File.Exists(path)) {
            Console.WriteLine("File does not exist. Exiting.");
            return null;
        }

        string content = File.ReadAllText(path);
        
        var jsonObj = JsonNode.Parse(content);
        string name = jsonObj?["name"]?.ToString() ?? "";
        string version = jsonObj?["version"]?.ToString() ?? "";
        string author = jsonObj?["author"]?.ToString() ?? "";
        JsonArray? filesArr = jsonObj?["files"]?.AsArray();

        if (filesArr is null) {
            return new Manifest(name, version, author, 0);
        }

        var manifest = new Manifest(name, version, author, filesArr.Count);
        foreach (var fileElement in filesArr) {
            if (fileElement is null) {
                continue;
            }

            var file = ManifestFile.Parse(fileElement);
            manifest.files.Add(file);
        }

        return manifest;
    }
    
    public static void serializeManifest(Manifest manifest, string path) {
        using var file = File.Create(path);
        using var stream = new BufferedStream(file);
        var jsonOptions = new JsonWriterOptions {
            Indented = true
        };
        var writer = new Utf8JsonWriter(stream, jsonOptions);

        writer.WriteStartObject();
        writer.WriteString("name", manifest.name);
        writer.WriteString("author", manifest.author);
        writer.WriteString("version", manifest.version);
        writer.WriteStartArray("files");
        foreach (var mod in manifest.files) {
            writer.WriteStartObject();
            writer.WriteNumber("fileID", mod.fileID);
            writer.WriteNumber("projectID", mod.projectID);
            writer.WriteBoolean("required", mod.required);
            writer.WriteEndObject();
            writer.Flush();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
    }
}

public class ManifestFile {
    public readonly uint projectID;
    public readonly uint fileID;
    public readonly bool required;

    public ManifestFile(uint projectId, uint fileId, bool required) {
        projectID = projectId;
        fileID = fileId;
        this.required = required;
    }

    public static ManifestFile Parse(JsonNode file) {
        var projectId = uint.Parse(file["projectID"]?.ToString() ?? "");
        var fileId = uint.Parse(file["fileID"]?.ToString() ?? "");
        var isRequired = bool.Parse(file["required"]?.ToString() ?? "");
        return new ManifestFile(projectId, fileId, isRequired);
    }
}