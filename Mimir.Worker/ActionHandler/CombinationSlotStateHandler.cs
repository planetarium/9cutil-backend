using System.Globalization;
using Bencodex.Types;
using Libplanet.Crypto;
using Mimir.Worker.Exceptions;
using Mimir.Worker.Models;
using Mimir.Worker.Services;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Serilog;
using CombinationSlotState = Mimir.Worker.Models.CombinationSlotState;
using NCCombinationSlotState = Nekoyume.Model.State.CombinationSlotState;

namespace Mimir.Worker.ActionHandler;

public class CombinationSlotStateHandler(IStateService stateService, MongoDbService store)
    : BaseActionHandler(
        stateService,
        store,
        "^combination_consumable[0-9]*$|^combination_equipment[0-9]*$|^event_consumable_item_crafts[0-9]*$|^item_enhancement[0-9]*$|^rapid_combination[0-9]*$",
        Log.ForContext<CombinationSlotStateHandler>()
    )
{
    protected override async Task HandleAction(
        string actionType,
        long processBlockIndex,
        IValue? actionPlainValueInternal
    )
    {
        {
            if (actionPlainValueInternal is not Dictionary actionValues)
            {
                throw new InvalidTypeOfActionPlainValueInternalException(
                    [ValueKind.Dictionary],
                    actionPlainValueInternal?.Kind
                );
            }

            Address avatarAddress;
            int slotIndex;

            if (
                System.Text.RegularExpressions.Regex.IsMatch(
                    actionType,
                    "^combination_consumable[0-9]*$"
                )
                || System.Text.RegularExpressions.Regex.IsMatch(
                    actionType,
                    "^combination_equipment[0-9]*$"
                )
            )
            {
                avatarAddress = new Address(actionValues["a"]);
                slotIndex = actionValues["s"].ToInteger();
            }
            else if (
                System.Text.RegularExpressions.Regex.IsMatch(actionType, "^item_enhancement[0-9]*$")
            )
            {
                avatarAddress = new Address(actionValues["avatarAddress"]);
                slotIndex = actionValues["slotIndex"].ToInteger();
            }
            else if (
                System.Text.RegularExpressions.Regex.IsMatch(
                    actionType,
                    "^event_consumable_item_crafts[0-9]*$"
                )
            )
            {
                if (!(actionValues["l"] is Bencodex.Types.List list))
                {
                    throw new ArgumentException("'l' must be a bencodex list");
                }
                avatarAddress = new Address(list[0]);
                slotIndex = list[3].ToInteger();
            }
            else if (
                System.Text.RegularExpressions.Regex.IsMatch(
                    actionType,
                    "^rapid_combination[0-9]*$"
                )
            )
            {
                avatarAddress = new Address(actionValues["avatarAddress"]);
                slotIndex = actionValues["slotIndex"].ToInteger();
            }
            else
            {
                throw new ArgumentException($"Unknown actionType: {actionType}");
            }

            Logger.Information(
                "CombinationSlotStateHandler, avatar: {avatarAddress}",
                avatarAddress
            );

            var slotAddress = avatarAddress.Derive(
                string.Format(
                    CultureInfo.InvariantCulture,
                    NCCombinationSlotState.DeriveFormat,
                    slotIndex
                )
            );

            var combinationSlotState = await StateGetter.GetCombinationSlotState(slotAddress);

            if (combinationSlotState is null)
            {
                Logger.Error(
                    "CombinationSlotState is null\navatar: {avatarAddress}, slotIndex: {slotIndex}",
                    avatarAddress,
                    slotIndex
                );

                return;
            }

            await Store.UpsertStateDataAsync(
                new StateData(
                    slotAddress,
                    new CombinationSlotState(
                        slotAddress,
                        avatarAddress,
                        slotIndex,
                        combinationSlotState
                    )
                )
            );
        }
    }
}
