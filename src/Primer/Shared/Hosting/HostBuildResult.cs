using Microsoft.Extensions.Hosting;
using Primer.Shared.Presentation;

namespace Primer.Shared.Hosting;

/// <summary>
/// The outcome of composing the host: either a host to run against, or the failure that
/// prevented one. Modelled as a result rather than out parameters so that neither the
/// caller nor the compiler has to reason about which value is present.
/// </summary>
internal sealed class HostBuildResult : IDisposable
{
    private readonly IHost? _host;
    private readonly PrimerError? _error;

    private HostBuildResult(IHost? host, PrimerError? error)
    {
        _host = host;
        _error = error;
    }

    internal static HostBuildResult Built(IHost host) => new(host, null);

    internal static HostBuildResult Failed(PrimerError error) => new(null, error);

    /// <summary>Whether a host was composed.</summary>
    internal bool Succeeded => _host is not null;

    /// <summary>The composed host. Valid only when <see cref="Succeeded" /> is true.</summary>
    internal IHost Host => _host
        ?? throw new InvalidOperationException("The host was not built; inspect Error instead.");

    /// <summary>The failure. Valid only when <see cref="Succeeded" /> is false.</summary>
    internal PrimerError Error => _error
        ?? throw new InvalidOperationException("The host was built; there is no error to report.");

    public void Dispose() => _host?.Dispose();
}
