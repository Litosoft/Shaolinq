﻿using System.Linq.Expressions;

namespace Shaolinq.Persistence.Sql.Linq
{
	internal struct GroupByInfo
	{
		public string Alias { get; private set; }
		public Expression Element { get; private set; }

		public GroupByInfo(string alias, Expression element)
			: this()
		{
			this.Alias = alias;
			this.Element = element;
		}
	}
}