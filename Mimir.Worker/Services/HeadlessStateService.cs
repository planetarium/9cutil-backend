using Bencodex;
using Bencodex.Types;
using HeadlessGQL;
using Libplanet.Crypto;
using Libplanet.Action.State;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using StrawberryShake;

namespace Mimir.Worker.Services;

public class HeadlessStateService(IHeadlessGQLClient client) : IStateService
{
    private static readonly Codec Codec = new();

    private (long, BlockHash) TipInfo { get; set; }

    public long TipIndex => TipInfo.Item1;

    public Task<IValue?[]> GetStates(Address[] addresses)
    {
        return Task.WhenAll(addresses.Select(GetState));
    }

    public Task<IValue?[]> GetStates(Address[] addresses, Address accountAddress)
    {
        return Task.WhenAll(addresses.Select(addr => GetState(addr, accountAddress)));
    }

    public async Task<IValue?> GetState(Address address)
    {
        return await GetState(address, ReservedAddresses.LegacyAccount);
    }

    public async Task<IValue?> GetState(Address address, Address accountAddress)
    {
        var result = await client.GetState.ExecuteAsync(accountAddress.ToString(), address.ToString());
        result.EnsureNoErrors();

        if (result.Data?.State is null)
        {
            return null;
        }

        return Codec.Decode(Convert.FromHexString(result.Data.State));
    }

    public async Task<int> GetLatestIndex()
    {
        var result = await client.GetTip.ExecuteAsync();
        result.EnsureNoErrors();

        return result.Data.NodeStatus.Tip.Index;
    }

    public async Task<FungibleAssetValue?> GetBalance(Address address, Currency currency)
    {
        var currencyInput = new CurrencyInput
        {
            Ticker = currency.Ticker,
            DecimalPlaces = currency.DecimalPlaces,
            MaximumSupplyMajorUnit = currency.MaximumSupply?.MajorUnit.ToString(),
            MaximumSupplyMinorUnit = currency.MaximumSupply?.MinorUnit.ToString(),
            Minters = currency.Minters?.Select(minter => minter.ToString()).ToList(),
            TotalSupplyTrackable = currency.TotalSupplyTrackable,
        };
        var result = await client.GetBalance.ExecuteAsync(address.ToString(), currencyInput);
        result.EnsureNoErrors();

        if (result.Data is null)
        {
            return null;
        }

        try
        {
            return FungibleAssetValue.Parse(currency, result.Data.StateQuery.Balance.Quantity);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
