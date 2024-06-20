using Bencodex.Types;
using Libplanet.Crypto;
using Mimir.Worker.Models;
using Mimir.Worker.Services;
using Serilog;

namespace Mimir.Worker.Handler;

public class BattleArenaHandler(IStateService stateService, MongoDbService store) :
    BaseActionHandler(
        stateService,
        store,
        "^battle_arena[0-9]*$",
        Log.ForContext<BattleArenaHandler>())
{
    protected override async Task HandleAction(
        string actionType,
        long processBlockIndex,
        Dictionary actionValues
    )
    {
        var myAvatarAddress = new Address(actionValues["maa"]);
        var enemyAvatarAddress = new Address(actionValues["eaa"]);

        Logger.Information(
            "Handle battle_arena, my: {MyAvatarAddress}, enemy: {EnemyAvatarAddress}",
            myAvatarAddress,
            enemyAvatarAddress
        );

        try
        {
            var roundData = await StateGetter.GetArenaRoundData(processBlockIndex);
            var myArenaScore = await StateGetter.GetArenaScoreState(
                myAvatarAddress,
                roundData.ChampionshipId,
                roundData.Round
            );
            var myArenaInfo = await StateGetter.GetArenaInfoState(
                myAvatarAddress,
                roundData.ChampionshipId,
                roundData.Round
            );
            var enemyArenaScore = await StateGetter.GetArenaScoreState(
                enemyAvatarAddress,
                roundData.ChampionshipId,
                roundData.Round
            );
            var enemyArenaInfo = await StateGetter.GetArenaInfoState(
                enemyAvatarAddress,
                roundData.ChampionshipId,
                roundData.Round
            );

            await Store.UpsertStateDataAsyncWithLinkAvatar(
                new StateData(
                    myArenaScore.Address,
                    new ArenaState(
                        myArenaScore,
                        myArenaInfo,
                        roundData,
                        myArenaScore.Address,
                        myAvatarAddress
                    )
                ),
                myAvatarAddress
            );
            await Store.UpsertStateDataAsyncWithLinkAvatar(
                new StateData(
                    enemyArenaScore.Address,
                    new ArenaState(
                        enemyArenaScore,
                        enemyArenaInfo,
                        roundData,
                        enemyArenaScore.Address,
                        enemyAvatarAddress
                    )
                ),
                enemyAvatarAddress
            );
        }
        catch (InvalidCastException ex)
        {
            Logger.Error(
                "Failed to arena states. my: {MyAvatarAddress}, enemy: {EnemyAvatarAddress}, error:\n{Error}",
                myAvatarAddress,
                enemyAvatarAddress,
                ex.Message
            );
        }
    }
}
