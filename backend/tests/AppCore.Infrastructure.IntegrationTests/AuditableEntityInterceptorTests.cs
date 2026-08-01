using AppCore.Application.Common.Abstractions;
using AppCore.Domain.Common.Auditing;
using AppCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AppCore.Infrastructure.IntegrationTests;

[Collection(PostgreSqlTestCollectionDefinition.Name)]
public sealed class AuditableEntityInterceptorTests(
    PostgreSqlContainerFixture database)
{
    [Fact]
    public async Task SaveChangesSetsUtcAuditValuesAndActor()
    {
        DateTimeOffset expectedTime =
            new(2026, 7, 26, 10, 30, 0, TimeSpan.Zero);
        var interceptor = new AuditableEntityInterceptor(
            new FixedTimeProvider(expectedTime),
            new StubActorContext("actor-42"));
        string auditConnectionString =
            await CreateAuditDatabaseAsync();
        var options = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseNpgsql(auditConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new AuditTestDbContext(options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        await context.Database.EnsureCreatedAsync(timeout.Token);
        var entity = new AuditTestEntity();

        context.Entities.Add(entity);
        await context.SaveChangesAsync(timeout.Token);

        Assert.Equal(expectedTime.UtcDateTime, entity.CreatedAtUtc);
        Assert.Equal("actor-42", entity.CreatedByActorId);
        Assert.Equal(expectedTime.UtcDateTime, entity.LastModifiedAtUtc);
        Assert.Equal("actor-42", entity.LastModifiedByActorId);
        Assert.Equal(DateTimeKind.Utc, entity.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task SaveChangesPreservesCreationAuditValuesOnUpdate()
    {
        DateTimeOffset createdTime =
            new(2026, 7, 26, 10, 30, 0, TimeSpan.Zero);
        DateTimeOffset modifiedTime = createdTime.AddHours(1);
        string auditConnectionString = await CreateAuditDatabaseAsync();
        await using (var createContext = CreateContext(
                         auditConnectionString,
                         createdTime,
                         "creator"))
        {
            await createContext.Database.EnsureCreatedAsync();
            createContext.Entities.Add(new AuditTestEntity
            {
                Id = KnownEntityId,
            });
            await createContext.SaveChangesAsync();
        }

        await using (var updateContext = CreateContext(
                         auditConnectionString,
                         modifiedTime,
                         "modifier"))
        {
            AuditTestEntity entity =
                await updateContext.Entities.SingleAsync(
                    item => item.Id == KnownEntityId);
            entity.CreatedAtUtc = modifiedTime.UtcDateTime;
            entity.CreatedByActorId = "tampered";
            updateContext.Entry(entity).State = EntityState.Modified;
            await updateContext.SaveChangesAsync();

            Assert.Equal(createdTime.UtcDateTime, entity.CreatedAtUtc);
            Assert.Equal("creator", entity.CreatedByActorId);
        }

        await using var verifyContext = CreateContext(
            auditConnectionString,
            modifiedTime,
            "verifier");
        AuditTestEntity persisted =
            await verifyContext.Entities.AsNoTracking().SingleAsync(
                item => item.Id == KnownEntityId);

        Assert.Equal(createdTime.UtcDateTime, persisted.CreatedAtUtc);
        Assert.Equal("creator", persisted.CreatedByActorId);
        Assert.Equal(modifiedTime.UtcDateTime, persisted.LastModifiedAtUtc);
        Assert.Equal("modifier", persisted.LastModifiedByActorId);
    }

    private static readonly Guid KnownEntityId =
        Guid.Parse("ccae776b-6b4b-4531-908e-ed84992bd8d8");

    private static AuditTestDbContext CreateContext(
        string connectionString,
        DateTimeOffset utcNow,
        string actorId)
    {
        var interceptor = new AuditableEntityInterceptor(
            new FixedTimeProvider(utcNow),
            new StubActorContext(actorId));
        var options = new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(interceptor)
            .Options;
        return new AuditTestDbContext(options);
    }

    private async Task<string> CreateAuditDatabaseAsync()
    {
        var adminConnectionString =
            new NpgsqlConnectionStringBuilder(database.ConnectionString)
            {
                Database = "postgres",
            };
        await using var connection =
            new NpgsqlConnection(adminConnectionString.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = connection.CreateCommand();
        string databaseName =
            $"audit_foundation_tests_{Guid.NewGuid():N}";
        command.CommandText = $"CREATE DATABASE {databaseName}";
        await command.ExecuteNonQueryAsync();

        var auditConnectionString =
            new NpgsqlConnectionStringBuilder(database.ConnectionString)
            {
                Database = databaseName,
            };
        return auditConnectionString.ConnectionString;
    }

    private sealed class AuditTestDbContext(
        DbContextOptions<AuditTestDbContext> options)
        : DbContext(options)
    {
        public DbSet<AuditTestEntity> Entities => Set<AuditTestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuditTestEntity>(entity =>
            {
                entity.ToTable("audit_test_entities");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.CreatedByActorId)
                    .HasMaxLength(450);
                entity.Property(item => item.LastModifiedByActorId)
                    .HasMaxLength(450);
            });
        }
    }

    private sealed class AuditTestEntity
        : IHasCreationAudit, IHasModificationAudit
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTime CreatedAtUtc { get; set; }

        public string? CreatedByActorId { get; set; }

        public DateTime? LastModifiedAtUtc { get; set; }

        public string? LastModifiedByActorId { get; set; }
    }

    private sealed class StubActorContext(string actorId) : IActorContext
    {
        public string ActorId { get; } = actorId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
