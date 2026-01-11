using Accounting.Application.Common.Abstractions;
using Accounting.Application.Services;
using Accounting.Infrastructure.Persistence;
using Accounting.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Accounting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        var conn = config.GetConnectionString("Default")!;
        services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(conn));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddScoped<IInvoiceBalanceService, InvoiceBalanceService>();
        services.AddScoped<IExcelService, Accounting.Application.Common.Services.ExcelService>();
        services.AddScoped<IAccountBalanceService, AccountBalanceService>();
        services.AddScoped<IContactBalanceService, ContactBalanceService>();
        services.AddScoped<IStockService, StockService>();
        
        services.AddHttpContextAccessor();

        // Auth
        services.Configure<Accounting.Infrastructure.Authentication.JwtSettings>(config.GetSection(Accounting.Infrastructure.Authentication.JwtSettings.SectionName));
        services.AddSingleton<Accounting.Application.Common.Interfaces.IJwtTokenGenerator, Accounting.Infrastructure.Authentication.JwtTokenGenerator>();
        services.AddSingleton<Accounting.Application.Common.Interfaces.IPasswordHasher, Accounting.Infrastructure.Authentication.PasswordHasher>();
        services.AddSingleton<Accounting.Application.Common.Interfaces.ICurrentUserService, Accounting.Infrastructure.Services.CurrentUserService>();

        // Dynamic Policy Registration
        services.AddAuthorization(options =>
        {
            foreach (var permission in Accounting.Domain.Constants.Permissions.GetAll())
            {
                options.AddPolicy(permission, policy => 
                    policy.RequireClaim("permission", permission));
            }
        });

        return services;
    }
}
