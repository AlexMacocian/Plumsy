using System.Extensions;

namespace Plum.Net.Tests.SimplePlugin;

public sealed class Main
{
    public Main()
    {
        this.ThrowIfNull("this");
    }

    public bool ReturnTrue() => true;
}