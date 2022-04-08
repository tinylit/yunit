using Xunit;
using Yunit;

namespace yunit.v2.tests
{
    public interface IDependency
    {
    }

    public interface IDependency<T> where T : new()
    {
    }

    public class DependencyTests : IDependency { }

    public class DependencyTests<T> : IDependency<T> where T : struct { }

    public class DependencyModel
    {

    }

    //[Startup(typeof(YStartup<TestStartup>))]//? 可以指定，未指定使用默认。
    [Startup]
    public class Tests
    {
        private readonly IDependency<DependencyModel> dependency1;
        private readonly IDependency dependency;

        public Tests(IDependency<DependencyModel> dependency1, IDependency dependency, byte[] buffer)
        {
            this.dependency1 = dependency1;
            this.dependency = dependency;
        }

        [Theory]
        [InlineData(1)]
        public void Test(int i)
        {
            int actual = 1;

            Assert.Equal(i, actual);
        }
    }
}
