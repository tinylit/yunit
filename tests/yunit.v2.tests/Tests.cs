using Xunit;
using Yunit;

namespace yunit.v2.tests
{
    public interface IDependency
    {
    }

    public class DependencyTests : IDependency { }

    //[Startup(typeof(YStartup<TestStartup>))]//? ����ָ����δָ��ʹ��Ĭ�ϡ�
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
