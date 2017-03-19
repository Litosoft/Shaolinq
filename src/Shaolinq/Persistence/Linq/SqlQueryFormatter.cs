// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using Platform;

namespace Shaolinq.Persistence.Linq
{
	public abstract class SqlQueryFormatter
		: SqlExpressionVisitor
	{
		public const char DefaultParameterIndicatorChar = '@';

		protected enum Indentation
		{
			Same,
			Inner,
			Outer
		}

		public class IndentationContext
			: IDisposable
		{
			private readonly Sql92QueryFormatter parent;

			public IndentationContext(Sql92QueryFormatter parent)
			{
				this.parent = parent;
				this.parent.depth++;
				this.parent.WriteLine();
			}

			public void Dispose()
			{
				this.parent.depth--;
			}
		}

		public static string PrefixedTableName(string tableNamePrefix, string tableName)
		{
			if (!string.IsNullOrEmpty(tableNamePrefix))
			{
				return tableNamePrefix + tableName;
			}

			return tableName;
		}

		private int depth;
		private int charCount;
		private TextWriter writer;
		protected List<LocatedTypedValue> parameterValues;
		protected readonly SqlQueryFormatterManager formatterManager;
		internal int IndentationWidth { get; }
		public string ParameterIndicatorPrefix { get; protected set; }
		protected bool canReuse = true;
		protected List<Pair<int, int>> parameterIndexToPlaceholderIndexes;

		protected int CurrentOffset => charCount;
		
		protected readonly SqlDialect sqlDialect;

		public virtual SqlQueryFormatResult Format(Expression expression)
		{
			return this.Format(expression, new StringWriter(new StringBuilder(1024)));
		}

		public virtual SqlQueryFormatResult Format(Expression expression, TextWriter writer)
		{
			this.depth = 0;
			this.canReuse = true;
			this.writer = writer;
			this.parameterValues = new List<LocatedTypedValue>();
			this.parameterIndexToPlaceholderIndexes = new List<Pair<int, int>>();

			this.Visit(this.PreProcess(expression));

			return new SqlQueryFormatResult(this.writer.ToString(), this.parameterValues, canReuse ? parameterIndexToPlaceholderIndexes : null);
		}

		protected SqlQueryFormatter(SqlDialect sqlDialect, TextWriter writer)
		{
			this.sqlDialect = sqlDialect ?? new SqlDialect();
			this.writer = writer;
			this.ParameterIndicatorPrefix = this.sqlDialect.GetSyntaxSymbolString(SqlSyntaxSymbol.ParameterPrefix);
			this.IndentationWidth = 2;
		}

		protected void Indent(Indentation style)
		{
			if (style == Indentation.Inner)
			{
				this.depth++;
			}
			else if (style == Indentation.Outer)
			{
				this.depth--;
			}
		}

		public virtual void WriteLine()
		{
			this.writer.Write("\n");
			charCount++;

			for (var i = 0; i < this.depth * this.IndentationWidth; i++)
			{
				this.writer.Write(' ');
				charCount++;
			}
		}

		public virtual void WriteLine(object value)
		{
			this.Write(value);
			this.WriteLine();
		}

		public virtual void Write(object value)
		{
			var s = value.ToString();
			this.writer.Write(s);
			charCount += s.Length;
		}

		public virtual void WriteFormat(string format, params object[] args)
		{
			var s = string.Format(this.writer.FormatProvider, format, args);

			this.writer.Write(s);
			this.charCount += s.Length;
		}

		protected virtual Expression PreProcess(Expression expression)
		{
			return expression;
		}

		protected void WriteDeliminatedListOfItems(IEnumerable listOfItems, Action<object> action, string deliminator = ", ")
		{
			var i = 0;

			foreach (var item in listOfItems)
			{
				if (i++ > 0)
				{
					this.Write(deliminator);
				}

				action(item);
			}
		}

		protected void WriteDeliminatedListOfItems<T>(IEnumerable<T> listOfItems, Action<T> action, string deliminator = ", ")
		{
			var i = 0;

			foreach (var item in listOfItems)
			{
				if (i++ > 0)
				{
					this.Write(deliminator);
				}

				action(item);
			}
		}
		
		protected void WriteDeliminatedListOfItems<T>(IEnumerable<T> listOfItems, Action<T> action, Action deliminationAction)
		{
			var i = 0;

			foreach (var item in listOfItems)
			{
				if (i++ > 0)
				{
					deliminationAction();
				}

				action(item);
			}
		}
	}
}
