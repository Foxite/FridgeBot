using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FridgeBot {
	public static class ServiceProviderExtensions {
		public static IServiceCollection ConfigureDbContext<TDbContext>(this IServiceCollection isc) where TDbContext : DbContext {
			isc.AddDbContext<TDbContext>((isp, dbcob) => {
				var connectionStrings = isp.GetRequiredService<ConnectionStringsConfiguration>();
				switch (connectionStrings.Mode) {
					case ConnectionStringsConfiguration.Backend.Sqlite:
						dbcob.UseSqlite(connectionStrings.GetConnectionString<TDbContext>());
						break;
					case ConnectionStringsConfiguration.Backend.Postgres:
						dbcob.UseNpgsql(connectionStrings.GetConnectionString<TDbContext>());
						break;
					default:
						throw new ArgumentOutOfRangeException(nameof(connectionStrings.Mode));
				}
			});
			return isc;
		}
	}
}
