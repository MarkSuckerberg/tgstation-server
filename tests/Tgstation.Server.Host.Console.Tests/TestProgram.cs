using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;

namespace Tgstation.Server.Host.Console.Tests
{
	[TestClass]
	public class TestProgram
	{
		[TestMethod]
		public async Task TestProgramRuns()
		{
			var mockServer = new Mock<IServer>();
			mockServer.Setup(x => x.RunAsync()).Returns(Task.CompletedTask).Verifiable();
			var mockServerFactory = new Mock<IServerFactory>();
			mockServerFactory.Setup(x => x.CreateServer()).Returns(mockServer.Object).Verifiable();
			Program.ServerFactory = mockServerFactory.Object;
			await Program.Main().ConfigureAwait(false);
			mockServer.VerifyAll();
			mockServerFactory.VerifyAll();
		}
	}
}
