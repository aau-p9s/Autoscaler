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
        .PostgresqlDatabase($"Server={addr ?? "localhost"};Port={port ?? "5432"};Database={database ?? "autoscaler"};Uid={user ?? "root"};Pwd={password ?? "password"}")
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build();

sqlUpgrader.PerformUpgrade();
