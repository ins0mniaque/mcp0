using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal sealed class Patcher(Dictionary<string, Models.Patch> patches)
{
    public Prompt? Patch(Prompt prompt)
    {
        if (!patches.TryGetValue(prompt.Name, out var patch))
            return prompt;

        if (patch == Models.Patch.Remove)
            return null;

        prompt.Name = patch.Name ?? prompt.Name;
        prompt.Description = patch.Description ?? prompt.Description;

        return prompt;
    }

    public Resource? Patch(Resource resource)
    {
        if (!patches.TryGetValue(resource.Name, out var patch))
            return resource;

        if (patch == Models.Patch.Remove)
            return null;

        return resource with
        {
            Name = patch.Name ?? resource.Name,
            Description = patch.Description ?? resource.Description
        };
    }

    public ResourceTemplate? Patch(ResourceTemplate resourceTemplate)
    {
        if (!patches.TryGetValue(resourceTemplate.Name, out var patch))
            return resourceTemplate;

        if (patch == Models.Patch.Remove)
            return null;

        return resourceTemplate with
        {
            Name = patch.Name ?? resourceTemplate.Name,
            Description = patch.Description ?? resourceTemplate.Description
        };
    }

    public Tool? Patch(Tool tool)
    {
        if (!patches.TryGetValue(tool.Name, out var patch))
            return tool;

        if (patch == Models.Patch.Remove)
            return null;

        tool.Name = patch.Name ?? tool.Name;
        tool.Description = patch.Description ?? tool.Description;

        return tool;
    }
}