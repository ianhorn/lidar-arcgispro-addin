/*
 * STAC API HTTP client, trimmed to what lidar-arcgispro-addin needs: listing a catalog's collections
 * and a single "intersects" search, optionally scoped to specific collections. There is no default
 * catalog: BaseUri is set from the catalog the user picks, and LiDAR tiles within a matched item are
 * identified by asset convention (StacAsset.IsLidar/IsCopc), not by hardcoding an asset key.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StaclidarAddin.Stac
{
    /// <summary>
    /// HttpClient-backed client for a STAC API. Uses a single shared static HttpClient
    /// (best practice to avoid socket exhaustion).
    /// </summary>
    public class StacClient : IDisposable
    {
        private static readonly HttpClient _http;
        private static readonly JsonSerializerOptions _json;

        static StacClient()
        {
            _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate })
            {
                Timeout = TimeSpan.FromSeconds(120)
            };
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("StaclidarAddin/1.0");
            _json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public string BaseUri { get; set; } = string.Empty;

        /// <summary>Fetch all collections from the catalog.</summary>
        public async Task<List<StacCollection>> GetCollectionsAsync(CancellationToken ct = default)
        {
            var url = BaseUri.TrimEnd('/') + "/collections";
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            await EnsureSuccessWithBodyAsync(resp, ct).ConfigureAwait(false);
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var data = await JsonSerializer.DeserializeAsync<StacCollectionsResponse>(stream, _json, ct).ConfigureAwait(false);
            return data?.Collections ?? new List<StacCollection>();
        }

        /// <summary>
        /// Search for items intersecting a GeoJSON geometry (lon/lat, CRS84), via POST /search.
        /// </summary>
        public async Task<StacItemCollection> SearchIntersectsAsync(string intersectsGeoJson,
            IReadOnlyCollection<string> collections = null, int limit = 200, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                if (collections != null && collections.Count > 0)
                {
                    writer.WritePropertyName("collections");
                    writer.WriteStartArray();
                    foreach (var c in collections) writer.WriteStringValue(c);
                    writer.WriteEndArray();
                }
                writer.WritePropertyName("intersects");
                using (var doc = JsonDocument.Parse(intersectsGeoJson))
                    doc.RootElement.WriteTo(writer);
                writer.WriteNumber("limit", Clamp(limit, 1, 10000));
                writer.WriteEndObject();
                writer.Flush();
            }

            using var content = new ByteArrayContent(ms.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/geo+json");
            using var resp = await _http.PostAsync(BaseUri.TrimEnd('/') + "/search", content, ct).ConfigureAwait(false);
            await EnsureSuccessWithBodyAsync(resp, ct).ConfigureAwait(false);
            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<StacItemCollection>(stream, _json, ct).ConfigureAwait(false);
        }

        /// <summary>Throw with the response body included so 4xx errors are self-explanatory.</summary>
        private static async Task EnsureSuccessWithBodyAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            if (resp.IsSuccessStatusCode) return;
            string body = null;
            try { body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { /* ignore */ }
            throw new HttpRequestException($"STAC API {(int)resp.StatusCode} {resp.StatusCode}: {body}");
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        public void Dispose() { /* static http is shared; nothing to dispose here */ }
    }
}
