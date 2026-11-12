using System.Security.Cryptography;

namespace Clipsa;

public static class UpdateTrust
{
    // Public key only. The private release key stays in the developer's Windows CNG store.
    public const string PublicKey = "MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAsphT6VxKodYVif5VoYoPJYhSyqRq7Jj/AYYAGbyw+icW2DyhOhTxazgclV4nxP901G97In3B9I44z+ye55WPEnB5W0+8AcqiAUH4fx6UGahSuEUgeCXij9/wA8P0mDE/Z5PYyueaGEtaNzGodrMCM2BG0jPVZUNTczjVM2HBMP2kf+nRMkjJ8zLzmWOzKVXOPdm0fvosRIFVMpsZhsOPLknfpGduZJIpRwkbZxLfT+ZFv6AVJDZ0GaSLCN6Vne1EH5uH1aPhLH5aZM6fxnL+d8U8tNU/mocs/2mBBtllr7KeJB82z5HGjgTCauoJvOALENpnz62nI1XPXqHER/2JTvoXHk5oOLqB1P6kdF6gKcRcS+irM2PWYIUeO5tja1sLEo0jhFvIUDxOywFZLD7VZGTOAVidWJx9R2HJW4zY2ZeaJYs5wwujOv7q/3EgwVBG36/Vnq1YTVN7U2seE/A4cJ+ek0AsZvlu/gslmUWUqMfzLWtTXFLz0uEN+BkxW82ZAgMBAAE=";
    public static bool Verify(byte[] payload, byte[] signature)
    {
        using var rsa = RSA.Create(); rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKey), out _);
        return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
    }
}
