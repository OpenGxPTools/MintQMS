using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Built-in validation messages ("The Title field is required.") in the user's language.
    ///
    /// Attributes such as [Required] without their own ErrorMessage use a framework template that
    /// data-annotation localization never sees. This provider gives each such attribute the framework's
    /// OWN template — read from the framework at startup, not retyped — as its ErrorMessage, so English
    /// output is unchanged by construction and the same template is the translation key for es/pt-BR.
    /// Model-binding messages ("The value 'x' is not valid for Y.") call the original framework
    /// accessor in English and the translated template otherwise.
    /// </summary>
    public class DefaultValidationMessagesProvider : IValidationMetadataProvider
    {
        private static readonly Type? SR = typeof(RequiredAttribute).Assembly.GetType("System.SR");

        /// <summary>The framework's own message template (e.g. "The {0} field is required."), or null.</summary>
        public static string? FrameworkTemplate(string resourceName) =>
            SR?.GetProperty(resourceName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
               .GetValue(null) as string;

        private static readonly (Type type, string resource)[] Map =
        {
            (typeof(RequiredAttribute),     "RequiredAttribute_ValidationError"),
            (typeof(RangeAttribute),        "RangeAttribute_ValidationError"),
            (typeof(EmailAddressAttribute), "EmailAddressAttribute_Invalid"),
            (typeof(MaxLengthAttribute),    "MaxLengthAttribute_ValidationError"),
            (typeof(PhoneAttribute),        "PhoneAttribute_Invalid"),
        };

        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attr in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (attr.ErrorMessage != null || attr.ErrorMessageResourceType != null) continue;
                string? template = null;
                if (attr is StringLengthAttribute sl)
                    template = FrameworkTemplate(sl.MinimumLength > 0
                        ? "StringLengthAttribute_ValidationErrorIncludingMinimum"
                        : "StringLengthAttribute_ValidationError");
                else
                    foreach (var (type, resource) in Map)
                        if (attr.GetType() == type) { template = FrameworkTemplate(resource); break; }
                if (template != null) attr.ErrorMessage = template;
            }
        }
    }

    public class LocalizedValidationSetup : IConfigureOptions<MvcOptions>
    {
        private readonly IStringLocalizerFactory _factory;
        public LocalizedValidationSetup(IStringLocalizerFactory factory) => _factory = factory;

        private static bool English => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en";

        public void Configure(MvcOptions o)
        {
            o.ModelMetadataDetailsProviders.Add(new DefaultValidationMessagesProvider());

            var l = _factory.Create(typeof(SharedResource));
            var p = o.ModelBindingMessageProvider;
            var a1 = p.AttemptedValueIsInvalidAccessor;
            p.SetAttemptedValueIsInvalidAccessor((v, f) => English ? a1(v, f) : l["The value '{0}' is not valid for {1}.", v, f]);
            var a2 = p.NonPropertyAttemptedValueIsInvalidAccessor;
            p.SetNonPropertyAttemptedValueIsInvalidAccessor(v => English ? a2(v) : l["The value '{0}' is not valid.", v]);
            var a3 = p.UnknownValueIsInvalidAccessor;
            p.SetUnknownValueIsInvalidAccessor(f => English ? a3(f) : l["The supplied value is invalid for {0}.", f]);
            var a4 = p.NonPropertyUnknownValueIsInvalidAccessor;
            p.SetNonPropertyUnknownValueIsInvalidAccessor(() => English ? a4() : l["The supplied value is invalid."]);
            var a5 = p.ValueIsInvalidAccessor;
            p.SetValueIsInvalidAccessor(v => English ? a5(v) : l["The value '{0}' is invalid.", v]);
            var a6 = p.ValueMustBeANumberAccessor;
            p.SetValueMustBeANumberAccessor(f => English ? a6(f) : l["The field {0} must be a number.", f]);
            var a7 = p.NonPropertyValueMustBeANumberAccessor;
            p.SetNonPropertyValueMustBeANumberAccessor(() => English ? a7() : l["The field must be a number."]);
            var a8 = p.ValueMustNotBeNullAccessor;
            p.SetValueMustNotBeNullAccessor(v => English ? a8(v) : l["The value '{0}' is invalid.", v]);
            var a9 = p.MissingBindRequiredValueAccessor;
            p.SetMissingBindRequiredValueAccessor(f => English ? a9(f) : l["A value for the '{0}' parameter or property was not provided.", f]);
            var a10 = p.MissingKeyOrValueAccessor;
            p.SetMissingKeyOrValueAccessor(() => English ? a10() : l["A value is required."]);
        }
    }
}
