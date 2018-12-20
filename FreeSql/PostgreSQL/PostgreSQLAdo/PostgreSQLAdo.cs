﻿using FreeSql.Internal;
using Microsoft.Extensions.Logging;
using Npgsql;
using SafeObjectPool;
using System;
using System.Collections;
using System.Data.Common;
using System.Text;
using System.Threading;

namespace FreeSql.PostgreSQL {
	class PostgreSQLAdo : FreeSql.Internal.CommonProvider.AdoProvider {
		CommonUtils _util;

		public PostgreSQLAdo() : base(null, null) { }
		public PostgreSQLAdo(CommonUtils util, ICache cache, ILogger log, string masterConnectionString, string[] slaveConnectionStrings) : base(cache, log) {
			this._util = util;
			MasterPool = new PostgreSQLConnectionPool("主库", masterConnectionString, null, null);
			if (slaveConnectionStrings != null) {
				foreach (var slaveConnectionString in slaveConnectionStrings) {
					var slavePool = new PostgreSQLConnectionPool($"从库{SlavePools.Count + 1}", slaveConnectionString, () => Interlocked.Decrement(ref slaveUnavailables), () => Interlocked.Increment(ref slaveUnavailables));
					SlavePools.Add(slavePool);
				}
			}
		}
		static DateTime dt1970 = new DateTime(1970, 1, 1);
		public override object AddslashesProcessParam(object param) {
			if (param == null) return "NULL";
			if (param is bool || param is bool?)
				return (bool)param ? "'t'" : "'f'";
			else if (param is string || param is Enum)
				return string.Concat("'", param.ToString().Replace("'", "''"), "'");
			else if (decimal.TryParse(string.Concat(param), out var trydec))
				return param;
			else if (param is DateTime)
				return string.Concat("'", ((DateTime)param).ToString("yyyy-MM-dd HH:mm:ss.ffffff"), "'");
			else if (param is DateTime?)
				return string.Concat("'", (param as DateTime?).Value.ToString("yyyy-MM-dd HH:mm:ss.ffffff"), "'");
			else if (param is TimeSpan) {
				var ts = (TimeSpan)param;
				return string.Concat("'", ts.Ticks > 0 ? "" : "-", ts.TotalHours, dt1970.AddTicks(Math.Abs(ts.Ticks)).ToString(":mm:ss.ffffff"), "'");
			} else if (param is TimeSpan) {
				var ts = (param as TimeSpan?).Value;
				return string.Concat("'", ts.Ticks > 0 ? "" : "-", ts.TotalHours, dt1970.AddTicks(Math.Abs(ts.Ticks)).ToString(":mm:ss.ffffff"), "'");
			} else if (param is IEnumerable) {
				var sb = new StringBuilder();
				var ie = param as IEnumerable;
				foreach (var z in ie) sb.Append(",").Append(AddslashesProcessParam(z));
				return sb.Length == 0 ? "(NULL)" : sb.Remove(0, 1).Insert(0, "(").Append(")").ToString();
			} else {
				return string.Concat("'", param.ToString().Replace("'", "''"), "'");
				//if (param is string) return string.Concat('N', nparms[a]);
			}
		}

		protected override DbCommand CreateCommand() {
			return new NpgsqlCommand();
		}

		protected override void ReturnConnection(ObjectPool<DbConnection> pool, Object<DbConnection> conn, Exception ex) {
			(pool as PostgreSQLConnectionPool).Return(conn, ex);
		}

		protected override DbParameter[] GetDbParamtersByObject(string sql, object obj) => _util.GetDbParamtersByObject(sql, obj);
	}
}