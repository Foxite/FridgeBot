using System;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FridgeBot; 

public static class Util {
	// TODO this can probably be removed
	public static IServiceCollection ConfigureDbContext<TDbContext>(this IServiceCollection isc) where TDbContext : DbContext {
		isc.AddDbContext<TDbContext>((isp, dbcob) => {
			ConnectionStringsConfiguration connectionStrings = isp.GetRequiredService<IOptions<ConnectionStringsConfiguration>>().Value;
			_ = connectionStrings.Mode switch {
				ConnectionStringsConfiguration.Backend.Sqlite => dbcob.UseSqlite(connectionStrings.GetConnectionString<TDbContext>()),
				ConnectionStringsConfiguration.Backend.Postgres => dbcob.UseNpgsql(connectionStrings.GetConnectionString<TDbContext>()),
				//_ => throw new ArgumentOutOfRangeException(nameof(connectionStrings.Mode))
			};
		}, ServiceLifetime.Transient);
		return isc;
	}
}
