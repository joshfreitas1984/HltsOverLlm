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


public static class TranslationService
{
    public static TextFileToSplit[] GetTextFilesToSplit()
        => [
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

    public static void Export(string workingDirectory)
    {
        string inputPath = $"{workingDirectory}/TextAsset";
        string outputPath = $"{workingDirectory}/Export";

        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        foreach (var textFileToTranslate in GetTextFilesToSplit())
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

    public static async Task PackageFinalTranslation(string workingDirectory)
    {
        string inputPath = $"{workingDirectory}/Translated";
        string outputPath = $"{workingDirectory}/Mod/EnglishLlmByLash/config/textfiles";
        string failedPath = $"{workingDirectory}/TestResults/Failed";

        if (Directory.Exists(outputPath))
            Directory.Delete(outputPath, true);

        if (Directory.Exists(failedPath))
            Directory.Delete(failedPath, true);

        Directory.CreateDirectory(outputPath);
        Directory.CreateDirectory(failedPath);

        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            var failedLines = new List<string>();
            var finalLines = new List<string>();

            foreach (var line in fileLines)
            {
                var splits = line.Raw.Split('\t');
                var failed = false;

                foreach (var split in line.Splits)
                {
                    if (!string.IsNullOrEmpty(split.Translated))
                        splits[split.Split] = split.Translated;
                    //If it was already blank its all good
                    else if (!string.IsNullOrEmpty(split.Text)) 
                        failed = true;
                }

                line.Translated = string.Join('\t', splits);

                if (!failed)
                    finalLines.Add(line.Translated);
                else
                    failedLines.Add(line.Raw);
            }

            if (finalLines.Count > 0)
                File.WriteAllLines($"{outputPath}/{textFileToTranslate.Path}", finalLines);

            if (failedLines.Count > 0)
                File.WriteAllLines($"{failedPath}/{textFileToTranslate.Path}", failedLines);

            await Task.CompletedTask;
        });

        ModHelper.GenerateModConfig(workingDirectory);
    }


    public static async Task IterateThroughTranslatedFilesAsync(string workingDirectory, Func<string, TextFileToSplit, List<TranslationLine>, Task> performActionAsync)
    {
        string outputPath = $"{workingDirectory}/Translated";

        foreach (var textFileToTranslate in GetTextFilesToSplit())
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

    public static async Task FillTranslationCache(string workingDirectory, int charsToCache, Dictionary<string, string> cache)
    {
        await TranslationService.IterateThroughTranslatedFilesAsync(workingDirectory, async (outputFile, textFileToTranslate, fileLines) =>
        {
            foreach (var line in fileLines)
            {
                foreach (var split in line.Splits)
                {
                    if (string.IsNullOrEmpty(split.Translated))
                        continue;

                    if (split.Text.Length <= charsToCache && !cache.ContainsKey(split.Text))
                        cache.Add(split.Text, split.Translated);
                }
            }

            await Task.CompletedTask;
        });
    }

    public static async Task TranslateViaLlmAsync(string workingDirectory, bool forceRetranslation, bool useTranslationCache = true)
    {
        string inputPath = $"{workingDirectory}/Export";
        string outputPath = $"{workingDirectory}/Translated";

        // Translation Cache - for smaller translations that tend to hallucinate
        var translationCache = new Dictionary<string, string>();
        var charsToCache = 6;

        if (useTranslationCache)
            await FillTranslationCache(workingDirectory, charsToCache, translationCache);

        // Create output folder
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        var config = Configuration.GetConfiguration(workingDirectory);

        // Create an HttpClient instance
        using var client = new HttpClient();

        if (config.ApiKeyRequired)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        foreach (var textFileToTranslate in GetTextFilesToSplit())
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
                    {
                        var cacheHit = translationCache.ContainsKey(split.Text);

                        if (string.IsNullOrEmpty(split.Translated) || forceRetranslation)
                        {
                            if (useTranslationCache && cacheHit)
                                split.Translated = translationCache[split.Text];
                            else
                                split.Translated = await TranslateSplitAsync(config, split.Text, client);

                            recordsProcessed++;
                        }

                        //Two translations could be doing this at the same time
                        if (!cacheHit && useTranslationCache && split.Text.Length <= charsToCache && !string.IsNullOrEmpty(split.Translated))
                            translationCache.TryAdd(split.Text, split.Translated);
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

    public static async Task<string> TranslateSplitAsync(LlmConfig config, string? raw, HttpClient client, bool ignoreCheck = false, bool optimisationMode = false)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        // If it is already translated or just special characters return it
        if (!Regex.IsMatch(raw, @"\p{IsCJKUnifiedIdeographs}"))
            return raw;

        var optimisationFolder = $"{config.WorkingDirectory}/TestResults/Optimisation";

        // Define the request payload
        var messages = new List<object>
        {
            LlmHelpers.GenerateSystemPrompt(config.Prompts["BaseSystemPrompt"]),
            LlmHelpers.GenerateUserPrompt(raw)
        };

        try
        {
            var translationValid = false;
            var retryCount = 0;
            var result = string.Empty;
            var requestData = string.Empty;

            while (!translationValid && retryCount < (config.RetryCount ?? 1))
            {
                // Create an HttpContent object
                requestData = LlmHelpers.GenerateLlmRequestData(config, messages);
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

                //// Deepseek
                //if (result.StartsWith("<think>"))
                //    result = Regex.Replace(result, @"<think>.*?</think>\n\n(.*)", "$1", RegexOptions.Singleline);

                if (!ignoreCheck)
                {
                    var validationResult = CheckTransalationSuccessful(config, raw, result);
                    translationValid = validationResult.Valid;

                    // Append history of failures
                    if (!translationValid)
                    {
                        messages.Add(LlmHelpers.GenerateAssistantPrompt(result));
                        messages.Add(LlmHelpers.GenerateUserPrompt($"{config.Prompts["BaseCorrectionPrompt"]}{validationResult.CorrectionPrompt}"));
                    }
                }
                else
                    translationValid = true;

                retryCount++;
            }

            if (optimisationMode && retryCount > 1)
                File.WriteAllText($"{optimisationFolder}/{DateTime.Now.ToString("yyyyMMddHHmmss")}-{Guid.NewGuid()}.json", requestData);

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

    public static void AddPromptWithValues(this StringBuilder builder, LlmConfig config, string promptName, params string[] values)
    {
        var prompt = string.Format(config.Prompts[promptName], values);
        builder.AppendLine(prompt);
    }

    public static ValidationResult CheckTransalationSuccessful(LlmConfig config, string raw, string result)
    {
        var response = true;
        var correctionPrompts = new StringBuilder();

        if (string.IsNullOrEmpty(raw))
            response = false;

        //Alternativves
        if (result.Contains('/') && !raw.Contains('/'))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", "/");
        }

        if (result.Contains('\\') && !raw.Contains('\\'))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", "\\");
        }

        // Small source with 'or' is ususually an alternative
        if (result.Contains("or") && raw.Length < 4)
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", "or");
        }

        // Small source with ';' is ususually an alternative
        if (result.Contains(';') && raw.Length < 4)
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", ";");
        }

        //// Added Brackets (Literation) where no brackets or widebrackets in raw
        if (result.Contains('(') && !raw.Contains('(') && !raw.Contains('（'))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectExplainationPrompt");
        }

        // Added literal
        if (result.Contains("(lit."))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectExplainationPrompt");
        }

        //Place holders - incase the model ditched them
        if (raw.Contains("{0}") && !result.Contains("{0}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectPlaceholderPrompt", "{0}");
        }
        if (raw.Contains("{1}") && !result.Contains("{1}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectPlaceholderPrompt", "{1}");
        }
        if (raw.Contains("{2}") && !result.Contains("{2}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectPlaceholderPrompt", "{2}");
        }
        if (raw.Contains("{3}") && !result.Contains("{3}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectPlaceholderPrompt", "{3}");
        }
        if (raw.Contains("{name_1}") && !result.Contains("{name_1}"))
        {
            if (result.Contains("{Name_1}"))
                result = result.Replace("{Name_1}", "{name_1}");
            else
            {
                response = false;
                correctionPrompts.AddPromptWithValues(config, "CorrectPlaceholderPrompt", "{name_1}");
            }
        }
        if (raw.Contains("{name_2}") && !result.Contains("{name_2}"))
        {
            if (result.Contains("{Name_2}"))
                result = result.Replace("{Name_2}", "{name_2}");
            else
            {
                response = false;
                correctionPrompts.AddPromptWithValues(config, "CorrectPlaceholderPrompt", "{name_2}");
            }
        }

        // This can cause bad hallucinations if not being explicit on retries
        if (raw.Contains("<br>") && !result.Contains("<br>"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectTagBrPrompt");
        }
        // New Lines only if <br> correction isnt there helps quite a bit
        else if (result.Contains('\n') && !raw.Contains('\n'))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectNewLinesPrompt");
        }
        else if (raw.Contains("<color") && !result.Contains("<color"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectTagColorPrompt");
        }
        else if (raw.Contains('<'))
        {
            // Check markup
            var markup = FindMarkup(raw);
            if (markup.Count > 0)
            {
                var resultMarkup = FindMarkup(result);
                if (resultMarkup.Count != markup.Count)
                {
                    response = false;
                    correctionPrompts.AddPromptWithValues(config, "CorrectTagMiscPrompt");
                }
            }
        }

        if (Regex.IsMatch(result, @"\p{IsCJKUnifiedIdeographs}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectChinesePrompt");
        }

        return new ValidationResult
        {
            Valid = response,
            CorrectionPrompt = correctionPrompts.ToString(),
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
