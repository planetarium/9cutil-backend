using Lib9c.Abstractions;
using Libplanet.Action;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Mimir.Worker.Models;
using Mimir.Worker.Services;
using Serilog;

namespace Mimir.Worker.Handler;

public class TransferAssetsActionHandler(IStateService stateService, MongoDbService store)
    : BaseActionHandler(
        stateService,
        store,
        string.Empty, // "^transfer_assets[0-9]*$". Handled by IAction's type.
        Log.ForContext<TransferAssetsActionHandler>())
{
    protected override async Task HandleAction(IAction action)
    {
        if (action is not ITransferAssetsV1 transferAssetsV1)
        {
            return;
        }

        var sender = transferAssetsV1.Sender;
        var senderCurrencies = new HashSet<Currency>();
        foreach (var (recipient, amount) in transferAssetsV1.Recipients)
        {
            var currency = amount.Currency;
            senderCurrencies.Add(currency);
            await UpsertBalance(recipient, currency);
        }

        foreach (var currency in senderCurrencies)
        {
            await UpsertBalance(sender, currency);
        }
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
