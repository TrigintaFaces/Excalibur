#if (UseSqlServer)
using Excalibur.EventSourcing.SqlServer;
#elif (UsePostgreSql)
using Excalibur.EventSourcing.Postgres;
#endif
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Observability.Metrics;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

builder.Services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
#if (UseSqlServer)
        es.UseSqlServer(sql => sql.ConnectionStringName("EventStore"));
#elif (UsePostgreSql)
        es.UsePostgres(pg => pg.ConnectionStringName("EventStore"));
#elif (UseInMemoryDatabase)
        es.UseInMemory();
#endif
    });
});

// OpenTelemetry: one call registers all Dispatch meters + activity sources
builder.Services.AddOpenTelemetry()
    .AddDispatchInstrumentation();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
