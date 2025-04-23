using ModelContextProtocol.Protocol.Types;

namespace mcp0.Mcp;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
internal sealed class EmulatedSamplingCapability : SamplingCapability
{
    public EmulatedSamplingCapability()
    {
        SamplingHandler = async (request, _, _) =>
        {
            var model = "model";
            if (request?.ModelPreferences?.Hints is { } hints && hints.Count is not 0)
                model = hints[0].Name ?? model;

            return new()
            {
                Model = model,
                Role = Role.Assistant,
                Content = new() { Text = $"[Emulated sampling from {model}]" }
            };
        };
    }
}