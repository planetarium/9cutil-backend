using Libplanet.Crypto;
using Nekoyume.Model.State;

namespace Mimir.Worker.Models;

public class StateData(Address address, State state) : BaseData
{
    public Address Address { get; } = address;

    public State State { get; } = state;
}
