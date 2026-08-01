namespace AppCore.Infrastructure.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class PostgreSqlTestCollectionDefinition
    : ICollectionFixture<PostgreSqlContainerFixture>
{
    public const string Name = "PostgreSQL";
}
