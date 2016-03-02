﻿// Copyright (c) 2007-2016 Thong Nguyen (tumtumtum@gmail.com)

using System;
using System.Linq;
using System.Transactions;
using NUnit.Framework;
using Shaolinq.Persistence;
using Shaolinq.Tests.TestModel;

namespace Shaolinq.Tests
{
	[TestFixture("MySql")]
	[TestFixture("Postgres")]
	[TestFixture("Postgres.DotConnect")]
	[TestFixture("Postgres.DotConnect.Unprepared")]
	[TestFixture("SqlServer", Category = "IgnoreOnMono")]
	[TestFixture("Sqlite")]
	[TestFixture("Sqlite:DataAccessScope")]
	[TestFixture("SqliteInMemory")]
	[TestFixture("SqliteClassicInMemory")]
	public class DeleteTests
		: BaseTests<TestDataAccessModel>
	{
		public DeleteTests(string providerName)
			: base(providerName)
		{
		}

		[Test]
		public void Test_Use_Deflated_Reference_To_Update_Related_Object_That_Was_Deleted()
		{
			Guid student1Id, student2Id;

			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.Create();
				
				scope.Flush();

				var student1 = school.Students.Create(); 
				var student2 = school.Students.Create();

				student1Id = student1.Id;
				student2Id = student2.Id;

				student1.BestFriend = student2;

				scope.Complete();
			}

			using (var scope = new TransactionScope())
			{
				if (!this.model.GetCurrentSqlDatabaseContext().SqlDialect.SupportsCapability(SqlCapability.SetNullAction))
				{
					this.model.Students.Single(c => c.Id == student1Id).BestFriend = null;

					scope.Save();
				}

				var count = this.model.Students.Where(c => c.Id != Guid.Empty).Where(c => c.Id != Guid.NewGuid()).Delete(c => c.Id == student2Id);

				this.model.Students
					.Set(c => c.Firstname, c => "")
					.Set(c => c.Firstname, c => "Hello")
					.Set(c => c.Lastname, c => "Hello")
					.Update();

				Assert.AreEqual(1, count);

				scope.Complete();
			}

			using (var scope = new TransactionScope())
			{
				Assert.IsNull(this.model.Students.FirstOrDefault(c => c.Id == student2Id));

				var student1 = this.model.Students.First(c => c.Id == student1Id);

				Assert.IsNull(student1.BestFriend);

				scope.Complete();
			}
		}

		[Test]
		public void Test_Delete_Object_With_Deflated_Reference()
		{
			long schoolId;

			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.Create();

				scope.Flush();

				Assert.IsEmpty(((IDataAccessObjectAdvanced)school).GetChangedPropertiesFlattened());
				Assert.IsFalse(((IDataAccessObjectAdvanced)school).HasObjectChanged);

				schoolId = school.Id;

				scope.Complete();
			}

			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.GetReference(schoolId);

				school.Delete();

				Assert.IsTrue(((IDataAccessObjectAdvanced)school).IsDeleted);

				scope.Flush();

				Assert.IsNull(this.model.Schools.FirstOrDefault(c => c.Id == schoolId));

				scope.Complete();
			}

			using (var scope = new TransactionScope())
			{
				Assert.IsNull(this.model.Schools.FirstOrDefault(c => c.Id == schoolId));
			}
		}

		[Test]
		public void Test_Delete_Object_With_Invalid_Deflated_Reference()
		{
			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.GetReference(100000);

				school.Delete();

				scope.Complete();
			}
		}
	
		[Test]
		public void Test_Object_Deleted_Flushed_Still_Deleted()
		{
			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.Create();

				Assert.IsFalse(school.IsDeleted());
				school.Delete();
				Assert.IsTrue(school.IsDeleted());
				scope.Flush();
				Assert.IsTrue(school.IsDeleted());

				scope.Complete();
			}
		}

		[Test]
		public void Test_Modify_Deleted_Object()
		{
			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.Create();

				school.Delete();

				Assert.Catch<DeletedDataAccessObjectException>(() =>
				{
					school.Name = "Hello";
				});

				scope.Complete();
			}
		}

		[Test]
		public void Test_Query_Then_Delete_Object_Then_Query_Then_Access()
		{
			long schoolId;

			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.Create();

				school.Name = "Yoga Decorum";

				scope.Flush();

				schoolId = school.Id;

				scope.Complete();
			}

			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.First(c => c.Id == schoolId);

				Assert.IsFalse(school.IsDeleted()); 
				school.Delete();
				Assert.IsTrue(school.IsDeleted());

				school = this.model.Schools.First(c => c.Id == schoolId);

				Assert.IsTrue(school.IsDeleted());
			}

			using (var scope = new TransactionScope())
			{
				Assert.IsNotNull(this.model.Schools.Single(c => c.Id == schoolId));
			}

			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.First(c => c.Id == schoolId);

				Assert.IsFalse(school.IsDeleted());
				school.Delete();
				Assert.IsTrue(school.IsDeleted());

				school = this.model.Schools.First(c => c.Id == schoolId);

				Assert.IsTrue(school.IsDeleted());
				Assert.AreEqual("Yoga Decorum", school.Name);

				scope.Complete();
			}

			using (var scope = new TransactionScope())
			{
				Assert.IsNull(this.model.Schools.FirstOrDefault(c => c.Id == schoolId));
			}
		}

		[Test, ExpectedException(typeof(MissingDataAccessObjectException))]
		public void Test_Query_Access_Deleted_Object_Via_DeflatedReference()
		{
			long schoolId;

			using (var scope = new TransactionScope())
			{
				var school = this.model.Schools.Create();

				school.Name = "Yoga Decorum";

				scope.Flush();

				schoolId = school.Id;

				scope.Complete();
			}

			using (var scope = new TransactionScope())
			{
			    this.model.Schools.Where(c => c.Id == schoolId).Delete();

				scope.Complete();
			}

			Assert.AreEqual(0, this.model.Schools.Count(c => c.Id == schoolId));

			try
			{
				using (var scope = new TransactionScope())
				{
					var school = this.model.Schools.GetReference(schoolId);

					school.Name = "Yoga Decorum!!!";

					scope.Complete();
				}
			}
			catch (TransactionAbortedException e)
			{
				throw e.InnerException;
			}
		}
	}
}
