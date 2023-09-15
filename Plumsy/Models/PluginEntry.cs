namespace Plumsy.Models;

public sealed class PluginEntry
{
    public string Name { get; init; }
    public string Path { get; init; }

    internal PluginEntry()
    {
    }
}
