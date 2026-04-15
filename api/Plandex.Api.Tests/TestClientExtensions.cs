using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Plandex.Api.DTOs;

namespace Plandex.Api.Tests;

public static class TestClientExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<(HttpClient client, AuthResponseDto auth)> RegisterAsync(
        this HttpClient client, string email, string password = "password123", string name = "Test")
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterDto(email, password, name));
        resp.EnsureSuccessStatusCode();
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponseDto>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }

    public static async Task<T?> GetJsonAsync<T>(this HttpClient client, string url)
        => await client.GetFromJsonAsync<T>(url, Json);

    public static async Task<T?> ReadJsonAsync<T>(this HttpResponseMessage resp)
        => await resp.Content.ReadFromJsonAsync<T>(Json);

    public static async Task<T?> ReadJsonAsync<T>(this HttpContent content)
        => await content.ReadFromJsonAsync<T>(Json);
}
