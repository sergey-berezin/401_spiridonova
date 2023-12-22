var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSwaggerDocument();

var app = builder.Build();

app.UseRouting();

app.UseOpenApi();
app.UseSwaggerUi3();

app.UseCors(builder =>
{
    builder
        .WithOrigins("*")
        .WithHeaders("*")
        .WithMethods("*");
});

app.MapControllers();

app.Run();

public partial class Program { }
