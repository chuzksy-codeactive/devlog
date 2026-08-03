namespace DevLog.Infrastructure.Security;

public interface IIpHashingService
{
    string? HashIp(string? ip);
}
