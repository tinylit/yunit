using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace yunit.api.tests.Controllers
{
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> logger;

        public TestController(ILogger<TestController> logger)
        {
            this.logger = logger;
        }

        public int Multi(int x, int y) => x * y;
    }
}
