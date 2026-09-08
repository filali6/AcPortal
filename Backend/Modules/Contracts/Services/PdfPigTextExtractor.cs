using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Backend.Modules.Contracts.Services;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public string Extract(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var pages = document.GetPages()
            .Select(page => string.Join(" ", page.GetWords().Select(w => w.Text)));

        return string.Join("\n", pages);
    }
}