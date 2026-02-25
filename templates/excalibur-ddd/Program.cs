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
        es.UseSqlServer(builder.Configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException("ConnectionStrings:EventStore is required."));
#elif (UsePostgreSql)
        es.UsePostgreSql(builder.Configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException("ConnectionStrings:EventStore is required."));
#elif (UseInMemoryDatabase)
        es.UseInMemory();
#endif
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
