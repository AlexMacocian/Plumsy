﻿using Plum.Net.Models;

namespace Plum.Net.Exceptions;

public sealed class PluginLoadException : Exception
{
    public PluginEntry Plugin { get; }

    public PluginLoadException(string? message, Exception? innerException, PluginEntry plugin) : base(message, innerException)
    {
        this.Plugin = plugin;
    }
}
