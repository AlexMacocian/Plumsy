namespace Plumsy.Models;

public sealed class PluginEntry
{
    public required string Name { get; init; }
    public required string Path { get; init; }

    internal PluginEntry()
    {
    }
}
