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

    public string? SystemPrompt { get; set; }
    public string? CorrectionPrompt { get; set; }
}

public static class Configuration
{
    public static LlmConfig GetConfiguration(string file)
    {
        if (!File.Exists(file))
        {
            var defaultConfig = new LlmConfig()
            {
                Url = "http://localhost:11434/api/chat",
                Model = "llama3.1",
                SystemPrompt = "Please Adjust my Prompt\n\nI can be multi line",
                ApiKeyRequired = false,
                ApiKey = "Replace Me if needed"
            };

            var serializer = new SerializerBuilder()
               .Build();

            string yaml = serializer.Serialize(defaultConfig);
            File.WriteAllText(file, yaml);
        }

        var yamlDeserializer = new DeserializerBuilder()
            .Build();

        return yamlDeserializer.Deserialize<LlmConfig>(File.ReadAllText(file, Encoding.UTF8));
    }
}
