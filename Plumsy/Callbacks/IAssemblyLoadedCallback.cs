using System.Reflection;

namespace Plumsy.Callbacks;

public interface IAssemblyLoadedCallback
{
    void AssemblyLoaded(Assembly assembly);
}
