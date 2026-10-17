using System.Net;
using System.Net.Http;
using System.Text;
using DataForge.Core;
using DataForge.Core.Http;
using FluentAssertions;
using Xunit;

namespace DataForge.Core.Tests.Http;

public class RestApiHttpTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var json = "[{\"Id\":1,\"Name\":\"a\"},{\"Id\":2,\"Name\":\"b\"}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task FromRestApi_UsesHeaders_AndDeserializesArray()
    {
        var handler = new StubHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        var result = await new DataForgePipeline()
            .FromRestApi<Item>(client, "items", new RestApiSourceOptions
            {
                Headers = new Dictionary<string, string> { ["X-Test"] = "1" },
                UseStreaming = true
            })
            .ToListAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("a");
        handler.LastRequest.Should().NotBeNull();
        handler.LastRequest!.Headers.GetValues("X-Test").Should().ContainSingle("1");
    }

    [Fact]
    public async Task RestApiSource_Dispose_OnlyDisposesOwnedClient()
    {
        var handler = new StubHandler();
        using var external = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var owned = new RestApiSource<Item>(null, new RestApiSourceOptions
        {
            BaseUrl = "https://example.test/",
            Endpoint = "items"
        });
        var borrowed = new RestApiSource<Item>(external, new RestApiSourceOptions { Endpoint = "items" });

        owned.Dispose();
        borrowed.Dispose();

        // External client must remain usable after borrowed source dispose.
        using var probe = await external.GetAsync("items");
        probe.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
