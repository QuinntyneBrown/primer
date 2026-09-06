using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Primer.Shared.Hosting;

namespace Primer.IntegrationTests.TestSupport;

/// <summary>Shorthand for reaching the bound options a built host carries.</summary>
internal static class HostExtensions
{
    internal static PrimerOptions Options(this IHost host) =>
        host.Services.GetRequiredService<IOptions<PrimerOptions>>().Value;
}
