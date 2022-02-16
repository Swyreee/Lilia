﻿using Lilia.Commons;
using Lilia.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Serilog;
using Serilog.Extensions.Logging;

namespace Lilia.Database;

public class HelyaDatabaseContextFactory : IDesignTimeDbContextFactory<HelyaDatabaseContext>
{
    public HelyaDatabaseContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HelyaDatabaseContext>();
        var connStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = "database.db",
            Password = JsonManager<BotConfiguration>.Read().Credentials.DbPassword
        };

        optionsBuilder
#if DEBUG
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .UseLoggerFactory(new SerilogLoggerFactory(Log.Logger))
#endif
            .UseSqlite(connStringBuilder.ToString());

        var ctx = new HelyaDatabaseContext(optionsBuilder.Options);
        ctx.Database.SetCommandTimeout(30);
        return ctx;
    }
}