using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MebKoordinatorluk.Keygen;

public static class KeygenGenerator
{
    private const string RsaPrivateKeyXml = @"<RSAKeyValue><Modulus>zuEkm1pcrkGl23cjvm6JwasDvQLWpBYCYyW+SjzlEdfhNbfst0Hhln1uWlAsj87c71K+VeGkW387naM5UYuLJ8xVmEV3+W5Z7SaT4EpGIY4U495wQhmTNMJ6VgaDWCpBCgAS+c/tHLLrhmFfZ9J8rjROsu6TYzLXNkm/HGupqR1ccsjhqs1Wbccv2aHBVHvuRD8v8hQIHkzIOrCuPsCO7S2lEk3yfZn7WoZ4QXUzIAgSz2tNeAfpH1lLceIZmEDYMCoY/Y7V87qyljwxV29Nvj4YklKQJubqKKb6BVuilC+FmvfCE8gQXmvYNea7eyVVQ+wARJVcHesL97iYmCulxQ==</Modulus><Exponent>AQAB</Exponent><P>2PPw+UqtfQ8fle/93r+RiWoBMrdSXzKtTEL9WFjRY5yCdwa4PZwL9SenI/0KRCu0wi+QaO5vbTSeljt0Nmz9pKKiL6eXTJFujCd7sEMvcmyYkqpLTCglUwLvUaLG/SPnmJus4TmQQAPsKK7xHXvbCbJtwLdbydcyd6eKRpQ/7k8=</P><Q>9B0SDCZtidzozA9usjv/irrJfG5CYcBfcizeEVZut6KXNrhrZipmaqrXW3c06dZTpQm6sJMet4Nv2ppRsa6YAPAbex5r9NwvodzO27zBq/tYkE+VYj88jYsYCqZcglrvFFvccLpO+I5ydbS1ZSzYKV9UUzcezmM9RBHVExUCWas=</Q><DP>znPM453h3DkxCIC5qGfKOearPzTeaQMTkJfK+HCEyXLdYCD/4HkHlAJo9kFMrLSerY+Pr5r+ZACHzVTEcSpkcG/nQMwNmWpntW25MXGtzAh1ZFQzlUQ6yHTpglkRXUD2me3CpQ0fLRvOdqSLhFAhj2fBM3wJF9j2CJFhb2qSL9c=</DP><DQ>edbAmTvc1EBkGOVi9Kaphs5vniFSFIP1n4DxXWnsXM9T9cXaJOXotDiMwGQhDs0gdnECsvrbC/EYNN1tBOgFigJNVQf8DfyP8ZPJ0zbE21fZ/j0abCzvxh20ZCxgY9eLGHg48DKtE7XVPxZwqwzU3uFrrxUF1TEG9SCJXrrPr30=</DQ><InverseQ>Ekz1pL3PVVuICLS3WyZJCVV8qxqDmiV20kcTA5Ljh2wM47vl5N6Iw/Su4PmOanSeornUo+DyMwk/eiAY3m+TUnpEj11BS/lwrT6TWo+Xfnwm3MhMWdqvFBL5QFHWbwcHyFiL2yZEL+H3FbwMSkeWrXf+ngFbppy8ImW9jfP/7hg=</InverseQ><D>IuDQVCS65WIJ/WMrsopeTTlwyuf0mftf8alGWKvFjumPfYgBr6+6wa1p8m375U7h7rNLhZ0iIY7k1SHUowvcrRwE/kQQAwx4usO7QKAlDLkwim5RDMPVDpsPFvvIHxAYoP/8MwO8HQ+JJ8G43P8eqBwHfKE+VgYXRH7QapE4LFuKI9jA1TVbtCsf937qJMOLfKwentuCeyczv2+PPIZSenHBS9MhLD3QOfHmH2rRGAqnugq51/8Vfrie/JzANx2Bzgqe1lyA8M9UBmrwDj0m6+zTpxzkC275fIkWhcoNGCc/duZCnw6ZezxrmCfdrJUzcLOita2TgwGiOLDBLHRJrQ==</D></RSAKeyValue>";

    public static string GenerateLicenseKey(
        string hwid,
        string schoolName,
        string cityDistrict,
        DateTime startDate,
        DateTime endDate,
        string? note = null)
    {
        var payloadObj = new
        {
            H = hwid.Trim().ToUpperInvariant(),
            S = schoolName.Trim(),
            C = cityDistrict.Trim(),
            B = startDate.ToString("yyyy-MM-dd"),
            E = endDate.ToString("yyyy-MM-dd"),
            N = note?.Trim()
        };

        var json = JsonSerializer.Serialize(payloadObj);
        var payloadBytes = Encoding.UTF8.GetBytes(json);

        using var rsa = RSA.Create();
        rsa.FromXmlString(RsaPrivateKeyXml);

        var signatureBytes = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        string pBase64 = Convert.ToBase64String(payloadBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string sBase64 = Convert.ToBase64String(signatureBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"LIC-{pBase64}.{sBase64}";
    }
}
