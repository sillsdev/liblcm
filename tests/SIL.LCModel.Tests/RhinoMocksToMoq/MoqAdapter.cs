using System;
using System.Linq.Expressions;
using Moq;

namespace SIL.LCModel.Tests.Rhino.Mocks
{
	class MoqAdapter<T, TR> where T : class
	{
		private readonly Mock<T> _mock;
		private readonly Expression<Func<T, TR>> _expression;

		public MoqAdapter(Mock<T> mock, Expression<Func<T, TR>> expression)
		{
			_mock = mock;
			_expression = expression;
		}

		public void Setup(TR result)
		{
			if (result != null)
			{
				_mock.Setup(_expression).Returns(result);
			}
		}

		public void Setup(Func<TR> result)
		{
			if (result != null)
			{
				_mock.Setup(_expression).Returns(result);
			}
		}

		public void SetupReturnInOrder(params TR[] results)
		{
			_mock.Setup(_expression).ReturnsInOrder(results);
		}

		public void Throws(Exception exception)
		{
			_mock.Setup(_expression).Throws(exception);
		}
	}

	class MoqAdapter<T> where T : class
	{
		private readonly Mock<T> _mock;
		private readonly Expression<Action<T>> _expression;

		public MoqAdapter(Mock<T> mock, Expression<Action<T>> expression)
		{
			_mock = mock;
			_expression = expression;
		}

		public void Throws(Exception exception)
		{
			_mock.Setup(_expression).Throws(exception);
		}
	}
}