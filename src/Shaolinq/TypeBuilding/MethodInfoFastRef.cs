﻿using System;
using System.Linq;
using System.Reflection;
using Platform;

namespace Shaolinq.TypeBuilding
{
	public static class MethodInfoFastRef
	{
		public static readonly MethodInfo QueryableWhereMethod;
		public static readonly MethodInfo GuidEqualsMethod = typeof(Guid).GetMethod("Equals", new Type[] { typeof(Guid) });
		public static readonly MethodInfo GuidNewGuid = typeof(Guid).GetMethod("NewGuid", BindingFlags.Public | BindingFlags.Static);
		public static readonly MethodInfo StringExtensionsIsLikeMethodInfo = typeof(ShaolinqStringExtensions).GetMethod("IsLike", BindingFlags.Static | BindingFlags.Public);
		public static readonly MethodInfo StringSubstring= typeof(String).GetMethod("Substring", BindingFlags.Static | BindingFlags.Public);
		public static readonly MethodInfo ObjectToStringMethod = typeof(object).GetMethod("ToString", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
		public static readonly MethodInfo EnumToObjectMethod = typeof(Enum).GetMethod("ToObject", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(Type), typeof(int) }, null);
		public static readonly MethodInfo EnumerableFirstMethod = typeof(Enumerable).GetMethods().First(c => c.Name == "First" && c.GetParameters().Length == 1);
		public static readonly MethodInfo StringStaticEqualsMethod = typeof(string).GetMethod("Equals", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
		public static readonly MethodInfo ObjectEqualsMethod = typeof(object).GetMethod("Equals", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(object) }, null);
		public static readonly MethodInfo ObjectStaticEqualsMethod = typeof(object).GetMethod("Equals", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(object), typeof(object) }, null);
		public static readonly MethodInfo StringConcatMethod2 = typeof(string).GetMethod("Concat", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string) }, null);
		public static readonly MethodInfo StringConcatMethod3 = typeof(string).GetMethod("Concat", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string), typeof(string) }, null);
		public static readonly MethodInfo StringConcatMethod4 = typeof(string).GetMethod("Concat", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(string), typeof(string), typeof(string) }, null);
		public static readonly MethodInfo TypeGetTypeFromHandle = typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(RuntimeTypeHandle) }, null);
		public static readonly MethodInfo ConvertChangeTypeMethod = typeof(Convert).GetMethod("ChangeType", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(object), typeof(Type) }, null);
		
		static MethodInfoFastRef()
		{
			foreach (var method in typeof(Queryable).GetMethods().Filter(c => c.Name == "Where"))
			{
				var parameters = method.GetParameters();
				
				if (parameters.Length == 2)
				{
					var genericargs = parameters[1].ParameterType.GetGenericArguments();

					if (genericargs.Length == 1)
					{
						if (genericargs[0].GetGenericArguments().Length == 2)
						{
							QueryableWhereMethod = method;

							break;
						}
					}
				}
			}
		}
	}
}