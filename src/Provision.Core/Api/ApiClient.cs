using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Provision.Core.Api;

/// <summary>Error que devuelve la API: { error: { codigo, mensaje, detalles } }.</summary>
public sealed class ApiException(int status, string codigo, string mensaje, JsonElement? detalles = null)
    : Exception(mensaje)
{
    public int Status { get; } = status;
    public string Codigo { get; } = codigo;
    public JsonElement? Detalles { get; } = detalles;

    /// <summary>Mensaje listo para mostrar, con los errores de validación por campo si los hay.</summary>
    public string MensajeCompleto
    {
        get
        {
            if (Codigo != "VALIDACION" || Detalles is not { ValueKind: JsonValueKind.Object } d) return Message;
            var lineas = new List<string> { Message };
            if (d.TryGetProperty("fieldErrors", out var campos) && campos.ValueKind == JsonValueKind.Object)
            {
                foreach (var campo in campos.EnumerateObject())
                    foreach (var err in campo.Value.EnumerateArray())
                        lineas.Add($"• {campo.Name}: {err.GetString()}");
            }
            if (d.TryGetProperty("formErrors", out var forma) && forma.ValueKind == JsonValueKind.Array)
            {
                foreach (var err in forma.EnumerateArray()) lineas.Add($"• {err.GetString()}");
            }
            return string.Join(Environment.NewLine, lineas);
        }
    }
}

/// <summary>
/// Cliente HTTP de bajo nivel: serializa en camelCase, adjunta el token y traduce errores.
/// </summary>
public sealed class ApiClient : IDisposable
{
    public static readonly JsonSerializerOptions Json = CrearOpciones();

    private readonly HttpClient _http;

    public ApiClient(string urlBase, HttpMessageHandler? handler = null)
    {
        UrlBase = urlBase.TrimEnd('/') + "/";
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(UrlBase);
        // El plan gratuito de Render "duerme" el servicio: la primera petición puede tardar ~1 minuto.
        _http.Timeout = TimeSpan.FromSeconds(90);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string UrlBase { get; }

    public string? Token
    {
        get => _http.DefaultRequestHeaders.Authorization?.Parameter;
        set => _http.DefaultRequestHeaders.Authorization =
            value is null ? null : new AuthenticationHeaderValue("Bearer", value);
    }

    /// <summary>Se dispara cuando la API responde 401 con sesión iniciada (token vencido).</summary>
    public event EventHandler? SesionExpirada;

    public Task<T> GetAsync<T>(string ruta, CancellationToken ct = default) =>
        EnviarAsync<T>(HttpMethod.Get, ruta, null, ct);

    public Task<T> PostAsync<T>(string ruta, object? cuerpo = null, CancellationToken ct = default) =>
        EnviarAsync<T>(HttpMethod.Post, ruta, cuerpo ?? new { }, ct);

    public Task<T> PatchAsync<T>(string ruta, object cuerpo, CancellationToken ct = default) =>
        EnviarAsync<T>(HttpMethod.Patch, ruta, cuerpo, ct);

    public Task<T> PutAsync<T>(string ruta, object cuerpo, CancellationToken ct = default) =>
        EnviarAsync<T>(HttpMethod.Put, ruta, cuerpo, ct);

    public Task DeleteAsync(string ruta, CancellationToken ct = default) =>
        EnviarAsync<JsonElement?>(HttpMethod.Delete, ruta, null, ct);

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string ruta, object? cuerpo, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(metodo, ruta.TrimStart('/'));
        if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: Json);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new ApiException(0, "SIN_RESPUESTA",
                "El servidor tardó demasiado en responder. Si es el entorno de desarrollo, puede estar despertando: intenta de nuevo.");
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(0, "SIN_CONEXION", $"No se pudo conectar con {UrlBase}. ¿Está encendido el servidor? ({ex.Message})");
        }

        using (resp)
        {
            if (resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.NoContent || resp.Content.Headers.ContentLength == 0)
                    return default!;
                var texto = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(texto) || texto == "null") return default!;
                return JsonSerializer.Deserialize<T>(texto, Json)!;
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized && Token is not null)
                SesionExpirada?.Invoke(this, EventArgs.Empty);

            throw await LeerErrorAsync(resp, ct).ConfigureAwait(false);
        }
    }

    private static async Task<ApiException> LeerErrorAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var status = (int)resp.StatusCode;
        try
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            if (doc.RootElement.TryGetProperty("error", out var e))
            {
                var codigo = e.TryGetProperty("codigo", out var c) ? c.GetString() ?? "ERROR" : "ERROR";
                var mensaje = e.TryGetProperty("mensaje", out var m) ? m.GetString() ?? "Error" : "Error";
                JsonElement? detalles = e.TryGetProperty("detalles", out var d) ? d.Clone() : null;
                return new ApiException(status, codigo, mensaje, detalles);
            }
        }
        catch (JsonException) { /* respuesta no JSON (proxy, servidor caído) */ }
        return new ApiException(status, "HTTP_" + status, $"El servidor respondió {status} {resp.ReasonPhrase}");
    }

    private static JsonSerializerOptions CrearOpciones()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        // Los enums de la API van en MAYÚSCULAS_CON_GUION: LenteOftalmico ↔ "LENTE_OFTALMICO".
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        return o;
    }

    public void Dispose() => _http.Dispose();
}
