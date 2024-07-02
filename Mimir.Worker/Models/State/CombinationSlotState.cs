using Libplanet.Crypto;
using Nekoyume.Model.State;

namespace Mimir.Worker.Models;

public class CombinationSlotState(
    Address address,
    Address avatarAddress,
    int slotIndex,
    Nekoyume.Model.State.CombinationSlotState combinationSlotState
) : State(address)
{
    public int slotIndex { get; set; } = slotIndex;
    public Address avatarAddress { get; set; } = avatarAddress;

    public Nekoyume.Model.State.CombinationSlotState Object { get; set; } = combinationSlotState;
}
