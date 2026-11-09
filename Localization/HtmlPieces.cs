using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;

namespace OpenQMS.Localization
{
    /// <summary>
    /// Pieces of markup for whole-sentence translations with H["… {0} …", piece]: the sentence is
    /// translated as one unit (word order differs between languages) and the link goes where {0} is.
    /// Address and text are HTML-encoded here, so a translation can never inject markup.
    /// </summary>
    public static class HtmlPieces
    {
        public static IHtmlContent Link(string? href, string text, string? id = null)
        {
            var enc = HtmlEncoder.Default;
            var idAttr = string.IsNullOrEmpty(id) ? "" : $" id=\"{enc.Encode(id)}\"";
            return new HtmlString($"<a{idAttr} href=\"{enc.Encode(href ?? "#")}\">{enc.Encode(text)}</a>");
        }
    }
}
