namespace Primer.Shared.Presentation;

/// <summary>
/// Renders a command result. A handler writes a payload without knowing which
/// representation is active.
/// </summary>
internal interface IOutputFormatter
{
    string Render(object payload);
}
