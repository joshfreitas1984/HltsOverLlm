using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Translate;

public class TextFileToSplit
{
    public string? Path { get; set; }
    public int[]? SplitIndexes { get; set; }
}

public class TranslationSplit
{
    public int Split { get; set; }
    public string Text { get; set; }
    public string? Translated { get; set; }

    public TranslationSplit() { }

    public TranslationSplit(int split, string text)
    {
        Split = split;
        Text = text;
    }
}

public class TranslationLine
{
    public int LineNum { get; set; }
    public string Raw { get; set; }
    public string? Translated { get; set; }
    public List<TranslationSplit> Splits { get; set; }

    public TranslationLine() { }
    public TranslationLine(int lineNum, string raw)
    {
        LineNum = lineNum;
        Raw = raw;
        Splits = new List<TranslationSplit>();
    }
}

public static class Translation
{
    static TextFileToSplit[] textFiles = [
          //new() { Path = "AchievementItem.txt", SplitIndexes = [1, 2, 12] },
          //new() { Path = "AreaItem.txt", SplitIndexes = [1] },
          //new() { Path = "BufferItem.txt", SplitIndexes = [1,2] },
          //new() { Path = "CharacterPropertyItem.txt", SplitIndexes = [5] },
          //new() { Path = "CreatePlayerQuestionItem.txt", SplitIndexes = [1] },
          //new() { Path = "DefaultSkillItem.txt", SplitIndexes = [1] },
          //new() { Path = "DefaultTalentItem.txt", SplitIndexes = [1] },
          //new() { Path = "EquipInventoryItem.txt", SplitIndexes = [1,3] },
          //new() { Path = "EventCubeItem.txt", SplitIndexes = [1] },
          //new() { Path = "HelpItem.txt", SplitIndexes = [3,4] },
          //new() { Path = "NicknameItem.txt", SplitIndexes = [1,2] },
          //new() { Path = "NormalBufferItem.txt", SplitIndexes = [1] },
          //new() { Path = "NormalInventoryItem.txt", SplitIndexes = [1,3] },
          //new() { Path = "NpcItem.txt", SplitIndexes = [1] },
          //new() { Path = "NpcTalkItem.txt", SplitIndexes = [6] },
          //new() { Path = "ReforgeItem.txt", SplitIndexes = [3] },
          //new() { Path = "SkillNodeItem.txt", SplitIndexes = [1,2] },
          //new() { Path = "SkillTreeItem.txt", SplitIndexes = [1,3] },
          new() { Path = "StringTableItem.txt", SplitIndexes = [1] },
          //new() { Path = "TalentItem.txt", SplitIndexes = [1,2] },
          //new() { Path = "TeleporterItem.txt", SplitIndexes = [1] },
          //new() { Path = "QuestItem.txt", SplitIndexes = [1,3] },
        ];

    public static void Export()
    {
        string inputPath = "../../../../Files/TextAsset";
        string outputPath = "../../../../Files/Export";

        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        foreach (var textFileToTranslate in textFiles)
        {
            var lines = File.ReadAllLines($"{inputPath}/{textFileToTranslate.Path}");

            var exportContent = new List<TranslationLine>();
            var lineNum = 0;

            foreach (var line in lines)
            {
                var translationLine = new TranslationLine(lineNum, line);

                if (!line.StartsWith("#"))
                {
                    var splits = line.Split('\t');
                    foreach (var index in textFileToTranslate.SplitIndexes)
                        translationLine.Splits.Add(new TranslationSplit(index, splits[index]));
                }

                exportContent.Add(translationLine);
                lineNum++;
            }

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            File.WriteAllText($"{outputPath}\\{textFileToTranslate.Path}", serializer.Serialize(exportContent));
        }
    }

    public static async Task TranslateViaLlmAsync(bool forceRetranslation)
    {
        string inputPath = "../../../../Files/Export";
        string outputPath = "../../../../Files/Translated";

        // Create output folder
        if (!Directory.Exists(outputPath))        
            Directory.CreateDirectory(outputPath);       

        var config = Configuration.GetConfiguration($"{inputPath}/../Config.yaml");

        // Create an HttpClient instance
        using HttpClient client = new HttpClient();

        if (config.ApiKeyRequired)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        foreach (var textFileToTranslate in textFiles)
        {
            var inputFile = $"{inputPath}/{textFileToTranslate.Path}";
            var outputFile = $"{outputPath}/{textFileToTranslate.Path}";

            if (!File.Exists(outputFile))
                File.Copy(inputFile, outputFile);

            var content = File.ReadAllText(outputFile);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var fileLines = deserializer.Deserialize<List<TranslationLine>>(content);
            var serializer = new SerializerBuilder()
               .WithNamingConvention(CamelCaseNamingConvention.Instance)
               .Build();

            //foreach (var line in fileLines)
            //{
            //    foreach (var split in line.Splits)
            //        if (string.IsNullOrEmpty(split.Translated) || forceRetranslation)
            //            split.Translated = await TranslateSplitAsync(config, split.Text, client);

            //    Console.WriteLine($"Line: {line.LineNum} of {fileLines.Count}");

            //    if (line.LineNum % 10 == 0)
            //    {
            //        Console.WriteLine($"Writing Progress...");
            //        File.WriteAllText(outputFile, serializer.Serialize(fileLines));
            //    }
            //}

            var batchSize = 5;
            var totalLines = fileLines.Count;
            var stopWatch = Stopwatch.StartNew();

            for (int i = 0; i < totalLines; i += batchSize)
            {
                stopWatch.Restart();
                
                int batchRange = Math.Min(batchSize, totalLines - i);

                // Use a slice of the list directly
                var batch = fileLines.GetRange(i, batchRange);

                // Process the batch in parallel
                await Task.WhenAll(batch.Select(async line =>
                {
                    foreach (var split in line.Splits)
                        if (string.IsNullOrEmpty(split.Translated) || forceRetranslation)
                            split.Translated = await TranslateSplitAsync(config, split.Text, client);
                }));

                var elapsed = stopWatch.ElapsedMilliseconds;
                Console.WriteLine($"Line: {i + batchRange} of {totalLines} ({elapsed} ms ~ {elapsed / batchRange}/line)");
                File.WriteAllText(outputFile, serializer.Serialize(fileLines));
            }
        }
    }

    public static async Task<string> TranslateSplitAsync(LlmConfig config, string text, HttpClient client)
    {       
        // Define the request payload
        var requestData = GetRequestData(config.SystemPrompt, config.Model, [ text ] );
        
        // Create an HttpContent object
        HttpContent content = new StringContent(requestData, Encoding.UTF8, "application/json");

        try
        {
            bool translationValid = false;
            int retryCount = 0;
            string result = string.Empty;

            while (!translationValid && retryCount < 3)
            {
                // Make the POST request
                HttpResponseMessage response = await client.PostAsync(config.Url, content);

                // Ensure the response was successful
                response.EnsureSuccessStatusCode();

                // Read and display the response content
                string responseBody = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseBody);
                result = jsonDoc.RootElement
                    .GetProperty("message")!
                    .GetProperty("content")!
                    .GetString()
                    ?.Trim() ?? string.Empty;

                translationValid = CheckTransalationSuccessful(text, result);
            }

            retryCount++;
            return translationValid ? CleanupLine(result, text) : string.Empty;
    }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Request error: {e.Message}");
            return string.Empty;
        }
    }

    public static string CleanupLine(string input, string raw)
    {
        if (!string.IsNullOrEmpty(input))
        {
            if (input.Contains('\"') && !raw.Contains('\"'))
                input = input.Replace("\"", "");

            if (input.Contains('[') && !raw.Contains('['))
                input = input.Replace("[", "");

            if (input.Contains(']') && !raw.Contains(']'))
                input = input.Replace("]", "");
        }

        return input;
    }
    public static bool CheckTransalationSuccessful(string raw, string result)
    {
        //Check placeholders
        var placeholders = FindPlaceholders(raw);
        if (placeholders.Count > 0)
        {
            var resultPlaceholders = FindPlaceholders(result);
            if (resultPlaceholders.Count != placeholders.Count)
                return false;
        }

        //Check markup
        var markup = FindMarkup(raw);
        if (markup.Count > 0)
        {
            var resultMarkup = FindMarkup(result);
            if (resultMarkup.Count != markup.Count)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Finds markup tags in the format <tag> in the given string.
    /// </summary>
    /// <param name="input">The input string to search.</param>
    /// <returns>A list of markup tags found in the string.</returns>
    public static List<string> FindMarkup(string input)
    {
        List<string> markupTags = new List<string>();

        if (input == null)
            return markupTags;

        // Regular expression to match markup tags in the format <tag>
        string pattern = "<[^>]+>";
        MatchCollection matches = Regex.Matches(input, pattern);

        // Add each match to the list of markup tags
        foreach (Match match in matches)
            markupTags.Add(match.Value);

        return markupTags;
    }

    /// <summary>
    /// Finds placeholders in the format {0}, {1}, etc., in the given string.
    /// </summary>
    /// <param name="input">The input string to search.</param>
    /// <returns>A list of placeholders found in the string.</returns>
    public static List<string> FindPlaceholders(string input)
    {
        List<string> placeholders = new List<string>();

        if (input == null)
            return placeholders;

        // Regular expression to match placeholders in the format {number}
        string pattern = "\\{\\d+\\}";
        MatchCollection matches = Regex.Matches(input, pattern);

        // Add each match to the list of placeholders
        foreach (Match match in matches)
            placeholders.Add(match.Value);

        return placeholders;
    }

    public static string GetRequestData(string? systemPrompt, string? model, string[] texts)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var text in texts)
        {
            messages.Add(new { role = "user", content = text });
        }

        var requestBody = new
        {
            model,
            temperature = 0.1,
            max_tokens = 1000,
            top_p = 1,
            frequency_penalty = 0,
            presence_penalty = 0,
            stream = false,
            messages
        };

        return JsonSerializer.Serialize(requestBody);
    }
}
