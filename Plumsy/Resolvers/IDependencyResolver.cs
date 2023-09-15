using System.Reflection;

namespace Plum.Net.Resolvers;

public interface IDependencyResolver
{
    bool TryResolveDependency(Assembly? requestingAssembly, string dependencyName, out string? path);
}
