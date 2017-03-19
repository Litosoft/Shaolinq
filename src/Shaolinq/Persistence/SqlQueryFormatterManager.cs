// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Platform;
using Shaolinq.Persistence.Linq;

namespace Shaolinq.Persistence
{
	public abstract class SqlQueryFormatterManager
	{
		public struct FormatParamValue
		{
			public object Value { get; set; }
			public bool AutoQuote { get; set; }

			public FormatParamValue(object value, bool autoQuote)
			{
				this.Value = value;
				this.AutoQuote = autoQuote;
			}
		}

		private readonly SqlDialect sqlDialect;
		private readonly string stringQuote; 
		private readonly string parameterPrefix;
		private readonly string stringEscape;

		public abstract SqlQueryFormatter CreateQueryFormatter(SqlQueryFormatterOptions options = SqlQueryFormatterOptions.Default);
		
		protected SqlQueryFormatterManager(SqlDialect sqlDialect)
		{
			this.sqlDialect = sqlDialect;
			this.stringEscape = this.sqlDialect.GetSyntaxSymbolString(SqlSyntaxSymbol.StringEscape);
			this.stringQuote = this.sqlDialect.GetSyntaxSymbolString(SqlSyntaxSymbol.StringQuote); 
			this.parameterPrefix = this.sqlDialect.GetSyntaxSymbolString(SqlSyntaxSymbol.ParameterPrefix);
		}

		internal virtual string GetConstantValue(object value)
		{
			if (value == null || value == DBNull.Value)
			{
				return this.sqlDialect.GetSyntaxSymbolString(SqlSyntaxSymbol.Null);
			}
			
			var type = value.GetType();

			type = Nullable.GetUnderlyingType(type) ?? type;

			if (type == typeof(string) || type.IsEnum)
			{
				var str = (string)value;

				if (str.Contains(this.stringQuote))
				{
					return this.stringQuote + str.Replace(this.stringQuote, this.stringEscape + this.stringQuote) + this.stringQuote;
				}

				return this.stringQuote + value + this.stringQuote;
			}

			if (type == typeof(Guid))
			{
				var guidValue = (Guid)value;

				return this.stringQuote + guidValue.ToString("D") + this.stringQuote;
			}

			if (type == typeof(TimeSpan))
			{
				var timespanValue = (TimeSpan)value;

				return this.stringQuote + timespanValue + this.stringQuote;
			}

			if (type == typeof(DateTime))
			{
				var dateTime = ((DateTime)value).ToUniversalTime();

				return this.stringQuote + dateTime.ToString("yyyy-MM-dd HH:mm:ss.fffff") + this.stringQuote;
			}

			return Convert.ToString(value);
		}

		public virtual SqlQueryFormatResult Format(Expression expression, SqlQueryFormatterOptions options = SqlQueryFormatterOptions.Default)
		{
			return this.CreateQueryFormatter(options).Format(expression);
		}
		
		public virtual string GetQueryText(SqlQueryFormatResult formatResult, Func<LocatedTypedValue, object> convert = null)
		{
			if (!(formatResult.ParameterValues?.Count > 0))
			{
				return formatResult.CommandText;
			}

			var index = 0;
			var stringBuilder = new StringBuilder(formatResult.CommandText.Length * 2);

			foreach (var parameterValue in formatResult.ParameterValues)
			{
				stringBuilder.Append(formatResult.CommandText, index, parameterValue.Offset - index);
				stringBuilder.Append(convert?.Invoke(parameterValue) ?? GetConstantValue(parameterValue.TypedValue.Value));

				index += parameterValue.Length;
			}

			return stringBuilder.ToString();
		}
	}
}
