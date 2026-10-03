using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using ServerManager.Application.Files;
using ServerManager.Application.Interfaces.Files;

namespace ServerManager.Infrastructure.Files;

internal sealed class SftpFileSession : IRemoteFileSession
{
    private readonly SftpClient _client;

    public SftpFileSession(SftpClient client)
    {
        _client = client;
        HomeDirectory = client.WorkingDirectory;
    }

    public string HomeDirectory { get; }

    public async Task<RemoteFileInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _client.GetAsync(path, cancellationToken);
            return ToInfo(file, path);
        }
        catch (SftpPathNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (SftpErrorMapper.TryMap(ex, out var mapped))
        {
            throw mapped;
        }
    }

    public Task<IReadOnlyList<RemoteFileInfo>> ListAsync(string path, CancellationToken cancellationToken = default) =>
        MapErrorsAsync<IReadOnlyList<RemoteFileInfo>>(async () =>
        {
            var entries = new List<RemoteFileInfo>();
            await foreach (var file in _client.ListDirectoryAsync(path, cancellationToken))
            {
                if (file.Name is "." or "..")
                    continue;

                entries.Add(ToInfo(file, file.FullName));
            }

            return entries;
        });

    public Task<byte[]> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken = default) =>
        MapErrorsAsync(async () =>
        {
            await using var stream = await _client.OpenAsync(path, FileMode.Open, FileAccess.Read, cancellationToken);
            var limit = maxBytes + 1;
            var buffer = new byte[Math.Min(limit, 81920)];
            using var output = new MemoryStream();
            int read;
            while (output.Length < limit && (read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, limit - output.Length)), cancellationToken)) > 0)
                output.Write(buffer, 0, read);

            return output.ToArray();
        });

    public Task WriteAsync(string path, byte[] content, bool createNew, CancellationToken cancellationToken = default) =>
        MapErrorsAsync(async () =>
        {
            await using var stream = await _client.OpenAsync(path, createNew ? FileMode.CreateNew : FileMode.Create, FileAccess.Write, cancellationToken);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return true;
        });

    public Task UploadAsync(Stream content, string path, CancellationToken cancellationToken = default) =>
        MapErrorsAsync(async () =>
        {
            await _client.UploadFileAsync(content, path, cancellationToken);
            return true;
        });

    public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        MapErrorsAsync(async () =>
        {
            await _client.CreateDirectoryAsync(path, cancellationToken);
            return true;
        });

    public Task RenameAsync(string source, string destination, CancellationToken cancellationToken = default) =>
        MapErrorsAsync(async () =>
        {
            await _client.RenameFileAsync(source, destination, cancellationToken);
            return true;
        });

    public Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) =>
        MapErrorsAsync(async () =>
        {
            await _client.DeleteFileAsync(path, cancellationToken);
            return true;
        });

    public Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        MapErrorsAsync(async () =>
        {
            await _client.DeleteDirectoryAsync(path, cancellationToken);
            return true;
        });

    internal static RemoteFileInfo ToInfo(ISftpFile file, string fullPath)
    {
        var attributes = file.Attributes;
        var kind = attributes.IsSymbolicLink ? RemoteFileKind.SymbolicLink
            : attributes.IsDirectory ? RemoteFileKind.Directory
            : attributes.IsRegularFile ? RemoteFileKind.File
            : RemoteFileKind.Other;

        return new RemoteFileInfo(
            file.Name,
            fullPath,
            kind,
            attributes.Size,
            attributes.LastWriteTimeUtc,
            ModeOf(attributes),
            attributes.UserId,
            attributes.GroupId);
    }

    private static int ModeOf(SftpFileAttributes a)
    {
        var mode = 0;
        if (a.OwnerCanRead) mode |= 0x100;
        if (a.OwnerCanWrite) mode |= 0x80;
        if (a.OwnerCanExecute) mode |= 0x40;
        if (a.GroupCanRead) mode |= 0x20;
        if (a.GroupCanWrite) mode |= 0x10;
        if (a.GroupCanExecute) mode |= 0x8;
        if (a.OthersCanRead) mode |= 0x4;
        if (a.OthersCanWrite) mode |= 0x2;
        if (a.OthersCanExecute) mode |= 0x1;
        if (a.IsUIDBitSet) mode |= FileModes.SetUid;
        if (a.IsGroupIDBitSet) mode |= FileModes.SetGid;
        if (a.IsStickyBitSet) mode |= FileModes.Sticky;
        return mode;
    }

    private static async Task<T> MapErrorsAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex) when (SftpErrorMapper.TryMap(ex, out var mapped))
        {
            throw mapped;
        }
    }
}
