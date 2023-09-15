namespace Plumsy.Models;

public abstract class PluginLoadOperation
{
    public PluginEntry PluginEntry { get; init; } = default!;
    public abstract string Description { get; }

    public sealed class Success : PluginLoadOperation
    {
        public Plugin Plugin { get; init; } = default!;
        public override string Description  => "Plugin loaded successfully";

        internal Success()
        {
        }
    }

    public sealed class NullEntry : PluginLoadOperation
    {
        public override string Description => $"Provided {nameof(PluginEntry)} is null";

        internal NullEntry()
        {
        }
    }

    public sealed class FileNotFound : PluginLoadOperation
    {
        public string Path { get; init; } = default!;
        public override string Description => $"Failed to load plugin. Check {nameof(Path)} property for details";
        
        internal FileNotFound()
        {
        }
    }

    public sealed class ExceptionEncountered : PluginLoadOperation
    {
        public override string Description => "Exception encountered while loading plugin";
        public Exception Exception { get; init; } = default!;

        internal ExceptionEncountered()
        {
        }
    }

    public sealed class UnexpectedErrorOccurred : PluginLoadOperation
    {
        public override string Description { get; }

        internal UnexpectedErrorOccurred(string description)
        {
            this.Description = description;
        }
    }
}
