using Bencodex;
using Bencodex.Types;
using HeadlessGQL;
using Libplanet.Crypto;
using Mimir.Worker.ActionHandler;
using Mimir.Worker.Services;
using Nekoyume.Action.Loader;
using Serilog;

namespace Mimir.Worker.Poller;

public class BlockPoller : BaseBlockPoller
{
    private static readonly NCActionLoader ActionLoader = new();

    private readonly BaseActionHandler[] _handlers;

    private readonly IHeadlessGQLClient _headlessGqlClient;

    private readonly Codec _codec = new();

    public BlockPoller(
        IStateService stateService,
        IHeadlessGQLClient headlessGqlClient,
        MongoDbService store)
        : base(stateService, store, "BlockPoller", Log.ForContext<BlockPoller>())
    {
        _headlessGqlClient = headlessGqlClient;

        _handlers =
        [
            new BattleArenaHandler(stateService, store),
            new EventDungeonBattleHandler(stateService, store),
            new HackAndSlashHandler(stateService, store),
            new HackAndSlashSweepHandler(stateService, store),
            new JoinArenaHandler(stateService, store),
            new PatchTableHandler(stateService, store),
            new ProductsHandler(stateService, store),
            new RaidHandler(stateService, store),
            new RuneSlotStateHandler(stateService, store),
            new RaidActionHandler(stateService, store),
            new StakeHandler(stateService, store),
            new ClaimStakeRewardHandler(stateService, store),
            new CombinationSlotStateHandler(stateService, store)
        ];
    }

    protected override async Task ProcessBlocksAsync(
        long syncedBlockIndex,
        long currentBlockIndex,
        CancellationToken stoppingToken)
    {
        long indexDifference = Math.Abs(currentBlockIndex - syncedBlockIndex);
        int limit = 1;

        _logger.Information(
            "Process block, current&sync: {CurrentBlockIndex}&{SyncedBlockIndex}, index-diff: {IndexDiff}, limit: {Limit}",
            currentBlockIndex,
            syncedBlockIndex,
            indexDifference,
            limit
        );

        await ProcessTransactions(syncedBlockIndex, limit, stoppingToken);
    }

    private async Task ProcessTransactions(
        long syncedBlockIndex,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var operationResult = await _headlessGqlClient.GetTransactions.ExecuteAsync(
            syncedBlockIndex,
            limit,
            cancellationToken
        );
        if (operationResult.Data is null)
        {
            var errors = operationResult.Errors.Select(e => e.Message);
            _logger.Error("Failed to get txs. response data is null. errors:\n{Errors}", errors);
            return;
        }

        var txs = operationResult.Data.Transaction.NcTransactions;
        if (txs is null || txs.Count == 0)
        {
            _logger.Information("Transactions is null or empty");
            return;
        }

        var blockIndex = syncedBlockIndex + limit;
        var tuples = txs
            .Where(tx => tx is not null)
            .Select(tx => (
                Signer: new Address(tx!.Signer),
                actions: tx.Actions
                    .Where(action => action is not null)
                    .Select(action => ActionLoader.LoadAction(
                        blockIndex,
                        _codec.Decode(Convert.FromHexString(action!.Raw))))
                    .ToList()))
            .ToList();
        _logger.Information("GetTransaction Success, tx-count: {TxCount}", txs.Count);
        foreach (var (signer, actions) in tuples)
        {
            foreach (var action in actions)
            {
                var (actionType, actionPlainValueInternal) = DeconstructActionPlainValue(action.PlainValue);
                var handled = false;
                foreach (var handler in _handlers)
                {
                    if (await handler.TryHandleAction(
                            blockIndex,
                            signer,
                            action,
                            actionType,
                            actionPlainValueInternal))
                    {
                        handled = true;
                    }
                }

                if (!handled)
                {
                    _logger.Warning("Action is not handled. action: {Action}", action.PlainValue);
                }
            }
        }

        await _store.UpdateLatestBlockIndex(syncedBlockIndex + limit, _pollerType);
    }

    /// <summary>
    /// Deconstructs the given action plain value.
    /// </summary>
    /// <param name="actionPlainValue"><see cref="Libplanet.Action.IAction.PlainValue"/></param>
    /// <returns>
    /// A tuple of two values: the first is the value of the "type_id" key, and the second is the value of the
    /// "values" key.
    /// If the given action plain value is not a dictionary, both values are null.
    /// And if the given action plain value is a dictionary but does not contain the "type_id" or "values" key,
    /// the value of the key is null.
    /// "type_id": Bencodex.Types.Text or Bencodex.Types.Integer.
    ///            (check <see cref="Nekoyume.Action.GameAction.PlainValue"/> with
    ///            <see cref="Libplanet.Action.ActionTypeAttribute"/>)
    /// "values": It can be any type of Bencodex.Types.
    /// </returns>
    private static (IValue? typeId, IValue? values) DeconstructActionPlainValue(IValue actionPlainValue)
    {
        if (actionPlainValue is not Dictionary actionPlainValueDict)
        {
            return (null, null);
        }

        var actionType = actionPlainValueDict.ContainsKey("type_id")
            ? actionPlainValueDict["type_id"]
            : null;
        var actionPlainValueInternal = actionPlainValueDict.ContainsKey("values")
            ? actionPlainValueDict["values"]
            : null;
        return (actionType, actionPlainValueInternal);
    }
}
