// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Linq.Expressions;

namespace Shaolinq.Persistence.Linq.Expressions
{
	public class SqlUpdateColumnExpression
		: SqlBaseExpression
	{
		public Expression Value { get; private set; }
		public SqlColumnExpression Column { get; private set; }
		public override ExpressionType NodeType => (ExpressionType)SqlExpressionType.UpdateColumn;

		public SqlUpdateColumnExpression(Type type, SqlColumnExpression column, Expression value)
			: base(type)
		{
			this.Value = value;
			this.Column = column;
		}
	}
}
