using Microsoft.Extensions.Options;

namespace Primer.Shared.Hosting;

/// <summary>
/// Checks each declared constraint. Validation runs before any file is read or written, so
/// a configuration error is reported against the configuration rather than discovered
/// halfway through a run that has already changed the repository.
/// </summary>
internal sealed class PrimerOptionsValidator : IValidateOptions<PrimerOptions>
{
    public ValidateOptionsResult Validate(string? name, PrimerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.Budget.MaxDepth < 1)
        {
            failures.Add(Failure("Budget:MaxDepth", options.Budget.MaxDepth, "at least 1"));
        }

        if (options.Budget.MaxFileCount < 1)
        {
            failures.Add(Failure("Budget:MaxFileCount", options.Budget.MaxFileCount, "at least 1"));
        }

        if (options.Budget.MaxFileBytes < 1024)
        {
            failures.Add(Failure("Budget:MaxFileBytes", options.Budget.MaxFileBytes, "at least 1024"));
        }

        if (options.Budget.NetworkTimeout <= TimeSpan.Zero)
        {
            failures.Add(Failure("Budget:NetworkTimeout", options.Budget.NetworkTimeout, "greater than zero"));
        }

        for (var index = 0; index < options.McpRequirements.Count; index++)
        {
            var requirement = options.McpRequirements[index];

            if (string.IsNullOrWhiteSpace(requirement.Name))
            {
                failures.Add(Failure($"McpRequirements:{index}:Name", requirement.Name, "a server name"));
            }

            if (string.IsNullOrWhiteSpace(requirement.VersionRange))
            {
                failures.Add(Failure($"McpRequirements:{index}:VersionRange", requirement.VersionRange, "a constraint"));
            }

            if (string.IsNullOrWhiteSpace(requirement.InstallMethod))
            {
                failures.Add(Failure($"McpRequirements:{index}:InstallMethod", requirement.InstallMethod, "an install method"));
            }
        }

        if (string.IsNullOrWhiteSpace(options.TemplatePath))
        {
            failures.Add(Failure("TemplatePath", options.TemplatePath, "a non-empty relative path"));
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static string Failure(string key, object? actual, string constraint) =>
        $"{PrimerOptions.SectionName}:{key} is '{actual}', which is not {constraint}.";
}
