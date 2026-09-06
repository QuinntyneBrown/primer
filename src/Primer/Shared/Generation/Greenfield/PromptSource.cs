using Microsoft.Extensions.Options;
using Primer.Shared.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Generation.Greenfield;

/// <summary>The description one greenfield run was given.</summary>
internal sealed record PromptText(string Value);

/// <summary>Raised when the two ways of supplying a description are used together.</summary>
internal sealed class ConflictingPromptSourceException()
    : PrimerException(new PrimerError(
        What: "--prompt and --prompt-file are mutually exclusive",
        Subject: "the supplied description",
        NextAction: "Pass the description inline with --prompt, or in a file with --prompt-file, but not both.",
        ExitCode: ExitCode.Usage));

/// <summary>Raised when the named description file cannot be read.</summary>
internal sealed class PromptFileNotFoundException(string path)
    : PrimerException(new PrimerError(
        What: "The description file was not found",
        Subject: path,
        NextAction: "Check the path. A relative path resolves against the directory being initialised.",
        ExitCode: ExitCode.Configuration));

/// <summary>Raised when the description carries nothing to generate from.</summary>
internal sealed class EmptyPromptException(string subject)
    : PrimerException(new PrimerError(
        What: "The description is empty",
        Subject: subject,
        NextAction: "Describe the project in a sentence or two; the description becomes the Purpose section.",
        ExitCode: ExitCode.Configuration));

/// <summary>Raised when the description file is larger than a description should be.</summary>
internal sealed class PromptTooLargeException(string path, long limit)
    : PrimerException(new PrimerError(
        What: "The description file is too large to read",
        Subject: path,
        NextAction: $"Keep the description under {limit} bytes, or raise Primer:Budget:MaxFileBytes.",
        ExitCode: ExitCode.Configuration));

/// <summary>
/// Resolves the description a run was given. Reading lives here rather than in the command
/// so that a feature folder never reaches the file system directly, and so the same
/// bounded-read rule that governs analysis governs this file too.
/// </summary>
internal sealed class PromptSource(RepositoryLocation location, IOptions<PrimerOptions> options)
{
    internal PromptText Resolve(string? inline, string? filePath)
    {
        var hasInline = inline is not null;
        var hasFile = !string.IsNullOrWhiteSpace(filePath);

        if (hasInline && hasFile)
        {
            throw new ConflictingPromptSourceException();
        }

        return hasFile ? FromFile(filePath!) : FromInline(inline!);
    }

    private static PromptText FromInline(string inline) =>
        string.IsNullOrWhiteSpace(inline)
            ? throw new EmptyPromptException("--prompt")
            : new PromptText(inline.Trim());

    private PromptText FromFile(string filePath)
    {
        var absolute = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(location.Path, filePath);

        if (!File.Exists(absolute))
        {
            throw new PromptFileNotFoundException(filePath);
        }

        var limit = options.Value.Budget.MaxFileBytes;

        if (new FileInfo(absolute).Length > limit)
        {
            throw new PromptTooLargeException(filePath, limit);
        }

        var content = ReadOrThrow(absolute, filePath);

        return string.IsNullOrWhiteSpace(content)
            ? throw new EmptyPromptException(filePath)
            : new PromptText(content.Trim());
    }

    private static string ReadOrThrow(string absolute, string reported)
    {
        try
        {
            return File.ReadAllText(absolute);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            throw new PromptFileNotFoundException(reported);
        }
    }
}
