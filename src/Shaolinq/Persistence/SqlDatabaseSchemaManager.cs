// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using Platform.Collections;
using Shaolinq.Logging;
using Shaolinq.Persistence.Linq;

namespace Shaolinq.Persistence
{
	internal static class DataRowExtensions
	{
		public static object GetValue(this DataRow dataRow, int? row)
		{
			return row == null ? null : dataRow[row.Value];
		}
	}
	
	public abstract class SqlDatabaseSchemaManager
		: IDisposable
	{
		protected static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

		public SqlDatabaseContext SqlDatabaseContext { get; }
		public ServerSqlDataDefinitionExpressionBuilder ServerSqlDataDefinitionExpressionBuilder { get; }
		
		protected SqlDatabaseSchemaManager(SqlDatabaseContext sqlDatabaseContext)
		{
			this.SqlDatabaseContext = sqlDatabaseContext;
			this.ServerSqlDataDefinitionExpressionBuilder = new ServerSqlDataDefinitionExpressionBuilder(this);
		}

		public virtual void CreateDatabaseAndSchema(DatabaseCreationOptions options)
		{
			var dataDefinitionExpressions = this.BuildDataDefinitonExpressions(options);
			
			if (!this.CreateDatabaseOnly(dataDefinitionExpressions, options))
			{
				return;
			}

			this.CreateDatabaseSchema(dataDefinitionExpressions, options);
		}

		public virtual Expression LoadDataDefinitionExpressions()
		{
			using (var dataTransactionContext = this.SqlDatabaseContext.CreateSqlTransactionalCommandsContext(null))
			{
				var columnsTable = dataTransactionContext
					.DbConnection
					.GetSchema("Columns");

				var columnIndexesByName = columnsTable
					.Columns
					.Cast<DataColumn>()
					.ToDictionary(c => c.ColumnName, c => c.Ordinal, StringComparer.InvariantCultureIgnoreCase);

				var columnNameOrdinal = columnIndexesByName.GetValueOrNull("COLUMN_NAME");
				var tableNameOrdinal = columnIndexesByName.GetValueOrNull("TABLE_NAME");
				var descriptionOrdinal = columnIndexesByName.GetValueOrNull("DESCRIPTION");
				var dataTypeOrdinal = columnIndexesByName.GetValueOrNull("DATA_TYPE");
				var isNullableOrdinal = columnIndexesByName.GetValueOrNull("IS_NULLABLE");
				var autoIncrementOrdinal = columnIndexesByName.GetValueOrNull("AUTOINCREMENT");
				var uniqueOrdinal = columnIndexesByName.GetValueOrNull("UNIQUE");
				var primaryKeyOrdinal = columnIndexesByName.GetValueOrNull("PRIMARY_KEY");

				var columns = columnsTable
					.Rows
					.Cast<DataRow>()
					.Select(c => new
					{
						ColumnName = c.GetValue(columnNameOrdinal),
						TableName = c.GetValue(tableNameOrdinal),
						Description = c.GetValue(descriptionOrdinal),
						DataType = c.GetValue(dataTypeOrdinal),
						IsNullable = c.GetValue(isNullableOrdinal),
						AutoIncrement = c.GetValue(autoIncrementOrdinal),
						UniqueOrdinal = c.GetValue(uniqueOrdinal),
						PrimaryKey = c.GetValue(primaryKeyOrdinal)
                    })
					.ToList();
				
			}

			return null;
		}

		protected virtual SqlDataDefinitionBuilderFlags GetBuilderFlags()
		{
			return SqlDataDefinitionBuilderFlags.BuildTables | SqlDataDefinitionBuilderFlags.BuildIndexes;
		}

		protected virtual Expression BuildDataDefinitonExpressions(DatabaseCreationOptions options)
		{
			return SqlDataDefinitionExpressionBuilder.Build(this.SqlDatabaseContext.SqlDataTypeProvider, this.SqlDatabaseContext.SqlDialect, this.SqlDatabaseContext.DataAccessModel, options, this.SqlDatabaseContext.TableNamePrefix, this.GetBuilderFlags());
		}

		protected abstract bool CreateDatabaseOnly(Expression dataDefinitionExpressions, DatabaseCreationOptions options);

		protected virtual void CreateDatabaseSchema(Expression dataDefinitionExpressions, DatabaseCreationOptions options)
		{
			using (var scope = new DataAccessScope())
			{
				using (var dataTransactionContext = this.SqlDatabaseContext.CreateSqlTransactionalCommandsContext(null))
				{
					using (this.SqlDatabaseContext.AcquireDisabledForeignKeyCheckContext(dataTransactionContext))
					{
						var result = this.SqlDatabaseContext.SqlQueryFormatterManager.Format(dataDefinitionExpressions);

						using (var command = dataTransactionContext.CreateCommand(SqlCreateCommandOptions.Default | SqlCreateCommandOptions.UnpreparedExecute))
						{
							command.CommandText = result.CommandText;

							Logger.Debug(command.CommandText);

							command.ExecuteNonQuery();
						}
					}

					dataTransactionContext.Commit();
				}

				scope.Complete();
			}
		}

		public virtual void Dispose()
		{
		}
	}
}
