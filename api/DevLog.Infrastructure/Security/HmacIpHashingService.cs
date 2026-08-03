using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace DevLog.Infrastructure.Security;

public class HmacIpHashingService : IIpHashingService
{
    private readonly byte[] _secret;

    public HmacIpHashingService(IOptions<IpHashingOptions> options)
    {
        var secret = options.Value.Secret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "IpHashing:Secret must be configured to hash IP addresses with HMAC.");
        }

        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        using var hmac = new HMACSHA256(_secret);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(hashBytes);
    }
}
