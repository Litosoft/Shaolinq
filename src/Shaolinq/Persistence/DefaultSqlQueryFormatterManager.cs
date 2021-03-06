﻿// Copyright (c) 2007-2017 Thong Nguyen (tumtumtum@gmail.com)

using Shaolinq.Persistence.Linq;

namespace Shaolinq.Persistence
{
	public class DefaultSqlQueryFormatterManager
		: SqlQueryFormatterManager
	{
		public SqlDialect SqlDialect { get; }
		public SqlQueryFormatterConstructorMethod ConstructorMethod { get; }
		public delegate SqlQueryFormatter SqlQueryFormatterConstructorMethod(SqlQueryFormatterOptions options);

		public DefaultSqlQueryFormatterManager(SqlDialect sqlDialect, NamingTransformsConfiguration namingTransformsConfiguration, SqlQueryFormatterConstructorMethod constructorMethod)
			: base(sqlDialect, namingTransformsConfiguration)
		{
			this.SqlDialect = sqlDialect;
			this.ConstructorMethod = constructorMethod;
		}

		public override SqlQueryFormatter CreateQueryFormatter(SqlQueryFormatterOptions options = SqlQueryFormatterOptions.Default)
		{
			return this.ConstructorMethod(options);
		}
	}
}
