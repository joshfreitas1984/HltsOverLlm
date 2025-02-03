using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Translate.Tests;

public class DataGenerationTests
{
    const string workingDirectory = "../../../../Files";


    /// <summary>
    /// Stick raw glossary entry in zzConvertMe.yaml and itll give it to you in zzzConverted.yaml
    /// </summary>
    [Fact]
    public async Task ConvertPromptToDataFormat()
    {
        var dataFormat = new DataFormat();
        var convertMeContents = File.ReadAllLines($"{workingDirectory}/TestResults/zzConvertMe.yaml");

        foreach (var line in convertMeContents)
        {
            var text = line[1..];
            var splits = SplitOnFirst(text, ':');
            dataFormat.Entries.Add(new DataLine(splits.raw, splits.result));
        }

        var serializer = Yaml.CreateSerializer();
      
        await File.WriteAllTextAsync($"{workingDirectory}/TestResults/zzzConverted.yaml", serializer.Serialize(dataFormat));
    }

    public static (string raw, string result) SplitOnFirst(string input, char delimiter)
    {
        int index = input.IndexOf(delimiter);

        // Delimiter not found
        if (index == -1)
            return (input, string.Empty);

        return (input[..index].Trim(), input[(index + 1)..].Trim());
    }
}
