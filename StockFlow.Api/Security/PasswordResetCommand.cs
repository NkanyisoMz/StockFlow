using System.Text;
using Microsoft.AspNetCore.Identity;

namespace StockFlow.Api.Security;

public static class PasswordResetCommand
{
    public static async Task<int> RunAsync(UserManager<IdentityUser> manager, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            Console.Error.WriteLine("Password reset is available only in Development.");
            return 1;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Password reset requires an interactive terminal.");
            return 1;
        }

        Console.Write("Existing account email: ");
        var email = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("An email address is required.");
            return 1;
        }

        Console.Write("New password (Escape to cancel): ");
        var password = ReadPassword();
        if (password is null)
        {
            Console.WriteLine("Password reset cancelled.");
            return 1;
        }

        var result = await ResetAsync(manager, environment, email, password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                Console.Error.WriteLine(error.Description);
            return 1;
        }

        Console.WriteLine("Password reset successfully.");
        return 0;
    }

    public static async Task<IdentityResult> ResetAsync(
        UserManager<IdentityUser> manager, IHostEnvironment environment, string email, string password)
    {
        if (!environment.IsDevelopment())
            return IdentityResult.Failed(new IdentityError
            {
                Code = "DevelopmentOnly",
                Description = "Password reset is available only in Development."
            });

        var user = await manager.FindByEmailAsync(email.Trim());
        if (user is null)
            return IdentityResult.Failed(new IdentityError
            {
                Code = "AccountNotFound",
                Description = "No existing account matches that email."
            });

        var token = await manager.GeneratePasswordResetTokenAsync(user);
        return await manager.ResetPasswordAsync(user, token, password);
    }

    private static string? ReadPassword()
    {
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Escape)
            {
                Console.WriteLine();
                return key.Key == ConsoleKey.Escape ? null : password.ToString();
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write('*');
            }
        }
    }
}
