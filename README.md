# Plumsy
## Plugin management library
Plumsy is a library for plugin management in C#. It is capable of inspecting and validating dlls, before loading them into the current AppDomain.

Plumsy offers a set of extensions and callbacks to allow the caller to configure what plugins to load, how to handle plugin dependencies and runtime differences.

## Examples

### Create an instance of PluginManager
```C#
var pluginManager = new PluginManager(pathToPlugins);
````

### Retrieve a list of available plugins
```C#
var availablePlugins = pluginManager.GetAvailablePlugins();
```
Plumsy will call the validators for each of the dlls found in the `pathToPlugins` and validate the metadata of those dlls. If the dlls pass the validation, they are marked as available and returned by the above call.

### Load plugins into the current AppDomain
```C#
var results = pluginManager.LoadPlugins(availablePlugins);
```
Plumsy will attempt to load the provided list of plugin entries into the current AppDomain. `results` is a list of `PluginLoadOperation`, the type depending on the result of the load operation.
- `PluginLoadOperation.Success` contains a reference to the loaded assembly and implies that the plugin has been loaded successfully.
- `PluginLoadOperation.NullEntry` implies that the plugin entry was null.
- `PluginLoadOperation.FileNotFound` implies that the plugin path was not found.
- `PluginLoadOperation.ExceptionEncountered` implies that the plugin failed to load due to an exception. Check the inner `Exception` property for details.
- `PluginLoadOperation.UnexpectedErrorOccurred` implies that an unexpected error occurred. This happens when the `Assembly.Load` call succeeds but the returned assembly is `null`.

### Setup validators
```C#
pluginManager
	.WithEnvironmentVersionValidator(environmentVersionValidator)
	.WithMetadataValidator(metadataValidator)
	.WithTypeDefinitionsValidator(typeDefinitionsValidator);
```
- `environmentVersionValidator` is of type `IEnvironmentVersionValidator`. This validator is used to validate the `Version` of the current runtime in comparison to the plugin target runtime.
- `metadataValidator` is of type `IMetadataValidator`. This validator receives a reference to the `MetadataReader` and can perform general validations over the plugin.
- `typeDefinitionsValidator` is of type `ITypeDefinitionsValidator`. This validator receives a reference to the `MetadataReader` as well as a list of `Type`s in order to perform validations over the plugin. This is useful when wanting to validate that the plugin contains a specific entry point class of a known type.

### Callbacks
```C#
pluginManager
	.WithAssemblyLoadedCallback(assemblyLoadedCallback);
```
- `assemblyLoadedCallback` is of type `IAssemblyLoadedCallback`. This callback is called after an assembly has been loaded into the current AppDomain.

### Dependency Resolving
```C#
pluginManager
	.WithDependencyResolver(dependencyResolver);
```
- `dependencyResolver` is of type `IDependencyResolver`. The dependency solver is called when the runtime is unable to find some dependencies of the currently loading plugin. *!! Due to the runtime lazily loading types, dependencies might only be resolved when calling specific methods !!*

```C#
pluginManager
	.WithForceLoadDependencies(true|false);
```
- When forcing the Plumsy to load dependencies, Plumsy will attempt to manually cause JIT-ing of the methods in the loaded assembly. This would in turn cause the runtime to load its dependencies and would trigger `IDependencyResolver.TryResolveDependency` in cases where a dependency is not found.
- By default, Plumsy forces dependencies to load on assembly load. If you want instead to lazily load dependencies, call `pluginManager.WithForceLoadDependencies(false)`.