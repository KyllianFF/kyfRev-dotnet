using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using KyfRev.Application;
using KyfRev.Application.Ports;

namespace KyfRev.Infrastructure.Windows;

/// <summary>
/// Windows Hello par le fournisseur de clés de Windows (« Microsoft Passport Key Storage Provider », NCrypt) : une clé RSA
/// de 2 048 bits propre à kyfRev et à l'utilisateur, que Windows garde et ne laisse jamais sortir. Sceller ne demande rien ;
/// rouvrir demande le code PIN, le visage ou l'empreinte, dans une fenêtre de Windows posée sur celle de kyfRev
/// (<see cref="Owner"/>), et la clé exige ce geste à chaque fois.
/// </summary>
public sealed class WindowsHello : IWindowsHello
{
    /// <summary>Le fournisseur de clés de Windows Hello.</summary>
    internal const string HelloProvider = "Microsoft Passport Key Storage Provider";

    private const int RsaBits = 2048;
    private const int AllowDecryptAndSign = 0x3;
    private const int CacheAuthMandatory = 1;

    private readonly INcrypt _ncrypt;
    private readonly string _keyName;
    private readonly Lazy<bool> _available;
    private nint _owner;

    /// <summary>Windows Hello de ce poste, pour l'utilisateur de la session.</summary>
    public WindowsHello()
        : this(new NativeNcrypt(HelloProvider), WindowsIdentity.GetCurrent().User?.Value ?? string.Empty)
    {
    }

    internal WindowsHello(INcrypt ncrypt, string sid)
    {
        _ncrypt = ncrypt;
        _keyName = KeyNameOf(sid);
        _available = new Lazy<bool>(() => sid.Length > 0 && ncrypt.HasHello(sid));
    }

    /// <summary>La fenêtre de kyfRev, sur laquelle Windows pose la sienne ; zéro tant qu'elle n'existe pas.</summary>
    public nint Owner
    {
        get => Volatile.Read(ref _owner);
        set => Volatile.Write(ref _owner, value);
    }

    /// <inheritdoc/>
    public bool IsAvailable => _available.Value;

    /// <summary>Le nom de la clé de kyfRev chez Windows, propre à l'utilisateur.</summary>
    internal static string KeyNameOf(string sid) => $"{sid}//kyfRev//cles-retenues";

    /// <inheritdoc/>
    public byte[]? Seal(ReadOnlySpan<byte> secret)
    {
        if (!IsAvailable)
        {
            return null;
        }
        Check(_ncrypt.OpenProvider(out var provider), "ouvrir le fournisseur de clés");
        try
        {
            var status = _ncrypt.OpenKey(provider, _keyName, silent: true, out var key);
            if (status == Ncrypt.NoKey)
            {
                status = Create(provider, out key);
                if (Ncrypt.IsCancel(status))
                {
                    return null;
                }
            }
            Check(status, "trouver la clé de kyfRev");
            try
            {
                Check(_ncrypt.Encrypt(key, secret, out var sealedSecret), "sceller la clé");
                return sealedSecret;
            }
            finally
            {
                _ncrypt.Free(key);
            }
        }
        finally
        {
            _ncrypt.Free(provider);
        }
    }

    /// <inheritdoc/>
    public byte[]? Open(byte[] sealedSecret, string message)
    {
        ArgumentNullException.ThrowIfNull(sealedSecret);
        ArgumentNullException.ThrowIfNull(message);
        if (!IsAvailable)
        {
            throw new WindowsHelloException(Messages.Unavailable);
        }
        Check(_ncrypt.OpenProvider(out var provider), "ouvrir le fournisseur de clés");
        try
        {
            var status = _ncrypt.OpenKey(provider, _keyName, silent: false, out var key);
            if (status == Ncrypt.NoKey)
            {
                throw new WindowsHelloException(Messages.NoKey);
            }
            Check(status, "trouver la clé de kyfRev");
            try
            {
                byte[] secret = [];
                status = Prompt(key, message);
                // Le geste est exigé à chaque fois, même si Windows en garde un récent.
                status = status == 0 ? SetInt(key, "PinCacheIsGestureRequired", 1) : status;
                status = status == 0 ? _ncrypt.Decrypt(key, sealedSecret, out secret) : status;
                if (Ncrypt.IsCancel(status))
                {
                    return null;
                }
                Check(status, "rouvrir la clé");
                return secret;
            }
            finally
            {
                _ncrypt.Free(key);
            }
        }
        finally
        {
            _ncrypt.Free(provider);
        }
    }

    /// <summary>
    /// Crée la clé de kyfRev : RSA de 2 048 bits, pour déchiffrer et signer, le geste exigé à chaque usage ; Windows demande
    /// à l'utilisateur de confirmer que c'est lui. Rien ne reste d'une création qui échoue.
    /// </summary>
    private int Create(nint provider, out nint key)
    {
        var status = _ncrypt.CreateKey(provider, _keyName, out key);
        if (status != 0)
        {
            return status;
        }
        status = SetInt(key, "Length", RsaBits);
        status = status == 0 ? SetInt(key, "Key Usage", AllowDecryptAndSign) : status;
        if (status == 0 && SetInt(key, "NgcCacheType", CacheAuthMandatory) != 0)
        {
            // Les premières versions de Windows 10 nommaient la propriété autrement.
            status = SetInt(key, "NgcCacheTypeProperty", CacheAuthMandatory);
        }
        status = status == 0 ? Prompt(key, Messages.Creating) : status;
        status = status == 0 ? _ncrypt.FinalizeKey(key) : status;
        if (status != 0)
        {
            _ncrypt.Free(key);
            key = 0;
        }
        return status;
    }

    /// <summary>La fenêtre de Windows Hello se pose sur celle de kyfRev, et dit pourquoi elle est là.</summary>
    private int Prompt(nint key, string message)
    {
        if (Owner is var owner and not 0)
        {
            Span<byte> handle = stackalloc byte[nint.Size];
            MemoryMarshal.Write(handle, in owner);
            // Sans elle, la fenêtre de Windows paraît quand même, seulement moins bien placée.
            _ = _ncrypt.SetProperty(key, "HWND Handle", handle);
        }
        return _ncrypt.SetProperty(key, "Use Context", Encoding.Unicode.GetBytes(message + "\0"));
    }

    private int SetInt(nint key, string property, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        MemoryMarshal.Write(bytes, in value);
        return _ncrypt.SetProperty(key, property, bytes);
    }

    private static void Check(int status, string doing)
    {
        if (status != 0)
        {
            throw new WindowsHelloException(Messages.Refused(doing, status));
        }
    }

    /// <summary>Les messages, en français.</summary>
    internal static class Messages
    {
        public const string Creating = "Créer la clé Windows Hello de kyfRev, qui ouvrira vos coffres chiffrés sur cet ordinateur.";

        public const string Unavailable = "Windows Hello n'est pas configuré sur cet ordinateur : saisissez le mot de passe.";

        public const string NoKey = "La clé Windows Hello de kyfRev n'existe plus sur cet ordinateur : saisissez le mot de passe.";

        public static string Refused(string doing, int status) =>
            $"Windows Hello a refusé de {doing} (code 0x{status:X8}) : saisissez le mot de passe.";
    }
}

/// <summary>Windows Hello a refusé : le message le dit, prêt à afficher.</summary>
public sealed class WindowsHelloException : OperationException
{
    /// <summary>Refus sans précision.</summary>
    public WindowsHelloException()
    {
    }

    /// <summary>Refus avec le message donné.</summary>
    public WindowsHelloException(string message)
        : base(message)
    {
    }

    /// <summary>Refus avec le message donné et sa cause.</summary>
    public WindowsHelloException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Les appels élémentaires à NCrypt, chacun rendant le code de Windows (0 : réussi).</summary>
internal interface INcrypt
{
    /// <summary>Vrai si l'utilisateur <paramref name="sid"/> a configuré Windows Hello.</summary>
    bool HasHello(string sid);

    int OpenProvider(out nint provider);

    int OpenKey(nint provider, string name, bool silent, out nint key);

    /// <summary>Une clé RSA à finaliser ; sans nom, elle n'est gardée nulle part.</summary>
    int CreateKey(nint provider, string? name, out nint key);

    int SetProperty(nint handle, string property, ReadOnlySpan<byte> value);

    int FinalizeKey(nint key);

    int Encrypt(nint key, ReadOnlySpan<byte> secret, out byte[] sealedSecret);

    int Decrypt(nint key, byte[] sealedSecret, out byte[] secret);

    void Free(nint handle);
}

/// <summary>Les codes de NCrypt que Windows Hello rend.</summary>
internal static class Ncrypt
{
    /// <summary>NTE_NO_KEY : la clé n'existe pas.</summary>
    public const int NoKey = unchecked((int)0x8009000D);

    /// <summary>NTE_USER_CANCELLED : l'utilisateur a fermé la fenêtre de Windows Hello.</summary>
    public const int UserCancelled = unchecked((int)0x80090036);

    /// <summary>HRESULT de ERROR_CANCELLED.</summary>
    public const int Cancelled = unchecked((int)0x800704C7);

    /// <summary>Vrai si l'utilisateur a renoncé.</summary>
    public static bool IsCancel(int status) => status is UserCancelled or Cancelled;
}

/// <summary>NCrypt pour de vrai, sur le fournisseur de clés <paramref name="providerName"/>.</summary>
/// <param name="providerName">Celui de Windows Hello, ou, pour les essais, le fournisseur logiciel.</param>
internal sealed partial class NativeNcrypt(string providerName) : INcrypt
{
    private const int Silent = 0x40;
    private const int PadPkcs1 = 0x2;
    private const int RsaBytes = 256;

    public bool HasHello(string sid)
    {
        try
        {
            return NgcGetDefaultDecryptionKeyName(sid, 0, 0, out var name) == 0 && !string.IsNullOrEmpty(name);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            // Un Windows sans Windows Hello : la clé retenue reste celle de la session.
            return false;
        }
    }

    public int OpenProvider(out nint provider) => NCryptOpenStorageProvider(out provider, providerName, 0);

    public int OpenKey(nint provider, string name, bool silent, out nint key) => NCryptOpenKey(provider, out key, name, 0, silent ? Silent : 0);

    public int CreateKey(nint provider, string? name, out nint key) => NCryptCreatePersistedKey(provider, out key, "RSA", name, 0, 0);

    public int SetProperty(nint handle, string property, ReadOnlySpan<byte> value) => NCryptSetProperty(handle, property, value, value.Length, 0);

    public int FinalizeKey(nint key) => NCryptFinalizeKey(key, 0);

    public int Encrypt(nint key, ReadOnlySpan<byte> secret, out byte[] sealedSecret)
    {
        var output = new byte[RsaBytes];
        var status = NCryptEncrypt(key, secret, secret.Length, 0, output, output.Length, out var written, PadPkcs1);
        sealedSecret = status == 0 ? output[..written] : [];
        return status;
    }

    public int Decrypt(nint key, byte[] sealedSecret, out byte[] secret)
    {
        var output = new byte[sealedSecret.Length];
        var status = NCryptDecrypt(key, sealedSecret, sealedSecret.Length, 0, output, output.Length, out var written, PadPkcs1);
        secret = status == 0 ? output[..written] : [];
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(output);
        return status;
    }

    public void Free(nint handle)
    {
        if (handle != 0)
        {
            _ = NCryptFreeObject(handle);
        }
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("cryptngc.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NgcGetDefaultDecryptionKeyName(string sid, int reserved1, int reserved2, out string? name);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ncrypt.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NCryptOpenStorageProvider(out nint provider, string name, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ncrypt.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NCryptOpenKey(nint provider, out nint key, string name, int legacyKeySpec, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ncrypt.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NCryptCreatePersistedKey(nint provider, out nint key, string algorithm, string? name, int legacyKeySpec, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ncrypt.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int NCryptSetProperty(nint handle, string property, ReadOnlySpan<byte> value, int size, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ncrypt.dll")]
    private static partial int NCryptFinalizeKey(nint key, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ncrypt.dll")]
    private static partial int NCryptEncrypt(nint key, ReadOnlySpan<byte> input, int inputSize, nint padding, Span<byte> output, int outputSize, out int written, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ncrypt.dll")]
    private static partial int NCryptDecrypt(nint key, ReadOnlySpan<byte> input, int inputSize, nint padding, Span<byte> output, int outputSize, out int written, int flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ncrypt.dll")]
    private static partial int NCryptFreeObject(nint handle);
}
