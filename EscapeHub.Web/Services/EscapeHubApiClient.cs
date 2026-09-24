using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EscapeHub.Web.Services;

public sealed class EscapeHubApiClient(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApiCallResult<T>> GetAsync<T>(string path) =>
        SendAsync<T>(HttpMethod.Get, path);

    public Task<ApiCallResult<T>> PostAsync<T>(string path, object? body = null) =>
        SendAsync<T>(HttpMethod.Post, path, body);

    public Task<ApiCallResult<T>> PutAsync<T>(string path, object? body = null) =>
        SendAsync<T>(HttpMethod.Put, path, body);

    public async Task<ApiCallResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        var context = httpContextAccessor.HttpContext;
        if (context is not null)
        {
            if (context.Request.Headers.TryGetValue("Cookie", out var cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader.ToString());
            }

            if (method != HttpMethod.Get && method != HttpMethod.Head)
            {
                var token = context.Request.Headers["RequestVerificationToken"].ToString();
                if (string.IsNullOrWhiteSpace(token) && context.Request.HasFormContentType)
                {
                    var form = await context.Request.ReadFormAsync();
                    token = form["__RequestVerificationToken"].ToString();
                }

                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.TryAddWithoutValidation("RequestVerificationToken", token);
                }
            }
        }

        using var response = await httpClient.SendAsync(request);
        if (context is not null && response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                context.Response.Headers.Append("Set-Cookie", setCookie);
            }
        }

        var content = await response.Content.ReadAsStringAsync();
        T? value = default;
        string? message = null;
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(content))
        {
            try
            {
                using var json = JsonDocument.Parse(content);
                if (response.IsSuccessStatusCode)
                {
                    value = JsonSerializer.Deserialize<T>(content, JsonOptions);
                }
                else
                {
                    var root = json.RootElement;
                    if (TryGetProperty(root, "message", out var messageElement) ||
                        TryGetProperty(root, "detail", out messageElement))
                    {
                        message = messageElement.GetString();
                    }

                    if (TryGetProperty(root, "errors", out var errorsElement) &&
                        errorsElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in errorsElement.EnumerateObject())
                        {
                            errors[property.Name] = property.Value.ValueKind == JsonValueKind.Array
                                ? property.Value.EnumerateArray().Select(item => item.GetString() ?? "").ToArray()
                                : [property.Value.GetString() ?? ""];
                        }
                    }

                    if (string.IsNullOrWhiteSpace(message) && TryGetProperty(root, "title", out var title))
                    {
                        message = title.GetString();
                    }
                }
            }
            catch (JsonException)
            {
                if (!response.IsSuccessStatusCode)
                {
                    message = content;
                }
            }
        }

        return new ApiCallResult<T>(response.StatusCode, value, message, errors);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            (element.TryGetProperty(name, out value) ||
             element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out value)))
        {
            return true;
        }

        value = default;
        return false;
    }
}

public sealed record ApiCallResult<T>(
    HttpStatusCode StatusCode,
    T? Value,
    string? Message,
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool Succeeded => (int)StatusCode is >= 200 and < 300;

    public void AddErrorsTo(ModelStateDictionary modelState)
    {
        foreach (var (key, messages) in Errors)
        {
            foreach (var error in messages)
            {
                modelState.AddModelError(key, error);
            }
        }

        if (Errors.Count == 0 && !string.IsNullOrWhiteSpace(Message))
        {
            modelState.AddModelError(string.Empty, Message);
        }
    }
}
