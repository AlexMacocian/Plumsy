using System.Reflection.Metadata;

namespace Plumsy.Validators;

/// <summary>
/// A general validator that validates a plugin based on the entire metadata
/// </summary>
public interface IMetadataValidator
{
    bool Validate(MetadataReader metadataReader);
}
