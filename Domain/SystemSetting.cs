namespace ARCompletions.Domain;

public class SystemSetting
{
    public string Id { get; set; } = default!;
    public string SettingKey { get; set; } = default!;
    public string SettingValue { get; set; } = default!;
    public string? Description { get; set; }
    public long UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
