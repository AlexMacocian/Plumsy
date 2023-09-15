using Plumsy.Callbacks;
using Plumsy.Exceptions;
using Plumsy.Models;
using Plumsy.Resolvers;
using Plumsy.Validators;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace Plumsy;

public sealed class PluginManager
{
    private static readonly object Lock = new();

    private readonly string pluginsBaseDirectory;
    private readonly List<IEnvironmentVersionValidator> environmentVersionValidators = new();
    private readonly List<IMetadataValidator> metadataValidators = new();
    private readonly List<ITypeDefinitionsValidator> typeDefinitionsValidators = new();
    private readonly List<IAssemblyLoadedCallback> assemblyLoadCallbacks = new();
    private readonly List<IDependencyResolver> dependencyResolvers = new();

    private bool locked = false;
    private bool attachedDomainEvents = false;
    private bool forceLoadDependencies = true;

    public PluginManager(string pluginsBaseDirectory)
    {
        this.pluginsBaseDirectory = Path.GetFullPath(pluginsBaseDirectory);
    }

    public PluginManager WithEnvironmentVersionValidator(IEnvironmentVersionValidator environmentVersionValidator)
    {
        if (this.locked)
        {
            throw new InvalidOperationException($"{nameof(PluginManager)} is in locked state and cannot be modified anymore");
        }

        this.environmentVersionValidators.Add(environmentVersionValidator);
        return this;
    }

    public PluginManager WithMetadataValidator(IMetadataValidator metadataValidator)
    {
        if (this.locked)
        {
            throw new InvalidOperationException($"{nameof(PluginManager)} is in locked state and cannot be modified anymore");
        }

        this.metadataValidators.Add(metadataValidator);
        return this;
    }

    public PluginManager WithTypeDefinitionsValidator(ITypeDefinitionsValidator typeDefinitionsValidator)
    {
        if (this.locked)
        {
            throw new InvalidOperationException($"{nameof(PluginManager)} is in locked state and cannot be modified anymore");
        }

        this.typeDefinitionsValidators.Add(typeDefinitionsValidator);
        return this;
    }

    public PluginManager WithAssemblyLoadedCallback(IAssemblyLoadedCallback assemblyLoadedCallback)
    {
        if (this.locked)
        {
            throw new InvalidOperationException($"{nameof(PluginManager)} is in locked state and cannot be modified anymore");
        }

        this.assemblyLoadCallbacks.Add(assemblyLoadedCallback);
        return this;
    }

    /// <summary>
    /// Add a <see cref="IDependencyResolver"/> to the <see cref="PluginManager"/>. To ensure that this is called,
    /// call <see cref="WithForceLoadDependencies(bool)"/> with <see cref="true"/> parameter.
    /// </summary>
    /// <param name="dependencyResolver"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public PluginManager WithDependencyResolver(IDependencyResolver dependencyResolver)
    {
        if (this.locked)
        {
            throw new InvalidOperationException($"{nameof(PluginManager)} is in locked state and cannot be modified anymore");
        }

        this.dependencyResolvers.Add(dependencyResolver);
        return this;
    }

    /// <summary>
    /// When false, loading plugins might not trigger dependency resolvers, as the runtime loads dependencies lazily.
    /// By default, this value is true.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public PluginManager WithForceLoadDependencies(bool forceLoadDependencies)
    {
        if (this.locked)
        {
            throw new InvalidOperationException($"{nameof(PluginManager)} is in locked state and cannot be modified anymore");
        }

        this.forceLoadDependencies = forceLoadDependencies;
        return this;
    }

    /// <summary>
    /// Enumerates over all the plugins found in the configured directory.
    /// </summary>
    /// <returns>Validated list of plugin entries that can be loaded by the plugin manager.</returns>
    public IEnumerable<PluginEntry> GetAvailablePlugins()
    {
        if (Directory.Exists(pluginsBaseDirectory))
        {
            foreach (var potentialPluginPath in Directory.GetFiles(pluginsBaseDirectory, "*.dll", SearchOption.AllDirectories))
            {
                if (this.ValidatePlugin(potentialPluginPath, out var plugin))
                {
                    yield return plugin!;
                }
            }
        }
    }

    /// <summary>
    /// Loads a list of plugins. This operation locks the <see cref="PluginManager"/> and no more validators/callbacks/resolvers can be added anymore.
    /// </summary>
    /// <returns>List of <see cref="PluginLoadOperation"/> results in the same order as the provided plugins.</returns>
    /// <exception cref="ArgumentNullException">Throws when provided plugins are null.</exception>
    public IEnumerable<PluginLoadOperation> LoadPlugins(IEnumerable<PluginEntry> plugins)
    {
        _ = plugins ?? throw new ArgumentNullException(nameof(plugins));       
        try
        {
            while (!Monitor.TryEnter(Lock)) { }

            this.locked = true;
            if (!this.attachedDomainEvents)
            {
                AppDomain.CurrentDomain.AssemblyLoad += this.AssemblyLoaded;
                AppDomain.CurrentDomain.AssemblyResolve += this.AssemblyResolve;
                this.attachedDomainEvents = true;
            }

            var results = this.LoadPluginsInternal(plugins).ToList();
            return results;
        }
        finally
        {
            Monitor.Exit(Lock);
        }
    }

    private IEnumerable<PluginLoadOperation> LoadPluginsInternal(IEnumerable<PluginEntry> plugins)
    {
        foreach (var plugin in plugins)
        {
            if (plugin is null)
            {
                yield return new PluginLoadOperation.NullEntry();
                continue;
            }

            if (!File.Exists(plugin.Path))
            {
                yield return new PluginLoadOperation.FileNotFound { PluginEntry = plugin, Path = plugin.Path };
                continue;
            }

            var success = false;
            PluginLoadException pluginLoadException = default!;
            Assembly assembly = default!;
            try
            {
                assembly = Assembly.LoadFrom(plugin.Path);
                if (this.forceLoadDependencies)
                {
                    ForceLoadDependenciesOfLoadedAssembly(assembly);
                }

                success = true;
            }
            catch (Exception e)
            {
                success = false;
                pluginLoadException = new PluginLoadException("Failed to load plugin. Check inner exception for details", e, plugin);
            }

            if (success)
            {
                if (assembly is not null)
                {
                    yield return new PluginLoadOperation.Success { Plugin = new Plugin { Assembly = assembly, PluginEntry = plugin }, PluginEntry = plugin };
                }
                else
                {
                    yield return new PluginLoadOperation.UnexpectedErrorOccurred($"Plugin loaded without any error but no Assembly was obtained from {nameof(Assembly.LoadFrom)}") { PluginEntry = plugin };
                }
            }
            else
            {
                yield return new PluginLoadOperation.ExceptionEncountered { Exception = pluginLoadException!, PluginEntry = plugin };
            }
        }
    }

    private void AssemblyLoaded(object? _, AssemblyLoadEventArgs assemblyLoadEventArgs)
    {
        foreach (var callback in this.assemblyLoadCallbacks)
        {
            callback.AssemblyLoaded(assemblyLoadEventArgs.LoadedAssembly);
        }
    }

    private Assembly AssemblyResolve(object? _, ResolveEventArgs resolveEventArgs)
    {
        foreach (var resolver in this.dependencyResolvers)
        {
            if (resolver.TryResolveDependency(resolveEventArgs.RequestingAssembly, resolveEventArgs.Name, out var assemblyPath) &&
                assemblyPath is string resolvedPath)
            {
                return Assembly.LoadFrom(resolvedPath);
            }
        }

        throw new InvalidOperationException($"Unable to resolve dependency {resolveEventArgs.Name}. Requesting assembly: {resolveEventArgs.RequestingAssembly}");
    }

    private bool ValidatePlugin(string path, out PluginEntry? plugin)
    {
        if (!File.Exists(path))
        {
            plugin = default;
            return false;
        }

        var pluginName = Path.GetFileNameWithoutExtension(path);
        plugin = new PluginEntry { Name = pluginName, Path = path };
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var peReader = new PEReader(stream);
        var metadataReader = peReader.GetMetadataReader();

        if (!this.IsValidPlugin(metadataReader))
        {
            return false;
        }

        if (!this.IsTargetingCorrectDotnetVersion(metadataReader))
        {
            return false;
        }

        var typeDefinitions = metadataReader.TypeDefinitions.Select(metadataReader.GetTypeDefinition);
        if (!this.HasValidTypeDefinitions(typeDefinitions, metadataReader))
        {
            return false;
        }

        return true;
    }

    private bool IsTargetingCorrectDotnetVersion(MetadataReader metadataReader)
    {
        foreach (var referenceHandle in metadataReader.AssemblyReferences)
        {
            var reference = metadataReader.GetAssemblyReference(referenceHandle);
            var name = metadataReader.GetString(reference.Name);

            if (name.Equals("System.Runtime", StringComparison.OrdinalIgnoreCase))
            {
                /*
                 * If any validator rejects the plugin version, reject the plugin.
                 * Otherwise, accept the plugin.
                 */
                var pluginVersion = reference.Version;
                var currentVersion = Environment.Version;
                foreach(var validator in this.environmentVersionValidators)
                {
                    if (!validator.Validate(currentVersion, pluginVersion))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        return false;
    }

    private bool HasValidTypeDefinitions(IEnumerable<TypeDefinition> typeDefinitions, MetadataReader metadataReader)
    {
        foreach(var validator in this.typeDefinitionsValidators)
        {
            if (!validator.Validate(typeDefinitions, metadataReader))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsValidPlugin(MetadataReader metadataReader)
    {
        foreach(var validator in this.metadataValidators)
        {
            if (!validator.Validate(metadataReader))
            {
                return false;
            }
        }

        return true;
    }

    private static void ForceLoadDependenciesOfLoadedAssembly(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            // We'll force JIT compilation of all the methods, constructors and property setter/getter, which in turn forces dependencies to load
            foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly |
                                                   BindingFlags.Public |
                                                   BindingFlags.NonPublic |
                                                   BindingFlags.Instance |
                                                   BindingFlags.Static))
            {
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
            }

            foreach(var constructor in type.GetConstructors())
            {
                RuntimeHelpers.PrepareMethod(constructor.MethodHandle);
            }

            foreach (var property in type.GetProperties(BindingFlags.Public |
                                                BindingFlags.NonPublic |
                                                BindingFlags.Instance |
                                                BindingFlags.Static))
            {
                MethodInfo? getMethod = property.GetGetMethod(nonPublic: true);
                MethodInfo? setMethod = property.GetSetMethod(nonPublic: true);

                if (getMethod is not null)
                {
                    RuntimeHelpers.PrepareMethod(getMethod.MethodHandle);
                }

                if (setMethod is not null)
                {
                    RuntimeHelpers.PrepareMethod(setMethod.MethodHandle);
                }
            }
        }
    }
}
