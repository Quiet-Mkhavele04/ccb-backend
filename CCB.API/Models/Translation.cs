using System;
using System.Collections.Generic;

namespace CCB.API.Models;

public partial class Translation
{
    public long TranslationId { get; set; }

    public long MessageId { get; set; }

    public string? SourceLanguage { get; set; }

    public string? TargetLanguage { get; set; }

    public string? OriginalText { get; set; }

    public string? TranslatedText { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Message Message { get; set; } = null!;
}
