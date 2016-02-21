using NUnit.Framework;
using Shaolinq.Tests.TestModel;

namespace Shaolinq.Tests
{
	[TestFixture("Sqlite")]
	[TestFixture("Postgres")]
	public class SchemaMigrationTests
		: BaseTests<TestDataAccessModel>
	{
		public SchemaMigrationTests(string providerName)
			: base(providerName)
		{
			this.model.Create(DatabaseCreationOptions.DeleteExistingDatabase);
		}

		[Test]
		public void Test()
		{
			var definitions = this.model.GetCurrentSqlDatabaseContext().SchemaManager.LoadDataDefinitionExpressions();
		}
	}
}
