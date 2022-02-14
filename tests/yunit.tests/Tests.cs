using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using Xunit;
using yunit.api.tests;
using yunit.api.tests.Controllers;
using Yunit;

namespace yunit.tests
{
    [HostStartup(typeof(Startup))]
    public class Tests
    {
        private readonly TestController controller;

        public Tests(TestController controller)
        {
            this.controller = controller;
        }

        [Theory]
        [InlineData(1)]
        public void Test(int i)
        {
            int actual = 1;

            Assert.Equal(i, actual);

            int r = controller.Multi(i, 10);

            Assert.Equal(r, i * 10);
        }
    }
}
