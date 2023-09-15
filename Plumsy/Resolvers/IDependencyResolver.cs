using System.Reflection;

namespace Plumsy.Resolvers;

public interface IDependencyResolver
{
    bool TryResolveDependency(Assembly? requestingAssembly, string dependencyName, out string? path);
}
