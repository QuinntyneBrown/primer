using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Primer.Shared.Hosting;
using Primer.Shared.Verification;

namespace Primer.Features.Check;

/// <summary>Defines <c>primer check</c>.</summary>
internal sealed class CheckCommand : ICommandModule
{
    public Command Build(InvocationConfiguration configuration)
    {
        var command = new Command(
            "check",
            "Report whether the generated agent files still match the repository.");

        command.SetAction((parseResult, cancellationToken) => InvocationPipeline.RunAsync(
            parseResult,
            configuration,
            (services, _, _) => Task.FromResult(Handle(services)),
            cancellationToken));

        return command;
    }

    private static ExitCode Handle(IServiceProvider services)
    {
        var report = services.GetRequiredService<DriftChecker>().Compare(recursive: false);

        return services.GetRequiredService<CheckResultPresenter>().Present(report);
    }
}
