using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Translate.Tests;

public class ConfigurationTests
{
    const string workingDirectory = "../../../../Files";

    [Fact]
    public async Task CachePromptsTest()
    {
        var prompts = Configuration.CachePrompts(workingDirectory);

        var serializer = Yaml.CreateSerializer();       

        await File.WriteAllTextAsync($"{workingDirectory}/TestResults/AllPrompts.yaml", serializer.Serialize(prompts));
    }
}
