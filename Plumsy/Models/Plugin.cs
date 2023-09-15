using System.Reflection;

namespace Plumsy.Models;

public sealed class Plugin
{
    public PluginEntry PluginEntry { get; init; } = default!;
    public Assembly Assembly { get; init; } = default!;

    internal Plugin()
    {
    }
}
