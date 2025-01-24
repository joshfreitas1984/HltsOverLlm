﻿using System.Text.Json;

namespace Translate;

public class ModConfigFile
{
    public string UID { get; set; } = "3412932629";
    public string Id { get; set; } = "EnglishLlmByLash";
    public string Name { get; set; } = "BETA - Lash's English Patch Over LLM";
    public string Description { get; set; } = "English Translation using an LLM\nNot complete yet!";
    public string Author { get; set; } = "Lash";
    public string Version { get; set; } = string.Empty;
}

public static class ModHelper
{
    public static void GenerateModConfig(string workingDirectory)
    {
        var config = new ModConfigFile()
        {
            Version = DateTime.Now.ToString("yyyyMMddHHmm")
        };

        var outputFile = $"{workingDirectory}/Mod/EnglishLlmByLash/Mod.config";
        var lines = JsonSerializer.Serialize(config);
        File.WriteAllText(outputFile, lines);
    }

}
