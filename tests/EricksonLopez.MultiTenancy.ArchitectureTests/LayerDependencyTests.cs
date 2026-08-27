// Copyright © Erickson Lopez. MIT License.
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using EricksonLopez.MultiTenancy;
using EricksonLopez.MultiTenancy.AspNetCore;
using EricksonLopez.MultiTenancy.Dapper;
using EricksonLopez.MultiTenancy.MariaDb;
using EricksonLopez.MultiTenancy.MySql;
using EricksonLopez.MultiTenancy.OpenTelemetry;
using EricksonLopez.MultiTenancy.Oracle;
using EricksonLopez.MultiTenancy.PostgreSql;
using EricksonLopez.MultiTenancy.Sqlite;
using EricksonLopez.MultiTenancy.SqlServer;
using EricksonLopez.MultiTenancy.Testing;
using Xunit;

namespace EricksonLopez.MultiTenancy.ArchitectureTests;

public class LayerDependencyTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(TenantId).Assembly, // Abstractions
            typeof(TenantContext).Assembly, // Core
            typeof(TenantResolutionMiddleware).Assembly, // AspNetCore
            typeof(TenantDapperExtensions).Assembly, // Dapper
            typeof(PostgreSqlRlsExtensions).Assembly, // PostgreSql
            typeof(SqlServerSessionContextExtensions).Assembly, // SqlServer
            typeof(MySqlTenantExtensions).Assembly, // MySql
            typeof(MariaDbTenantExtensions).Assembly, // MariaDb
            typeof(OracleVpdExtensions).Assembly, // Oracle
            typeof(SqliteTenantExtensions).Assembly, // Sqlite
            typeof(TestTenantContext).Assembly, // Testing
            typeof(TenantActivitySource).Assembly // OpenTelemetry
        )
        .Build();

    private readonly IObjectProvider<IType> _abstractionsLayer =
        Types().That().ResideInAssembly("EricksonLopez.MultiTenancy.Abstractions").As("Abstractions Layer");

    private readonly IObjectProvider<IType> _coreLayer =
        Types().That().ResideInAssembly("EricksonLopez.MultiTenancy").As("Core Layer");

    private readonly IObjectProvider<IType> _providerLayers =
        Types().That().ResideInAssembly("EricksonLopez.MultiTenancy.*Sql*")
        .Or().ResideInAssembly("EricksonLopez.MultiTenancy.MariaDb")
        .Or().ResideInAssembly("EricksonLopez.MultiTenancy.Oracle").As("Database Provider Layers");

    [Fact]
    public void MTN001_DatabaseProviders_MustNotDependOn_CoreLayer()
    {
        var rule = Classes().That().Are(_providerLayers)
            .Should().NotDependOnAny(_coreLayer).WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void MTN002_AbstractionsLayer_MustNotDependOn_CoreOrProviders()
    {
        var rule = Classes().That().Are(_abstractionsLayer)
            .Should().NotDependOnAny(_coreLayer)
            .AndShould().NotDependOnAny(_providerLayers)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void MTN003_AbstractionsLayer_MustNotDependOn_DatabaseDrivers()
    {
        var dbDriverTypes = Types().That().ResideInNamespace("Npgsql.*")
            .Or().ResideInNamespace("Microsoft.Data.SqlClient.*")
            .Or().ResideInNamespace("MySqlConnector.*")
            .Or().ResideInNamespace("Oracle.ManagedDataAccess.*")
            .Or().ResideInNamespace("Microsoft.Data.Sqlite.*")
            .Or().ResideInNamespace("Dapper.*");

        var rule = Classes().That().Are(_abstractionsLayer)
            .Should().NotDependOnAny(dbDriverTypes)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }

    [Fact]
    public void MTN004_DatabaseProviders_MustBeIsolatedFromSiblings()
    {
        var postgresTypes = Types().That().ResideInAssembly("EricksonLopez.MultiTenancy.PostgreSql");
        var sqlServerTypes = Types().That().ResideInAssembly("EricksonLopez.MultiTenancy.SqlServer");
        var oracleTypes = Types().That().ResideInAssembly("EricksonLopez.MultiTenancy.Oracle");
        var sqliteTypes = Types().That().ResideInAssembly("EricksonLopez.MultiTenancy.Sqlite");

        var rulePostgres = Classes().That().Are(postgresTypes)
            .Should().NotDependOnAny(sqlServerTypes)
            .AndShould().NotDependOnAny(oracleTypes)
            .AndShould().NotDependOnAny(sqliteTypes)
            .WithoutRequiringPositiveResults();

        rulePostgres.Check(Architecture);
    }

    [Fact]
    public void DIR001_OpenTelemetry_MustNotDependOn_DatabaseProviders()
    {
        var otelTypes = Types().That().ResideInAssembly("EricksonLopez.MultiTenancy.OpenTelemetry");

        var rule = Classes().That().Are(otelTypes)
            .Should().NotDependOnAny(_providerLayers)
            .WithoutRequiringPositiveResults();

        rule.Check(Architecture);
    }
}
