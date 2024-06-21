using System.Text.RegularExpressions;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Crypto;
using Mimir.Worker.Util;
using Mimir.Worker.Services;
using ILogger = Serilog.ILogger;

namespace Mimir.Worker.Handler;

public abstract class BaseActionHandler(
    IStateService stateService,
    MongoDbService store,
    string actionTypeRegex,
    ILogger logger)
{
    protected readonly IStateService StateService = stateService;

    protected readonly StateGetter StateGetter = new(stateService);

    protected readonly MongoDbService Store = store;

    protected readonly ILogger Logger = logger;

    public async Task<bool> TryHandleAction(
        long blockIndex,
        Address signer,
        IAction action,
        string? actionType,
        Dictionary? actionPlainValueInternal)
    {
        try
        {
            await HandleAction(blockIndex, signer, action);
            return true;
        }
        catch (NotImplementedException)
        {
            // ignored
        }

        if (actionType is null ||
            string.IsNullOrEmpty(actionType) ||
            !Regex.IsMatch(actionType, actionTypeRegex))
        {
            return false;
        }

        try
        {
            await HandleAction(
                actionType,
                blockIndex,
                actionPlainValueInternal ?? Dictionary.Empty);
        }
        catch (NotImplementedException)
        {
            // ignored
        }

        return false;
    }

    protected virtual Task HandleAction(
        long blockIndex,
        Address signer,
        IAction action)
    {
        throw new NotImplementedException();
    }

    protected virtual Task HandleAction(
        string actionType,
        long processBlockIndex,
        Dictionary actionValues)
    {
        throw new NotImplementedException();
    }
}
