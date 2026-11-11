
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;
using Sistema_Classificacao_Seca.Services;

var builder = WebApplication.CreateBuilder(
    args.FirstOrDefault() is "--validate-csv" or "--import-csv" or "--evaluate-csv" ? [] : args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));


// Registar os serviços da API e da interface MVC
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

await using var app = builder.Build();

if (args is ["--validate-csv" or "--import-csv" or "--evaluate-csv", var path])
{
    using var reader = File.OpenText(path);
    var rows = await CsvImportService.ReadAsync(reader, CancellationToken.None);
    if (rows.Count != 782)
        throw new InvalidDataException($"Esperadas 782 observações. Encontradas: {rows.Count}.");

    Console.WriteLine($"Observações validadas: {rows.Count}");
    if (args[0] == "--import-csv")
    {
        await using var scope = app.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<CsvImportService>();
        var inserted = await importer.ImportAsync(rows, CancellationToken.None);
        Console.WriteLine($"Inseridas: {inserted}. Já existentes: {rows.Count - inserted}.");
    }
    else if (args[0] == "--evaluate-csv")
    {
        using var classifier = DroughtClassifier.Train(rows) ??
            throw new InvalidDataException("Dados insuficientes para avaliação.");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(classifier.Evaluation));
    }

    return;
}

// Configuração da aplicação
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Rotas da API
app.MapControllers();

// Rota principal da interface Web
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
