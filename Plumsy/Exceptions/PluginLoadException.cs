using Plumsy.Models;

namespace Plumsy.Exceptions;

public sealed class PluginLoadException(string? message, Exception? innerException, PluginEntry plugin) : Exception(message, innerException)
{
    public PluginEntry Plugin { get; } = plugin;
}
