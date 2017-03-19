// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.com)

using System.Collections.Generic;
using Platform;

namespace Shaolinq.Persistence.Linq
{
	public struct LocatedTypedValue
	{
		public int Offset { get; }
		public int Length { get; }
		public TypedValue TypedValue { get; }
		
		public LocatedTypedValue(TypedValue typedValue, int offset, int length)
		{
			this.TypedValue = typedValue;
			this.Offset = offset;
			this.Length = length;
		}

		public LocatedTypedValue ChangeValue(object value)
		{
			return new LocatedTypedValue(this.TypedValue.ChangeValue(value), this.Offset, this.Length);
		}
	}

	public class SqlQueryFormatResult
	{
		public string CommandText { get; }
		public IReadOnlyList<LocatedTypedValue> ParameterValues { get; }
		public Dictionary<int, int> ParameterIndexToPlaceholderIndexes { get; }
		public bool Cacheable => ParameterIndexToPlaceholderIndexes != null;
		public Dictionary<int, int> PlaceholderIndexToParameterIndex {get; }
		
		public SqlQueryFormatResult(string commandText, IEnumerable<LocatedTypedValue> parameterValues, IReadOnlyList<Pair<int, int>> parameterIndexToPlaceholderIndexes)
			: this(commandText, parameterValues.ToReadOnlyCollection(), parameterIndexToPlaceholderIndexes)
		{
		}

		public SqlQueryFormatResult(string commandText, IReadOnlyList<LocatedTypedValue> parameterValues, IReadOnlyList<Pair<int, int>> parameterIndexToPlaceholderIndexes)
		{
			this.CommandText = commandText;
			this.ParameterValues = parameterValues;

			if (parameterIndexToPlaceholderIndexes?.Count > 0)
			{
				this.ParameterIndexToPlaceholderIndexes = new Dictionary<int, int>();
				this.PlaceholderIndexToParameterIndex = new Dictionary<int, int>();

				foreach (var value in parameterIndexToPlaceholderIndexes)
				{
					this.ParameterIndexToPlaceholderIndexes[value.Left] = value.Right;
					this.PlaceholderIndexToParameterIndex[value.Right] = value.Left;
				}
			}
		}

		private SqlQueryFormatResult(string commandText, IReadOnlyList<LocatedTypedValue> parameterValues, Dictionary<int, int> parameterIndexToPlaceholderIndexes, Dictionary<int, int> placeholderIndexToParameterIndexes)
		{
			this.CommandText = commandText;
			this.ParameterValues = parameterValues;

			this.ParameterIndexToPlaceholderIndexes = parameterIndexToPlaceholderIndexes;
			this.PlaceholderIndexToParameterIndex = placeholderIndexToParameterIndexes;
		}

		public SqlQueryFormatResult ChangeParameterValues(IEnumerable<LocatedTypedValue> values)
		{
			return new SqlQueryFormatResult(this.CommandText, values.ToReadOnlyCollection(), this.ParameterIndexToPlaceholderIndexes, this.PlaceholderIndexToParameterIndex);
		}
	}
}
