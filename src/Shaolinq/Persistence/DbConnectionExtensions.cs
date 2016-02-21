// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Data;
using System.Data.Common;

namespace Shaolinq.Persistence
{
	public static class DbConnectionExtensions
	{
		public static DataTable GetSchema(this IDbConnection connection)
		{
			if (connection == null)
			{
				throw new ArgumentNullException(nameof(connection));
			}

			var dbConnection = connection as DbConnection;

			if (dbConnection != null)
			{
				return dbConnection.GetSchema();
			}

			var connectionWrapper = connection as DbConnectionWrapper;

			if (connectionWrapper != null)
			{
				return connectionWrapper.Inner.GetSchema();
			}

			throw new NotSupportedException(nameof(GetSchema));
		}

		public static DataTable GetSchema(this IDbConnection connection, string collectionName)
		{
			if (connection == null)
			{
				throw new ArgumentNullException(nameof(connection));
			}

			var dbConnection = connection as DbConnection;

			if (dbConnection != null)
			{
				return dbConnection.GetSchema(collectionName);
			}

			var connectionWrapper = connection as DbConnectionWrapper;

			if (connectionWrapper != null)
			{
				return connectionWrapper.Inner.GetSchema(collectionName);
			}

			throw new NotSupportedException(nameof(GetSchema));
		}

		public static DataTable GetSchema(this IDbConnection connection, string collectionName, string[] restrictionValues)
		{
			if (connection == null)
			{
				throw new ArgumentNullException(nameof(connection));
			}

			var dbConnection = connection as DbConnection;

			if (dbConnection != null)
			{
				return dbConnection.GetSchema(collectionName, restrictionValues);
			}

			var connectionWrapper = connection as DbConnectionWrapper;

			if (connectionWrapper != null)
			{
				return connectionWrapper.Inner.GetSchema(collectionName, restrictionValues);
			}

			throw new NotSupportedException(nameof(GetSchema));
		}
	}
}
