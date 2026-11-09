
using Microsoft.EntityFrameworkCore;
using Sistema_Classificacao_Seca.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));


// Registar os serviços da API e da interface MVC
builder.Services.AddControllersWithViews();
builder.Services.AddOpenApi();

var app = builder.Build();

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
