﻿// Copyright (c) 2007-2014 Thong Nguyen (tumtumtum@gmail.com)

using System.Linq.Expressions;

namespace Shaolinq.Persistence.Linq.Expressions
{
	public class SqlSimpleConstraintExpression
		: SqlBaseExpression
	{
		public object Value { get; private set; }
		public string[] ColumnNames { get; private set; }
		public SqlSimpleConstraint Constraint { get; private set; }
		public override ExpressionType NodeType { get { return (ExpressionType)SqlExpressionType.SimpleConstraint; } }

		public SqlSimpleConstraintExpression(SqlSimpleConstraint constraint, string[] columnNames = null, object value = null)
			: base(typeof(void))
		{
			this.Constraint = constraint;
			this.ColumnNames = columnNames;
			this.Value = value;
		}
	}
}