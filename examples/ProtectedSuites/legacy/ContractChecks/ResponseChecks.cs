using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EchoApplication = Legacy.Echo;

[TestClass]
public sealed class ResponseChecks
{
    [TestMethod]
    public void EchoResponseHasRequiredFields()
    {
        using var contract = JsonDocument.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "openapi.json")));
        var schema = contract.RootElement.GetProperty("paths").GetProperty("/echo").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema");
        using var response = JsonDocument.Parse(EchoApplication.Response());
        Assert.AreEqual(JsonValueKind.Object, response.RootElement.ValueKind);
        foreach (string field in schema.GetProperty("required").EnumerateArray().Select(required => required.GetString()!))
        {
            string expected = schema.GetProperty("properties").GetProperty(field).GetProperty("type").GetString()!;
            var kind = expected switch
            {
                "string" => JsonValueKind.String,
                "number" => JsonValueKind.Number,
                "object" => JsonValueKind.Object,
                "array" => JsonValueKind.Array,
                _ => throw new InvalidOperationException($"The example does not validate JSON Schema type '{expected}'.")
            };
            Assert.IsTrue(response.RootElement.TryGetProperty(field, out var value), $"Field '{field}' is required.");
            Assert.AreEqual(kind, value.ValueKind, $"Field '{field}' must have JSON Schema type '{expected}'.");
        }
    }
}
