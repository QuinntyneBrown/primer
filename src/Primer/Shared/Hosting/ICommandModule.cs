using System.CommandLine;

namespace Primer.Shared.Hosting;

/// <summary>
/// One command, defined in its own file. Registration is by discovery, so adding a command
/// means adding a file rather than editing a table every feature has to touch.
/// </summary>
internal interface ICommandModule
{
    Command Build(InvocationConfiguration configuration);
}
