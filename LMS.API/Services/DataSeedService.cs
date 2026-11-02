using LMS.Infrastructure.Data;
using LMS.Shared.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LMS.API.Services;

//ToDo: Add in UserSecrets (you can change password and secretkey):
//{
//  "password": "abc",
//  "JwtSettings": {
//    "secretkey" :  "ThisMustNeReallyLong!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!"
//  }
//}
// The seeded users (DemoUser@Lms.com and teacher@lms.com) use the same password.
//
// Outside Development (for example on Azure) nothing runs unless these settings are on.
// On App Service they are app settings with "__" instead of ":" (Database__MigrateOnStartup).
//   "Database:MigrateOnStartup": true  -> apply missing EF migrations when the API starts
//   "Database:SeedData": true          -> create the roles and the two demo users there too
// Both default to false, so local development behaves as before.


internal class DataSeedService : IHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IConfiguration configuration;
    private readonly ILogger<DataSeedService> logger;
    private UserManager<ApplicationUser> userManager = null!;
    private RoleManager<IdentityRole> roleManager = null!;
    private string _password = null!;
    private const string DemoRole = "Demo";
    private const string DefaultUserEmail = "DemoUser@Lms.com";
    private const string TeacherEmail = "teacher@lms.com";

    public DataSeedService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<DataSeedService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();

        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        // GetValue<bool> returns false when the key is missing.
        var migrateOnStartup = configuration.GetValue<bool>("Database:MigrateOnStartup");
        var seedData = env.IsDevelopment() || configuration.GetValue<bool>("Database:SeedData");

        if (!migrateOnStartup && !seedData) return;

        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                            ?? throw new ArgumentNullException();

        // Migrations first: the seed below needs the Identity tables.
        // MigrateAsync only applies migrations that are not yet in __EFMigrationsHistory.
        if (migrateOnStartup)
        {
            logger.LogInformation("Applying pending database migrations");
            await context.Database.MigrateAsync(cancellationToken);
        }

        if (!seedData) return;

        roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>()
                            ?? throw new ArgumentNullException();

        await CreateRolesAsync([DemoRole, UserRoles.Student, UserRoles.Teacher]);

        userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                            ?? throw new ArgumentNullException();

        // The demo user is only seeded into an empty database.
        // The teacher is seeded whenever it is missing, so existing databases get one too.
        var needsDemoUser = !await context.Users.AnyAsync(cancellationToken);
        var needsTeacher = await userManager.FindByEmailAsync(TeacherEmail) is null;

        if (!needsDemoUser && !needsTeacher) return;

        _password = configuration["password"]!;
        ArgumentNullException.ThrowIfNull(_password, nameof(_password));

        try
        {
            if (needsDemoUser) await CreateDefaultUserAsync();
            if (needsTeacher) await CreateTeacherAsync();
            logger.LogInformation("Seed complete");
        }
        catch (Exception ex)
        {
            logger.LogError($"Data seed fail with message: {ex.Message}. Exceeption: {ex.InnerException}");
            throw;
        }
    }


    private async Task CreateRolesAsync(string[] rolenames)
    {
        foreach (string rolename in rolenames)
        {
            if (await roleManager.RoleExistsAsync(rolename)) continue;
            var role = new IdentityRole { Name = rolename };
            var res = await roleManager.CreateAsync(role);

            if (!res.Succeeded) throw new Exception
                    (string.Join("\n", res.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }
    }
    private async Task CreateDefaultUserAsync()
    {
        var user = new ApplicationUser
        {
            Name = "Demo User",
            Email = DefaultUserEmail,
            UserName = DefaultUserEmail,
        };

        await CreateUserAsync(user, DemoRole);
    }

    private async Task CreateTeacherAsync()
    {
        var user = new ApplicationUser
        {
            Name = "Demo Teacher",
            Email = TeacherEmail,
            UserName = TeacherEmail,
        };

        await CreateUserAsync(user, UserRoles.Teacher);
    }

    private async Task CreateUserAsync(ApplicationUser user, string role)
    {
        var result = await userManager.CreateAsync(user, _password);

        if (!result.Succeeded)
            throw new Exception(string.Join("\n",
            result.Errors.Select(e => $"{e.Code}: {e.Description}")));

        var roleResult = await userManager.AddToRoleAsync(user, role);

        if (!roleResult.Succeeded)
            throw new Exception(string.Join("\n",
            roleResult.Errors.Select(e => $"{e.Code}: {e.Description}")));

    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

}