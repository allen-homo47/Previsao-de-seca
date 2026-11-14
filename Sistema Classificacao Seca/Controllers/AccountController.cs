using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Sistema_Classificacao_Seca.Controllers;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o seu e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a sua palavra-passe.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Manter sessão iniciada")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Informe o seu e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Crie uma palavra-passe.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "A palavra-passe deve ter pelo menos {2} caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Palavra-passe")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a palavra-passe.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "As palavras-passe não coincidem.")]
    [Display(Name = "Confirmar palavra-passe")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public sealed class AccountController(
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signInManager) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "E-mail ou palavra-passe incorretos.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return RedirectToLocal(model.ReturnUrl);

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "A conta foi temporariamente bloqueada. Tente novamente mais tarde."
            : "E-mail ou palavra-passe incorretos.");
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new IdentityUser
        {
            UserName = model.Email.Trim(),
            Email = model.Email.Trim()
        };
        var result = await users.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToLocal(model.ReturnUrl);
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Code));

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");

    private static string TranslateIdentityError(string code) => code switch
    {
        "DuplicateEmail" or "DuplicateUserName" => "Já existe uma conta registada com este e-mail.",
        "InvalidEmail" => "O e-mail indicado não é válido.",
        "PasswordTooShort" => "A palavra-passe deve ter pelo menos 8 caracteres.",
        "PasswordRequiresDigit" => "A palavra-passe deve conter pelo menos um número.",
        "PasswordRequiresLower" => "A palavra-passe deve conter pelo menos uma letra minúscula.",
        "PasswordRequiresUpper" => "A palavra-passe deve conter pelo menos uma letra maiúscula.",
        "PasswordRequiresNonAlphanumeric" => "A palavra-passe deve conter pelo menos um símbolo.",
        _ => "Não foi possível criar a conta. Verifique os dados e tente novamente."
    };
}
