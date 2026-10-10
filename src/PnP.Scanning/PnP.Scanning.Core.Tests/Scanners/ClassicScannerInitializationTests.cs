using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PnP.Core.Services;
using PnP.Scanning.Core.Scanners;
using Xunit;

namespace PnP.Scanning.Core.Tests.Scanners
{
    public class ClassicScannerInitializationTests
    {
        [Theory]
        [InlineData(true, false, false, false)]
        [InlineData(true, true, true, true)]
        [InlineData(false, true, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        public async Task Initialization_WithoutWorkflow_DoesNotExpandSiteRootWeb(
            bool pages, bool infoPath, bool lists, bool extensibility)
        {
            var requests = new List<Uri>();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPnPCore(options => options.DisableTelemetry = true, configureSharePointRest: builder =>
                builder.AddHttpMessageHandler(() => new InitializationHandler(requests)));
            using var provider = services.BuildServiceProvider();
            var factory = provider.GetRequiredService<IPnPContextFactory>();
            var options = ClassicScanner.CreateContextOptions(new ClassicOptions
            {
                Pages = pages,
                InfoPath = infoPath,
                Lists = lists,
                Extensibility = extensibility,
            });

            using var context = await factory.CreateAsync(new Uri(InitializationHandler.SiteUrl),
                new FixtureAuthentication(), options: options);

            var siteRequest = requests.Single(r => r.AbsolutePath.EndsWith("/_api/Site"));
            Uri.UnescapeDataString(siteRequest.Query).Should().NotContain("RootWeb");
            requests.Should().Contain(r => r.AbsolutePath.EndsWith("/_api/Web"));
            if (pages)
            {
                Uri.UnescapeDataString(siteRequest.Query).Should().Contain("Features");
            }
            if (extensibility)
            {
                Uri.UnescapeDataString(siteRequest.Query).Should().Contain("UserCustomActions");
            }
        }

        [Fact]
        public void Initialization_Workflow_PreservesRequiredRootContentTypes()
        {
            var options = ClassicScanner.CreateContextOptions(new ClassicOptions { Workflow = true });
            options.AdditionalSitePropertiesOnCreate.Select(p => p.ToString())
                .Should().ContainSingle(p => p.Contains("RootWeb") && p.Contains("ContentTypes"));
        }

        private sealed class FixtureAuthentication : IAuthenticationProvider
        {
            public Task AuthenticateRequestAsync(Uri resource, HttpRequestMessage request) => Task.CompletedTask;
            public Task<string> GetAccessTokenAsync(Uri resource) => Task.FromResult("fixture");
            public Task<string> GetAccessTokenAsync(Uri resource, string[] scopes) => Task.FromResult("fixture");
        }

        private sealed class InitializationHandler(List<Uri> requests) : DelegatingHandler
        {
            internal const string SiteUrl = "https://contoso.sharepoint.com/sites/classic";

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                requests.Add(request.RequestUri);
                string body;
                if (request.RequestUri.AbsolutePath.EndsWith("/_api/Web"))
                {
                    body = """
                        {"odata.metadata":"https://contoso.sharepoint.com/sites/classic/_api/$metadata#SP.Web",
                         "Id":"c9e3f59c-95a0-4bf9-934a-0ee5b3d039b1","Url":"https://contoso.sharepoint.com/sites/classic",
                         "LastItemUserModifiedDate":"2026-10-10T00:00:00Z","Lists":[],"Features":[],"UserCustomActions":[]}
                        """;
                }
                else if (request.RequestUri.AbsolutePath.EndsWith("/_api/Site"))
                {
                    // Reproduce the observed endpoint failure if a regression restores the expansion.
                    if (Uri.UnescapeDataString(request.RequestUri.Query).Contains("RootWeb"))
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                        {
                            Content = new StringContent("""
                                {"odata.error":{"code":"-2147467261, System.ArgumentNullException",
                                 "message":{"lang":"en-US","value":"Value cannot be null. Parameter name: target"}}}
                                """, Encoding.UTF8, "application/json")
                        });
                    }
                    body = """
                        {"odata.metadata":"https://contoso.sharepoint.com/sites/classic/_api/$metadata#SP.Site",
                         "Id":"772375b3-381a-4015-bec9-bb031ad89fa3","GroupId":"00000000-0000-0000-0000-000000000000",
                         "Features":[],"UserCustomActions":[]}
                        """;
                }
                else
                {
                    throw new InvalidOperationException($"Unexpected offline request: {request.RequestUri}");
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
