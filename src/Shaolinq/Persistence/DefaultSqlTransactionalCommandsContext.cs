﻿// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Platform;
using Shaolinq.Logging;
using Shaolinq.Persistence.Linq;
using Shaolinq.Persistence.Linq.Expressions;
using Shaolinq.Persistence.Linq.Optimizers;
using Shaolinq.TypeBuilding;
using FormatParamValue = Shaolinq.Persistence.SqlQueryFormatterManager.FormatParamValue;

namespace Shaolinq.Persistence
{
	public partial class DefaultSqlTransactionalCommandsContext
		: SqlTransactionalCommandsContext
	{
		protected static readonly ILog Logger = LogProvider.GetLogger("Shaolinq.Query");

		protected internal struct SqlCachedUpdateInsertFormatValue
		{
			public SqlQueryFormatResult formatResult;
			public int[] valueIndexesToParameterPlaceholderIndexes;
			public int[] primaryKeyIndexesToParameterPlaceholderIndexes;
		}

		protected internal struct SqlCachedUpdateInsertFormatKey
		{
			public readonly Type dataAccessObjectType;
			public readonly IList<ObjectPropertyValue> changedProperties;
			public readonly bool requiresIdentityInsert;

			public SqlCachedUpdateInsertFormatKey(Type dataAccessObjectType, IList<ObjectPropertyValue> changedProperties, bool requiresIdentityInsert = false)
			{
				this.dataAccessObjectType = dataAccessObjectType;
				this.changedProperties = changedProperties;
				this.requiresIdentityInsert = requiresIdentityInsert;
			}
		}

		protected internal class CommandKeyComparer
			: IEqualityComparer<SqlCachedUpdateInsertFormatKey>
		{
			public static readonly CommandKeyComparer Default = new CommandKeyComparer();

			public bool Equals(SqlCachedUpdateInsertFormatKey x, SqlCachedUpdateInsertFormatKey y)
			{
				if (x.dataAccessObjectType != y.dataAccessObjectType)
				{
					return false;
				}

				if (x.changedProperties.Count != y.changedProperties.Count)
				{
					return false;
				}

				for (int i = 0, n = x.changedProperties.Count; i < n; i++)
				{
					if (!ReferenceEquals(x.changedProperties[i].PersistedName, y.changedProperties[i].PersistedName))
					{
						return false;
					}
				}

				return x.requiresIdentityInsert == y.requiresIdentityInsert;
			}

			public int GetHashCode(SqlCachedUpdateInsertFormatKey obj)
			{
				var count = obj.changedProperties.Count;
				var retval = obj.dataAccessObjectType.GetHashCode() ^ count;

				if (count > 0)
				{
					retval ^= obj.changedProperties[0].PropertyNameHashCode;

					if (count > 1)
					{
						retval ^= obj.changedProperties[count - 1].PropertyNameHashCode;
					}
				}

				retval ^= obj.requiresIdentityInsert ? 26542323 : 0;

				return retval;
			}
		}

		protected readonly string tableNamePrefix;
		protected readonly SqlDataTypeProvider sqlDataTypeProvider;
		protected readonly string parameterIndicatorPrefix;
		private Dictionary<RuntimeTypeHandle, Func<DataAccessObject, IDataReader, DataAccessObject>> serverSideGeneratedPropertySettersByType = new Dictionary<RuntimeTypeHandle, Func<DataAccessObject, IDataReader, DataAccessObject>>();

		public DefaultSqlTransactionalCommandsContext(SqlDatabaseContext sqlDatabaseContext, IDbConnection connection, TransactionContext transactionContext)
			: base(sqlDatabaseContext, connection, transactionContext)
		{
		    try
		    {
		        this.sqlDataTypeProvider = sqlDatabaseContext.SqlDataTypeProvider;
		        this.tableNamePrefix = sqlDatabaseContext.TableNamePrefix;
		        this.parameterIndicatorPrefix = sqlDatabaseContext.SqlDialect.GetSyntaxSymbolString(SqlSyntaxSymbol.ParameterPrefix);
		    }
		    catch
		    {
		        this.Dispose(true);

		        throw;
		    }
		}

		internal string FormatCommand(SqlQueryFormatResult formatResult)
		{
			return this.SqlDatabaseContext.SqlQueryFormatterManager.GetQueryText(formatResult);
		}
		
		private Exception LogAndDecorateException(Exception e, SqlQueryFormatResult formatResult)
		{
			var relatedSql = this.SqlDatabaseContext.GetRelatedSql(e) ?? this.FormatCommand(formatResult);
			var decoratedException = this.SqlDatabaseContext.DecorateException(e, null, relatedSql);

			Logger.Error(this.FormatCommand(formatResult));
			Logger.Error(e.ToString());

			if (decoratedException != e)
			{
				return decoratedException;
			}

			return null;
		}

		private DataAccessObject ApplyPropertiesGeneratedOnServerSide(DataAccessObject dataAccessObject, IDataReader reader)
		{
			if (!dataAccessObject.GetAdvanced().DefinesAnyDirectPropertiesGeneratedOnTheServerSide)
			{
				return dataAccessObject;
			}

			Func<DataAccessObject, IDataReader, DataAccessObject> applicator;

			if (!this.serverSideGeneratedPropertySettersByType.TryGetValue(Type.GetTypeHandle(dataAccessObject), out applicator))
			{
				var objectParameter = Expression.Parameter(typeof(DataAccessObject));
				var readerParameter = Expression.Parameter(typeof(IDataReader));
				var propertiesGeneratedOnServerSide = dataAccessObject.GetAdvanced().GetPropertiesGeneratedOnTheServerSide();
				var local = Expression.Variable(dataAccessObject.GetType());

				var statements = new List<Expression>
				{
					Expression.Assign(local, Expression.Convert(objectParameter, dataAccessObject.GetType()))
				};
				
				var index = 0;
				
				foreach (var property in propertiesGeneratedOnServerSide)
				{
					var sqlDataType = this.sqlDataTypeProvider.GetSqlDataType(property.PropertyType);
					var valueExpression = sqlDataType.GetReadExpression(readerParameter, index++);
					var member = dataAccessObject.GetType().GetMostDerivedProperty(property.PropertyName);
					
					statements.Add(Expression.Assign(Expression.MakeMemberAccess(local, member), valueExpression));
				}
				
				statements.Add(objectParameter);
				
				var body = Expression.Block(new [] { local }, statements);

				var lambda = Expression.Lambda<Func<DataAccessObject, IDataReader, DataAccessObject>>(body, objectParameter, readerParameter);
				
				applicator = lambda.Compile();
				
				this.serverSideGeneratedPropertySettersByType = this.serverSideGeneratedPropertySettersByType.Clone(Type.GetTypeHandle(dataAccessObject), applicator, "ServerSideGeneratedPropertySettersByType");
			}

			return applicator(dataAccessObject, reader);
		}

		private void FillParameters(IDbCommand command, SqlCachedUpdateInsertFormatValue cachedValue, IReadOnlyCollection<ObjectPropertyValue> changedProperties, IReadOnlyCollection<ObjectPropertyValue> primaryKeys)
		{
			if (changedProperties == null && primaryKeys == null)
			{
				foreach (var parameter in cachedValue.formatResult.ParameterValues)
				{
					this.AddParameter(command, parameter.TypedValue.Type, parameter.TypedValue.Value);
				}

				return;
			}

			if (cachedValue.formatResult.ParameterValues.Count == (changedProperties?.Count ?? 0) + (primaryKeys?.Count ?? 0))
			{
				foreach (var value in changedProperties)
				{
					this.AddParameter(command, value.PropertyType, value.Value);
				}

				if (primaryKeys != null)
				{
					foreach (var value in primaryKeys)
					{
						this.AddParameter(command, value.PropertyType, value.Value);
					}
				}

				return;
			}

			var newParameters = new List<LocatedTypedValue>(cachedValue.formatResult.ParameterValues);
			
			if (changedProperties != null)
			{
				var i = 0;

				foreach (var changed in changedProperties)
				{
					var temp = cachedValue.valueIndexesToParameterPlaceholderIndexes[i];
					int parameterIndex;

					if (cachedValue.formatResult.PlaceholderIndexToParameterIndex.TryGetValue(temp, out parameterIndex))
					{
						var typedValue = newParameters[parameterIndex];

						newParameters[parameterIndex] = typedValue.ChangeValue(changed.Value);
					}

					i++;
				}
			}

			if (primaryKeys != null)
			{
				var i = 0;
				
				foreach (var changed in primaryKeys)
				{
					var temp = cachedValue.primaryKeyIndexesToParameterPlaceholderIndexes[i];
					int parameterIndex;

					if (cachedValue.formatResult.PlaceholderIndexToParameterIndex.TryGetValue(temp, out parameterIndex))
					{
						var typedValue = newParameters[parameterIndex];

						newParameters[parameterIndex] = typedValue.ChangeValue(changed.Value);
					}

					i++;
				}
			}

			foreach (var parameter in newParameters)
			{
				this.AddParameter(command, parameter.TypedValue.Type, parameter.TypedValue.Value);
			}
		}

		protected IDbCommand BuildUpdateCommandForDeflatedPredicated(TypeDescriptor typeDescriptor, DataAccessObject dataAccessObject, bool valuesPredicated, bool primaryKeysPredicated, List<ObjectPropertyValue> updatedProperties, ObjectPropertyValue[] primaryKeys, out SqlQueryFormatResult formatResult)
		{
			var constantPlaceholdersCount = 0;
			var assignments = new List<Expression>();
			var success = false;

			var parameter1 = Expression.Parameter(typeDescriptor.Type);
			var requiresIdentityInsert = dataAccessObject.ToObjectInternal().HasAnyChangedPrimaryKeyServerSideProperties;

			foreach (var updated in updatedProperties)
			{
				var value = updated.Value;
				var placeholder = updated.Value as Expression;

				if (placeholder == null)
				{
					placeholder = (Expression)new SqlConstantPlaceholderExpression(constantPlaceholdersCount++, Expression.Constant(updated.Value, updated.PropertyType.CanBeNull() ? updated.PropertyType : updated.PropertyType.MakeNullable()));
				}

				if (placeholder.Type != updated.PropertyType)
				{
					placeholder = Expression.Convert(placeholder, updated.PropertyType);
				}

				var m = TypeUtils.GetMethod(() => default(DataAccessObject).SetColumnValue(default(string), default(int)))
					.GetGenericMethodDefinition()
					.MakeGenericMethod(typeDescriptor.Type, updated.PropertyType);

				assignments.Add(Expression.Call(null, m, parameter1, Expression.Constant(updated.PersistedName), placeholder));
			}

			var parameter = Expression.Parameter(typeDescriptor.Type);

			if (primaryKeys.Length <= 0)
			{
				throw new InvalidOperationException("Expected more than 1 primary key");
			}

			Expression where = null;

			foreach (var primaryKey in primaryKeys)
			{
				var value = primaryKey.Value;
				var placeholder = primaryKey.Value as Expression;

				if (placeholder == null)
				{
					placeholder = new SqlConstantPlaceholderExpression(constantPlaceholdersCount++, Expression.Constant(value, primaryKey.PropertyType.CanBeNull() ? primaryKey.PropertyType : primaryKey.PropertyType.MakeNullable()));
				}

				if (placeholder.Type != primaryKey.PropertyType)
				{
					placeholder = Expression.Convert(placeholder, primaryKey.PropertyType);
				}

				var pathComponents = primaryKey.PropertyName.Split('.');
				var propertyExpression = pathComponents.Aggregate<string, Expression>(parameter, Expression.Property);
				var currentExpression = Expression.Equal(propertyExpression, placeholder);

				where = where == null ? currentExpression : Expression.And(where, currentExpression);
			}

			var predicate = Expression.Lambda(where, parameter);
			var method = TypeUtils.GetMethod(() => default(IQueryable<DataAccessObject>).UpdateHelper(default(Expression<Action<DataAccessObject>>), requiresIdentityInsert))
				.GetGenericMethodDefinition()
				.MakeGenericMethod(typeDescriptor.Type);

			var source = Expression.Call(null, MethodInfoFastRef.QueryableWhereMethod.MakeGenericMethod(typeDescriptor.Type), Expression.Constant(this.DataAccessModel.GetDataAccessObjects(typeDescriptor.Type)), Expression.Quote(predicate));
			var selector = Expression.Lambda(Expression.Block(assignments), parameter1);
			var expression = (Expression)Expression.Call(null, method, source, Expression.Quote(selector), Expression.Constant(requiresIdentityInsert));

			expression = SqlQueryProvider.Bind(this.DataAccessModel, this.sqlDataTypeProvider, expression);
			expression = SqlQueryProvider.Optimize(this.DataAccessModel, this.SqlDatabaseContext, expression);
			var projectionExpression = expression as SqlProjectionExpression;

			expression = projectionExpression.Select.From;

			var result = this.SqlDatabaseContext.SqlQueryFormatterManager.Format(expression);

			IDbCommand command = null;

			try
			{
				command = this.CreateCommand();

				command.CommandText = result.CommandText;

				var cachedValue = new SqlCachedUpdateInsertFormatValue { formatResult = result };

				FillParameters(command, cachedValue, null, null);

				success = true;

				formatResult = result;

				return command;
			}
			finally
			{
				if (!success)
				{
					command?.Dispose();
				}
			}
		}

		protected virtual IDbCommand BuildUpdateCommand(TypeDescriptor typeDescriptor, DataAccessObject dataAccessObject, out SqlQueryFormatResult formatResult)
		{
			bool valuesPredicated;
			bool primaryKeysPredicated;
			IDbCommand command = null;
			SqlCachedUpdateInsertFormatValue cachedValue;
			var requiresIdentityInsert = dataAccessObject.ToObjectInternal().HasAnyChangedPrimaryKeyServerSideProperties;
			var updatedProperties = dataAccessObject.ToObjectInternal().GetChangedPropertiesFlattened(out valuesPredicated);

			if (updatedProperties.Count == 0)
			{
				formatResult = null;

				return null;
			}
			
			var success = false;
			var primaryKeys = dataAccessObject.ToObjectInternal().GetPrimaryKeysForUpdateFlattened(out primaryKeysPredicated);

			if (valuesPredicated || primaryKeysPredicated)
			{
				formatResult = null;

				return BuildUpdateCommandForDeflatedPredicated(typeDescriptor, dataAccessObject, valuesPredicated, primaryKeysPredicated, updatedProperties, primaryKeys);
			}

			var commandKey = new SqlCachedUpdateInsertFormatKey(dataAccessObject.GetType(), updatedProperties, requiresIdentityInsert);
		
			if (this.TryGetUpdateCommand(commandKey, out cachedValue))
			{
				try
				{
					command = this.CreateCommand();
					command.CommandText = cachedValue.formatResult.CommandText;

					this.FillParameters(command, cachedValue, updatedProperties, primaryKeys);

					success = true;

					formatResult = cachedValue.formatResult;

					return command;
				}
				finally
				{
					if (!success)
					{
						command?.Dispose();
					}
				}
			}

			var constantPlaceholdersCount = 0;
			var valueIndexesToParameterPlaceholderIndexes = new int[updatedProperties.Count];
			var primaryKeyIndexesToParameterPlaceholderIndexes = new int[primaryKeys.Length];

			var assignments = new List<Expression>(updatedProperties.Count);

			foreach (var updated in updatedProperties)
			{
				var value = (Expression)new SqlConstantPlaceholderExpression(constantPlaceholdersCount++, Expression.Constant(updated.Value, updated.PropertyType.CanBeNull() ? updated.PropertyType : updated.PropertyType.MakeNullable()));

				if (value.Type != updated.PropertyType)
				{
					value = Expression.Convert(value, updated.PropertyType);
				}

				assignments.Add(new SqlAssignExpression(new SqlColumnExpression(updated.PropertyType, null, updated.PersistedName), value));
			}
			
			Expression where = null;

			Debug.Assert(primaryKeys.Length > 0);

			foreach (var primaryKey in primaryKeys)
			{
				var value = primaryKey.Value;
				var placeholder = (Expression)new SqlConstantPlaceholderExpression(constantPlaceholdersCount++, Expression.Constant(value, primaryKey.PropertyType.CanBeNull() ? primaryKey.PropertyType : primaryKey.PropertyType.MakeNullable()));

				if (placeholder.Type != primaryKey.PropertyType)
				{
					placeholder = Expression.Convert(placeholder, primaryKey.PropertyType);
				}

				var currentExpression = Expression.Equal(new SqlColumnExpression(primaryKey.PropertyType, null, primaryKey.PersistedName), placeholder);
				
				where = where == null ? currentExpression : Expression.And(where, currentExpression);
			}

			for (var i = 0; i < assignments.Count; i++)
			{
				valueIndexesToParameterPlaceholderIndexes[i] = i;
			}

			for (var i = 0; i < primaryKeys.Length; i++)
			{
				primaryKeyIndexesToParameterPlaceholderIndexes[i] = i + assignments.Count;
			}

			var expression = (Expression)new SqlUpdateExpression(new SqlTableExpression(typeDescriptor.PersistedName), assignments, where, requiresIdentityInsert);

			expression = SqlObjectOperandComparisonExpander.Expand(expression);

			var result = this.SqlDatabaseContext.SqlQueryFormatterManager.Format(expression);

			try
			{
				command = this.CreateCommand();
				command.CommandText = result.CommandText;

				cachedValue = new SqlCachedUpdateInsertFormatValue
				{
					formatResult = result,
					valueIndexesToParameterPlaceholderIndexes = valueIndexesToParameterPlaceholderIndexes,
					primaryKeyIndexesToParameterPlaceholderIndexes = primaryKeyIndexesToParameterPlaceholderIndexes
				};

				if (result.Cacheable)
				{
					this.CacheUpdateCommand(commandKey, cachedValue);
				}

				FillParameters(command, cachedValue, null, null);
				
				success = true;

				return command;
			}
			finally
			{
				if (!success)
				{
					command?.Dispose();
				}
			}
		}

		protected IDbCommand BuildInsertCommandForDeflatedPredicated(TypeDescriptor typeDescriptor, DataAccessObject dataAccessObject, List<ObjectPropertyValue> updatedProperties, out SqlQueryFormatResult formatResult)
		{
			var constantPlaceholdersCount = 0;
			var assignments = new List<Expression>();
			var success = false;

			var requiresIdentityInsert = dataAccessObject.ToObjectInternal().HasAnyChangedPrimaryKeyServerSideProperties;

			var parameter1 = Expression.Parameter(typeDescriptor.Type);

			foreach (var updated in updatedProperties)
			{
				var placeholder = updated.Value as Expression ?? new SqlConstantPlaceholderExpression(constantPlaceholdersCount++, Expression.Constant(updated.Value, updated.PropertyType.CanBeNull() ? updated.PropertyType : updated.PropertyType.MakeNullable()));

				if (placeholder.Type != updated.PropertyType)
				{
					placeholder = Expression.Convert(placeholder, updated.PropertyType);
				}

				var m = TypeUtils.GetMethod(() => default(DataAccessObject).SetColumnValue(default(string), default(int)))
					.GetGenericMethodDefinition()
					.MakeGenericMethod(typeDescriptor.Type, updated.PropertyType);

				assignments.Add(Expression.Call(null, m, parameter1, Expression.Constant(updated.PersistedName), placeholder));
			}

			var method = TypeUtils.GetMethod(() => default(IQueryable<DataAccessObject>).InsertHelper(default(Expression<Action<DataAccessObject>>), default(bool)))
				.GetGenericMethodDefinition()
				.MakeGenericMethod(typeDescriptor.Type);

			var source = Expression.Constant(this.DataAccessModel.GetDataAccessObjects(typeDescriptor.Type));
			var selector = Expression.Lambda(Expression.Block(assignments), parameter1);
			var expression = (Expression)Expression.Call(null, method, source, Expression.Quote(selector), Expression.Constant(requiresIdentityInsert));

			expression = SqlQueryProvider.Bind(this.DataAccessModel, this.sqlDataTypeProvider, expression);
			expression = SqlQueryProvider.Optimize(this.DataAccessModel, this.SqlDatabaseContext, expression);
			var projectionExpression = expression as SqlProjectionExpression;

			expression = projectionExpression.Select.From;
			
			var result = this.SqlDatabaseContext.SqlQueryFormatterManager.Format(expression);

			IDbCommand command = null;

			try
			{
				command = this.CreateCommand();
				command.CommandText = result.CommandText;

				var cachedValue = new SqlCachedUpdateInsertFormatValue { formatResult = result };

				FillParameters(command, cachedValue, null, null);

				success = true;

				formatResult = result;

				return command;
			}
			finally
			{
				if (!success)
				{
					command?.Dispose();
				}
			}
		}

		protected virtual IDbCommand BuildInsertCommand(TypeDescriptor typeDescriptor, DataAccessObject dataAccessObject, out SqlQueryFormatResult formatResult)
		{
			var success = false;
			IDbCommand command = null;
			SqlCachedUpdateInsertFormatValue cachedValue;
			bool predicated;

			var requiresIdentityInsert = dataAccessObject.ToObjectInternal().HasAnyChangedPrimaryKeyServerSideProperties;

			var updatedProperties = dataAccessObject.ToObjectInternal().GetChangedPropertiesFlattened(out predicated);

			if (predicated)
			{
				return BuildInsertCommandForDeflatedPredicated(typeDescriptor, dataAccessObject, updatedProperties, out formatResult);
			}

			var commandKey = new SqlCachedUpdateInsertFormatKey(dataAccessObject.GetType(), updatedProperties, requiresIdentityInsert);

			if (this.TryGetInsertCommand(commandKey, out cachedValue))
			{
				try
				{
					command = this.CreateCommand();
					command.CommandText = cachedValue.formatResult.CommandText;
					this.FillParameters(command, cachedValue, updatedProperties, null);

					success = true;

					formatResult = cachedValue.formatResult;

					return command;
				}
				finally
				{
					if (!success)
					{
						command?.Dispose();
					}
				}
			}

			IReadOnlyList<string> returningAutoIncrementColumnNames = null;

			if (dataAccessObject.GetAdvanced().DefinesAnyDirectPropertiesGeneratedOnTheServerSide)
			{
				var propertyDescriptors = typeDescriptor.PersistedPropertiesWithoutBackreferences.Where(c => c.IsPropertyThatIsCreatedOnTheServerSide).ToList();

				returningAutoIncrementColumnNames = propertyDescriptors.Select(c => c.PersistedName).ToReadOnlyCollection();
			}

			var constantPlaceholdersCount = 0;
			var valueIndexesToParameterPlaceholderIndexes = new int[updatedProperties.Count];
			
			var columnNames = updatedProperties.Select(c => c.PersistedName).ToReadOnlyCollection();
			
			var valueExpressions = new List<Expression>(updatedProperties.Count);

			foreach (var updated in updatedProperties)
			{
				var value = (Expression)new SqlConstantPlaceholderExpression(constantPlaceholdersCount++, Expression.Constant(updated.Value, updated.PropertyType.CanBeNull() ? updated.PropertyType : updated.PropertyType.MakeNullable()));

				if (value.Type != updated.PropertyType)
				{
					value = Expression.Convert(value, updated.PropertyType);
				}

				valueExpressions.Add(value);
			}

			Expression expression = new SqlInsertIntoExpression(new SqlTableExpression(typeDescriptor.PersistedName), columnNames, returningAutoIncrementColumnNames, valueExpressions, null, requiresIdentityInsert);

			for (var i = 0; i < constantPlaceholdersCount; i++)
			{
				valueIndexesToParameterPlaceholderIndexes[i] = i;
			}

			var result = this.SqlDatabaseContext.SqlQueryFormatterManager.Format(expression);

			try
			{
				command = this.CreateCommand();

				var commandText = result.CommandText;

				command.CommandText = commandText;
				cachedValue = new SqlCachedUpdateInsertFormatValue { formatResult = result, valueIndexesToParameterPlaceholderIndexes = valueIndexesToParameterPlaceholderIndexes, primaryKeyIndexesToParameterPlaceholderIndexes = null };

				if (result.Cacheable)
				{
					this.CacheInsertCommand(commandKey, cachedValue);
				}

				this.FillParameters(command, cachedValue, updatedProperties, null);

				success = true;

				formatResult = result;

				return command;
			}
			finally
			{
				if (!success)
				{
					command?.Dispose();
				}
			}
		}

		protected void CacheInsertCommand(SqlCachedUpdateInsertFormatKey sqlCachedUpdateInsertFormatKey, SqlCachedUpdateInsertFormatValue sqlCachedUpdateInsertFormatValue)
		{
			this.SqlDatabaseContext.formattedInsertSqlCache = this.SqlDatabaseContext.formattedInsertSqlCache.Clone(sqlCachedUpdateInsertFormatKey, sqlCachedUpdateInsertFormatValue, "formattedInsertSqlCache");
		}

		protected bool TryGetInsertCommand(SqlCachedUpdateInsertFormatKey sqlCachedUpdateInsertFormatKey, out SqlCachedUpdateInsertFormatValue sqlCachedUpdateInsertFormatValue)
		{
			return this.SqlDatabaseContext.formattedInsertSqlCache.TryGetValue(sqlCachedUpdateInsertFormatKey, out sqlCachedUpdateInsertFormatValue);
		}

		protected void CacheUpdateCommand(SqlCachedUpdateInsertFormatKey sqlCachedUpdateInsertFormatKey, SqlCachedUpdateInsertFormatValue sqlCachedUpdateInsertFormatValue)
		{
			this.SqlDatabaseContext.formattedUpdateSqlCache = this.SqlDatabaseContext.formattedUpdateSqlCache.Clone(sqlCachedUpdateInsertFormatKey, sqlCachedUpdateInsertFormatValue, "formattedUpdateSqlCache");
		}

		protected bool TryGetUpdateCommand(SqlCachedUpdateInsertFormatKey sqlCachedUpdateInsertFormatKey, out SqlCachedUpdateInsertFormatValue sqlCachedUpdateInsertFormatValue)
		{
			return this.SqlDatabaseContext.formattedUpdateSqlCache.TryGetValue(sqlCachedUpdateInsertFormatKey, out sqlCachedUpdateInsertFormatValue);
		}
	}
}
