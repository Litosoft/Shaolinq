﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Shaolinq.Logging;
using Shaolinq.Persistence.Linq;
using Shaolinq.Persistence.Linq.Expressions;
using Shaolinq.TypeBuilding;

namespace Shaolinq.Persistence
{
	public partial class DefaultSqlTransactionalCommandsContext
	{
		#region ExecuteReader
		[RewriteAsync]
		public override IDataReader ExecuteReader(SqlQueryFormatResult formatResult)
		{
			using (var command = this.CreateCommand())
			{
				foreach (var value in formatResult.ParameterValues)
				{
					this.AddParameter(command, value.TypedValue.Type, value.TypedValue.Value);
				}

				command.CommandText = formatResult.CommandText;

				Logger.Info(() => this.SqlDatabaseContext.SqlQueryFormatterManager.GetQueryText(formatResult));

				try
				{
					return command.ExecuteReaderEx(this.DataAccessModel);
				}
				catch (Exception e)
				{
					var decoratedException = LogAndDecorateException(e, command);

					if (decoratedException != null)
					{
						throw decoratedException;
					}

					throw;
				}
			}
		}
		#endregion

		#region Update

		[RewriteAsync]
		public override void Update(Type type, IEnumerable<DataAccessObject> dataAccessObjects)
		{
			Update(type, dataAccessObjects, true);
		}

		[RewriteAsync]
		private void Update(Type type, IEnumerable<DataAccessObject> dataAccessObjects, bool resetModified)
		{
			var typeDescriptor = this.DataAccessModel.GetTypeDescriptor(type);

			foreach (var dataAccessObject in dataAccessObjects)
			{
				var objectState = dataAccessObject.GetAdvanced().ObjectState;

				if ((objectState & (DataAccessObjectState.Changed | DataAccessObjectState.ServerSidePropertiesHydrated)) == 0)
				{
					continue;
				}

				using (var command = this.BuildUpdateCommand(typeDescriptor, dataAccessObject))
				{

					if (command == null)
					{
						Logger.ErrorFormat("Object is reported as changed but GetChangedProperties returns an empty list ({0})", dataAccessObject);

						continue;
					}

					Logger.Info(() => this.FormatCommand(command));

					int result;

					try
					{
						result = command.ExecuteNonQueryEx(this.DataAccessModel);
					}
					catch (Exception e)
					{
						var decoratedException = LogAndDecorateException(e, command);

						if (decoratedException != null)
						{
							throw decoratedException;
						}

						throw;
					}

					if (result == 0)
					{
						throw new MissingDataAccessObjectException(dataAccessObject, null, command.CommandText);
					}

					if (resetModified)
					{
						dataAccessObject.ToObjectInternal().ResetModified();
					}
				}
			}
		}
		#endregion

		#region Insert

		[RewriteAsync]
		public override InsertResults Insert(Type type, IEnumerable<DataAccessObject> dataAccessObjects)
		{
			var listToFixup = new List<DataAccessObject>();
			var listToRetry = new List<DataAccessObject>();

			var canDefer = !this.DataAccessModel.hasAnyAutoIncrementValidators;

			foreach (var dataAccessObject in dataAccessObjects)
			{
				var objectState = dataAccessObject.GetAdvanced().ObjectState;

				switch (objectState & DataAccessObjectState.NewChanged)
				{
				case DataAccessObjectState.Unchanged:
					continue;
				case DataAccessObjectState.New:
				case DataAccessObjectState.NewChanged:
					break;
				case DataAccessObjectState.Changed:
					throw new NotSupportedException($"Changed state not supported {objectState}");
				}

				var primaryKeyIsComplete = (objectState & DataAccessObjectState.PrimaryKeyReferencesNewObjectWithServerSideProperties) != DataAccessObjectState.PrimaryKeyReferencesNewObjectWithServerSideProperties;
				var constraintsDeferrableOrNotReferencingNewObject = (canDefer && this.SqlDatabaseContext.SqlDialect.SupportsCapability(SqlCapability.Deferrability)) || (objectState & DataAccessObjectState.ReferencesNewObject) == 0;
				var objectReadyToBeCommited = primaryKeyIsComplete && constraintsDeferrableOrNotReferencingNewObject;

				if (objectReadyToBeCommited)
				{
					SqlQueryFormatResult formatResult;
					var typeDescriptor = this.DataAccessModel.GetTypeDescriptor(type);

					using (var command = this.BuildInsertCommand(typeDescriptor, dataAccessObject, out formatResult))
					{
retryInsert:
						Logger.Info(() => this.FormatCommand(formatResult));

						try
						{
							var reader = command.ExecuteReaderEx(this.DataAccessModel);

							using (reader)
							{
								if (dataAccessObject.GetAdvanced().DefinesAnyDirectPropertiesGeneratedOnTheServerSide)
								{
									var dataAccessObjectInternal = dataAccessObject.ToObjectInternal();

									var result = reader.ReadEx();

									if (result)
									{
										this.ApplyPropertiesGeneratedOnServerSide(dataAccessObject, reader);
									}

									reader.Close();

									if (!dataAccessObjectInternal.ValidateServerSideGeneratedIds())
									{
										this.Delete(dataAccessObject.GetType(), new[] { dataAccessObject });

										goto retryInsert;
									}

									dataAccessObjectInternal.MarkServerSidePropertiesAsApplied();

									var updateRequired = dataAccessObjectInternal.ComputeServerGeneratedIdDependentComputedTextProperties();

									if (updateRequired)
									{
										this.Update(dataAccessObject.GetType(), new[] { dataAccessObject }, false);
									}
								}
							}
						}
						catch (Exception e)
						{
							var decoratedException = LogAndDecorateException(e, formatResult);

							if (decoratedException != null)
							{
								throw decoratedException;
							}

							throw;
						}

						if ((objectState & DataAccessObjectState.ReferencesNewObjectWithServerSideProperties) == DataAccessObjectState.ReferencesNewObjectWithServerSideProperties)
						{
							listToFixup.Add(dataAccessObject);
						}
						else
						{
							dataAccessObject.ToObjectInternal().ResetModified();
						}
					}
				}
				else
				{
					listToRetry.Add(dataAccessObject);
				}
			}

			return new InsertResults(listToFixup, listToRetry);
		}
		#endregion

		#region DeleteExpression

		[RewriteAsync]
		public override void Delete(SqlDeleteExpression deleteExpression)
		{
			var formatResult = this.SqlDatabaseContext.SqlQueryFormatterManager.Format(deleteExpression);

			using (var command = this.CreateCommand())
			{
				command.CommandText = formatResult.CommandText;

				foreach (var value in formatResult.ParameterValues)
				{
					this.AddParameter(command, value.TypedValue.Type, value.TypedValue.Value);
				}

				Logger.Info(() => this.FormatCommand(formatResult));

				try
				{
					var count = command.ExecuteNonQueryEx(this.DataAccessModel);
				}
				catch (Exception e)
				{
					var decoratedException = LogAndDecorateException(e, formatResult);

					if (decoratedException != null)
					{
						throw decoratedException;
					}

					throw;
				}
			}
		}

		#endregion

		#region DeleteObjects

		[RewriteAsync]
		public override void Delete(Type type, IEnumerable<DataAccessObject> dataAccessObjects)
		{
			var provider = new SqlQueryProvider(this.DataAccessModel, this.SqlDatabaseContext);
			var expression = this.BuildDeleteExpression(type, dataAccessObjects);

			if (expression == null)
			{
				return;
			}

			((ISqlQueryProvider)provider).Execute<int>(expression);
		}

		public virtual Expression BuildDeleteExpression(Type type, IEnumerable<DataAccessObject> dataAccessObjects)
		{
			var typeDescriptor = this.DataAccessModel.GetTypeDescriptor(type);
			var parameter = Expression.Parameter(typeDescriptor.Type, "value");

			Expression body = null;

			foreach (var dataAccessObject in dataAccessObjects)
			{
				var currentExpression = Expression.Equal(parameter, Expression.Constant(dataAccessObject));

				if (body == null)
				{
					body = currentExpression;
				}
				else
				{
					body = Expression.OrElse(body, currentExpression);
				}
			}

			if (body == null)
			{
				return null;
			}
			
			var condition = Expression.Lambda(body, parameter);
			var expression = (Expression)Expression.Call(Expression.Constant(this.DataAccessModel), MethodInfoFastRef.DataAccessModelGetDataAccessObjectsMethod.MakeGenericMethod(typeDescriptor.Type));

			expression = Expression.Call(MethodInfoFastRef.QueryableWhereMethod.MakeGenericMethod(typeDescriptor.Type), expression, Expression.Quote(condition));
			expression = Expression.Call(MethodInfoFastRef.QueryableExtensionsDeleteMethod.MakeGenericMethod(typeDescriptor.Type), expression);

			return expression;
		}

		#endregion
	}
}
