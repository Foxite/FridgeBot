using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace FridgeBot {
	public class ConnectionStringsConfiguration {
		public Backend Mode { get; set; }
		public string FridgeDbContext { get; set; }

		private readonly Dictionary<Type, Func<string>> m_GetValues = new();

		public ConnectionStringsConfiguration() {
			m_GetValues.Add(typeof(FridgeDbContext), () => FridgeDbContext);
		}

		public string GetConnectionString<TDbContext>() where TDbContext : DbContext => m_GetValues[typeof(TDbContext)]();

		public enum Backend {
			Sqlite,
			Postgres
		}
	}
}
