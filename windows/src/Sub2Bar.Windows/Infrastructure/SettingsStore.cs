using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sub2Bar.Core.Models;

namespace Sub2Bar.Windows.Infrastructure;

public sealed class SettingsStore
{
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsFile))
            {
                return new AppSettings();
            }

            await using var stream = File.OpenRead(AppPaths.SettingsFile);
            return (await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken) ??
                    new AppSettings()).Normalize();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            AppPaths.EnsureDataDirectory();
            var temporaryFile = $"{AppPaths.SettingsFile}.tmp";
            await using (var stream = new FileStream(temporaryFile, FileMode.Create, FileAccess.Write, FileShare.None,
                             4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, settings.Normalize(), Options, cancellationToken);
            }

            File.Move(temporaryFile, AppPaths.SettingsFile, true);
        }
        finally
        {
            _saveGate.Release();
        }
    }
}
