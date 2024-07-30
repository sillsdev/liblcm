using Moq;

namespace Rhino.Mocks
{
    public static class MockRepository
    {
	    public static T GenerateStub<T>() where T : class
        {
            return GenerateMock<T>();
        }

        public static T GenerateStrictMock<T>() where T : class
        {
            return GenerateMock<T>(MockBehavior.Strict);
        }

        public static T GenerateMock<T>(MockBehavior behavior = MockBehavior.Default) where T : class
        {
            var mock = new Mock<T>(behavior);
			return mock.Object;
        }
    }
}