using System.Extensions;

namespace Plumsy.Tests.SimplePlugin;

public sealed class Main
{
    public Main()
    {
        this.ThrowIfNull("this");
    }

    public bool ReturnTrue() => true;
}