using Microsoft.Owin;
using Owin;
using SampleServer;

[assembly: OwinStartup(typeof(Startup))]
namespace SampleServer
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
