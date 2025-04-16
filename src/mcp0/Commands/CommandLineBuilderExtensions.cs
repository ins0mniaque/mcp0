using System.CommandLine.Builder;
using System.CommandLine.Invocation;

using Microsoft.Extensions.DependencyInjection;

namespace mcp0.Commands;

internal static class CommandLineBuilderExtensions
{
    public static CommandLineBuilder UseServiceProvider(this CommandLineBuilder builder, IServiceProvider serviceProvider)
    {
        builder.AddMiddleware(context => context.BindingContext.AddService(_ => serviceProvider), MiddlewareOrder.Configuration);

        foreach (var configure in serviceProvider.GetServices<Action<CommandLineBuilder, IServiceProvider>>())
            configure(builder, serviceProvider);

        return builder;
    }
}