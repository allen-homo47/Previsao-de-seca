using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;
using Sistema_Classificacao_Seca.Services;

var builder = WebApplication.CreateBuilder(
    args.FirstOrDefault() is "--validate-csv" or "--import-csv" or "--evaluate-csv" or "--make-admin"
        ? []
        : args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<CsvImportService>();
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();

await using var app = builder.Build();

if (args.FirstOrDefault() == "--make-admin")
{
    if (args.Length != 2 || string.IsNullOrWhiteSpace(args[1]))
        throw new ArgumentException("Indica o nome da conta: --make-admin <utilizador>");

    const string roleName = "Administrador";
    var userName = args[1];

    await using var scope = app.Services.CreateAsyncScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    var user = await users.FindByNameAsync(userName)
        ?? throw new InvalidOperationException("Conta não encontrada. Regista-a primeiro.");

    if (!await roles.RoleExistsAsync(roleName))
    {
        var created = await roles.CreateAsync(new IdentityRole(roleName));
        if (!created.Succeeded)
            throw new InvalidOperationException(
                string.Join(" ", created.Errors.Select(error => error.Description)));
    }

    if (!await users.IsInRoleAsync(user, roleName))
    {
        var assigned = await users.AddToRoleAsync(user, roleName);
        if (!assigned.Succeeded)
            throw new InvalidOperationException(
                string.Join(" ", assigned.Errors.Select(error => error.Description)));
    }

    Console.WriteLine($"Perfil Administrador confirmado para {userName}.");
    return;
}

if (args is ["--validate-csv" or "--import-csv" or "--evaluate-csv", var path])
{
    using var reader = File.OpenText(path);
    var rows = await CsvImportService.ReadAsync(reader, CancellationToken.None);

    if (rows.Count != 782)
        throw new InvalidDataException(
            $"Esperadas 782 observações. Encontradas: {rows.Count}.");

    Console.WriteLine($"Observações validadas: {rows.Count}");

    if (args[0] == "--import-csv")
    {
        await using var scope = app.Services.CreateAsyncScope();
        var importer = scope.ServiceProvider.GetRequiredService<CsvImportService>();
        var inserted = await importer.ImportAsync(rows, CancellationToken.None);

        Console.WriteLine(
            $"Inseridas: {inserted}. Já existentes: {rows.Count - inserted}.");
    }
    else if (args[0] == "--evaluate-csv")
    {
        using var classifier = DroughtClassifier.Train(rows) ??
            throw new InvalidDataException("Dados insuficientes para avaliação.");

        Console.WriteLine(
            System.Text.Json.JsonSerializer.Serialize(classifier.Evaluation));
    }

    return;
}

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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();