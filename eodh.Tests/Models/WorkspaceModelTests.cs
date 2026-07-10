using System.Text.Json;
using eodh.Models;
using Xunit;

namespace eodh.Tests.Models;

public class WorkspaceModelTests
{
    [Theory]
    [InlineData("completed", true)]
    [InlineData("fulfilled", true)]
    [InlineData("pending", false)]
    [InlineData("processing", false)]
    [InlineData("failed", false)]
    public void CommercialRecord_RecognizesLoadableCompletionStates(string status, bool completed)
    {
        var record = CreateRecord(new StacItemProperties(
            null, null, null, null, null, null, null, null, null, OrderStatus: status));

        Assert.Equal(completed, record.IsCompleted);
        Assert.Equal(status, record.Status);
    }

    [Fact]
    public void CommercialRecord_ReadsForwardCompatibleExtensionFields()
    {
        using var status = JsonDocument.Parse("\"failed\"");
        using var message = JsonDocument.Parse("\"Provider rejected request\"");
        var properties = new StacItemProperties(
            null, null, null, null, null, null, null, null, null)
        {
            ExtensionData = new Dictionary<string, JsonElement>
            {
                ["order_status"] = status.RootElement.Clone(),
                ["failure_message"] = message.RootElement.Clone()
            }
        };

        var record = CreateRecord(properties);

        Assert.Equal("failed", record.Status);
        Assert.Equal("Provider rejected request", record.Message);
    }

    private static WorkspaceCommercialRecord CreateRecord(StacItemProperties properties)
    {
        var collection = new StacCollection("orders", "Orders", null, null, null, null, null);
        var item = new StacItem("item", "orders", null, null, properties, null, null);
        return new WorkspaceCommercialRecord("Airbus", collection, item);
    }
}
