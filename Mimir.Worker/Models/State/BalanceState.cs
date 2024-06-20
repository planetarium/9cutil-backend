using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume.Model.State;

namespace Mimir.Worker.Models;

public class BalanceState(Address address, FungibleAssetValue fungibleAssetValue) : State(address)
{
    public FungibleAssetValue Object { get; set; } = fungibleAssetValue;
}
