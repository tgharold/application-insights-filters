using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Moq;
using RimDev.ApplicationInsights.Filters.Processors;
using Xunit;

namespace RimDev.ApplicationInsights.Filters.Tests.Processors
{
    public class RedactQueryStringValueTelemetryProcessorTests
    {
        [Fact]
        public void RedactTelemetryItem_handles_null_options()
        {
            ITelemetry item = CreateTelemetryItemFromAbsoluteUrl("https://example.com/");
            var sut = CreateSut(null);

            sut.RedactTelemetryItem(item);

            var result = ((RequestTelemetry) item).Url;
            Assert.NotNull(result);
        }

        [Fact]
        public void RedactTelemetryItem_handles_null_keys()
        {
            ITelemetry item = CreateTelemetryItemFromAbsoluteUrl("https://example.com/");
            var options = new RedactQueryStringValueTelemetryProcessorOptions
            {
                Keys = null,
            };
            var sut = CreateSut(options);

            sut.RedactTelemetryItem(item);

            var result = ((RequestTelemetry) item).Url;
            Assert.NotNull(result);
        }

        [Fact]
        public void RedactTelemetryItem_skips_over_DependencyTelemetry()
        {
            // https://docs.microsoft.com/en-us/dotnet/api/microsoft.applicationinsights.channel.itelemetry?view=azure-dotnet
            ITelemetry item = new DependencyTelemetry();
            var sut = CreateSut(null);

            sut.RedactTelemetryItem(item);

            // make sure item did not get null'd out, or changed to a different type, and no exceptions
            Assert.NotNull(item);
            Assert.IsType<DependencyTelemetry>(item);
        }

        [Fact]
        public void RedactTelemetryItem_skips_over_EventTelemetry()
        {
            ITelemetry item = new EventTelemetry();
            var sut = CreateSut(null);

            sut.RedactTelemetryItem(item);

            Assert.NotNull(item);
            Assert.IsType<EventTelemetry>(item);
        }

        [Fact]
        public void RedactTelemetryItem_skips_over_ExceptionTelemetry()
        {
            ITelemetry item = new ExceptionTelemetry();
            var sut = CreateSut(null);

            sut.RedactTelemetryItem(item);

            Assert.NotNull(item);
            Assert.IsType<ExceptionTelemetry>(item);
        }

        [Theory]
        [InlineData(
            "http://www1.example.com/",
            "http://www1.example.com/"
            )]
        [InlineData(
            "https://www2.example.com:8081/?s=abc123",
            "https://www2.example.com:8081/?s=abc123"
            // caller passed in zero argument names
            )]
        [InlineData( // arguments get sorted
            "https://www2.example.com:8081/?a=a&b=b&c=c&s=abc123&z=z",
            "https://www2.example.com:8081/?s=abc123&z=z&c=c&a=a&b=b"
            // caller passed in zero argument names
            )]
        [InlineData(
            "https://www3.example.com:81/?s=REDACTED",
            "https://www3.example.com:81/?s=abc",
            "s"
            )]
        [InlineData(
            "https://www4.example.com/?s=REDACTED",
            "https://www4.example.com/?s=abc",
            "S" // even with different case, the "s" param gets redacted
            )]
        [InlineData(
            "https://www5.example.com/?s=REDACTED",
            "https://www5.example.com/?s=abc",
            "S", "s", "s" // caller passed in multiples
            )]
        [InlineData( // arguments get put in alphabetical (Ordinal) order
            "https://www6.example.com/?s=REDACTED&SECRET=REDACTED&Zid=63643",
            "https://www6.example.com/?Zid=63643&s=abc&SECRET=xyz",
            "s", "secret"
            )]
        [InlineData(
            "https://www7.example.com:8081/?id=173&s=REDACTED&secret=REDACTED",
            "https://www7.example.com:8081/?id=173&secret=xyz&s=abc",
            "S", "SECRET" // case does not matter, for multiple argument names
            )]
        [InlineData(
            // mixed-case argument names get collapsed into same-case (it use the first for the others)
            "https://www8.example.com:8081/?id=23122&s=REDACTED&secret=REDACTED&secret=REDACTED&secret=REDACTED",
            "https://www8.example.com:8081/?id=23122&secret=xyz&s=abc&secret=21389382&SECRET=J235",
            "s", "secret"
            )]
        [InlineData(
            // mixed-case argument names get collapsed into same-case (it use the first for the others)
            "https://www8.example.com:8081/?id=23124&s=REDACTED&SeCRet=REDACTED&SeCRet=REDACTED&SeCRet=REDACTED",
            "https://www8.example.com:8081/?id=23124&SeCRet=xyz&s=abc&secret=21389382&SECRET=J235",
            "s", "secret"
            )]
        public void RedactTelemetryItem_mutates_absolute_URL_item_correctly(
            string expectedAbsoluteUrl,
            string inputAbsoluteUrl,
            params string[] names
            )
        {
            ITelemetry item = CreateTelemetryItemFromAbsoluteUrl(inputAbsoluteUrl);
            var options = new RedactQueryStringValueTelemetryProcessorOptions
            {
                Keys = names,
            };
            var sut = CreateSut(options);

            sut.RedactTelemetryItem(item);

            var result = ((RequestTelemetry) item).Url;
            Assert.Equal(expectedAbsoluteUrl, result.AbsoluteUri);
        }

        [Theory]
        [InlineData(
            "/",
            "/"
            )]
        [InlineData(
            "/?s=abc123",
            "/?s=abc123"
            // caller passed in zero argument names
            )]
        [InlineData(
            "/?s=abc",
            "/?s=abc",
            "s"
            )]
        [InlineData(
            "/?s=abc",
            "/?s=abc",
            "S" // even with different case, the "s" param gets redacted
            )]
        [InlineData(
            "/?s=abc",
            "/?s=abc",
            "S", "s", "s" // caller passed in multiples
            )]
        [InlineData(
            "/?id=173&secret=xyz&s=abc",
            "/?id=173&secret=xyz&s=abc",
            "S", "SECRET" // case does not matter, for multiple argument names
            )]
        public void RedactTelemetryItem_does_not_mutate_relative_URL_item(
            string expectedRelativeUrl,
            string inputRelativeUrl,
            params string[] names
            )
        {
            ITelemetry item = CreateTelemetryItemFromRelativeUrl(inputRelativeUrl);
            var options = new RedactQueryStringValueTelemetryProcessorOptions
            {
                Keys = names,
            };
            var sut = CreateSut(options);

            sut.RedactTelemetryItem(item);

            var result = ((RequestTelemetry) item).Url;
            Assert.Equal(expectedRelativeUrl, result.ToString());
        }

        [Theory]
        [InlineData(
            // mixed-case argument names get collapsed into same-case (it use the first for the others)
            "https://www8.example.com:8081/?s=HIDDEN&SECRET=HIDDEN&SECRET=HIDDEN&SECRET=HIDDEN&ZZid=724",
            "https://www8.example.com:8081/?ZZid=724&SECRET=xyz&s=abc&secret=21389382&SECRET=J235",
            "s", "secret"
            )]
        public void RedactTelemetryItem_uses_RedactedValue_from_options_for_absolute_URL(
            string expectedAbsoluteUrl,
            string inputAbsoluteUrl,
            params string[] names
            )
        {
            ITelemetry item = CreateTelemetryItemFromAbsoluteUrl(inputAbsoluteUrl);
            var options = new RedactQueryStringValueTelemetryProcessorOptions
            {
                Keys = names,
                RedactedValue = "HIDDEN"
            };
            var sut = CreateSut(options);

            sut.RedactTelemetryItem(item);

            var result = ((RequestTelemetry) item).Url;
            Assert.Equal(expectedAbsoluteUrl, result.AbsoluteUri);
        }

        private static RedactQueryStringValueTelemetryProcessor CreateSut(
            RedactQueryStringValueTelemetryProcessorOptions options = null
            )
        {
            var telemetryProcessorMock = new Mock<ITelemetryProcessor>(MockBehavior.Loose);
            options ??= new RedactQueryStringValueTelemetryProcessorOptions();
            var sut = new RedactQueryStringValueTelemetryProcessor(
                telemetryProcessorMock.Object,
                options
                );
            return sut;
        }

        private static RequestTelemetry CreateTelemetryItemFromAbsoluteUrl(string inputAbsoluteUrl)
        {
            return new RequestTelemetry
            {
                Url = new Uri(inputAbsoluteUrl, UriKind.Absolute),
            };
        }

        private static RequestTelemetry CreateTelemetryItemFromRelativeUrl(string inputRelativeUrl)
        {
            return new RequestTelemetry
            {
                Url = new Uri(inputRelativeUrl, UriKind.Relative),
            };
        }
    }
}
