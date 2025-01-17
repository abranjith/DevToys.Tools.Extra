using DevToys.Api;
using System.ComponentModel.Composition;

namespace DevToys.PKCE;

[Export(typeof(IResourceAssemblyIdentifier))]
[Name(nameof(DevToysToolsExtraPkceResourceManagerAssemblyIdentifier))]
public sealed class DevToysToolsExtraPkceResourceManagerAssemblyIdentifier : IResourceAssemblyIdentifier
{
    public ValueTask<FontDefinition[]> GetFontDefinitionsAsync()
    {
        throw new NotImplementedException();
    }
}
