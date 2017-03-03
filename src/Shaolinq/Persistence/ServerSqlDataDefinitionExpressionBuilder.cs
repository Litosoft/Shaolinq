// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.c    om)

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using Shaolinq.Persistence.Linq.Expressions;

namespace Shaolinq.Persistence
{
	public class ServerSqlDataDefinitionExpressionBuilder
	{
		private List<SqlColumnDefinitionExpression> columns = new List<SqlColumnDefinitionExpression>();
		private readonly List<SqlCreateTableExpression> tables = new List<SqlCreateTableExpression>();
		   
		public SqlDatabaseSchemaManager SchemaManager { get; }

		public ServerSqlDataDefinitionExpressionBuilder(SqlDatabaseSchemaManager schemaManager)
		{
			this.SchemaManager = schemaManager;
		}

		private static bool ParseBool(string value)
		{
			return value.Equals("yes", StringComparison.InvariantCultureIgnoreCase);
		}

		protected SqlColumnDefinitionExpression BuildColumnDefinition(DataRow row)
		{
			var isNullable = ParseBool(row["is_nullable"].ToString());
			var dataType = (string)row["data_type"];
			var constraints = new List<Expression>();

			if (!isNullable)
			{
				var constraint = new SqlSimpleConstraintExpression(SqlSimpleConstraint.NotNull);

				constraints.Add(constraint);
			}
			
			var sqlTypeExpression = new SqlTypeExpression(dataType);
			var columnDefinition = new SqlColumnDefinitionExpression((string)row["column_name"], sqlTypeExpression, constraints);

			return columnDefinition;
		}

		public virtual Expression Build()
		{
			var sqlDatabaseContext = this.SchemaManager.SqlDatabaseContext;
			
			using (var connection =  (DbConnection)sqlDatabaseContext.OpenConnection())
			{
				var columnsTable = connection.GetSchema("Columns").Rows.Cast<DataRow>().ToList();

				var tablesTable = connection.GetSchema("Tables");

				foreach (DataRow row in tablesTable.Rows)
				{
					var table = new SqlTableExpression((string)row["table_name"]);
					var tableColumns = columnsTable
						.Where(c => c["table_name"]?.ToString() == table.Name)
						.Select(this.BuildColumnDefinition).ToList();

					var createTable = new SqlCreateTableExpression(table, true, tableColumns, null);

					this.tables.Add(createTable);
				}
			}

			return new SqlStatementListExpression(this.tables);
		}
	}
}
