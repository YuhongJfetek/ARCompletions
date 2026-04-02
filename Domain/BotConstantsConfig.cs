namespace ARCompletions.Domain;

public class BotConstantsConfig
{
    public string ConfigKey { get; set; } = default!;
    public string? ConfigValue { get; set; }
    public string? ValueType { get; set; } // 'int' | 'float' | 'boolean' | 'string'
    public string? Description { get; set; }
    public System.DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
