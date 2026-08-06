namespace DevLog.Infrastructure.Ai;

public static class TextChunker
{
    private const int TargetChunkSize = 800;
    private const int MaxChunkSize = 1200;

    public static IReadOnlyList<string> Chunk(string text)
    {
        var paragraphs = text
            .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (paragraphs.Count == 0)
        {
            return Array.Empty<string>();
        }

        var chunks = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length > MaxChunkSize)
            {
                if (current.Count > 0)
                {
                    chunks.Add(string.Join("\n\n", current));
                    current.Clear();
                    currentLength = 0;
                }

                for (var i = 0; i < paragraph.Length; i += MaxChunkSize)
                {
                    chunks.Add(paragraph.Substring(i, Math.Min(MaxChunkSize, paragraph.Length - i)));
                }
                continue;
            }

            if (currentLength + paragraph.Length > TargetChunkSize && current.Count > 0)
            {
                chunks.Add(string.Join("\n\n", current));
                current.Clear();
                currentLength = 0;
            }

            current.Add(paragraph);
            currentLength += paragraph.Length;
        }

        if (current.Count > 0)
        {
            chunks.Add(string.Join("\n\n", current));
        }

        return chunks;
    }
}
