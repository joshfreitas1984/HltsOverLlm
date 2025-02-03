using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Translate;

public class TranslatedRaw(string raw)
{
    public string Raw { get; set; } = raw;
    public string Trans { get; set; } = string.Empty;
}

public class StructuredGlossaryLine
{
    public string Criteria { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public string TransliteratedText { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;

}

public class DataFormat
{
    public List<DataLine> Entries { get; set; } = [];
}

public class DataLine
{
    public string Raw { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Literal { get; set; } = string.Empty;

    public DataLine()
    {
    }

    public DataLine(string raw, string result)
    {
        Raw = raw;
        Result= result;
    }
}

public class TextFileToSplit
{
    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string? Path { get; set; }

    public int[]? SplitIndexes { get; set; }
}

public class TranslationSplit
{
    public int Split { get; set; } = 0;

    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string Text { get; set; } = string.Empty;

    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string? Translated { get; set; }

    public bool FlaggedForRetranslation { get; set; } = false;

    public bool FlaggedForGlossaryExtraction { get; set; } = true;

    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string FlaggedGlossaryIn { get; set; } = string.Empty;

    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string FlaggedGlossaryOut { get; set; } = string.Empty;

    public TranslationSplit() { }

    public TranslationSplit(int split, string text)
    {
        Split = split;
        Text = text;
    }

    public void ResetFlags()
    {
        FlaggedForRetranslation = false;
        FlaggedGlossaryIn = string.Empty;
        FlaggedGlossaryOut = string.Empty;
    }

    public void ResetGlossaryFlags()
    {
        FlaggedForGlossaryExtraction = true;
    }
}

public class TranslationLine
{
    public int LineNum { get; set; } = 0;

    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string Raw { get; set; } = string.Empty;

    [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
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
    public string CorrectionPrompt = string.Empty;
}