using System.Text.Json;

namespace NezamMonitor.Core.Api;

public sealed class NezamApiClient : IDisposable
{
    private readonly HttpClient _http;
    private string _token = "";
    public string Token => _token;
    private int _userId;
    private int _cityId;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);
    public int UserId => _userId;
    public int CityId => _cityId;

    private const string BaseUrl = "http://service.yazdnezam.ir:8033";

    public NezamApiClient() => _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { ozv_num = username, ozv_pass = password, ozv_type = 0 });
            var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/panel/api/login")
            { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") };
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return false;
            var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            _token = json.RootElement.GetProperty("token").GetString() ?? "";
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(_token);
            return await LoadUserInfoAsync();
        }
        catch { return false; }
    }

    private async Task<bool> LoadUserInfoAsync()
    {
        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}/panel/api/user");
            if (!resp.IsSuccessStatusCode) return false;
            var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var user = json.RootElement.GetProperty("user");
            _userId = user.GetProperty("id").GetInt32();
            _cityId = user.GetProperty("user_shahrestan").GetInt32();
            return true;
        }
        catch { return false; }
    }

    public async Task<List<Dictionary<string, object?>>> GetCasesRawAsync()
    {
        var resp = await _http.GetAsync($"{BaseUrl}/panel/api/showParvandeNezaratMeybod/{_userId}/{_cityId}");
        resp.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = new List<Dictionary<string, object?>>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in item.EnumerateObject())
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? (object)i : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            result.Add(dict);
        }
        return result;
    }

    public async Task<List<Dictionary<string, object?>>> GetEngineersRawAsync(int dbId)
    {
        var resp = await _http.GetAsync($"{BaseUrl}/panel/api/showNazer/{dbId}/{_cityId}");
        resp.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());

        JsonElement arr;
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(dbId.ToString(), out arr))
        {
            // Object format: {"3817": [...]}
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            arr = doc.RootElement;
        }
        else
        {
            return new();
        }

        var result = new List<Dictionary<string, object?>>();
        foreach (var item in arr.EnumerateArray())
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in item.EnumerateObject())
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            result.Add(dict);
        }
        return result;
    }

    public async Task<List<Dictionary<string, object?>>> GetFeesRawAsync(int dbId)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { db_id = dbId.ToString(), sha_id = _cityId.ToString(), ozv_id = _userId.ToString() });
        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/panel/api/showMali")
        { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") };
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = new List<Dictionary<string, object?>>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in item.EnumerateObject())
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
            result.Add(dict);
        }
        return result;
    }

    /// <summary>Get reports for a specific case.</summary>
    public async Task<List<Dictionary<string, object?>>> GetReportsRawAsync(int dbId)
    {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { db_id = dbId.ToString(), sha_id = _cityId.ToString() });
        var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/panel/api/getGozareshat")
        { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") };
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var result = new List<Dictionary<string, object?>>();
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var dict = new Dictionary<string, object?>();
                foreach (var prop in item.EnumerateObject())
                    dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                result.Add(dict);
            }
        }
        return result;
    }

    public static string S(Dictionary<string, object?> d, string k) =>
        d.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";

    public static string S(System.Text.Json.JsonElement el, string k)
    {
        if (el.TryGetProperty(k, out var val))
            return val.ValueKind == System.Text.Json.JsonValueKind.String ? val.GetString() ?? "" : val.ToString();
        return "";
    }

    public void Dispose() => _http.Dispose();
}
