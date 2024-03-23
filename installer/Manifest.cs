using System.Text;
using System.Text.Json.Nodes;

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