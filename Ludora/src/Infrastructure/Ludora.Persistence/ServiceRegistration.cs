using Ludora.Application.Interfaces;
using Ludora.Persistence; // Sənin DbContext-inin olduğu qovluq yolu
using Ludora.Persistence.Data;
using Ludora.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ludora.Persistence
{
    public static class ServiceRegistration
    {
        public static void AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("default")));
            services.AddScoped<IGameService, GameService>();

        }

    }
}