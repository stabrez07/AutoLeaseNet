using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using AutoLeaseNet.Application.Ports.Storage;

namespace AutoLeaseNet.Adapters.Storage.InMemory;

public sealed class InMemoryObjectStorage : IObjectStorage
{
    private sealed record StoredObject(byte[] Data, string ContentType, string ETag);

    private readonly ConcurrentDictionary<string, StoredObject> _store = new();

    private static string K(string container, string key) => $"{container}/{key}";

    public async Task<ObjectUploadResult> UploadAsync(
        string container, string objectKey, Stream content, string contentType,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var etag = Guid.NewGuid().ToString("N");
        _store[K(container, objectKey)] = new StoredObject(bytes, contentType, etag);
        return new ObjectUploadResult(objectKey, new Uri($"in-memory://{container}/{objectKey}"), bytes.LongLength, etag);
    }

    public Task<Stream> DownloadAsync(string container, string objectKey, CancellationToken ct)
    {
        if (!_store.TryGetValue(K(container, objectKey), out var obj))
            throw new FileNotFoundException($"{container}/{objectKey}");
        return Task.FromResult<Stream>(new MemoryStream(obj.Data));
    }

    public Task<bool> DeleteAsync(string container, string objectKey, CancellationToken ct)
        => Task.FromResult(_store.TryRemove(K(container, objectKey), out _));

    public Task<Uri> GetSignedReadUrlAsync(string container, string objectKey, TimeSpan validity, CancellationToken ct)
        => Task.FromResult(new Uri($"in-memory://{container}/{objectKey}?validUntil={DateTimeOffset.UtcNow.Add(validity):O}"));

    public Task<bool> ExistsAsync(string container, string objectKey, CancellationToken ct)
        => Task.FromResult(_store.ContainsKey(K(container, objectKey)));
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryStorage(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryObjectStorage>();
        services.AddSingleton<IObjectStorage>(sp => sp.GetRequiredService<InMemoryObjectStorage>());
        return services;
    }
}
