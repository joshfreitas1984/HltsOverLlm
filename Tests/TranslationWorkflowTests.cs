namespace Translate.Tests;
public class TranslationWorkflowTests
{
    const string workingDirectory = "../../../../Files";

    [Fact]
    public void ExportAssetsIntoTranslated()
    {
        TranslationService.Export(workingDirectory);
    }


    [Fact]
    public async Task TranslateLines()
    {
        await TranslationService.TranslateViaLlmAsync(workingDirectory, false);
    }

    [Fact]
    public async Task PackageFinalTranslation()
    {
        await TranslationService.PackageFinalTranslation(workingDirectory);

        var sourceDirectory = $"{workingDirectory}/Mod";
        var gameDirectory = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\河洛群俠傳 (Ho Tu Lo Shu ： The Books of Dragon)\\Mod";
        if (Directory.Exists(gameDirectory))
            Directory.Delete(gameDirectory, true);

        TranslationService.CopyDirectory(sourceDirectory, gameDirectory);
    }
}
