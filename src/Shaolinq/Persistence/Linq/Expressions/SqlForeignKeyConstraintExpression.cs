﻿// Copyright (c) 2007-2015 Thong Nguyen (tumtumtum@gmail.com)

using System.Collections.Generic;
using System.Linq.Expressions;
using Platform.Collections;

namespace Shaolinq.Persistence.Linq.Expressions
{
	public class SqlForeignKeyConstraintExpression
		: SqlBaseExpression
	{
		public string ConstraintName { get; set; }
		public IReadOnlyList<string> ColumnNames { get; set; }
		public SqlReferencesColumnExpression ReferencesColumnExpression { get; private set; }
		public override ExpressionType NodeType { get { return (ExpressionType)SqlExpressionType.ForeignKeyConstraint; } }

		public SqlForeignKeyConstraintExpression(string constraintName, IEnumerable<string> columnNames, SqlReferencesColumnExpression referencesColumnExpression)
			: this(constraintName, columnNames.ToReadOnlyList(), referencesColumnExpression)
		{	
		}

		public SqlForeignKeyConstraintExpression(string constraintName, IReadOnlyList<string> columnNames, SqlReferencesColumnExpression referencesColumnExpression)
			: base(typeof(void))
		{
			this.ConstraintName = constraintName;
			this.ColumnNames = columnNames;
			this.ReferencesColumnExpression = referencesColumnExpression;
		}

		public SqlForeignKeyConstraintExpression UpdateColumnNamesAndReferencedColumnExpression(IEnumerable<string> columnNames, SqlReferencesColumnExpression sqlReferencesColumnExpression)
		{
			return new SqlForeignKeyConstraintExpression(this.ConstraintName, columnNames, sqlReferencesColumnExpression);
		}
	}
}
