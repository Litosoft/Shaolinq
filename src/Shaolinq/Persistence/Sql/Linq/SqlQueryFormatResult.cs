﻿using System;
using System.Collections.Generic;
using Platform;

namespace Shaolinq.Persistence.Sql.Linq
{
	public class SqlQueryFormatResult
	{
		public string CommandText { get; private set; }
		public IEnumerable<Pair<Type, object>> ParameterValues { get; set; }

		public SqlQueryFormatResult(string commandText, IEnumerable<Pair<Type, object>> parameterValues)
		{
			this.CommandText = commandText;
			this.ParameterValues = parameterValues;
		}
	}
}