namespace BankApi.Integrations.Caching;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "redis:6379";
}
