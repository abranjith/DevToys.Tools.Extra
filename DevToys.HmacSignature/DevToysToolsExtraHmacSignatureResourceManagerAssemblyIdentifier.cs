using DevToys.Api;
using System.ComponentModel.Composition;

namespace DevToys.HmacSignature;

[Export(typeof(IResourceAssemblyIdentifier))]
[Name(nameof(DevToysToolsExtraHmacSignatureResourceManagerAssemblyIdentifier))]
public sealed class DevToysToolsExtraHmacSignatureResourceManagerAssemblyIdentifier : IResourceAssemblyIdentifier
{
    public ValueTask<FontDefinition[]> GetFontDefinitionsAsync()
    {
        throw new NotImplementedException();
    }
}