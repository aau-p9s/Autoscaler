using System;
using System.Reflection;
using DbUp;

var addr = Environment.GetEnvironmentVariable("AUTOSCALER__PGSQL__ADDR");
var port = Environment.GetEnvironmentVariable("AUTOSCALER__PGSQL__PORT");
var database = Environment.GetEnvironmentVariable("AUTOSCALER__PGSQL__DATABASE");
var user = Environment.GetEnvironmentVariable("AUTOSCALER__PGSQL__USER");
var password = Environment.GetEnvironmentVariable("AUTOSCALER__PGSQL__PASSWORD"); // TODO: fix

var sqlUpgrader =
    DeployChanges.To
        .PostgresqlDatabase(
            $"Server={addr ?? "localhost"};Port={port ?? "5432"};Database={database ?? "autoscaler"};Uid={user ?? "root"};Pwd={password ?? "password"}")
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build();

sqlUpgrader.PerformUpgrade();