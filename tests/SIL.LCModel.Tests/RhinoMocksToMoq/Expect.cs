using System;
using System.Linq.Expressions;
using Moq;

namespace Rhino.Mocks
{
    public interface IExpect<T> where T : class
    {
        void Throw(Exception exception);
    }

    public interface IExpect<T, TR> where T : class
    {
	    IExpect<T, TR> Do(Func<TR> action);

        IExpect<T, TR> Return(TR result);

        void ReturnInOrder(params TR[] results);

        void Throw(Exception exception);
    }

    public class Expect<T, TR> : IExpect<T, TR> where T : class
    {
        private readonly MoqAdapter<T, TR> _moqAdapter;

        private bool _isResultAssigned;

        public Expect(Mock<T> mock, Expression<Func<T, TR>> expression)
        {
            _moqAdapter = new MoqAdapter<T, TR>(mock, expression);
        }

        public IExpect<T, TR> Do(Func<TR> action)
        {
			_moqAdapter.Setup(action);
	        return this;
        }

        public IExpect<T, TR> Return(TR result)
        {
            if (_isResultAssigned)
            {
                throw new InvalidOperationException("Return should be setup only once");
            }

            _isResultAssigned = true;

            _moqAdapter.Setup(result);
            return this;
        }

        public void Throw(Exception exception)
        {
            _moqAdapter.Throws(exception);
        }

        public void ReturnInOrder(params TR[] results)
        {
            _moqAdapter.SetupReturnInOrder(results);
        }
    }

    public class Expect<T> : IExpect<T> where T : class
    {
        private readonly MoqAdapter<T> _mockAdapter;

        public Expect(Mock<T> mock, Expression<Action<T>> expression)
        {
            _mockAdapter = new MoqAdapter<T>(mock, expression);
        }

        public void Throw(Exception exception)
        {
            _mockAdapter.Throws(exception);
        }
    }
}