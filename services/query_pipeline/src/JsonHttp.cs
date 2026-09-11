using System.Net.Http.Json;
using System.Text.Json;

namespace Rbq.QueryApi;

/// <summary>
/// The single place JSON requests are sent. Both the OpenAI and Supabase clients
/// go through it, so error handling and deserialization exist once.
/// </summary>
public static class JsonHttp
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task<T> PostAsync<T>(
        this HttpClient http, string path, object body, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync(path, body, Options, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"{http.BaseAddress}{path} failed ({(int)response.StatusCode}): {error}");
        }

        return await response.Content.ReadFromJsonAsync<T>(Options, ct)
               ?? throw new HttpRequestException($"{path} returned an empty body.");
    }
}
