using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Translate;

public class LlmConfig
{
    public string? ApiKey { get; set; }
    public bool ApiKeyRequired { get; set; }
    public string? Url { get; set; }
    public string? Model { get; set; }    
    public int? RetryCount { get; set; }
    public int? BatchSize { get; set; }
    public Dictionary<string, object>? ModelParams { get; set; }

    // Not serialised in Yaml
    public Dictionary<string, string> Prompts { get; set; } = [];
    public string? WorkingDirectory { get; set; }
}

public static class Configuration
{
    public static LlmConfig GetConfiguration(string workingDirectory)
    {
        var yamlDeserializer = new DeserializerBuilder().Build();
        var response = yamlDeserializer.Deserialize<LlmConfig>(File.ReadAllText($"{workingDirectory}/Config.yaml", Encoding.UTF8));

        response.WorkingDirectory = workingDirectory;
        response.Prompts = CachePrompts(workingDirectory);

        return response;
    }

    public static Dictionary<string, string> CachePrompts(string workingDirectory)
    {
        var prompts = new Dictionary<string, string>();
        var path = $"{workingDirectory}/Prompts";

        foreach (var file in Directory.EnumerateFiles(path))
            prompts.Add(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file));

        return prompts;
    }
}
