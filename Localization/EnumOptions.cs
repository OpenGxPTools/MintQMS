using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Drop-down lists built with Html.GetEnumSelectList show each option under the same name the rest of
    /// the interface uses (<see cref="EnumHelper.Display"/>, e.g. "Needs Improvement"), in the user's
    /// language. Without this only options with a [Display] attribute were translated; the others showed
    /// their code name in English. Option values (what is posted and stored) are unchanged.
    /// </summary>
    public class EnumOptionNamesProvider : IDisplayMetadataProvider
    {
        private readonly IStringLocalizerFactory _factory;
        public EnumOptionNamesProvider(IStringLocalizerFactory factory) => _factory = factory;

        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            var type = Nullable.GetUnderlyingType(context.Key.ModelType) ?? context.Key.ModelType;
            var existing = context.DisplayMetadata.EnumGroupedDisplayNamesAndValues;
            if (!type.IsEnum || existing == null) return;

            var l = _factory.Create(typeof(SharedResource));
            context.DisplayMetadata.EnumGroupedDisplayNamesAndValues = existing.Select(kv =>
            {
                var value = (Enum)Enum.ToObject(type, long.Parse(kv.Value, CultureInfo.InvariantCulture));
                var english = EnumHelper.Display(value);
                // Looked up when the list is drawn, so it follows the language of each request.
                return new KeyValuePair<EnumGroupAndName, string>(
                    new EnumGroupAndName(kv.Key.Group, () => l[english].Value), kv.Value);
            }).ToList();
        }
    }

    /// <summary>Runs after the data-annotation provider, which fills the option list this one renames.</summary>
    public class EnumOptionNamesSetup : IPostConfigureOptions<MvcOptions>
    {
        private readonly IStringLocalizerFactory _factory;
        public EnumOptionNamesSetup(IStringLocalizerFactory factory) => _factory = factory;

        public void PostConfigure(string? name, MvcOptions o) =>
            o.ModelMetadataDetailsProviders.Add(new EnumOptionNamesProvider(_factory));
    }
}
