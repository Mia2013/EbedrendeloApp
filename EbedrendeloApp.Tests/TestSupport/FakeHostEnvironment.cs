using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace EbedrendeloApp.Tests.TestSupport;

/// <summary>Környezet-stub azokhoz a komponensekhez, amelyek fejlesztői/éles módban máshogy
/// viselkednek (pl. a <c>Home</c> dev-felhasználóváltója).</summary>
public sealed class FakeHostEnvironment(string environmentName) : IWebHostEnvironment
{
    public static FakeHostEnvironment Development => new("Development");

    public static FakeHostEnvironment Production => new("Production");

    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "EbedrendeloApp.Tests";
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
