﻿using Shaolinq.Persistence;

namespace Shaolinq.SqlServer
{
	public class SqlServerSqlDialect
		: SqlDialect
	{
		public new static readonly SqlServerSqlDialect Default = new SqlServerSqlDialect();

		private SqlServerSqlDialect()
		{	
		}

		public override bool SupportsFeature(SqlFeature feature)
		{
			switch (feature)
			{
			case SqlFeature.InsertOutput:
			case SqlFeature.PragmaIdentityInsert:
				return true;
			case SqlFeature.Deferrability:
			case SqlFeature.CascadeAction:
			case SqlFeature.DeleteAction:
			case SqlFeature.SetNullAction:
			case SqlFeature.SetDefaultAction:
			case SqlFeature.UpdateAutoIncrementColumns:
				return false;
			default:
				return base.SupportsFeature(feature);
			}
		}

		public override string GetSyntaxSymbolString(SqlSyntaxSymbol symbol)
		{
			switch (symbol)
			{
			case SqlSyntaxSymbol.AutoIncrement:
				return "IDENTITY(1,1)";
			default:
				return base.GetSyntaxSymbolString(symbol);
			}
		}
	}
}