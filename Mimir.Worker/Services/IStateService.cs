using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Mimir.Worker.Services;

public interface IStateService
{
    Task<int> GetLatestIndex();
    Task<IValue?> GetState(Address address);
    Task<IValue?> GetState(Address address, Address accountAddress);
    Task<IValue?[]> GetStates(Address[] addresses);
    Task<IValue?[]> GetStates(Address[] addresses, Address accountAddress);
    Task<FungibleAssetValue?> GetBalance(Address address, Currency currency);
}
