using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace Pinta.Modules.Media.Storage;

/// <summary>
/// S3-compatible storage (docs/integrations.md): MinIO locally, any S3 provider in production by configuration.
/// Connection string <c>minio</c> (set by Aspire): <c>Endpoint=http://…;AccessKey=…;SecretKey=…</c>.
/// <c>Storage:PublicEndpoint</c> is the address browsers use when it differs from the API's.
/// </summary>
public sealed class StorageOptions
{
    public const string Region = "us-east-1";

    public required Uri Endpoint { get; init; }

    public required Uri PublicEndpoint { get; init; }

    public required string AccessKey { get; init; }

    public required string SecretKey { get; init; }

    public required string Bucket { get; init; }

    public static StorageOptions FromConfiguration(IConfiguration configuration)
    {
        var raw = configuration.GetConnectionString("minio")
            ?? throw new InvalidOperationException("ConnectionStrings:minio is not configured.");
        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0].Trim(), p => p.Length > 1 ? p[1].Trim() : "", StringComparer.OrdinalIgnoreCase);
        var endpoint = new Uri(parts["Endpoint"]);
        return new StorageOptions
        {
            Endpoint = endpoint,
            PublicEndpoint = configuration["Storage:PublicEndpoint"] is { Length: > 0 } publicEndpoint ? new Uri(publicEndpoint) : endpoint,
            AccessKey = parts["AccessKey"],
            SecretKey = parts["SecretKey"],
            Bucket = configuration["Storage:Bucket"] ?? "pinta-media",
        };
    }
}

/// <summary>A browser upload form: POST <see cref="Url"/> as multipart with these fields and the file last.</summary>
public sealed record PresignedPost(string Url, IReadOnlyDictionary<string, string> Fields, DateTimeOffset ExpiresAt);

public sealed record StoredObject(long SizeBytes, string? ContentType);

public sealed class ObjectStorage(StorageOptions options, TimeProvider clock) : IDisposable
{
    private readonly AmazonS3Client _s3 = new(
        new BasicAWSCredentials(options.AccessKey, options.SecretKey),
        new AmazonS3Config { ServiceURL = options.Endpoint.ToString(), ForcePathStyle = true, AuthenticationRegion = StorageOptions.Region });

    // Signs read URLs for the address browsers use; signing needs no request to the storage.
    private readonly AmazonS3Client _publicS3 = new(
        new BasicAWSCredentials(options.AccessKey, options.SecretKey),
        new AmazonS3Config { ServiceURL = options.PublicEndpoint.ToString(), ForcePathStyle = true, AuthenticationRegion = StorageOptions.Region });

    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    /// <summary>Creates the private bucket on first use.</summary>
    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
        {
            return;
        }

        await _bucketLock.WaitAsync(cancellationToken);
        try
        {
            if (!_bucketReady)
            {
                var buckets = await _s3.ListBucketsAsync(cancellationToken);
                if (buckets.Buckets?.Any(b => b.BucketName == options.Bucket) != true)
                {
                    await _s3.PutBucketAsync(new PutBucketRequest { BucketName = options.Bucket }, cancellationToken);
                }

                _bucketReady = true;
            }
        }
        finally
        {
            _bucketLock.Release();
        }
    }

    /// <summary>
    /// SigV4 POST policy (https://docs.aws.amazon.com/AmazonS3/latest/API/sigv4-HTTPPOSTConstructPolicy.html): S3 itself
    /// rejects another key, another content type or a file larger than <paramref name="maxBytes"/>.
    /// </summary>
    public PresignedPost CreateUpload(string key, string contentType, long maxBytes, TimeSpan lifetime)
    {
        var now = clock.GetUtcNow();
        var expires = now.Add(lifetime);
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var credential = $"{options.AccessKey}/{date}/{StorageOptions.Region}/s3/aws4_request";

        var policy = JsonSerializer.Serialize(new
        {
            expiration = expires.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            conditions = new object[]
            {
                new Dictionary<string, string> { ["bucket"] = options.Bucket },
                new Dictionary<string, string> { ["key"] = key },
                new Dictionary<string, string> { ["Content-Type"] = contentType },
                new object[] { "content-length-range", 1, maxBytes },
                new Dictionary<string, string> { ["x-amz-algorithm"] = "AWS4-HMAC-SHA256" },
                new Dictionary<string, string> { ["x-amz-credential"] = credential },
                new Dictionary<string, string> { ["x-amz-date"] = amzDate },
            },
        });
        var policyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(policy));

        var signingKey = Hmac(Encoding.UTF8.GetBytes("AWS4" + options.SecretKey), date);
        signingKey = Hmac(signingKey, StorageOptions.Region);
        signingKey = Hmac(signingKey, "s3");
        signingKey = Hmac(signingKey, "aws4_request");
        var signature = Convert.ToHexStringLower(Hmac(signingKey, policyBase64));

        var url = new Uri(options.PublicEndpoint, options.Bucket).ToString();
        return new PresignedPost(url, new Dictionary<string, string>
        {
            ["key"] = key,
            ["Content-Type"] = contentType,
            ["x-amz-algorithm"] = "AWS4-HMAC-SHA256",
            ["x-amz-credential"] = credential,
            ["x-amz-date"] = amzDate,
            ["policy"] = policyBase64,
            ["x-amz-signature"] = signature,
        }, expires);
    }

    /// <summary>A presigned GET URL (SigV4 query string) valid for <paramref name="lifetime"/>.</summary>
    public (string Url, DateTimeOffset ExpiresAt) CreateDownload(string key, TimeSpan lifetime)
    {
        var expires = clock.GetUtcNow().Add(lifetime);
        var url = _publicS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expires.UtcDateTime,
            Protocol = options.PublicEndpoint.Scheme == "https" ? Protocol.HTTPS : Protocol.HTTP,
        });
        return (url, expires);
    }

    public async Task<StoredObject?> FindAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _s3.GetObjectMetadataAsync(options.Bucket, key, cancellationToken);
            return new StoredObject(metadata.ContentLength, metadata.Headers.ContentType);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        _s3.DeleteObjectAsync(options.Bucket, key, cancellationToken);

    /// <summary>Stores a file from the server itself (the demo seed); everything else uploads through a signed POST.</summary>
    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        using var stream = new MemoryStream(content);
        await _s3.PutObjectAsync(new PutObjectRequest { BucketName = options.Bucket, Key = key, InputStream = stream, ContentType = contentType }, cancellationToken);
    }

    /// <summary>Removes every stored file (the demo reset).</summary>
    public async Task DeleteAllAsync(CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        string? token = null;
        do
        {
            var page = await _s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = options.Bucket, ContinuationToken = token }, cancellationToken);
            foreach (var item in page.S3Objects ?? [])
            {
                await _s3.DeleteObjectAsync(options.Bucket, item.Key, cancellationToken);
            }

            token = page.IsTruncated == true ? page.NextContinuationToken : null;
        }
        while (token is not null);
    }

    public void Dispose()
    {
        _s3.Dispose();
        _publicS3.Dispose();
        _bucketLock.Dispose();
    }

    private static byte[] Hmac(byte[] key, string data) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));
}
