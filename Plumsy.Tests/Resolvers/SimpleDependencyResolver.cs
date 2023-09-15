using Plum.Net.Resolvers;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Plum.Net.Tests.Resolvers;

public sealed class SimpleDependencyResolver : IDependencyResolver
{
    public bool TryResolveDependency(Assembly? requestingAssembly, string dependencyName, out string? path)
    {
        var dllName = dependencyName.Split(',').First();
        path = Path.Combine(Path.GetFullPath("Dependencies"), $"{dllName}.dll");
        return true;
    }
}
