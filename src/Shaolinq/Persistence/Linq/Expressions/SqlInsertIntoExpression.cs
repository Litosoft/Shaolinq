﻿// Copyright (c) 2007-2014 Thong Nguyen (tumtumtum@gmail.com)

using System.Collections.Generic;
using System.Linq.Expressions;
using Platform.Collections;

namespace Shaolinq.Persistence.Linq.Expressions
{
	public class SqlInsertIntoExpression
		: SqlBaseExpression
	{
		public string TableName { get; private set; }
		public IReadOnlyList<string> ColumnNames { get; private set; }
		public IReadOnlyList<Expression> ValueExpressions { get; private set; }
		public IReadOnlyList<string> ReturningAutoIncrementColumnNames { get; private set; }
		public override ExpressionType NodeType { get { return (ExpressionType)SqlExpressionType.InsertInto; } }

		public SqlInsertIntoExpression(string tableName, IEnumerable<string> columnNames, IEnumerable<string> returningAutoIncrementColumnNames, IEnumerable<Expression> valueExpressions)
			: this(tableName, columnNames.ToReadOnlyList(), returningAutoIncrementColumnNames.ToReadOnlyList(), valueExpressions.ToReadOnlyList())
		{	
		}

		public SqlInsertIntoExpression(string tableName, IReadOnlyList<string> columnNames, IReadOnlyList<string> returningAutoIncrementColumnNames, IReadOnlyList<Expression> valueExpressions)
			: base(typeof(void))
		{
			this.TableName = tableName;
			this.ColumnNames = columnNames;
			this.ReturningAutoIncrementColumnNames = returningAutoIncrementColumnNames;
			this.ValueExpressions = valueExpressions;
		}
	}
}
