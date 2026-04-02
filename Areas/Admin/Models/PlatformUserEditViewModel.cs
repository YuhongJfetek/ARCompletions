using System;

namespace ARCompletions.Areas.Admin.Models;

public class PlatformUserEditViewModel
{
    public string? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // 暫時明文存入 PasswordHash
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public string[] SelectedRoleIds { get; set; } = Array.Empty<string>();
}