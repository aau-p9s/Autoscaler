using System;
using System.Reflection;
using DbUp;

var addr = Environment.GetEnvironmentVariable("AUTOSCALER_PGSQL_ADDR");
var port = Environment.GetEnvironmentVariable("AUTOSCALER_PGSQL_PORT");
var database = Environment.GetEnvironmentVariable("AUTOSCALER_PGSQL_DATABASE");
var user = Environment.GetEnvironmentVariable("AUTOSCALER_PGSQL_USER");
var password = Environment.GetEnvironmentVariable("AUTOSCALER_PGSQL_PASSWORD"); // TODO: fix

var sqlUpgrader =
    DeployChanges.To
        .PostgresqlDatabase($"Server={addr};Port={port};Database={database};Uid={user};Pwd={password}")
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build();

sqlUpgrader.PerformUpgrade();
