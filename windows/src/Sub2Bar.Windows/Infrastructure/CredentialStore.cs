using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sub2Bar.Core.Models;

namespace Sub2Bar.Windows.Infrastructure;

public sealed class CredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("com.sub2bar.windows.credentials.v1");
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task<Credentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(AppPaths.CredentialsFile))
            {
                return null;
            }

            var encrypted = await File.ReadAllBytesAsync(AppPaths.CredentialsFile, cancellationToken);
            var clear = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                return JsonSerializer.Deserialize<Credentials>(clear, Options);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(clear);
            }
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or IOException)
        {
            return null;
        }
    }

    public async Task SaveAsync(Credentials credentials, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDataDirectory();
        var clear = JsonSerializer.SerializeToUtf8Bytes(credentials, Options);
        try
        {
            var encrypted = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
            var temporaryFile = $"{AppPaths.CredentialsFile}.tmp";
            await File.WriteAllBytesAsync(temporaryFile, encrypted, cancellationToken);
            File.Move(temporaryFile, AppPaths.CredentialsFile, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
        }
    }

    public void Delete()
    {
        if (File.Exists(AppPaths.CredentialsFile))
        {
            File.Delete(AppPaths.CredentialsFile);
        }
    }
}
