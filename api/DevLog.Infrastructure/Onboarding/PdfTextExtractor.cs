using UglyToad.PdfPig;

namespace DevLog.Infrastructure.Onboarding;

// Milestone 13b — plain text extraction from an uploaded resume PDF, via
// PdfPig (see DevLog.Infrastructure.csproj's comment on that package
// reference for why it was chosen over sending the raw PDF to the LLM
// directly). Deliberately page-text, not word-position/layout-aware
// extraction — a resume's content, not its visual layout, is what the
// onboarding-suggestion prompt needs.
public static class PdfTextExtractor
{
    // A resume producing literally zero extractable text almost always
    // means it's a scanned image PDF (no embedded text layer) rather than
    // a text-based PDF — PdfPig can't OCR, and neither can the rest of
    // this pipeline. Returning empty lets the caller give a clear "this
    // doesn't look like a text-based PDF" message instead of silently
    // asking the LLM to draft suggestions from nothing.
    public static string Extract(Stream pdfContent)
    {
        using var document = PdfDocument.Open(pdfContent);

        var pageTexts = document.GetPages().Select(page => page.Text.Trim()).Where(text => text.Length > 0);
        return string.Join("\n\n", pageTexts);
    }
}
