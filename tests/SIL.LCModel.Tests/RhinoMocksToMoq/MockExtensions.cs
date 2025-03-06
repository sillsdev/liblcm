using System;
using System.Linq.Expressions;
using Moq;

namespace SIL.LCModel.Tests.Rhino.Mocks
{
    public static class MockExtensions
    {
        public static IExpect<T, TR> Expect<T, TR>(this T obj, Expression<Func<T, TR>> expression) where T : class
        {
            return Stub(obj, expression);
        }

        public static IExpect<T> Expect<T>(this T obj, Expression<Action<T>> expression) where T : class
        {
            return Stub(obj, expression);
        }

        public static IExpect<T, TR> Stub<T, TR>(this T obj, Expression<Func<T, TR>> expression) where T : class
        {
            return new Expect<T, TR>(Mock.Get(obj), expression);
        }

        public static IExpect<T> Stub<T>(this T obj, Expression<Action<T>> expression) where T : class
        {
            return new Expect<T>(Mock.Get(obj), expression);
        }

        public static void AssertWasNotCalled<T>(this T obj, Expression<Action<T>> expression) where T : class
        {
            Mock.Get(obj).Verify(expression, Times.Never);
        }

        public static void VerifyAllExpectations<T>(this T obj) where T : class
        {
            Mock.Get(obj).Verify();
        }
    }
}