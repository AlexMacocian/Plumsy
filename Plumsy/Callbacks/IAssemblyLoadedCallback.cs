using System.Reflection;

namespace Plum.Net.Callbacks;

public interface IAssemblyLoadedCallback
{
    void AssemblyLoaded(Assembly assembly);
}
