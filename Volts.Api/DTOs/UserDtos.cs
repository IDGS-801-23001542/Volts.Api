namespace Volts.Api.DTOs;

public class UserUpdateDto
{
    public string FirstNames { get; set; } = string.Empty;
    public string PaternalLastName { get; set; } = string.Empty;
    public string? MaternalLastName { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class UserStatusUpdateDto
{
    public bool IsActive { get; set; }
}

public class UserUnlockDto
{
    public bool ResetFailedAttempts { get; set; } = true;
}
