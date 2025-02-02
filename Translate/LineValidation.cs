using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Translate;

public class LineValidation
{
    public const string ChineseCharPattern = @".*\p{IsCJKUnifiedIdeographs}.*";

    public static string PrepareRaw(string raw)
    {
        return StripColorTags(raw);
    }

    public static string CleanupNamesResult(string input)
    {
        var result = input;
        string[] replacements = ["Senior Brother", "Senior", "Brother", "Young Master", "Young Hero"];

        result = result
            .Replace("{Name_1}", "{name_1}")
            .Replace("{Name_2}", "{name_2}");

        foreach (var title in replacements)
        {
            result = result
                .Replace($"{{name_1}} {{name_2}} {title}", $"{title} {{name_1}} {{name_2}}")
                .Replace($"{{name_1}} {{name_2}}, {title}", $"{title} {{name_1}} {{name_2}}");

            result = result
                .Replace($"{{name_1}} {title}", $"{title} {{name_1}}")
                .Replace($"{{name_1}}, {title}", $"{title} {{name_1}}")
                .Replace($"{{name_2}} {title}", $"{title} {{name_2}}")
                .Replace($"{{name_2}}, {title}", $"{title} {{name_2}}");
        }
          
        return result;
    }

    public static string PrepareResult(string llmResult)
    {
        llmResult = llmResult
            .Replace("<p>", "")
            .Replace("</p>", "")
            .Replace("<Div>", "<div>")
            .Replace("</Div>", "</div>");


        if (llmResult.Contains("<div>"))
        {
            var pattern = @"<div>(.*?)</div>";
            var result = Regex.Match(llmResult, pattern, RegexOptions.Singleline).Groups[1].Value;

            //Handle LLM adding line breaks in the div tag
            if (result.EndsWith('\n'))
                result = result[..^1];
            if (result.StartsWith('\n'))
                result = result[1..];

            result = CleanupNamesResult(result);
            return result;
        }
        else
            return llmResult;
    }

    public static ValidationResult CheckTransalationSuccessful(LlmConfig config, string raw, string result)
    {
        var response = true;
        var correctionPrompts = new StringBuilder();

        if (string.IsNullOrEmpty(raw))
            response = false;

        // Didnt translate at all and default response to prompt.
        if (result.Contains("provide the text") || result.Contains("'''") || result.Contains("<p") || result.Contains("<em"))
            response = false;

        // 99% chance its gone crazy with hallucinations
        if (result.Length > 50 && raw.Length < 4)
            response = false;

        ////Alternativves
        //if (result.Contains('/') && !raw.Contains('/'))
        //{
        //    response = false;
        //    correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", "/");
        //}

        //if (result.Contains('\\') && !raw.Contains('\\'))
        //{
        //    response = false;
        //    correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", "\\");
        //}

        // Small source with 'or' is ususually an alternative
        if ((result.Contains(" or") || result.Contains("(or")) && raw.Length < 3)
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", "or");
        }

        // Small source with 'and' is ususually an alternative
        if (result.Contains(" and") && raw.Length < 3 && !result.Contains("Spear and Staff", StringComparison.OrdinalIgnoreCase))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", "and");
        }

        // Small source with ';' is ususually an alternative
        if (result.Contains(';') && !raw.Contains(';') && raw.Length < 3)
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", ";");
        }

        //// Added Brackets (Literation) where no brackets or widebrackets in raw
        //if (result.Contains('(') && !raw.Contains('(') && !raw.Contains('（'))
        //{
        //    response = false;
        //    correctionPrompts.AddPromptWithValues(config, "CorrectExplainationPrompt");
        //}

        // Added literal
        if (result.Contains("(lit."))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectExplainationPrompt");
        }

        // Removed :
        if (raw.Contains(':') && !result.Contains(':'))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectColonSegementPrompt");
        }

        //Place holders - incase the model ditched them
        if (raw.Contains("{0}") && !result.Contains("{0}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectRemovalPrompt", "{0}");
        }
        if (raw.Contains("{1}") && !result.Contains("{1}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectRemovalPrompt", "{1}");
        }
        if (raw.Contains("{2}") && !result.Contains("{2}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectRemovalPrompt", "{2}");
        }
        if (raw.Contains("{3}") && !result.Contains("{3}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectRemovalPrompt", "{3}");
        }
        if (raw.Contains("{name_1}") && !result.Contains("{name_1}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectRemovalPrompt", "{name_1}");
        }
        if (raw.Contains("{name_2}") && !result.Contains("{name_2}"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectRemovalPrompt", "{name_2}");
        }

        // This can cause bad hallucinations if not being explicit on retries
        if (raw.Contains("<br>") && !result.Contains("<br>"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectRemovalPrompt", "<br>");
            //correctionPrompts.AddPromptWithValues(config, "CorrectTagPrompt");
        }
        // Color tags are evil
        //else if (raw.Contains("<color") && !result.Contains("<color"))
        //{
        //    response = false;
        //    correctionPrompts.AddPromptWithValues(config, "CorrectRemovalPrompt", "<color>");
        //    correctionPrompts.AddPromptWithValues(config, "CorrectTagPrompt");
        //}
        //else if (raw.Contains('<'))
        //{
        //  // Check markup
        //  var markup = FindMarkup(raw);
        //  if (markup.Count > 0)
        //  {
        //    var resultMarkup = FindMarkup(result);
        //    if (resultMarkup.Count != markup.Count)
        //    {
        //      response = false;
        //      correctionPrompts.AddPromptWithValues(config, "CorrectTagPrompt");
        //    }
        //  }
        //}

        // Random additions
        if (result.Contains("<br>") && !raw.Contains("<br>"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAdditionalPrompt", "<br>");
        }

        if (result.Contains('\n'))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAdditionalPrompt", "\\n");
        }

        //if (result.Contains("<color") && !raw.Contains("<color"))
        //{
        //    response = false;
        //    correctionPrompts.AddPromptWithValues(config, "CorrectAdditionalPrompt", "<color>");
        //}

        // It sometime can be in [] or {} or ()
        if (result.Contains("name_1") && !raw.Contains("name_1"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAdditionalPrompt", "name_1");
        }

        if (result.Contains("name_2") && !raw.Contains("name_2"))
        {
            response = false;
            correctionPrompts.AddPromptWithValues(config, "CorrectAdditionalPrompt", "name_2");
        }

        var pattern = ChineseCharPattern;
        if (Regex.IsMatch(result, pattern))
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

    public static string CleanupLineBeforeSaving(string input, string raw, string outputFile)
    {
        var result = input.Trim();
        if (!string.IsNullOrEmpty(result))
        {
            if (result.Contains('\"') && !raw.Contains('\"'))
                result = result.Replace("\"", "");

            if (result.Contains('[') && !raw.Contains('['))
                result = result.Replace("[", "");

            if (result.Contains(']') && !raw.Contains(']'))
                result = result.Replace("]", "");

            if (result.Contains('`') && !raw.Contains('`'))
                result = result.Replace("`", "'");

            // Take out wide quotes
            if (result.Contains('“') && !raw.Contains('“'))
                result = result.Replace("“", "");

            if (result.Contains('”') && !raw.Contains('”'))
                result = result.Replace("”", "");

            //Take out wide quotes
            result = result
                .Replace("’", "'")
                .Replace("‘", "'");

            //Strip .'s
            if (!outputFile.EndsWith("NpcTalkItem.txt") && result.EndsWith('.') && raw != "." && !result.EndsWith(".."))
                result = result[..^1];

            result = LineValidation.RemoveDiacritics(result);
            result = LineValidation.ReplaceIncorrectLowercaseWords(result);

            result = LineValidation.EncaseColorsForWholeLines(raw, result);
            result = LineValidation.EncaseSquareBracketsForWholeLines(raw, result);

            if (string.IsNullOrEmpty(result))
            {
                Console.WriteLine($"Something Bad happened somewhere: {raw}\n{result}");
                return result;
            }

            if (Char.IsLower(result[0]) && raw != result)
                result = Char.ToUpper(result[0]) + result[1..];
        }

        return result;
    }

    public static string CalulateCorrectionPrompt(LlmConfig config, ValidationResult validationResult, string raw, string result)
    {
        return string.Format(config.Prompts["BaseCorrectionPrompt"], raw, result, validationResult.CorrectionPrompt); ;
    }

    public static (Dictionary<string, string> mappings, string stripped) StripHtml(string raw)
    {
        // Dictionary to store mappings from placeholders to actual HTML tags.
        var tagMappings = new Dictionary<string, string>();

        // Regex pattern to match opening and closing tags separately
        var openTagPattern = @"<([a-zA-Z0-9]+)(?:\s[^>]*)?>";
        var closeTagPattern = @"</([a-zA-Z0-9]+)>";

        // Use a counter for unique placeholder IDs
        var openCounter = 0;
        var closeCounter = 0;

        // MatchEvaluator delegate to replace each opening tag with its placeholder
        string openEvaluator(Match match)
        {
            var tagName = match.Groups[1].Value; // Capture the tag name
            var placeholder = $"<TagID_{openCounter}_Open>";

            // Map the actual HTML tag to this placeholder
            tagMappings.Add(placeholder, match.Value);

            openCounter++;
            return placeholder;
        }

        // MatchEvaluator delegate to replace each closing tag with its placeholder
        string closeEvaluator(Match match)
        {
            var tagName = match.Groups[1].Value; // Capture the tag name
            var placeholder = $"<TagID_{closeCounter}_Closed>";

            // Map the actual HTML tag to this placeholder
            tagMappings.Add(placeholder, match.Value);

            closeCounter++;
            return placeholder;
        }

        // Replace opening tags with placeholders
        var processedText = Regex.Replace(raw, openTagPattern, openEvaluator);

        // Replace closing tags with placeholders
        processedText = Regex.Replace(processedText, closeTagPattern, closeEvaluator);

        return (tagMappings, raw);

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

    public static string ConvertColorTagsToPlaceholderTags(string input)
    {
        // Regex to match <color> tags and capture their contents and attributes
        string pattern = @"<color=(#[0-9A-Fa-f]{6})>(.*?)<\/color>";
        string replacement = "<mark style=\"color: $1;\">$2</mark>";

        // Replace <color> tags with <font> tags
        string result = Regex.Replace(input, pattern, replacement, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return result;
    }

    public static string ConvertPlaceholderTagsToColorTags(string input)
    {
        // Regex to match <font> tags and capture their contents and attributes
        string pattern = @"<mark style=\""color: (#[0-9A-Fa-f]{6});\"">(.*?)<\/mark>";
        string replacement = "<color=$1>$2</color>";

        // Replace <font> tags with <color> tags
        string result = Regex.Replace(input, pattern, replacement, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return result;
    }

    public static string StripColorTags(string input)
    {
        // Regex to match <color> tags and capture their contents
        string pattern = @"<color=(#[0-9A-Fa-f]{6})>(.*?)<\/color>";
        string replacement = "$2"; // Keep only the contents within the <color> tags

        // Remove <color> tags and keep contents
        string result = Regex.Replace(input, pattern, replacement, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        return result;
    }

    public static string EncaseColorsForWholeLines(string raw, string translated)
    {
        var pattern = @"(<[^>]+>).*(</[^>]+>)";

        if (raw.StartsWith("<color") && raw.EndsWith("</color>")
            && raw.LastIndexOf("<color") == 0 && !translated.StartsWith("<color"))
        {
            var matches = Regex.Matches(raw, pattern);
            string start = matches[0].Groups[1].Value;
            string end = matches[0].Groups[2].Value;
            translated = $"{start}{translated}{end}";
        }

        return translated;
    }

    public static string EncaseSquareBracketsForWholeLines(string raw, string translated)
    {
        if (raw.StartsWith('【')
            && raw.EndsWith('】')
            && !translated.Contains('【')
            && !translated.Contains('】'))
        {
            translated = $"【{translated}】";
        }

        return translated;
    }

    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string ReplaceIncorrectLowercaseWords(string input)
    {
        var words = new Dictionary<string, string>
        {
            { "jianghu", "Jianghu" },
            { "wulin", "Wulin"  }
        };

        foreach (var word in words)
        {
            string pattern = $"\\b{word.Key}\\b"; // \b ensures 'jianghu' is a whole word
            input = Regex.Replace(input, pattern, word.Value);
        }

        return input;
    }
}
