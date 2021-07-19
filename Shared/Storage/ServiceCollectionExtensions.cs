using Microsoft.Extensions.DependencyInjection;

namespace Shared.Storage
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStorage(this IServiceCollection services)
        {
            services.AddSingleton<IItemRepository, ItemRepository>();

            return services;
        }
    }
}
