using System.Text;

namespace Translate;

public class Glossary
{
    public static string ConstructGlossaryPrompt(string raw, LlmConfig config)
    {
        var prompt = new StringBuilder();

        var names = AppendPromptsFor(raw, config.GameData.Names.Entries);
        var locations = AppendPromptsFor(raw, config.GameData.Locations.Entries);
        var specialTerms = AppendPromptsFor(raw, config.GameData.SpecialTerms.Entries);
        var factions = AppendPromptsFor(raw, config.GameData.Factions.Entries);

        if (names.Length > 0)
        {
            prompt.AppendLine("#### Character Names");
            prompt.Append(names);
        }

        if (locations.Length > 0)
        {
            prompt.AppendLine("#### Locations and Places");
            prompt.Append(locations);
        }

        if (specialTerms.Length > 0 || factions.Length > 0)
        {
            prompt.AppendLine("#### Special Terms");

            if (specialTerms.Length > 0)
                prompt.Append(specialTerms);

            if (factions.Length > 0)
                prompt.Append(factions);
        }

        if (prompt.Length > 0)
        {           
            var resultPrompt = $"{config.Prompts["BaseGlossaryPrompt"]}\n{prompt}";
            return resultPrompt;
        }
        else
            return string.Empty;
    }

    public static void AddGlossaryToCache(LlmConfig config, Dictionary<string, string> cache)
    {
        AddGlossaryLinesToCache(cache, config.GameData.Names.Entries);
        AddGlossaryLinesToCache(cache, config.GameData.Locations.Entries);
        AddGlossaryLinesToCache(cache, config.GameData.SpecialTerms.Entries);
        AddGlossaryLinesToCache(cache, config.GameData.Factions.Entries);
    }

    public static void AddGlossaryLinesToCache(Dictionary<string, string> cache, List<DataLine> dataLines)
    {
        foreach(var line in dataLines)
            if (!cache.ContainsKey(line.Raw))
                cache.Add(line.Raw, line.Result);
    }

    private static string AppendPromptsFor(string raw, List<DataLine> dataLines)
    {
        var prompt = new StringBuilder();

        foreach (var line in dataLines)
        {
            if (raw.Contains(line.Raw))
                prompt.Append($"- {line.Raw}: {line.Result}\n");
        }

        return prompt.ToString();
    }
}
