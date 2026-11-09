using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Conclave.Client.Services;

/// <summary>Talks to our own edge API (wwwroot/_worker.js) and to the browser glue (wwwroot/js/conclave.js).</summary>
public sealed class ConclaveApi(HttpClient http, IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _mod;
    public async ValueTask<IJSObjectReference> Js() => _mod ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/conclave.js");

    public async Task<AxowlUser?> UserAsync() => await (await Js()).InvokeAsync<AxowlUser?>("user");
    public async Task LoginAsync(string returnTo) => await (await Js()).InvokeVoidAsync("login", returnTo);
    public async Task LogoutAsync() => await (await Js()).InvokeVoidAsync("logout");
    public async Task<string?> CompleteLoginAsync() => await (await Js()).InvokeAsync<string?>("completeLogin");
    public async Task<bool> ConsumeAutoJoinAsync(string meetingId) => await (await Js()).InvokeAsync<bool>("consumeAutoJoin", meetingId);

    private async Task<HttpRequestMessage> Req(HttpMethod m, string path, object? body = null)
    {
        var r = new HttpRequestMessage(m, path);
        var t = await (await Js()).InvokeAsync<string?>("token");
        if (!string.IsNullOrEmpty(t)) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", t);
        if (body is not null) r.Content = JsonContent.Create(body);
        return r;
    }

    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    public async Task<(T? Value, string? Error)> SendAsync<T>(HttpMethod m, string path, object? body = null)
    {
        try
        {
            using var res = await http.SendAsync(await Req(m, path, body));
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                try { var e = JsonSerializer.Deserialize<ApiError>(text, J); return (default, e?.Detail ?? e?.Error ?? $"HTTP {(int)res.StatusCode}"); }
                catch { return (default, $"HTTP {(int)res.StatusCode}"); }
            }
            return (JsonSerializer.Deserialize<T>(text, J), null);
        }
        catch (Exception ex) { return (default, ex.Message); }
    }

    public Task<(MeetingList? Value, string? Error)> MyMeetingsAsync() => SendAsync<MeetingList>(HttpMethod.Get, "/api/meetings");
    public Task<(Meeting? Value, string? Error)> CreateMeetingAsync(string title) => SendAsync<Meeting>(HttpMethod.Post, "/api/meetings", new { title });
    public Task<(Meeting? Value, string? Error)> GetMeetingAsync(string id) => SendAsync<Meeting>(HttpMethod.Get, $"/api/meetings/{id}");
    public Task<(JoinInfo? Value, string? Error)> JoinTokenAsync(string id, string name) => SendAsync<JoinInfo>(HttpMethod.Post, $"/api/meetings/{id}/token", new { name });
    public Task<(Meeting? Value, string? Error)> EndMeetingAsync(string id) => SendAsync<Meeting>(HttpMethod.Post, $"/api/meetings/{id}/end");
    public Task<(Meeting? Value, string? Error)> SealAsync(string id, string sha256, long bytes, long durationMs, string? axowlEventId)
        => SendAsync<Meeting>(HttpMethod.Post, $"/api/meetings/{id}/seal", new { sha256, bytes, durationMs, axowlEventId });
    public Task<(VerifyInfo? Value, string? Error)> VerifyAsync(string sealId) => SendAsync<VerifyInfo>(HttpMethod.Get, $"/api/verify/{sealId}");

    public async ValueTask DisposeAsync() { if (_mod is not null) await _mod.DisposeAsync(); }
}

public sealed record AxowlUser(string Sub, string Name, string? Email);
public sealed record ApiError(string? Error, string? Detail);
public sealed record MeetingList(List<Meeting> Meetings);
public sealed record Meeting(string Id, string Title, string? HostName, DateTimeOffset? CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt,
    string? SealId, DateTimeOffset? SealedAt, string? Sha256, long? Bytes, long? DurationMs, string? JoinUrl, string? VerifyUrl);
public sealed record JoinInfo(string Token, string Url, string Identity, string Name, bool IsHost, string Room);
public sealed record VerifyInfo(string SealId, string MeetingId, string Title, string? HostName, string Sha256, long Bytes, long DurationMs,
    DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, DateTimeOffset? SealedAt, string? AxowlEventId, string Ledger, string Org);
public sealed record Participant(string Identity, string Name, bool Local);
public sealed record RecordingResult(string Sha256, long Bytes, long DurationMs, string DownloadUrl, string FileName);
public sealed record FileHash(string Sha256, long Bytes, string Name);
