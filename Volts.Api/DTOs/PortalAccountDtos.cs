namespace Volts.Api.DTOs;

public class PortalAccountRequestDto
{
    public bool CreatePortalAccount { get; set; }

    public bool AutoGeneratePassword { get; set; } = true;

    public string? TemporaryPassword { get; set; }
}

public class PortalAccountCredentialsDto
{
    public bool Created { get; set; }

    public string Email { get; set; } = string.Empty;

    public string TemporaryPassword { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; } = true;
}

public class EntityWithPortalAccountDto<T>
{
    public T Entity { get; set; } = default!;

    public PortalAccountCredentialsDto? PortalAccount { get; set; }
}
