using System.Reflection.Metadata;

namespace Plum.Net.Validators;

/// <summary>
/// A general validator that validates a plugin based on the entire metadata
/// </summary>
public interface IMetadataValidator
{
    bool Validate(MetadataReader metadataReader);
}
