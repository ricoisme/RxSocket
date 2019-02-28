using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RxSocket
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection SetupTcpService(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions();
            services.Configure<ServerConfig>(configuration);
            services.AddSingleton<IService, TcpService>();
            services.AddScoped<IClientSocket, ClientSocket>();
            return services;
        }
    }
}
