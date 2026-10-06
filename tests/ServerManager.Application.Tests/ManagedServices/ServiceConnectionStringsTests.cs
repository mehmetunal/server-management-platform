using ServerManager.Application.ManagedServices;

namespace ServerManager.Application.Tests.ManagedServices;

public class ServiceConnectionStringsTests
{
    private static readonly ServiceEndpoint Internal = new("sm-svc-db", 5432);
    private static readonly ServiceCredentials Credentials = new() { Username = "app", Password = "p@ss:w/rd#1", Database = "appdb" };

    [Fact]
    public void Postgres_url_escapes_user_info()
    {
        Assert.Equal("postgres://app:p%40ss%3Aw%2Frd%231@sm-svc-db:5432/appdb", ServiceConnectionStrings.Postgres(Internal, Credentials));
        Assert.Equal("Host=sm-svc-db;Port=5432;Database=appdb;Username=app;Password=p@ss:w/rd#1", ServiceConnectionStrings.Npgsql(Internal, Credentials));
    }

    [Fact]
    public void Mysql_redis_mongo_and_amqp_formats()
    {
        var plain = Credentials with { Password = "secret123456" };

        Assert.Equal("mysql://app:secret123456@sm-svc-db:3306/appdb", ServiceConnectionStrings.MySql(Internal with { Port = 3306 }, plain));
        Assert.Equal("redis://:secret123456@sm-svc-cache:6379", ServiceConnectionStrings.Redis(new ServiceEndpoint("sm-svc-cache", 6379), plain));
        Assert.Equal("mongodb://app:secret123456@203.0.113.5:27017/appdb?authSource=admin", ServiceConnectionStrings.Mongo(new ServiceEndpoint("203.0.113.5", 27017), plain));
        Assert.Equal("mongodb://app:secret123456@h:27017/?authSource=admin", ServiceConnectionStrings.Mongo(new ServiceEndpoint("h", 27017), plain with { Database = null }));
        Assert.Equal("amqp://app:secret123456@sm-svc-mq:5672/", ServiceConnectionStrings.Amqp(new ServiceEndpoint("sm-svc-mq", 5672), plain));
        Assert.Equal("sm-svc-cache:6379,password=secret123456", ServiceConnectionStrings.RedisAdo(new ServiceEndpoint("sm-svc-cache", 6379), plain));
    }

    [Fact]
    public void Sql_server_uses_comma_port_and_trusts_certificate()
    {
        var credentials = new ServiceCredentials { Username = "sa", Password = "Passw0rd" };
        Assert.Equal(
            "Server=203.0.113.5,1433;User Id=sa;Password=Passw0rd;TrustServerCertificate=True",
            ServiceConnectionStrings.SqlServer(new ServiceEndpoint("203.0.113.5", 1433), credentials));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a;b", "{a;b}")]
    [InlineData("x}y;", "{x}}y;}")]
    [InlineData(" lead", "{ lead}")]
    public void Ado_values_with_special_characters_are_braced(string value, string expected) =>
        Assert.Equal(expected, ServiceConnectionStrings.AdoValue(value));

    [Fact]
    public void Ipv6_hosts_are_bracketed()
    {
        Assert.Equal("[2001:db8::1]:5432", ServiceConnectionStrings.HostPort(new ServiceEndpoint("2001:db8::1", 5432)));
        Assert.Equal("http://[2001:db8::1]:9001", ServiceConnectionStrings.Http(new ServiceEndpoint("2001:db8::1", 9001)));
    }

    [Fact]
    public void Suggested_environment_uses_internal_address()
    {
        var postgres = TestTemplates.Find(ServiceTemplates.Postgres)!;
        var env = postgres.SuggestedEnvironment(Internal, Credentials with { Password = "secret123456" });

        Assert.Equal("postgres://app:secret123456@sm-svc-db:5432/appdb", env["DATABASE_URL"]);
        Assert.Equal("sm-svc-db", env["PGHOST"]);
        Assert.Contains("Host=sm-svc-db", env["ConnectionStrings__Default"]);

        var redis = TestTemplates.Find(ServiceTemplates.Redis)!.SuggestedEnvironment(new ServiceEndpoint("sm-svc-cache", 6379), Credentials with { Password = "secret123456" });
        Assert.Equal("redis://:secret123456@sm-svc-cache:6379", redis["REDIS_URL"]);

        var mongo = TestTemplates.Find(ServiceTemplates.MongoDb)!.SuggestedEnvironment(new ServiceEndpoint("sm-svc-mongo", 27017), Credentials with { Password = "secret123456" });
        Assert.StartsWith("mongodb://", mongo["MONGODB_URI"]);

        var mssql = TestTemplates.Find(ServiceTemplates.SqlServer)!.SuggestedEnvironment(new ServiceEndpoint("sm-svc-sql", 1433), new ServiceCredentials { Username = "sa", Password = "Passw0rd" });
        Assert.StartsWith("Server=sm-svc-sql,1433;", mssql["ConnectionStrings__Default"]);
    }
}
