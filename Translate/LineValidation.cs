using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Translate;

public class LineValidation
{
  public static ValidationResult CheckTransalationSuccessful(LlmConfig config, string raw, string result)
  {
    var response = true;
    var correctionPrompts = new StringBuilder();

    if (string.IsNullOrEmpty(raw))
      response = false;

    // Didnt translate at all and default response to prompt.
    if (result.Contains("provide the text you would"))
    {
      response = false;
    }

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

    // Small source with 'and' is ususually an alternative
    if (result.Contains("and") && raw.Length < 4)
    {
      response = false;
      correctionPrompts.AddPromptWithValues(config, "CorrectAlternativesPrompt", "and");
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

    // Random additions
    if (result.Contains("<br>") && !raw.Contains("<br>"))
    {
      response = false;
      correctionPrompts.AddPromptWithValues(config, "CorrectTagBrAddedPrompt");
    }

    if (result.Contains("<color") && !raw.Contains("<color"))
    {
      response = false;
      correctionPrompts.AddPromptWithValues(config, "CorrectTagColorAddedPrompt");
    }

    // It sometime can be in [] or {} or ()
    if (result.Contains("name_1") && !raw.Contains("name_1"))
    {
      response = false;
      correctionPrompts.AddPromptWithValues(config, "CorrectPlaceholderAddedPrompt", "name_1");
    }

    if (result.Contains("name_2") && !raw.Contains("name_2"))
    {
      response = false;
      correctionPrompts.AddPromptWithValues(config, "CorrectPlaceholderAddedPrompt", "name_2");
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

      if (input.Contains('`') && !raw.Contains('`'))
        input = input.Replace("`", "'");

      //Strip .'s
      if (input.EndsWith('.'))
        input = input[..^1];

      input = RemoveDiacritics(input);
    }

    return input;
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
}
