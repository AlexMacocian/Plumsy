namespace Plumsy.Validators;

public interface IEnvironmentVersionValidator
{
    /// <summary>
    /// Validate the version of .net of the plugin in comparison to the version of .net of the running assembly.
    /// </summary>
    /// <param name="currentVersion">.net version of the currently running assembly</param>
    /// <param name="pluginVersion">.net version of the plugin</param>
    /// <returns></returns>
    bool Validate(Version currentVersion, Version pluginVersion);
}
