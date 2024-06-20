using Lib9c.Abstractions;
using Libplanet.Action;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Mimir.Worker.Models;
using Mimir.Worker.Services;
using Serilog;

namespace Mimir.Worker.Handler;

public class TransferAssetActionHandler(IStateService stateService, MongoDbService store)
    : BaseActionHandler(
        stateService,
        store,
        string.Empty, // "^transfer_asset[0-9]*$". Handled by IAction's type.
        Log.ForContext<TransferAssetActionHandler>())
{
    protected override async Task HandleAction(IAction action)
    {
        if (action is not ITransferAssetV1 transferAssetV1)
        {
            return;
        }

        var currency = transferAssetV1.Amount.Currency;
        await UpsertBalance(transferAssetV1.Sender, currency);
        await UpsertBalance(transferAssetV1.Recipient, currency);
    }

    private async Task UpsertBalance(Address address, Currency currency)
    {
        var balance = await StateService.GetBalance(address, currency);
        if (balance.HasValue)
        {
            await Store.UpsertStateDataAsync(
                new StateData(
                    address,
                    new BalanceState(
                        address,
                        balance.Value)
                )
            );
        }
    }
}
