using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }

}
