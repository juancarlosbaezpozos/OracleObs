using Dapper;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.OpenTelemetry;
using System.Diagnostics;

var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService("OracleDbSampleService");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddOracleDataProviderInstrumentation(o =>
    {
        o.EnableConnectionLevelAttributes = true;
        o.RecordException = true;
        o.InstrumentOracleDataReaderRead = true;
        o.SetDbStatementForText = true;
    })
    .AddConsoleExporter()
    .SetResourceBuilder(resourceBuilder)
    .AddSource("OracleDbSample")
    .AddJaegerExporter(options =>
    {
        options.AgentHost = "localhost";
        options.AgentPort = 6831;
    })
    .Build();

var tracer = tracerProvider?.GetTracer("OracleDbSample");
var activitySource = new ActivitySource("OracleDbSample");

using var activity = activitySource.StartActivity("MainOperation");
const string connectionString = "User Id=system;Password=YourStrongPassword;Data Source=localhost:1521/XEPDB1";

using var connection = new OracleConnection(connectionString);

try
{
    connection.Open();
    Console.WriteLine("conectado a bd oracle.");

    using (var activity1 = activitySource.StartActivity("CheckSchema"))
    {
        const string checkSchemaQuery = "SELECT COUNT(*) FROM all_users WHERE username = 'MYAPPUSER'";
        var schemaExists = connection.ExecuteScalar<int>(checkSchemaQuery);

        if (schemaExists == 0)
        {
            using var activity2 = activitySource.StartActivity("CreateSchema");
            const string createSchemaQuery = @"
                                        CREATE USER MYAPPUSER IDENTIFIED BY MyAppPassword;
                                        GRANT CONNECT, RESOURCE, DBA TO MYAPPUSER;
                                        GRANT UNLIMITED TABLESPACE TO MYAPPUSER;";
            connection.Execute(createSchemaQuery);
            Console.WriteLine("esquema MYAPPUSER creado.");
        }
        else
        {
            Console.WriteLine("esquema MYAPPUSER ya existe.");
        }
    }

    using (var activity3 = activitySource.StartActivity("CheckAndCreateTable"))
    {
        connection.ChangeDatabase("MYAPPUSER");

        const string checkTableQuery = "SELECT COUNT(*) FROM user_tables WHERE table_name = 'MY_SAMPLE_TABLE'";
        var tableExists = connection.ExecuteScalar<int>(checkTableQuery);

        if (tableExists == 0)
        {
            const string createTableQuery = @"
                                    CREATE TABLE MY_SAMPLE_TABLE (
                                        ID NUMBER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                        NAME VARCHAR2(50) NOT NULL
                                    )";
            connection.Execute(createTableQuery);
            Console.WriteLine("tabla MY_SAMPLE_TABLE creada.");
        }
        else
        {
            Console.WriteLine("la tabla MY_SAMPLE_TABLE ya existe.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    connection.Close();
    Console.WriteLine("conexión cerrada.");
}