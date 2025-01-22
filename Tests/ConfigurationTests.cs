using System.Drawing;
using Translate;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic;

namespace Translate.Tests;

public class ConfigurationTests
{
    const string workingDirectory = "../../../../Files";
    
    [Fact]
    public async Task CachePromptsTest()
    {
        var prompts = Configuration.CachePrompts(workingDirectory);

        var serializer = new SerializerBuilder()
                 .WithNamingConvention(CamelCaseNamingConvention.Instance)
                 .Build();

        await File.WriteAllTextAsync($"{workingDirectory}/TestResults/AllPrompts.yaml", serializer.Serialize(prompts));
    }
}
