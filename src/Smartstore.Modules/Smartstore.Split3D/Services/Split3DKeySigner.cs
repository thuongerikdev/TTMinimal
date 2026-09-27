#nullable enable

using System.Buffers.Text;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Smartstore.Split3D.Services;

/// <summary>
/// The payload signed into a Split3D activation key. Field names and order match the
/// original Python License Studio so that the Blender addon accepts the keys unchanged.
/// </summary>
public sealed class Split3DKeyPayload
{
    public int V { get; init; } = 1;
    public required string Product { get; init; }
    public required string Customer { get; init; }
    public required string Email { get; init; }
    public long Issued { get; init; }
    public long? Expires { get; init; }
    public required string Id { get; init; }
}

/// <summary>
/// A short-lived activation lease that binds a key to one device. Signed with the same key pair as
/// activation keys; its "kind" field and the missing customer fields keep it from being accepted as a key.
/// </summary>
public sealed class Split3DLeasePayload
{
    public const string Kind = "lease";

    public int V { get; init; } = 1;
    public required string Product { get; init; }

    /// <summary>
    /// The signed "id" of the activation key.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Hashed device fingerprint the lease is bound to.
    /// </summary>
    public required string Device { get; init; }

    public long Issued { get; init; }

    /// <summary>
    /// End of the offline grace period. Never later than <see cref="KeyExpires"/>.
    /// </summary>
    public long Expires { get; init; }

    public long? KeyExpires { get; init; }

    /// <summary>
    /// Start of the activation on this device.
    /// </summary>
    public long Activated { get; init; }

    public int MaxDevices { get; init; }
    public int ActiveDevices { get; init; }
}

/// <summary>
/// Signs and verifies Split3D activation keys: RSASSA-PKCS1-v1_5 with SHA-256 over the raw
/// JSON payload, encoded as <c>base64url(payload) + "." + base64url(signature)</c>.
/// The private key only carries <c>n</c> and <c>d</c> (no CRT parameters), which the
/// platform RSA providers cannot import, so the modular exponentiation is done directly.
/// </summary>
public sealed class Split3DKeySigner
{
    // DER prefix of DigestInfo for SHA-256 (RFC 8017, section 9.2).
    private static readonly byte[] _sha256DigestInfoPrefix = Convert.FromHexString("3031300d060960864801650304020105000420");
    private static readonly byte[] _pairCheckChallenge = Encoding.ASCII.GetBytes("Split3D signing key pair check");

    private static readonly JsonWriterOptions _writerOptions = new()
    {
        // Keep non-ASCII names readable, like Python's json.dumps(ensure_ascii=False).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public const int MaxTokenLength = 8192;

    private readonly BigInteger _n;
    private readonly BigInteger _e;
    private readonly BigInteger? _d;
    private readonly int _size;

    private Split3DKeySigner(BigInteger n, BigInteger e, BigInteger? d)
    {
        _n = n;
        _e = e;
        _d = d;
        _size = (int)((n.GetBitLength() + 7) / 8);
    }

    public bool CanSign => _d.HasValue;

    /// <summary>
    /// Creates a signer from the addon's public key and, optionally, the private signing key.
    /// Validates that both belong to the same key pair.
    /// </summary>
    /// <exception cref="Split3DKeyException">The key material is missing or invalid.</exception>
    public static Split3DKeySigner Create(string? publicKeyJson, string? privateKeyJson)
    {
        if (string.IsNullOrWhiteSpace(publicKeyJson))
        {
            throw new Split3DKeyException("Public key (public_key.json) is not configured.");
        }

        var publicKey = ParseKey(publicKeyJson, "public_key.json");
        var n = GetNumber(publicKey, "n", "public_key.json");
        var e = GetNumber(publicKey, "e", "public_key.json");

        if (n.GetBitLength() < 2048 || e <= 1)
        {
            throw new Split3DKeyException("Public key is invalid.");
        }

        BigInteger? d = null;

        if (!string.IsNullOrWhiteSpace(privateKeyJson))
        {
            var privateKey = ParseKey(privateKeyJson, "private_key.json");
            var privateN = GetNumber(privateKey, "n", "private_key.json");
            var privateD = GetNumber(privateKey, "d", "private_key.json");

            if (privateN != n || privateD <= 1 || privateD >= n)
            {
                throw new Split3DKeyException("The signing key does not match the addon's public key.");
            }

            var challenge = new BigInteger(SHA256.HashData(_pairCheckChallenge), isUnsigned: true, isBigEndian: true);
            if (BigInteger.ModPow(BigInteger.ModPow(challenge, privateD, n), e, n) != challenge)
            {
                throw new Split3DKeyException("The signing key is not the pair of the addon's public key.");
            }

            d = privateD;
        }

        return new Split3DKeySigner(n, e, d);
    }

    /// <summary>
    /// Serializes and signs <paramref name="payload"/>.
    /// </summary>
    public string Sign(Split3DKeyPayload payload)
    {
        var token = SignRaw(Serialize(payload));

        if (token.Length > MaxTokenLength)
        {
            throw new Split3DKeyException("Customer information is too long. Please shorten the name.");
        }

        return token;
    }

    /// <summary>
    /// Serializes and signs an activation lease, in the same token format as keys.
    /// </summary>
    public string SignLease(Split3DLeasePayload payload)
    {
        Guard.NotNull(payload);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, _writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", payload.V);
            writer.WriteString("kind", Split3DLeasePayload.Kind);
            writer.WriteString("product", payload.Product);
            writer.WriteString("id", payload.Id);
            writer.WriteString("device", payload.Device);
            writer.WriteNumber("issued", payload.Issued);
            writer.WriteNumber("expires", payload.Expires);
            if (payload.KeyExpires.HasValue)
            {
                writer.WriteNumber("key_expires", payload.KeyExpires.Value);
            }
            else
            {
                writer.WriteNull("key_expires");
            }
            writer.WriteNumber("activated", payload.Activated);
            writer.WriteNumber("max_devices", payload.MaxDevices);
            writer.WriteNumber("active_devices", payload.ActiveDevices);
            writer.WriteEndObject();
        }

        return SignRaw(stream.ToArray());
    }

    private string SignRaw(byte[] raw)
    {
        if (!_d.HasValue)
        {
            throw new Split3DKeyException("Private signing key (private_key.json) is not configured.");
        }

        var block = new BigInteger(EncodeBlock(raw), isUnsigned: true, isBigEndian: true);
        var signature = ToFixedBytes(BigInteger.ModPow(block, _d.Value, _n));

        return Base64Url.EncodeToString(raw) + "." + Base64Url.EncodeToString(signature);
    }

    /// <summary>
    /// Verifies the signature of <paramref name="token"/> and returns its payload.
    /// </summary>
    /// <exception cref="Split3DKeyException">The token is malformed or its signature is invalid.</exception>
    public Split3DKeyPayload Verify(string token)
    {
        token = token?.Trim().Replace("\r", "").Replace("\n", "").Replace(" ", "") ?? string.Empty;

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            throw new Split3DKeyException("Key has an invalid format.");
        }

        byte[] raw, signature;
        try
        {
            raw = Base64Url.DecodeFromChars(parts[0]);
            signature = Base64Url.DecodeFromChars(parts[1]);
        }
        catch (FormatException)
        {
            throw new Split3DKeyException("Key has an invalid format.");
        }

        var expected = EncodeBlock(raw);
        var actual = ToFixedBytes(BigInteger.ModPow(new BigInteger(signature, isUnsigned: true, isBigEndian: true), _e, _n));

        if (signature.Length != _size || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            throw new Split3DKeyException("Key signature is invalid.");
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var expires = root.GetProperty("expires");

            return new Split3DKeyPayload
            {
                V = root.GetProperty("v").GetInt32(),
                Product = root.GetProperty("product").GetString()!,
                Customer = root.GetProperty("customer").GetString()!,
                Email = root.GetProperty("email").GetString()!,
                Issued = root.GetProperty("issued").GetInt64(),
                Expires = expires.ValueKind == JsonValueKind.Null ? null : expires.GetInt64(),
                Id = root.GetProperty("id").GetString()!
            };
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new Split3DKeyException("Key payload is invalid.");
        }
    }

    private static byte[] Serialize(Split3DKeyPayload payload)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, _writerOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", payload.V);
            writer.WriteString("product", payload.Product);
            writer.WriteString("customer", payload.Customer);
            writer.WriteString("email", payload.Email);
            writer.WriteNumber("issued", payload.Issued);
            if (payload.Expires.HasValue)
            {
                writer.WriteNumber("expires", payload.Expires.Value);
            }
            else
            {
                writer.WriteNull("expires");
            }
            writer.WriteString("id", payload.Id);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private byte[] EncodeBlock(byte[] raw)
    {
        // EMSA-PKCS1-v1_5: 0x00 0x01 FF..FF 0x00 DigestInfo
        var digest = SHA256.HashData(raw);
        var block = new byte[_size];
        var digestInfoLength = _sha256DigestInfoPrefix.Length + digest.Length;

        block[1] = 0x01;
        block.AsSpan(2, _size - digestInfoLength - 3).Fill(0xFF);
        _sha256DigestInfoPrefix.CopyTo(block, _size - digestInfoLength);
        digest.CopyTo(block, _size - digest.Length);

        return block;
    }

    private byte[] ToFixedBytes(BigInteger value)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (bytes.Length == _size)
        {
            return bytes;
        }

        if (bytes.Length > _size)
        {
            throw new Split3DKeyException("Key signature is invalid.");
        }

        var result = new byte[_size];
        bytes.CopyTo(result, _size - bytes.Length);
        return result;
    }

    private static JsonElement ParseKey(string json, string fileName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json.Trim().TrimStart('﻿'));
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new Split3DKeyException($"{fileName} is not valid JSON.");
        }
    }

    private static BigInteger GetNumber(JsonElement key, string name, string fileName)
    {
        if (key.ValueKind != JsonValueKind.Object || !key.TryGetProperty(name, out var element))
        {
            throw new Split3DKeyException($"{fileName} does not contain \"{name}\".");
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
        if (!BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new Split3DKeyException($"\"{name}\" in {fileName} is not a valid number.");
        }

        return value;
    }
}

/// <summary>
/// Thrown when Split3D key material or an activation key is invalid.
/// </summary>
public class Split3DKeyException(string message) : Exception(message)
{
}
