using DevToys.Api;
using System.ComponentModel.Composition;

namespace DevToys.UrlParser;

[Export(typeof(IResourceAssemblyIdentifier))]
[Name(nameof(DevToysToolsExtraUrlParserResourceManagerAssemblyIdentifier))]
public sealed class DevToysToolsExtraUrlParserResourceManagerAssemblyIdentifier : IResourceAssemblyIdentifier
{
    public ValueTask<FontDefinition[]> GetFontDefinitionsAsync()
    {
        throw new NotImplementedException();
    }
}