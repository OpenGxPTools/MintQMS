using System.Reflection;
using System.Resources;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace OpenQMS.Localization
{
    /// <summary>
    /// The .NET resource compiler treats resource names case-insensitively and keeps only one of two
    /// keys that differ only in capitals (MintQMS shows both "In Progress" and "In progress"). Lookups
    /// are therefore made case-insensitive too, so both spellings find the one entry. tools/i18n.py
    /// writes each case-variant once and refuses variants with different translations.
    /// </summary>
    public class CaseInsensitiveLocalizerFactory : ResourceManagerStringLocalizerFactory
    {
        private readonly IResourceNamesCache _cache = new ResourceNamesCache();
        private readonly ILoggerFactory _loggerFactory;

        public CaseInsensitiveLocalizerFactory(IOptions<LocalizationOptions> options, ILoggerFactory loggerFactory)
            : base(options, loggerFactory) => _loggerFactory = loggerFactory;

        protected override ResourceManagerStringLocalizer CreateResourceManagerStringLocalizer(Assembly assembly, string baseName) =>
            new(new ResourceManager(baseName, assembly) { IgnoreCase = true }, assembly, baseName, _cache,
                _loggerFactory.CreateLogger<ResourceManagerStringLocalizer>());
    }
}
