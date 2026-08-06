using UglyToad.PdfPig;

namespace DevLog.Infrastructure.Onboarding;

public static class PdfTextExtractor
{
    public static string Extract(Stream pdfContent)
    {
        using var document = PdfDocument.Open(pdfContent);

        var pageTexts = document.GetPages().Select(page => page.Text.Trim()).Where(text => text.Length > 0);
        return string.Join("\n\n", pageTexts);
    }
}
