using Xunit;
using Yunit;

namespace yunit.v2.tests
{
    public interface IDependency
    {
    }

    public class DependencyTests : IDependency { }

    //[Startup(typeof(YStartup<TestStartup>))]//? 可以指定，未指定使用默认。
    [Startup]
    public class Tests
    {
        private readonly IDependency dependency;

        public Tests(IDependency dependency, byte[] buffer)
        {
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
