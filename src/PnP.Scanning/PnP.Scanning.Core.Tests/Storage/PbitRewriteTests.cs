using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PnP.Scanning.Core.Services;
using Xunit;

namespace PnP.Scanning.Core.Tests.Storage
{
    /// <summary>
    /// Verifies the shipped Power BI template schema and the <c>.pbit</c> data-location rewrite
    /// (<see cref="ReportManager.RewriteDataLocationsInPbit"/>). The rewrite is what makes the
    /// generated report point at the freshly exported CSVs instead of the hard-coded authoring
    /// directory the template was created against. Visual rendering remains a manual Power BI check;
    /// schema contracts and path/delimiter rewriting are exercised here.
    /// </summary>
    public class PbitRewriteTests
    {
        // The literal directory prefix the Classic template references at authoring time, exactly as
        // passed by ReportManager.CreatePowerBiReportAsync. Inside the DataModelSchema's JSON-escaped M
        // code the backslashes are doubled, so the runtime string value carries doubled backslashes too.
        private const string OldLocation =
            "q:\\\\github\\\\pnpassessment\\\\src\\\\PnP.Scanning\\\\Reports\\\\Classic\\\\";

        [Fact]
        public void ClassicTemplate_PropertiesValue_IsText()
        {
            const string resourceName = "PnP.Scanning.Core.Scanners.Classic.ClassicAssessmentReport.pbit";
            using var pbit = typeof(ReportManager).Assembly.GetManifestResourceStream(resourceName);
            pbit.Should().NotBeNull();

            var schema = ReadDataModelSchema(pbit);
            using var document = JsonDocument.Parse(schema);
            var tables = document.RootElement.GetProperty("model").GetProperty("tables");
            var properties = tables.EnumerateArray()
                .Single(table => table.GetProperty("name").GetString() == "properties");
            var value = properties.GetProperty("columns")
                .EnumerateArray()
                .Single(column => column.GetProperty("name").GetString() == "Value");

            value.GetProperty("dataType").GetString().Should().Be("string");
            value.TryGetProperty("formatString", out _).Should().BeFalse();

            var expression = string.Join(
                "\n",
                properties.GetProperty("partitions")[0]
                    .GetProperty("source")
                    .GetProperty("expression")
                    .EnumerateArray()
                    .Select(line => line.GetString()));
            expression.Should().Contain("{\"Value\", type text}");
            expression.Should().NotContain("{\"Value\", type logical}");
        }

        [Fact]
        public void ClassicTemplate_UsesInvariantCsvImportCulture()
        {
            using var document = ReadEmbeddedClassicTemplate();

            // ReportManager exports CSV values using InvariantCulture (MM/dd/yyyy dates).
            document.RootElement.GetProperty("model")
                .GetProperty("sourceQueryCulture")
                .GetString()
                .Should()
                .Be("en-US");
        }

        [Theory]
        [InlineData("scans", "StartDate")]
        [InlineData("scans", "EndDate")]
        [InlineData("sitecollections", "StartDate")]
        [InlineData("sitecollections", "EndDate")]
        [InlineData("history", "EventDate")]
        [InlineData("webs", "StartDate")]
        [InlineData("webs", "EndDate")]
        [InlineData("classicpages", "ModifiedAt")]
        public void ClassicTemplate_StandardDateColumns_ParseWithExplicitCulture(string tableName, string columnName)
        {
            using var document = ReadEmbeddedClassicTemplate();
            var table = GetClassicTable(document, tableName);
            var column = table.GetProperty("columns")
                .EnumerateArray()
                .Single(value => value.GetProperty("name").GetString() == columnName);

            column.GetProperty("dataType").GetString().Should().Be("dateTime");

            // Assert the culture on the actual conversion, not just elsewhere in the query/model.
            var conversion = GetQueryLines(table)
                .Single(line => line.Contains($"{{\"{columnName}\", type datetime}}", StringComparison.Ordinal));
            conversion.Should().Contain("Table.TransformColumnTypes(");
            conversion.TrimEnd().TrimEnd(',').Should().EndWith(", \"en-US\")");
        }

        [Fact]
        public void ClassicTemplate_ClassicPages_PreservesQuotedLineBreaks()
        {
            using var document = ReadEmbeddedClassicTemplate();
            var source = GetQueryLines(GetClassicTable(document, "classicpages"))
                .Single(line => line.Contains("Csv.Document(", StringComparison.Ordinal));

            source.Should().MatchRegex(@"QuoteStyle\s*=\s*QuoteStyle\.Csv");
            source.Should().NotContain("QuoteStyle.None");
        }

        [Fact]
        public void ClassicTemplate_ClassicPages_NormalizesEmptyDatesBeforeConversion()
        {
            using var document = ReadEmbeddedClassicTemplate();
            var lines = GetQueryLines(GetClassicTable(document, "classicpages")).ToArray();
            var emptyReplacement = Array.FindIndex(
                lines,
                line => line.Contains("Table.ReplaceValue(", StringComparison.Ordinal)
                    && line.Contains("\"\",null,Replacer.ReplaceValue", StringComparison.Ordinal)
                    && line.Contains("\"ModifiedAt\"", StringComparison.Ordinal));
            var conversion = Array.FindIndex(
                lines,
                line => line.Contains("{\"ModifiedAt\", type datetime}", StringComparison.Ordinal));

            emptyReplacement.Should().BeGreaterThanOrEqualTo(0);
            conversion.Should().BeGreaterThan(emptyReplacement);
        }

        [Fact]
        public void ClassicTemplate_AuthoringCopy_MatchesEmbeddedSchema()
        {
            const string resourceName = "PnP.Scanning.Core.Scanners.Classic.ClassicAssessmentReport.pbit";
            using var embedded = typeof(ReportManager).Assembly.GetManifestResourceStream(resourceName);
            embedded.Should().NotBeNull();
            var authoringCopy = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ClassicAssessmentReport.pbit");

            File.Exists(authoringCopy).Should().BeTrue();
            ReadDataModelSchema(authoringCopy).Should().Be(ReadDataModelSchema(embedded));
        }

        /// <summary>Both shipped Classic templates use the supported page CSV independently of the retired projection.</summary>
        [Fact]
        public void ClassicTemplates_UseSupportedPageCsv()
        {
            const string resourceName = "PnP.Scanning.Core.Scanners.Classic.ClassicAssessmentReport.pbit";
            using Stream embedded = typeof(ReportManager).Assembly.GetManifestResourceStream(resourceName);
            embedded.Should().NotBeNull();
            string authoringCopy = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ClassicAssessmentReport.pbit");
            string[] schemas = { ReadDataModelSchema(embedded), ReadDataModelSchema(authoringCopy) };
            foreach (string schema in schemas)
            {
                schema.Should()
                    .Contain("classicpages.csv")
                    .And.NotContain("classicaspx.csv")
                    .And.NotContain("classicaspxsummary.json");
            }
        }

        /// <summary>The ASPX tab reads the physical inventory as UTF-8, keeps quoted line breaks and decodes the null token.</summary>
        [Fact]
        public void ClassicTemplate_Aspx_ReadsPhysicalInventoryCsv()
        {
            using var document = ReadEmbeddedClassicTemplate();
            var lines = GetQueryLines(GetClassicTable(document, "aspx")).ToArray();
            var source = lines.Single(line => line.Contains("Csv.Document(", StringComparison.Ordinal));

            source.Should().Contain("aspx.csv").And.Contain("Encoding=65001");
            source.Should().MatchRegex(@"QuoteStyle\s*=\s*QuoteStyle\.Csv");
            lines.Any(line => line.Contains("\"\\N\",null", StringComparison.Ordinal)).Should().BeTrue();
        }

        /// <summary>The Pages tab reads the audit usage counts from the supported page CSV as whole numbers.</summary>
        [Theory]
        [InlineData("AuditViewsCount")]
        [InlineData("AuditUniqueUsers")]
        public void ClassicTemplate_ClassicPages_LoadsAuditUsage(string columnName)
        {
            using var document = ReadEmbeddedClassicTemplate();
            var table = GetClassicTable(document, "classicpages");
            var column = table.GetProperty("columns")
                .EnumerateArray()
                .Single(value => value.GetProperty("name").GetString() == columnName);

            column.GetProperty("dataType").GetString().Should().Be("int64");
            GetQueryLines(table)
                .Any(line => line.Contains($"{{\"{columnName}\", Int64.Type}}", StringComparison.Ordinal))
                .Should()
                .BeTrue();
        }

        /// <summary>The report has an ASPX tab, and its Pages table shows audit usage in place of the remediation code.</summary>
        [Fact]
        public void ClassicTemplate_Layout_HasAspxTabAndPagesAuditColumns()
        {
            const string resourceName = "PnP.Scanning.Core.Scanners.Classic.ClassicAssessmentReport.pbit";
            using var pbit = typeof(ReportManager).Assembly.GetManifestResourceStream(resourceName);
            pbit.Should().NotBeNull();
            using var archive = new ZipArchive(pbit, ZipArchiveMode.Read);
            var entry = archive.GetEntry("Report/Layout");
            entry.Should().NotBeNull();
            using var reader = new StreamReader(entry.Open(), Encoding.Unicode);
            using var layout = JsonDocument.Parse(reader.ReadToEnd());

            var sections = layout.RootElement.GetProperty("sections").EnumerateArray().ToArray();
            sections.Select(section => section.GetProperty("displayName").GetString()).Should().Contain("ASPX");
            var pages = sections.Single(section => section.GetProperty("displayName").GetString() == "Pages");
            var visuals = string.Concat(
                pages.GetProperty("visualContainers")
                    .EnumerateArray()
                    .Select(visual => visual.GetProperty("config").GetString()));

            visuals.Should()
                .Contain("classicpages.AuditViewsCount")
                .And.Contain("classicpages.AuditUniqueUsers")
                .And.Contain("MC1464926")
                .And.NotContain("RemediationCode");
        }

        [Fact]
        public void RewriteDataLocationsInPbit_NewCsvSources_PathsRewritten()
        {
            var dir = NewTempDir();
            try
            {
                // A fixture DataModelSchema referencing the existing page CSV plus the two new
                // T11 page-scan sources, all under the authoring-time directory prefix.
                var schema = string.Join(
                    "\r\n",
                    new[]
                    {
                        SourceLine("classicpages.csv", "11"), SourceLine("classicpagewebparts.csv", "15"),
                        SourceLine("classicwebpartunique.csv", "4"),
                    });

                var pbit = BuildFixturePbit(dir, schema);

                // Rewrite exactly as the Classic branch of CreatePowerBiReportAsync does: same old
                // location, delimiter unchanged (",").
                ReportManager.RewriteDataLocationsInPbit(pbit, ",", OldLocation, ",");

                var rewritten = ReadDataModelSchema(pbit);

                // The hard-coded authoring directory prefix is gone...
                rewritten.Should().NotContain("q:\\\\github");

                // ...replaced by the .pbit's own directory (backslashes doubled, trailing separator),
                // and the new CSV sources now resolve against that export directory.
                var newPrefix = dir.Replace("\\", "\\\\") + "\\\\";
                rewritten.Should().Contain(newPrefix + "classicpages.csv");
                rewritten.Should().Contain(newPrefix + "classicpagewebparts.csv");
                rewritten.Should().Contain(newPrefix + "classicwebpartunique.csv");
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        [Fact]
        public void RewriteDataLocationsInPbit_NewDelimiter_DelimiterRewritten()
        {
            var dir = NewTempDir();
            try
            {
                var schema = SourceLine("classicpagewebparts.csv", "15");
                var pbit = BuildFixturePbit(dir, schema);

                // Caller chose ";" as the export delimiter; the template was authored with ",".
                ReportManager.RewriteDataLocationsInPbit(pbit, ";", OldLocation, ",");

                var rewritten = ReadDataModelSchema(pbit);

                rewritten.Should().Contain("Delimiter=\\\";\\\"");
                rewritten.Should().NotContain("Delimiter=\\\",\\\"");
            }
            finally
            {
                DeleteTempDir(dir);
            }
        }

        // One DataModelSchema M "Source" line for a CSV, mirroring the real template's JSON-escaped form:
        //   Source = Csv.Document(File.Contents("q:\\github\\...\\Classic\\<csv>"),[Delimiter=",", ...]),
        private static string SourceLine(string csv, string columns)
        {
            return $"    Source = Csv.Document(File.Contents(\\\"{OldLocation}{csv}\\\")," +
                   $"[Delimiter=\\\",\\\", Columns={columns}, Encoding=1252, QuoteStyle=QuoteStyle.None]),";
        }

        // Builds a minimal valid .pbit (a zip) in its own directory containing a single
        // "DataModelSchema" entry. The real template stores that entry as UTF-16 (Unicode), which is
        // how RewriteDataLocationsInPbit reads it back, so the fixture is written the same way.
        private static string BuildFixturePbit(string dir, string dataModelSchema)
        {
            var pbit = Path.Combine(dir, "ClassicAssessmentReport.pbit");

            using (var archive = ZipFile.Open(pbit, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("DataModelSchema");
                using var entryStream = entry.Open();
                var bytes = new UnicodeEncoding(bigEndian: false, byteOrderMark: true).GetBytes(dataModelSchema);
                entryStream.Write(bytes, 0, bytes.Length);
            }

            return pbit;
        }

        private static JsonDocument ReadEmbeddedClassicTemplate()
        {
            const string resourceName = "PnP.Scanning.Core.Scanners.Classic.ClassicAssessmentReport.pbit";
            using var pbit = typeof(ReportManager).Assembly.GetManifestResourceStream(resourceName);
            pbit.Should().NotBeNull();
            return JsonDocument.Parse(ReadDataModelSchema(pbit));
        }

        private static JsonElement GetClassicTable(JsonDocument document, string name)
        {
            return document.RootElement.GetProperty("model")
                .GetProperty("tables")
                .EnumerateArray()
                .Single(table => table.GetProperty("name").GetString() == name);
        }

        private static IEnumerable<string> GetQueryLines(JsonElement table)
        {
            return table.GetProperty("partitions")[0]
                .GetProperty("source")
                .GetProperty("expression")
                .EnumerateArray()
                .Select(line => line.GetString());
        }

        private static string ReadDataModelSchema(string pbit)
        {
            using var stream = File.OpenRead(pbit);
            return ReadDataModelSchema(stream);
        }

        private static string ReadDataModelSchema(Stream pbit)
        {
            using var archive = new ZipArchive(pbit, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry("DataModelSchema");
            entry.Should().NotBeNull();

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.Unicode);
            return reader.ReadToEnd();
        }

        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pnp-t12-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteTempDir(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup; a leaked temp dir must not fail the test.
            }
        }
    }
}