using System.Text.RegularExpressions;
using Bencodex.Types;
using Libplanet.Action;
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

    public readonly string ActionTypeRegex = actionTypeRegex;

    public async Task<bool> TryHandleAction(
        long blockIndex,
        IAction action,
        string? actionType,
        Dictionary? actionPlainValueInternal)
    {
        try
        {
            await HandleAction(action);
            return true;
        }
        catch (NotImplementedException)
        {
            // ignored
        }

        if (actionType is null ||
            string.IsNullOrEmpty(actionType) ||
            !Regex.IsMatch(actionType, ActionTypeRegex))
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

    protected virtual Task HandleAction(IAction action)
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
