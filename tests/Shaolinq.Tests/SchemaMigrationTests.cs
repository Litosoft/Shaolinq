using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Shaolinq.Tests.TestModel;

namespace Shaolinq.Tests
{
	[TestFixture("Postgres")]
	public class SchemaMigrationTests
		: BaseTests<TestDataAccessModel>
	{
		public SchemaMigrationTests(string providerName)
			: base(providerName)
		{
		}

		[Test]
		public void TestLoadSchema()
		{
			var dataDefinitionExpressions = this.model.GetCurrentSqlDatabaseContext().SchemaManager.LoadDataDefinitionExpressions();
			
		}
	}
}
