/*
 * Fetches the list of public STAC APIs from STAC Index (https://stacindex.org) so the
 * "Bring Your Own API" dialog can offer them in a dropdown.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StaclidarAddin.Stac
{
    /// <summary>One selectable STAC API from STAC Index. <c>Name</c> is what the dialog's Name box gets filled with.</summary>
    public sealed record StacIndexCatalog(string Title, string Name, string Url, string Detail, bool IsBuiltIn);

    public static class StacIndexClient
    {
        private const string CatalogsUrl = "https://stacindex.org/api/catalogs";

        private static readonly HttpClient _http = CreateClient();
        private static IReadOnlyList<StacIndexCatalog> _cache;

        private static HttpClient CreateClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("StaclidarAddin/1.0");
            return http;
        }

        /// <summary>
        /// Public, searchable STAC APIs from STAC Index, sorted by title. The list is cached for the
        /// life of the process, so reopening the dialog doesn't refetch it.
        /// </summary>
        public static async Task<IReadOnlyList<StacIndexCatalog>> GetSearchableCatalogsAsync(CancellationToken ct = default)
        {
            if (_cache != null) return _cache;

            using var resp = await _http.GetAsync(CatalogsUrl, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var list = new List<StacIndexCatalog>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                // isApi: a live STAC API rather than a static catalog. Protected/private catalogs
                // need credentials, which this add-in has no way to supply.
                if (!GetBool(el, "isApi") || GetBool(el, "isPrivate") || GetString(el, "access") != "public") continue;

                var title = GetString(el, "title");
                var url = GetString(el, "url")?.Trim();
                if (string.IsNullOrWhiteSpace(title) || !IsUsableBaseUrl(url)) continue;

                var baseUrl = url.TrimEnd('/');

                var summary = GetString(el, "summary");
                if (summary != null && summary.Length > 240) summary = summary.Substring(0, 240).TrimEnd() + "...";
                var detail = string.IsNullOrWhiteSpace(summary) ? url : url + "\n\n" + summary;
                list.Add(new StacIndexCatalog(title.Trim(), title.Trim(), baseUrl, detail, IsBuiltIn: false));
            }

            _cache = list.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
            return _cache;
        }

        // The add-in talks to {base}/collections and {base}/search, so anything that isn't a plain
        // API root is dead weight in the list: static catalogs mislabeled as APIs (".json"), openEO
        // endpoints (a different API), and URLs carrying a query string/API key.
        private static bool IsUsableBaseUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
            if (!string.IsNullOrEmpty(uri.Query)) return false;
            if (uri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return false;
            if (url.IndexOf("openeo", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        private static string GetString(JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

        private static bool GetBool(JsonElement el, string name) =>
            el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.True;
    }
}
