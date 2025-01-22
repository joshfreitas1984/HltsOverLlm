using System.Diagnostics;
using System.Dynamic;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Schema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Translate;

public class TranslatedRaw(string raw)
{
    public string Raw { get; set; } = raw;
    public string Trans { get; set; } = string.Empty;
}

public class TextFileToSplit
{
    public string? Path { get; set; }
    public int[]? SplitIndexes { get; set; }
}

public class TranslationSplit
{
    public int Split { get; set; } = 0;
    public string Text { get; set; } = string.Empty;
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
    public int LineNum { get; set; } = 0;
    public string Raw { get; set; } = string.Empty;
    public string? Translated { get; set; }
    public List<TranslationSplit> Splits { get; set; } = [];

    public TranslationLine() { }

    public TranslationLine(int lineNum, string raw)
    {
        LineNum = lineNum;
        Raw = raw;
    }
}

public class ValidationResult
{
    public bool Valid;
    public string CorrectionPrompts;
}

public static class Translation
{
    public static TextFileToSplit[] TextFilesToSplit = [
          new() { Path = "AchievementItem.txt", SplitIndexes = [1, 2, 12] },
          new() { Path = "AreaItem.txt", SplitIndexes = [1] },
          new() { Path = "BufferItem.txt", SplitIndexes = [1,2] },
          new() { Path = "CharacterPropertyItem.txt", SplitIndexes = [5] },
          new() { Path = "CreatePlayerQuestionItem.txt", SplitIndexes = [1] },
          new() { Path = "DefaultSkillItem.txt", SplitIndexes = [1] },
          new() { Path = "DefaultTalentItem.txt", SplitIndexes = [1] },
          new() { Path = "EquipInventoryItem.txt", SplitIndexes = [1,3] },
          new() { Path = "EventCubeItem.txt", SplitIndexes = [1] },
          new() { Path = "HelpItem.txt", SplitIndexes = [3,4] },
          new() { Path = "NicknameItem.txt", SplitIndexes = [1,2] },
          new() { Path = "NormalBufferItem.txt", SplitIndexes = [1] },
          new() { Path = "NormalInventoryItem.txt", SplitIndexes = [1,3] },
          new() { Path = "NpcItem.txt", SplitIndexes = [1] },
          new() { Path = "NpcTalkItem.txt", SplitIndexes = [6] },
          new() { Path = "ReforgeItem.txt", SplitIndexes = [3] },
          new() { Path = "SkillNodeItem.txt", SplitIndexes = [1,2] },
          new() { Path = "SkillTreeItem.txt", SplitIndexes = [1,3] },
          new() { Path = "StringTableItem.txt", SplitIndexes = [1] },
          new() { Path = "TalentItem.txt", SplitIndexes = [1,2] },
          new() { Path = "TeleporterItem.txt", SplitIndexes = [1] },
          new() { Path = "QuestItem.txt", SplitIndexes = [1,3] },
        ];

    public static void Export()
    {
        string inputPath = "../../../../Files/TextAsset";
        string outputPath = "../../../../Files/Export";

        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        foreach (var textFileToTranslate in TextFilesToSplit)
        {
            var lines = File.ReadAllLines($"{inputPath}/{textFileToTranslate.Path}");

            var exportContent = new List<TranslationLine>();
            var lineNum = 0;

            foreach (var line in lines)
            {
                var translationLine = new TranslationLine(lineNum, line);

                if (!line.StartsWith('#'))
                {
                    var splits = line.Split('\t');
                    foreach (var index in textFileToTranslate.SplitIndexes!)
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

    public static async Task IterateThroughTranslatedFilesAsync(Func<string, TextFileToSplit, List<TranslationLine>, Task> performActionAsync)
    {
        string outputPath = "../../../../Files/Translated";

        foreach (var textFileToTranslate in Translation.TextFilesToSplit)
        {
            var outputFile = $"{outputPath}/{textFileToTranslate.Path}";

            if (!File.Exists(outputFile))
                continue;

            var content = File.ReadAllText(outputFile);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var fileLines = deserializer.Deserialize<List<TranslationLine>>(content);

            if (performActionAsync != null)
                await performActionAsync(outputFile, textFileToTranslate, fileLines);
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
        using var client = new HttpClient();

        if (config.ApiKeyRequired)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        foreach (var textFileToTranslate in TextFilesToSplit)
        {
            var inputFile = $"{inputPath}/{textFileToTranslate.Path}";
            var outputFile = $"{outputPath}/{textFileToTranslate.Path}";

            if (!File.Exists(outputFile))
                File.Copy(inputFile, outputFile);

            var content = File.ReadAllText(outputFile);

            Console.WriteLine($"Processing File: {outputFile}");

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var fileLines = deserializer.Deserialize<List<TranslationLine>>(content);
            var serializer = new SerializerBuilder()
               .WithNamingConvention(CamelCaseNamingConvention.Instance)
               .Build();

            var batchSize = config.BatchSize ?? 20;
            var totalLines = fileLines.Count;
            var stopWatch = Stopwatch.StartNew();

            for (int i = 0; i < totalLines; i += batchSize)
            {
                stopWatch.Restart();

                int batchRange = Math.Min(batchSize, totalLines - i);

                // Use a slice of the list directly
                var batch = fileLines.GetRange(i, batchRange);

                int recordsProcessed = 0;

                // Process the batch in parallel
                await Task.WhenAll(batch.Select(async line =>
                {
                    foreach (var split in line.Splits)
                        if (string.IsNullOrEmpty(split.Translated) || forceRetranslation)
                        {
                            split.Translated = await TranslateSplitAsync(config, split.Text, client);
                            recordsProcessed++;
                        }
                }));

                var elapsed = stopWatch.ElapsedMilliseconds;
                var speed = recordsProcessed == 0 ? 0 : elapsed / recordsProcessed;
                Console.WriteLine($"Line: {i + batchRange} of {totalLines} ({elapsed} ms ~ {speed}/line)");

                if (recordsProcessed > 0)
                    await File.WriteAllTextAsync(outputFile, serializer.Serialize(fileLines));
            }
        }
    }

    public static object GenerateSystemPrompt(string? systemPrompt)
    {
        return new { role = "system", content = systemPrompt };
    }

    public static object GenerateUserPrompt(string? text)
    {
        return new { role = "user", content = text };
    }

    public static object GenerateAssistantPrompt(string? text)
    {
        return new { role = "assistant", content = text };
    }

    public static string GenerateLlmRequestData(LlmConfig config, List<object> messages)
    {
        if (config.ModelParams != null)
        {
            // Create a dynamic object and populate it with Params
            dynamic requestBody = new ExpandoObject();
            requestBody.model = config.Model;
            requestBody.stream = false;
            requestBody.messages = messages;

            // Add each key-value pair from Params to the dynamic object
            var requestBodyDict = (IDictionary<string, object>)requestBody;
            foreach (var param in config.ModelParams)
                requestBodyDict[param.Key] = param.Value;

            return JsonSerializer.Serialize(requestBody);
        }
        else
        {
            var requestBody = new
            {
                model = config.Model,
                temperature = 0.1,
                max_tokens = 1000,
                top_p = 1.0,
                top_k = 20,
                min_p = 0.05,
                frequency_penalty = 0,
                presence_penalty = 0,
                stream = false,
                messages
            };

            return JsonSerializer.Serialize(requestBody);
        }
    }

    public static async Task<string> TranslateSplitAsync(LlmConfig config, string? raw, HttpClient client, bool ignoreCheck = false, bool outputResponse = false)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        // Define the request payload
        var messages = new List<object>
        {
            GenerateSystemPrompt(config.SystemPrompt),
            GenerateUserPrompt(raw)
        };

        try
        {
            bool translationValid = false;
            int retryCount = 0;
            string result = string.Empty;

            while (!translationValid && retryCount < (config.RetryCount ?? 1))
            {
                // Create an HttpContent object
                var requestData = GenerateLlmRequestData(config, messages);
                HttpContent content = new StringContent(requestData, Encoding.UTF8, "application/json");

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

                if (outputResponse)
                    Console.WriteLine($"Response:\n{responseBody}\n");

                //// Deepseek
                //if (result.StartsWith("<think>"))
                //    result = Regex.Replace(result, @"<think>.*?</think>\n\n(.*)", "$1", RegexOptions.Singleline);

                if (!ignoreCheck)
                {
                    var validationResult = CheckTransalationSuccessful(raw, result);
                    translationValid = validationResult.Valid;

                    // Append history of failures
                    if (!translationValid)
                    {
                        messages.Add(GenerateAssistantPrompt(result));
                        messages.Add(GenerateUserPrompt($"{config.CorrectionPrompt}{validationResult.CorrectionPrompts}"));
                    }
                }
                else
                    translationValid = true;

                retryCount++;
            }

            if (!translationValid)
                Console.WriteLine($"Invalid Line: {raw}");

            return translationValid ? CleanupLine(result, raw) : string.Empty;
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

    public static ValidationResult CheckTransalationSuccessful(string raw, string result)
    {
        var response = true;
        var correctionPrompts = string.Empty;

        if (string.IsNullOrEmpty(raw))
            response = false;   

        if (result.Contains('/') && !raw.Contains('/'))
        {
            response = false;
            correctionPrompts += "\n- Stop providing alternatives after /";
        }

        if (result.Contains('\\') && !raw.Contains('\\'))
        {
            response = false;
            correctionPrompts += "\n- Stop providing alternatives after \\";
        }

        // Added New Lines
        if (result.Contains('\n') && !raw.Contains('\n'))
        {
            response = false;
            correctionPrompts += "\n- Stop adding new lines";
        }

        //// Added Brackets (Literation) where no brackets or widebrackets in raw
        if (result.Contains('(') && !raw.Contains('(') && !raw.Contains('（'))
        {
            response = false;
            correctionPrompts += "\n- Stop adding context and explainations that are in ()";
        }

        // Added literal
        if (result.Contains("(lit."))
        {
            response = false;
            correctionPrompts += "\n- Stop adding context and explainations";
        }

        //Place holders - incase the model ditched them
        if (raw.Contains("{0}") && !result.Contains("{0}"))
        {
            response = false;
            correctionPrompts += "\n- {0} has been removed";
        }
        if (raw.Contains("{1}") && !result.Contains("{1}"))
        {
            response = false;
            correctionPrompts += "\n- {1} has been removed";
        }
        if (raw.Contains("{2}") && !result.Contains("{2}"))
        {
            response = false;
            correctionPrompts += "\n- {2} has been removed";
        }
        if (raw.Contains("{3}") && !result.Contains("{3}"))
        {
            response = false;
            correctionPrompts += "\n- {3} has been removed";
        }
        if (raw.Contains("{name_1}") && !result.Contains("{name_1}"))
        {
            if (result.Contains("{Name_1}"))
                result = result.Replace("{Name_1}", "{name_1}");
            else
            {
                response = false;
                correctionPrompts += "\n- {name_1} has been removed";
            }
        }
        if (raw.Contains("{name_2}") && !result.Contains("{name_2}"))
        {
            if (result.Contains("{Name_2}"))
                result = result.Replace("{Name_2}", "{name_2}");
            else
            {
                response = false;
                correctionPrompts += "\n- {name_2} has been removed";
            }
        }

        // This can cause bad hallucinations if not being explicit on retries
        if (raw.Contains("<br>") && !result.Contains("<br>"))
        {
            response = false;
            correctionPrompts += "\n- <br> has been removed";
        }
        else if (raw.Contains("<color") && !result.Contains("<color"))
        {
            response = false;
            correctionPrompts += "\n- <color> has been removed";
        }
        else if (raw.Contains("<"))
        {
            // Check markup
            var markup = FindMarkup(raw);
            if (markup.Count > 0)
            {
                var resultMarkup = FindMarkup(result);
                if (resultMarkup.Count != markup.Count)
                {
                    response = false;
                    correctionPrompts += "\n- Markup has been applied incorrectly";
                }
            }
        }

        return new ValidationResult
        {
            Valid = response,
            CorrectionPrompts = correctionPrompts
        };
    }

    public static List<string> FindMarkup(string input)
    {
        var markupTags = new List<string>();

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

    public static List<string> FindPlaceholders(string input)
    {
        var placeholders = new List<string>();

        if (input == null)
            return placeholders;

        // Regular expression to match placeholders in the format {number}
        string pattern = "\\{.+\\}";
        MatchCollection matches = Regex.Matches(input, pattern);

        // Add each match to the list of placeholders
        foreach (Match match in matches)
            placeholders.Add(match.Value);

        return placeholders;
    }
}
