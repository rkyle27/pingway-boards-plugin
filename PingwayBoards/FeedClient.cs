using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PingwayBoards;

/// <summary>What the window shows: the last good posters plus the state of the last request. Never changed after it is made.</summary>
public sealed class FeedSnapshot
{
    public static readonly FeedSnapshot Empty = new();

    public IReadOnlyList<Poster> Posters { get; init; } = Array.Empty<Poster>();
    public bool HasData { get; init; }
    public bool Loading { get; init; }
    public DateTimeOffset LoadedAt { get; init; }

    /// <summary>null = fine; otherwise not_found, rate_limited, method, error, network or update.</summary>
    public string? Error { get; init; }
    public int RetryAfterSeconds { get; init; }
}

/// <summary>
/// Reads the public Pingway Boards feed: GET https://pingway.app/api/boards-feed, nothing else.
/// Runs off the game thread; the window reads <see cref="Current"/>, which is swapped in one step.
/// No login, no cookies, no telemetry; nothing about the player is sent.
/// </summary>
public sealed class FeedClient : IDisposable
{
    public const string FeedUrl = "https://pingway.app/api/boards-feed";

    /// <summary>While the window is open: at most one request every 2 minutes.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    /// <summary>When the window opens: a new request unless the last one is younger than the feed's 60 s cache.</summary>
    private static readonly TimeSpan OpenInterval = TimeSpan.FromSeconds(60);

    private const long MaxBytes = 4 * 1024 * 1024;

    private readonly HttpClient http;
    private readonly CancellationTokenSource cts = new();
    private FeedSnapshot current = FeedSnapshot.Empty;
    private string? etag;
    private int busy;
    private long lastAttemptTicks; // UTC ticks, game thread
    private long nextAllowedTicks; // UTC ticks (Retry-After), set by the request task

    public FeedClient(string version)
    {
        this.http = new HttpClient(new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        })
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        this.http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PingwayBoards", version));
        this.http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public FeedSnapshot Current => Volatile.Read(ref this.current);

    /// <summary>Called every frame while the window is open (game thread). Starts a request when one is due.</summary>
    /// <param name="justOpened">The window was just opened (or the demo was switched off).</param>
    public void Tick(bool justOpened)
    {
        if (Volatile.Read(ref this.busy) != 0)
            return;
        var now = DateTime.UtcNow.Ticks;
        if (now < Interlocked.Read(ref this.nextAllowedTicks))
            return; // 429: wait for Retry-After
        var since = TimeSpan.FromTicks(now - this.lastAttemptTicks);
        var due = this.lastAttemptTicks == 0 || since >= Interval || (justOpened && since >= OpenInterval);
        if (!due)
            return;
        if (Interlocked.CompareExchange(ref this.busy, 1, 0) != 0)
            return;
        this.lastAttemptTicks = now;
        var before = this.Current;
        this.Publish(new FeedSnapshot
        {
            Posters = before.Posters, HasData = before.HasData, LoadedAt = before.LoadedAt, Error = before.Error,
            RetryAfterSeconds = before.RetryAfterSeconds, Loading = true,
        });
        _ = Task.Run(this.FetchAsync);
    }

    private void Publish(FeedSnapshot snap) => Volatile.Write(ref this.current, snap);

    private async Task FetchAsync()
    {
        var before = this.Current;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
            if (this.etag != null)
                req.Headers.TryAddWithoutValidation("If-None-Match", this.etag);
            using var resp = await this.http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, this.cts.Token).ConfigureAwait(false);

            if (resp.StatusCode == HttpStatusCode.NotModified && before.HasData)
            {
                this.Publish(new FeedSnapshot { Posters = before.Posters, HasData = true, LoadedAt = DateTimeOffset.Now });
                return;
            }

            if (resp.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var wait = RetryAfter(resp);
                Interlocked.Exchange(ref this.nextAllowedTicks, DateTime.UtcNow.Add(wait).Ticks);
                this.Fail(before, "rate_limited", (int)Math.Ceiling(wait.TotalSeconds));
                return;
            }

            var length = resp.Content.Headers.ContentLength;
            if (length is > MaxBytes)
            {
                this.Fail(before, "error");
                return;
            }

            var body = await resp.Content.ReadAsByteArrayAsync(this.cts.Token).ConfigureAwait(false);
            if (body.LongLength > MaxBytes)
            {
                this.Fail(before, "error");
                return;
            }

            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                doc = null;
            }

            using (doc)
            {
                if (resp.StatusCode != HttpStatusCode.OK)
                {
                    var code = doc != null ? FeedParser.ErrorCode(doc.RootElement) : string.Empty;
                    if (code is not ("not_found" or "rate_limited" or "method" or "error"))
                    {
                        code = resp.StatusCode switch
                        {
                            HttpStatusCode.NotFound => "not_found",
                            HttpStatusCode.MethodNotAllowed => "method",
                            _ => "error",
                        };
                    }

                    this.Fail(before, code);
                    return;
                }

                if (doc == null)
                {
                    this.Fail(before, "error");
                    return;
                }

                if (FeedParser.Version(doc.RootElement) != FeedParser.FormatVersion)
                {
                    // a newer format: do not guess, ask for an update
                    this.Fail(before, "update");
                    return;
                }

                var posters = FeedParser.ReadPosters(doc.RootElement);
                this.etag = resp.Headers.ETag?.ToString();
                this.Publish(new FeedSnapshot { Posters = posters, HasData = true, LoadedAt = DateTimeOffset.Now });
            }
        }
        catch (OperationCanceledException) when (this.cts.IsCancellationRequested)
        {
            // plugin unloading
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or System.IO.IOException)
        {
            Plugin.Log.Debug("Pingway Boards: feed request failed ({Type})", ex.GetType().Name);
            this.Fail(before, "network");
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug("Pingway Boards: feed request error ({Type})", ex.GetType().Name);
            this.Fail(before, "error");
        }
        finally
        {
            Volatile.Write(ref this.busy, 0);
        }
    }

    private void Fail(FeedSnapshot before, string code, int retryAfter = 0)
    {
        this.Publish(new FeedSnapshot
        {
            Posters = before.Posters, HasData = before.HasData, LoadedAt = before.LoadedAt, Error = code, RetryAfterSeconds = retryAfter,
        });
    }

    private static TimeSpan RetryAfter(HttpResponseMessage resp)
    {
        var ra = resp.Headers.RetryAfter;
        TimeSpan wait;
        if (ra?.Delta is { } delta)
            wait = delta;
        else if (ra?.Date is { } date)
            wait = date - DateTimeOffset.UtcNow;
        else
            wait = TimeSpan.FromSeconds(60);
        if (wait < TimeSpan.FromSeconds(1))
            wait = TimeSpan.FromSeconds(1);
        if (wait > TimeSpan.FromHours(1))
            wait = TimeSpan.FromHours(1);
        return wait;
    }

    public void Dispose()
    {
        this.cts.Cancel();
        this.http.Dispose();
        this.cts.Dispose();
    }
}
