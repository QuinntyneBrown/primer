namespace Primer.Shared.Hosting;

/// <summary>
/// Where the run is operating. This is runtime context rather than configuration, so it is
/// registered directly rather than bound from a configuration section.
/// </summary>
internal sealed record RepositoryLocation(string Path);
