namespace Volts.Api.Models.Common;

public class PersonName
{
    public string FirstNames { get; set; } = string.Empty;

    public string PaternalLastName { get; set; } = string.Empty;

    public string? MaternalLastName { get; set; }

    public string FullName
    {
        get
        {
            var parts = new[]
            {
                FirstNames,
                PaternalLastName,
                MaternalLastName
            };

            return string.Join(
                " ",
                parts.Where(value =>
                    !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
            );
        }
    }

    public bool HasStructuredName =>
        !string.IsNullOrWhiteSpace(FirstNames) &&
        !string.IsNullOrWhiteSpace(PaternalLastName);
}