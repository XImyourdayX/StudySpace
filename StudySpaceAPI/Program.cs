
var builder = WebApplication.CreateBuilder(args);

// SERVICIOS DE LA API
builder.Services.AddOpenApi();

var app = builder.Build();

// DOCUMENTACIÓN EN DESARROLLO
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// UTILIZAR HTTPS
app.UseHttpsRedirection();

// RUTA DE PRUEBA
app.MapGet("/api/estado", () =>
{
    return Results.Ok(new
    {
        Aplicacion = "Study Space",
        Servicio = "Recuperación de contraseña",
        Estado = "Funcionando"
    });
});

app.Run();
