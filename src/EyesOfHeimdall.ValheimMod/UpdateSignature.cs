using System.Security.Cryptography;

namespace EyesOfHeimdall.ValheimMod;

internal static class UpdateSignature
{
    // Public half only: the private key never goes on GitHub, so a hijacked repo cannot ship an update that installed copies accept.
    private const string Modulus = "4niVbbmPIfqPKXzEU+6NL2YbIZCeCvESi8XJtxVP1lzOf9x7l80jApLVzR5c2lWmVcVrdIViKv1e+tAoX5cWJkrZSSEAC7hlGKzRlU1L5qi/ztpFNHtAwq/IJ2RJ0U9jkEM5B6guKbRi1SzYQhOmrtLYSkS8w8S77CHf5BLSLe5q+DQaQpbAGhNF6cPrbBO/RnRxx0K875CpJSmv7Hkw7oJUDMGKlsPPhhbBF8kEW4yaVJvQCryr45JUBo6EIYZRj6i4tGbWFrYIWKk0SLGCL1ZQrKb1IUMzZN0Aa7R4T5mOj9tpGHMD33/gUbr3J/up76UyLuk2GgXxNde+fQWieXyIctmckeFDTP+nFYVKqjidTOPvzWodEpG+HXL8qsTYLul12l5w57kmPatFDGR6cij/JoGsCbkBbTFfCeIkwXjnzKON8kbcdmTl1eQK3Ia7Gq0iNbsIZGA+TOPKBjaRSsT9wru6Nz/KJ804Ci8ZhJr8gBnFHofIWmQl9tMECQpp";
    private const string Exponent = "AQAB";

    // RSA PKCS#1 v1.5 over SHA-256 of the exact manifest bytes; the signature travels as Base64 text.
    public static bool IsValid(byte[] manifest, string signatureBase64)
    {
        try
        {
            var signature = Convert.FromBase64String(signatureBase64.Trim());

            using var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Convert.FromBase64String(Modulus),
                Exponent = Convert.FromBase64String(Exponent),
            });

            return rsa.VerifyData(manifest, "SHA256", signature);
        }
        catch (Exception e) when (e is FormatException || e is CryptographicException)
        {
            return false;
        }
    }
}
