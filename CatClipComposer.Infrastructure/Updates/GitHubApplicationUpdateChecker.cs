using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CatClipComposer.Core.Models;
using CatClipComposer.Core.Services;

namespace CatClipComposer.Infrastructure.Updates;

public sealed partial class GitHubApplicationUpdateChecker : IApplicationUpdateChecker
{
    public static readonly Uri RepositoryUri = new("https://github.com/propiro/CatClipComposer");
    public static readonly Uri ReleasesUri = new("https://github.com/propiro/CatClipComposer/releases/latest");

    private static readonly Uri LatestReleaseApiUri =
        new("https://api.github.com/repos/propiro/CatClipComposer/releases/latest");
    private static readonly Uri RepositoryVersionUri =
        new("https://raw.githubusercontent.com/propiro/CatClipComposer/main/Directory.Build.props");
    private static readonly HttpClient Client = CreateClient();
    private readonly SemaphoreSlim _checkGate = new(1, 1);
    private ApplicationUpdateInfo? _cachedResult;
    private string? _cachedRequestKey;

    public async Task<ApplicationUpdateInfo> CheckAsync(
        string currentVersion,
        string? currentRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        if (ParseVersion(currentVersion) is null)
        {
            throw new ArgumentException("The installed application version is invalid.", nameof(currentVersion));
        }

        var requestKey = $"{currentVersion}|{currentRevision}";
        await _checkGate.WaitAsync(cancellationToken);
        try
        {
            if (_cachedResult is not null &&
                requestKey.Equals(_cachedRequestKey, StringComparison.Ordinal) &&
                DateTimeOffset.UtcNow - _cachedResult.CheckedAtUtc < TimeSpan.FromMinutes(5))
            {
                return _cachedResult;
            }

            var result = await CheckCoreAsync(currentVersion, currentRevision, cancellationToken);
            _cachedRequestKey = requestKey;
            _cachedResult = result;
            return result;
        }
        finally
        {
            _checkGate.Release();
        }
    }

    private static async Task<ApplicationUpdateInfo> CheckCoreAsync(
        string currentVersion,
        string? currentRevision,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var completedChecks = 0;

        ReleaseResponse? release = null;
        try
        {
            release = JsonSerializer.Deserialize<ReleaseResponse>(
                          await DownloadStringAsync(LatestReleaseApiUri, 2 * 1024 * 1024, cancellationToken)) ??
                      throw new InvalidDataException("GitHub returned an empty release response.");
            completedChecks++;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                          IsExpectedRemoteFailure(exception))
        {
            warnings.Add($"Binary release check failed: {GetRemoteFailureMessage(exception)}");
        }

        string? latestCodeVersion = null;
        try
        {
            var versionXml = await DownloadStringAsync(RepositoryVersionUri, 64 * 1024, cancellationToken);
            using var xmlReader = XmlReader.Create(
                new StringReader(versionXml),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var versionDocument = XDocument.Load(xmlReader, LoadOptions.None);
            latestCodeVersion = versionDocument.Descendants("Version").FirstOrDefault()?.Value.Trim();
            if (string.IsNullOrWhiteSpace(latestCodeVersion))
            {
                throw new InvalidDataException("The repository version file did not contain a Version value.");
            }

            completedChecks++;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                          IsExpectedRemoteFailure(exception))
        {
            warnings.Add($"Repository version check failed: {GetRemoteFailureMessage(exception)}");
        }

        CompareResponse? comparison = null;
        var normalizedRevision = NormalizeRevision(currentRevision);
        if (normalizedRevision is not null)
        {
            try
            {
                var comparisonUri = new Uri(
                    $"https://api.github.com/repos/propiro/CatClipComposer/compare/{normalizedRevision}...main");
                comparison = JsonSerializer.Deserialize<CompareResponse>(
                                 await DownloadStringAsync(comparisonUri, 8 * 1024 * 1024, cancellationToken)) ??
                             throw new InvalidDataException("GitHub returned an empty comparison response.");
                completedChecks++;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                                              IsExpectedRemoteFailure(exception))
            {
                warnings.Add($"Repository revision check failed: {GetRemoteFailureMessage(exception)}");
            }
        }
        else
        {
            warnings.Add("This build has no comparable Git commit revision; version checks still ran.");
        }

        if (completedChecks == 0)
        {
            throw new InvalidOperationException(
                "GitHub could not be reached. Check the internet connection and try again.");
        }

        var current = ParseVersion(currentVersion);
        var releaseVersion = ParseVersion(release?.TagName);
        if (release is not null && releaseVersion is null)
        {
            warnings.Add("The latest GitHub Release tag was not a valid application version.");
        }

        var normalizedReleaseVersion = releaseVersion?.ToString(3);
        var expectedBinaryNames = normalizedReleaseVersion is null
            ? Array.Empty<string>()
            : new[]
            {
                $"CatClipComposer-v{normalizedReleaseVersion}-win-x64-light.zip",
                $"CatClipComposer-v{normalizedReleaseVersion}-win-x64.zip"
            };
        var binaryPackageFound = release?.Assets?.Any(asset =>
            expectedBinaryNames.Contains(asset.Name, StringComparer.OrdinalIgnoreCase)) == true;
        var latestBinaryVersion = binaryPackageFound ? normalizedReleaseVersion : null;
        var isBinaryUpdateAvailable = current is not null && releaseVersion is not null &&
                                      binaryPackageFound && releaseVersion > current;

        var codeVersion = ParseVersion(latestCodeVersion);
        if (latestCodeVersion is not null && codeVersion is null)
        {
            warnings.Add("The repository Version value was not a valid application version.");
        }

        var repositoryHasNewerCommits = comparison?.AheadBy > 0;
        var isCodeUpdateAvailable = current is not null && codeVersion is not null && codeVersion > current ||
                                    repositoryHasNewerCommits;

        return new ApplicationUpdateInfo(
            currentVersion,
            normalizedRevision,
            codeVersion?.ToString(3),
            comparison?.HeadCommit?.Sha,
            latestBinaryVersion,
            binaryPackageFound,
            isCodeUpdateAvailable,
            isBinaryUpdateAvailable,
            RepositoryUri,
            ReleasesUri,
            warnings,
            DateTimeOffset.UtcNow);
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CatClipComposer-UpdateChecker/1.0");
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static async Task<string> DownloadStringAsync(
        Uri uri,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(
            uri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if ((response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) &&
            response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) &&
            remaining.Contains("0", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("GitHub's anonymous API rate limit has been reached.");
        }

        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximumBytes)
        {
            throw new InvalidDataException("GitHub returned an unexpectedly large response.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (destination.Length + read > maximumBytes)
            {
                throw new InvalidDataException("GitHub returned an unexpectedly large response.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return Encoding.UTF8.GetString(destination.GetBuffer(), 0, checked((int)destination.Length));
    }

    private static bool IsExpectedRemoteFailure(Exception exception) => exception is
        HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or
        InvalidOperationException or System.Xml.XmlException;

    private static string GetRemoteFailureMessage(Exception exception) => exception switch
    {
        TaskCanceledException => "the request timed out",
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "the GitHub resource was not found",
        _ => exception.Message
    };

    private static string? NormalizeRevision(string? revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
        {
            return null;
        }

        var candidate = revision.Trim();
        return GitRevisionRegex().IsMatch(candidate) ? candidate.ToLowerInvariant() : null;
    }

    private static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        if (!Version.TryParse(candidate, out var version))
        {
            return null;
        }

        return new Version(version.Major, version.Minor, Math.Max(0, version.Build));
    }

    [GeneratedRegex("^[0-9a-fA-F]{7,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex GitRevisionRegex();

    private sealed class ReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("assets")]
        public List<ReleaseAsset> Assets { get; init; } = [];
    }

    private sealed class ReleaseAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }

    private sealed class CompareResponse
    {
        [JsonPropertyName("ahead_by")]
        public int AheadBy { get; init; }

        [JsonPropertyName("head_commit")]
        public CommitResponse? HeadCommit { get; init; }
    }

    private sealed class CommitResponse
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; init; }
    }
}
