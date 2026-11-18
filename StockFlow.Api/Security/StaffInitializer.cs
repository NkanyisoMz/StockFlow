using Microsoft.AspNetCore.Identity;

namespace StockFlow.Api.Security;

public static class StaffInitializer
{
    public static async Task InitializeAsync(
        UserManager<IdentityUser> userManager,
        string email,
        string password)
    {
        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            throw new InvalidOperationException(
                "An account with this email already exists. No changes were made.");
        }

        var user = new IdentityUser
        {
            UserName = email,
            Email = email
        };

        var createResult = await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            var errors = string.Join(
                "; ",
                createResult.Errors.Select(error => error.Description));

            throw new InvalidOperationException(
                $"Could not create Staff account: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(
            user,
            AppRoles.Staff);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "The account was created, but Staff assignment failed. "
                + "Review the account before retrying.");
        }
    }
}
