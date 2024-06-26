using Mimir.Worker.Constants;
using Mimir.Worker.ActionHandler;
using Mimir.Worker.Models;
using Mimir.Worker.Services;
using Mimir.Worker.Util;
using MongoDB.Bson;
using Serilog;

namespace Mimir.Worker.Initializer;

public class TableSheetInitializer(IStateService service, MongoDbService store)
    : BaseInitializer(service, store, Log.ForContext<MarketInitializer>())
{
    public override async Task RunAsync(CancellationToken stoppingToken)
    {
        var handler = new PatchTableHandler(_stateService, _store);
        var sheetTypes = TableSheetUtil.GetTableSheetTypes();

        foreach (var sheetType in sheetTypes)
        {
            _logger.Information("Init sheet, table: {TableName} ", sheetType.Name);

            await handler.SyncSheetStateAsync(sheetType);
        }
    }

    public override async Task<bool> IsInitialized()
    {
        var sheetTypes = TableSheetUtil.GetTableSheetTypes();

        var collection = _store.GetCollection(CollectionNames.GetCollectionName<SheetState>());
        var count = await collection.CountDocumentsAsync(new BsonDocument());
        var sheetTypesCount = sheetTypes.Count() - 4;

        return count >= sheetTypesCount;
    }
}
