// Copyright (c) 2026 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)

using System;
using System.Reflection;
using NUnit.Framework;
using SIL.LCModel;
using SIL.LCModel.Core.KernelInterfaces;

namespace SIL.LCModel.Infrastructure.Impl
{
	[TestFixture]
	public class UnitOfWorkServiceTests : MemoryOnlyBackendProviderTestBase
	{
		[Test]
		public void SaveOnIdle_UiCleared_DoesNotThrow()
		{
			var uowService = Cache.ServiceLocator.GetInstance<IUnitOfWorkService>();
			var serviceInstance = (object)uowService;

			InvokeNonPublicVoid(serviceInstance, "StopSaveTimer");
			SetNonPublicField(serviceInstance, "m_ui", null);

			Assert.DoesNotThrow(() =>
				InvokeNonPublicVoid(serviceInstance, "SaveOnIdle", null, null));
		}

		private static void SetNonPublicField(object instance, string fieldName, object value)
		{
			var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
			if (field == null)
				Assert.Fail("Field not found: " + fieldName);
			field.SetValue(instance, value);
		}

		private static void InvokeNonPublicVoid(object instance, string methodName, params object[] args)
		{
			var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			if (method == null)
				Assert.Fail("Method not found: " + methodName);
			method.Invoke(instance, args);
		}
	}
}
