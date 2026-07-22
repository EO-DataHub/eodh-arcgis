namespace eodh.Services;

/// <summary>
/// UI-agnostic progress sink used by asset loading services.
/// </summary>
internal interface IAssetLoadProgressReporter
{
    Guid Begin(
        string itemId,
        string assetKey,
        string fileType,
        long? expectedBytes,
        int assetIndex,
        int assetCount);
    void ReportStage(Guid operationId, string detail);
    void ReportDownload(Guid operationId, long bytesReceived, long? totalBytes);
    void Complete(Guid operationId, string assetKey);
    void Fail(Guid operationId, string detail);
}
