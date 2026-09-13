using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PnP.Scanning.Core.Discovery;

internal sealed record AspxReviewedPlatformRegistryProfile(
    string ProfileRevision,
    string IndependentReviewRef,
    string SchemaResourceName,
    string SchemaResourceId,
    string SchemaRelativePath,
    string SchemaSha256,
    string RegistrySchemaVersion,
    string RegistryRevision,
    string RegistryCanonicalHash,
    string RegistryFileSha256,
    string AuthorityKind,
    string AuthoritySourceRef,
    string AuthorityArtifactHash,
    string ContractReviewRef,
    string CompatibilityDecisionRef,
    string PlatformFamily,
    string PlatformBuild,
    string AuthorityFingerprint,
    string EntryIdentityHash,
    int EntryCount);

internal sealed record AspxPlatformRegistryAdmissionIdentity(
    string RegistrySchemaVersion,
    string RegistryRevision,
    string RegistryCanonicalHash,
    string RegistryFileSha256,
    string AuthorityFingerprint,
    string EntryIdentityHash);

internal sealed record AspxPlatformRegistryCompatibilityRevocation(
    string RegistryRevision,
    string RegistryCanonicalHash,
    string AuthorityFingerprint,
    string DecisionRef,
    string Reason)
{
    internal bool IsValid() => !string.IsNullOrWhiteSpace(RegistryRevision) &&
        IsHash(RegistryCanonicalHash) && IsHash(AuthorityFingerprint) &&
        !string.IsNullOrWhiteSpace(DecisionRef) && !string.IsNullOrWhiteSpace(Reason);

    internal bool Matches(AspxPlatformRegistryAdmissionIdentity identity) => identity != null &&
        string.Equals(RegistryRevision, identity.RegistryRevision, StringComparison.Ordinal) &&
        string.Equals(RegistryCanonicalHash, identity.RegistryCanonicalHash, StringComparison.Ordinal) &&
        string.Equals(AuthorityFingerprint, identity.AuthorityFingerprint, StringComparison.Ordinal);

    private static bool IsHash(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit) &&
        string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);
}

internal static class AspxReviewedPlatformRegistryProfiles
{
    internal static readonly AspxReviewedPlatformRegistryProfile SpoOnline2770912001 = new(
        "assessment-aspx-registry-authority/ccd-746-r2",
        "CCD-835",
        "PnP.Scanning.Core.Discovery.RegistryAuthority.spo-online-16.0.27709.12001.registry.schema.json",
        "urn:ccd:pnp:aspx-platform-registry:spo-online-16.0.27709.12001:r1",
        "../schema/aspx-platform-registry.schema.json",
        "6a22625b33961ea20069b541b09c459d21db23db1548da4cce4a5538f9f4c32d",
        AspxAcquisitionVersions.Registry,
        "spo-online-16.0.27709.12001-r1",
        "3138b6d0b1e1af1e170801939d2c1e00793e61cb17f53c6f7d01e61cf3507830",
        "203eec0e6b1d69242589c2d430ba52c2bd3fe973c80f6659d6a2176757e5a5dc",
        "SPOCoreReleaseShippingManifest",
        "45ad38aee279ef47f54dc04dfe8eb9fe4cfe5d01",
        "8a39d64cffaebdec4ee64c9d42418e0569a525f6a533a46adba9aaf71b312691",
        "CCD-394#document-aspx-surface-applicability-denominator-v3@7d44f61d-919e-478a-ab8e-bb4c8f9b4b4a",
        "CCD-411:approve_with_changes",
        "SharePointOnline-16",
        "16.0.27709.12001",
        "1ed587eb58c40ef961e58b4b97ac5a3a7e475e444ba5df9dad05da4e578cb80c",
        "2c511a8f90fd6b6aa7a56dd6e9417e08d0f598c2ce3ff4d96e33b25e5f98cff7",
        1161);

    private static readonly IReadOnlyList<AspxReviewedPlatformRegistryProfile> Admitted =
        new[] { SpoOnline2770912001 };

    internal static readonly IReadOnlyList<AspxPlatformRegistryCompatibilityRevocation>
        CompatibilityRevocations = Array.Empty<AspxPlatformRegistryCompatibilityRevocation>();

    internal static bool TryGet(AspxPlatformRegistryAdmissionIdentity identity,
        out AspxReviewedPlatformRegistryProfile profile)
    {
        profile = Admitted.SingleOrDefault(candidate =>
            string.Equals(identity?.RegistrySchemaVersion, candidate.RegistrySchemaVersion,
                StringComparison.Ordinal) &&
            string.Equals(identity.RegistryRevision, candidate.RegistryRevision, StringComparison.Ordinal) &&
            string.Equals(identity.RegistryCanonicalHash, candidate.RegistryCanonicalHash,
                StringComparison.Ordinal) &&
            string.Equals(identity.RegistryFileSha256, candidate.RegistryFileSha256, StringComparison.Ordinal) &&
            string.Equals(identity.AuthorityFingerprint, candidate.AuthorityFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(identity.EntryIdentityHash, candidate.EntryIdentityHash, StringComparison.Ordinal));
        return profile != null;
    }

    internal static AspxReviewedPlatformRegistryProfile DiagnosticCandidate(
        AspxPlatformRegistryAdmissionIdentity identity) =>
        Admitted.FirstOrDefault(candidate => string.Equals(identity?.RegistryRevision,
            candidate.RegistryRevision, StringComparison.Ordinal)) ?? Admitted.SingleOrDefault();
}

internal static class AspxPlatformRegistryAuthorityGate
{
    internal static async Task<AspxPlatformRegistryV1> ReadAndValidateAsync(string registryPath,
        string platformBuild, CancellationToken cancellationToken = default,
        IReadOnlyList<AspxPlatformRegistryCompatibilityRevocation> compatibilityRevocations = null,
        Action<string> warningSink = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registryPath);
        var bytes = await File.ReadAllBytesAsync(registryPath, cancellationToken).ConfigureAwait(false);
        var result = Validate(bytes, platformBuild, compatibilityRevocations);
        if (result.Errors.Count > 0)
            throw new InvalidOperationException("Independent registry authority gate rejected the input before authentication/network: " +
                string.Join(", ", result.Errors));
        foreach (var warning in result.Warnings) warningSink?.Invoke(warning);
        return result.Registry;
    }

    internal static async Task<T> ExecuteThenAsync<T>(string registryPath, string platformBuild,
        Func<AspxPlatformRegistryV1, CancellationToken, Task<T>> authorizedContinuation,
        CancellationToken cancellationToken = default,
        IReadOnlyList<AspxPlatformRegistryCompatibilityRevocation> compatibilityRevocations = null,
        Action<string> warningSink = null)
    {
        ArgumentNullException.ThrowIfNull(authorizedContinuation);
        var registry = await ReadAndValidateAsync(registryPath, platformBuild, cancellationToken,
                compatibilityRevocations, warningSink)
            .ConfigureAwait(false);
        return await authorizedContinuation(registry, cancellationToken).ConfigureAwait(false);
    }

    internal static AspxPlatformRegistryGateResult Validate(ReadOnlyMemory<byte> registryBytes,
        string platformBuild,
        IReadOnlyList<AspxPlatformRegistryCompatibilityRevocation> compatibilityRevocations = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        JsonDocument registryDocument = null;
        JsonDocument schemaDocument = null;
        try
        {
            registryDocument = ParseStrict(registryBytes, "registry_json_invalid", errors);
            if (registryDocument == null)
                return new(null, null, null, errors, warnings);

            JsonDuplicatePropertyValidator.Validate(registryDocument.RootElement, "$", errors);
            var fileHash = Sha256(registryBytes.Span);
            var canonicalBytes = CanonicalJson.SerializeWithoutRootProperty(
                registryDocument.RootElement, "registryHash");
            var canonicalHash = Sha256(canonicalBytes);
            var identity = CreateAdmissionIdentity(registryDocument.RootElement, canonicalHash, fileHash);
            var admitted = AspxReviewedPlatformRegistryProfiles.TryGet(identity, out var profile);
            profile ??= AspxReviewedPlatformRegistryProfiles.DiagnosticCandidate(identity);
            if (!admitted) errors.Add("reviewed_profile_missing_for_registry_identity");
            if (profile == null)
                return new(null, null, canonicalHash, errors, warnings);

            var schemaBytes = ReadEmbeddedSchema(profile, errors);
            if (schemaBytes != null)
                schemaDocument = ParseStrict(schemaBytes, "reviewed_schema_json_invalid", errors);
            if (schemaDocument == null)
                return new(null, profile, canonicalHash, errors, warnings);

            JsonDuplicatePropertyValidator.Validate(schemaDocument.RootElement, "$schema", errors);
            JsonSchemaSubsetValidator.Validate(registryDocument.RootElement, schemaDocument.RootElement, errors);

            if (!string.Equals(fileHash, profile.RegistryFileSha256, StringComparison.Ordinal))
                errors.Add("registry_file_hash_not_reviewed");

            ExpectString(registryDocument.RootElement, "$schema", profile.SchemaRelativePath, errors);
            ExpectString(registryDocument.RootElement, "registrySchemaVersion", profile.RegistrySchemaVersion, errors);
            ExpectString(registryDocument.RootElement, "registryRevision", profile.RegistryRevision, errors);
            ExpectString(registryDocument.RootElement, "registryHash", profile.RegistryCanonicalHash, errors);
            ExpectString(registryDocument.RootElement, "authorityKind", profile.AuthorityKind, errors);
            ExpectString(registryDocument.RootElement, "authoritySourceRef", profile.AuthoritySourceRef, errors);
            ExpectString(registryDocument.RootElement, "authorityArtifactHash", profile.AuthorityArtifactHash, errors);
            ExpectString(registryDocument.RootElement, "reviewRef", profile.IndependentReviewRef, errors);
            ExpectString(registryDocument.RootElement, "contractReviewRef", profile.ContractReviewRef, errors);
            ExpectString(registryDocument.RootElement, "compatibilityDecisionRef",
                profile.CompatibilityDecisionRef, errors);
            ExpectString(registryDocument.RootElement, "platformFamily", profile.PlatformFamily, errors);
            ExpectString(registryDocument.RootElement, "platformBuildMin", profile.PlatformBuild, errors);
            ExpectString(registryDocument.RootElement, "platformBuildMax", profile.PlatformBuild, errors);
            ExpectInteger(registryDocument.RootElement, "entryCount", profile.EntryCount, errors);
            if (!string.Equals(canonicalHash, profile.RegistryCanonicalHash, StringComparison.Ordinal))
                errors.Add("registry_canonical_hash_not_reviewed");
            if (!string.Equals(identity.AuthorityFingerprint, profile.AuthorityFingerprint,
                    StringComparison.Ordinal))
                errors.Add("registry_authority_fingerprint_not_reviewed");
            if (!string.Equals(identity.EntryIdentityHash, profile.EntryIdentityHash,
                    StringComparison.Ordinal))
                errors.Add("registry_entry_identity_hash_not_reviewed");

            var revocations = compatibilityRevocations ??
                AspxReviewedPlatformRegistryProfiles.CompatibilityRevocations;
            foreach (var revocation in revocations)
            {
                if (!revocation.IsValid()) errors.Add("registry_compatibility_revocation_invalid");
                else if (revocation.Matches(identity))
                    errors.Add("registry_compatibility_revoked:" + revocation.DecisionRef);
            }

            if (string.IsNullOrWhiteSpace(platformBuild)) errors.Add("observed_platform_build_missing");
            else if (!string.Equals(platformBuild, profile.PlatformBuild, StringComparison.Ordinal))
                warnings.Add("observed_platform_build_differs_from_registry_metadata");

            AspxPlatformRegistryV1 registry = null;
            try
            {
                registry = JsonSerializer.Deserialize<AspxPlatformRegistryV1>(registryBytes.Span,
                    AspxInventoryRuntime.JsonOptions());
            }
            catch (JsonException)
            {
                errors.Add("registry_typed_deserialization_failed");
            }
            if (registry == null)
                errors.Add("registry_typed_document_missing");
            else
                foreach (var invalid in registry.Validate(platformBuild)) errors.Add("registry:" + invalid);
            return new(registry, profile, canonicalHash,
                errors.Distinct(StringComparer.Ordinal).ToArray(),
                warnings.Distinct(StringComparer.Ordinal).ToArray());
        }
        finally
        {
            registryDocument?.Dispose();
            schemaDocument?.Dispose();
        }
    }

    private static AspxPlatformRegistryAdmissionIdentity CreateAdmissionIdentity(JsonElement root,
        string canonicalHash, string fileHash)
    {
        var authorityFingerprint = Sha256(Encoding.UTF8.GetBytes(string.Join('\n',
            StringProperty(root, "authorityKind"), StringProperty(root, "authoritySourceRef"),
            StringProperty(root, "authorityArtifactHash"))));
        var entryIdentities = root.TryGetProperty("entries", out var entries) &&
                              entries.ValueKind == JsonValueKind.Array
            ? entries.EnumerateArray().Select(entry => StringProperty(entry, "referenceId"))
                .OrderBy(value => value, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        var entryIdentityHash = Sha256(Encoding.UTF8.GetBytes(string.Join('\n', entryIdentities)));
        return new(StringProperty(root, "registrySchemaVersion"), StringProperty(root, "registryRevision"),
            canonicalHash, fileHash, authorityFingerprint, entryIdentityHash);
    }

    private static string StringProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static byte[] ReadEmbeddedSchema(AspxReviewedPlatformRegistryProfile profile,
        ICollection<string> errors)
    {
        using var stream = typeof(AspxPlatformRegistryAuthorityGate).Assembly
            .GetManifestResourceStream(profile.SchemaResourceName);
        if (stream == null)
        {
            errors.Add("reviewed_schema_resource_missing");
            return null;
        }
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        if (!string.Equals(Sha256(bytes), profile.SchemaSha256, StringComparison.Ordinal))
            errors.Add("reviewed_schema_resource_hash_mismatch");
        using var document = JsonDocument.Parse(bytes);
        ExpectString(document.RootElement, "$id", profile.SchemaResourceId, errors,
            "reviewed_schema_resource_id_mismatch");
        return bytes;
    }

    private static JsonDocument ParseStrict(ReadOnlyMemory<byte> bytes, string error,
        ICollection<string> errors)
    {
        try
        {
            return JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 256,
            });
        }
        catch (JsonException)
        {
            errors.Add(error);
            return null;
        }
    }

    private static void ExpectString(JsonElement root, string propertyName, string expected,
        ICollection<string> errors, string error = null)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
            errors.Add(error ?? $"reviewed_pin_mismatch:{propertyName}");
    }

    private static void ExpectInteger(JsonElement root, string propertyName, int expected,
        ICollection<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var actual) || actual != expected)
            errors.Add($"reviewed_pin_mismatch:{propertyName}");
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record AspxPlatformRegistryGateResult(
    AspxPlatformRegistryV1 Registry,
    AspxReviewedPlatformRegistryProfile Profile,
    string CanonicalHash,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

internal static class JsonDuplicatePropertyValidator
{
    internal static void Validate(JsonElement element, string path, ICollection<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) errors.Add($"json_duplicate_property:{path}.{property.Name}");
                Validate(property.Value, path + "." + property.Name, errors);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray()) Validate(item, $"{path}[{index++}]", errors);
        }
    }
}

internal static class JsonSchemaSubsetValidator
{
    internal static void Validate(JsonElement instance, JsonElement schema, ICollection<string> errors) =>
        ValidateNode(instance, schema, schema, "$", errors);

    private static void ValidateNode(JsonElement instance, JsonElement schema, JsonElement rootSchema,
        string path, ICollection<string> errors)
    {
        if (schema.ValueKind == JsonValueKind.False)
        {
            errors.Add($"schema_false:{path}");
            return;
        }
        if (schema.ValueKind == JsonValueKind.True) return;
        if (schema.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"reviewed_schema_unsupported_node:{path}");
            return;
        }

        if (schema.TryGetProperty("$ref", out var reference))
        {
            var resolved = ResolveReference(rootSchema, reference.GetString());
            if (resolved == null) errors.Add($"reviewed_schema_reference_unresolved:{path}");
            else ValidateNode(instance, resolved.Value, rootSchema, path, errors);
        }

        if (schema.TryGetProperty("type", out var type) && !TypeMatches(instance, type))
        {
            errors.Add($"schema_type:{path}");
            return;
        }
        if (schema.TryGetProperty("const", out var constant) && !JsonEquals(instance, constant))
            errors.Add($"schema_const:{path}");
        if (schema.TryGetProperty("enum", out var enumeration) &&
            !enumeration.EnumerateArray().Any(candidate => JsonEquals(instance, candidate)))
            errors.Add($"schema_enum:{path}");

        if (instance.ValueKind == JsonValueKind.String)
        {
            var value = instance.GetString() ?? string.Empty;
            if (schema.TryGetProperty("minLength", out var minLength) && value.Length < minLength.GetInt32())
                errors.Add($"schema_min_length:{path}");
            if (schema.TryGetProperty("pattern", out var pattern) && !Regex.IsMatch(value,
                pattern.GetString() ?? string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                errors.Add($"schema_pattern:{path}");
        }
        else if (instance.ValueKind == JsonValueKind.Number && schema.TryGetProperty("minimum", out var minimum) &&
            instance.TryGetDecimal(out var number) && minimum.TryGetDecimal(out var floor) && number < floor)
            errors.Add($"schema_minimum:{path}");

        if (instance.ValueKind == JsonValueKind.Object)
            ValidateObject(instance, schema, rootSchema, path, errors);
        else if (instance.ValueKind == JsonValueKind.Array)
            ValidateArray(instance, schema, rootSchema, path, errors);

        if (schema.TryGetProperty("allOf", out var allOf))
            foreach (var item in allOf.EnumerateArray()) ValidateNode(instance, item, rootSchema, path, errors);
        if (schema.TryGetProperty("if", out var condition))
        {
            var conditionErrors = new List<string>();
            ValidateNode(instance, condition, rootSchema, path, conditionErrors);
            if (conditionErrors.Count == 0 && schema.TryGetProperty("then", out var consequence))
                ValidateNode(instance, consequence, rootSchema, path, errors);
        }
    }

    private static void ValidateObject(JsonElement instance, JsonElement schema, JsonElement rootSchema,
        string path, ICollection<string> errors)
    {
        var count = instance.EnumerateObject().Count();
        if (schema.TryGetProperty("minProperties", out var minProperties) && count < minProperties.GetInt32())
            errors.Add($"schema_min_properties:{path}");
        if (schema.TryGetProperty("required", out var required))
            foreach (var name in required.EnumerateArray().Select(item => item.GetString()))
                if (name != null && !instance.TryGetProperty(name, out _))
                    errors.Add($"schema_required:{path}.{name}");

        schema.TryGetProperty("properties", out var properties);
        var known = properties.ValueKind == JsonValueKind.Object
            ? properties.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        if (schema.TryGetProperty("additionalProperties", out var additional) &&
            additional.ValueKind == JsonValueKind.False)
            foreach (var property in instance.EnumerateObject())
                if (!known.Contains(property.Name)) errors.Add($"schema_additional_property:{path}.{property.Name}");

        if (properties.ValueKind != JsonValueKind.Object) return;
        foreach (var property in properties.EnumerateObject())
            if (instance.TryGetProperty(property.Name, out var value))
                ValidateNode(value, property.Value, rootSchema, path + "." + property.Name, errors);
    }

    private static void ValidateArray(JsonElement instance, JsonElement schema, JsonElement rootSchema,
        string path, ICollection<string> errors)
    {
        var length = instance.GetArrayLength();
        if (schema.TryGetProperty("minItems", out var minItems) && length < minItems.GetInt32())
            errors.Add($"schema_min_items:{path}");
        if (schema.TryGetProperty("maxItems", out var maxItems) && length > maxItems.GetInt32())
            errors.Add($"schema_max_items:{path}");
        if (schema.TryGetProperty("uniqueItems", out var uniqueItems) && uniqueItems.GetBoolean())
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in instance.EnumerateArray())
                if (!seen.Add(Convert.ToBase64String(CanonicalJson.Serialize(item))))
                    errors.Add($"schema_unique_items:{path}");
        }

        var prefixCount = 0;
        if (schema.TryGetProperty("prefixItems", out var prefixItems))
        {
            prefixCount = prefixItems.GetArrayLength();
            var instanceItems = instance.EnumerateArray().ToArray();
            var prefixSchemas = prefixItems.EnumerateArray().ToArray();
            for (var index = 0; index < Math.Min(instanceItems.Length, prefixSchemas.Length); index++)
                ValidateNode(instanceItems[index], prefixSchemas[index], rootSchema, $"{path}[{index}]", errors);
        }
        if (!schema.TryGetProperty("items", out var items)) return;
        var position = 0;
        foreach (var item in instance.EnumerateArray())
        {
            if (position >= prefixCount) ValidateNode(item, items, rootSchema, $"{path}[{position}]", errors);
            position++;
        }
    }

    private static JsonElement? ResolveReference(JsonElement rootSchema, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || !reference.StartsWith("#/", StringComparison.Ordinal))
            return null;
        var current = rootSchema;
        foreach (var encoded in reference[2..].Split('/'))
        {
            var name = encoded.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
                return null;
        }
        return current;
    }

    private static bool TypeMatches(JsonElement instance, JsonElement type)
    {
        if (type.ValueKind == JsonValueKind.String) return TypeMatches(instance, type.GetString());
        return type.ValueKind == JsonValueKind.Array &&
            type.EnumerateArray().Any(item => TypeMatches(instance, item.GetString()));
    }

    private static bool TypeMatches(JsonElement instance, string type) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => instance.ValueKind == JsonValueKind.Null,
        _ => false,
    };

    private static bool JsonEquals(JsonElement left, JsonElement right) =>
        CanonicalJson.Serialize(left).AsSpan().SequenceEqual(CanonicalJson.Serialize(right));
}

internal static class CanonicalJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
        SkipValidation = false,
    };

    internal static byte[] SerializeWithoutRootProperty(JsonElement element, string excludedProperty)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, WriterOptions))
            Write(writer, element, excludedProperty, root: true);
        return memory.ToArray();
    }

    internal static byte[] Serialize(JsonElement element)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, WriterOptions)) Write(writer, element, null, root: true);
        return memory.ToArray();
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element, string excludedProperty, bool root)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                    .Where(property => !root || !string.Equals(property.Name, excludedProperty,
                        StringComparison.Ordinal))
                    .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Write(writer, property.Value, excludedProperty, root: false);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) Write(writer, item, excludedProperty, root: false);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON value kind {element.ValueKind}.");
        }
    }
}
