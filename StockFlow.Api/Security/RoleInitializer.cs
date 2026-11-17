using Microsoft.AspNetCore.Identity;

namespace StockFlow.Api.Security;

public static class RoleInitializer
{
    public static async Task InitializeAsync(
        RoleManager<IdentityRole> roleManager)
    {
        string[] roles = [AppRoles.Admin, AppRoles.Staff];

        foreach (var role in roles)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(
                new IdentityRole(role));

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Could not create role '{role}': {errors}");
            }
        }
    }
}
