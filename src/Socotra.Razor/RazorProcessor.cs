using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Socotra.Razor;

public static class RazorProcessor
{
    public static RazorCSharpDocument GenerateFromSource(string text, string filename, string @namespace)
    {
        if (!string.IsNullOrEmpty(@namespace) && !text.Contains("@namespace"))
        {
            text = $"{text}\n@namespace {@namespace}\n";
        }

        var engine = RazorProjectEngine.Create(RazorConfiguration.Default, RazorProjectFileSystem.Create("."));
        var source = RazorSourceDocument.Create(text, filename);
        var code = engine.Process(source, FileKinds.Component, new List<RazorSourceDocument>(), new List<TagHelperDescriptor>());
        code.SetCodeGenerationOptions(RazorCodeGenerationOptions.Create(_ => { }));
        return code.GetCSharpDocument();
    }
}
