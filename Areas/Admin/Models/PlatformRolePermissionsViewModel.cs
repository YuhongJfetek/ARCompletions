using System;

namespace ARCompletions.Areas.Admin.Models;

public class PlatformRolePermissionsViewModel
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string[] SelectedPermissionIds { get; set; } = Array.Empty<string>();
}