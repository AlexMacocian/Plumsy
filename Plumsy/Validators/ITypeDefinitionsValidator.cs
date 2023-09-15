using System.Reflection.Metadata;

namespace Plum.Net.Validators;

/// <summary>
/// A validator that validates a plugin based on the TypeDefinitions it contains.
/// </summary>
/// <remarks>
/// Useful when you want to validate if the plugin contains a certain type that may implement a base type, in order to determine the entry point of the plugin.
/// </remarks>
public interface ITypeDefinitionsValidator
{
    bool Validate(IEnumerable<TypeDefinition> typeDefinitions, MetadataReader metadataReader);
}
