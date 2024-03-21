using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;
using Nekoyume.Battle;
using Nekoyume.Model.Item;
using Nekoyume.Model.Stat;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChroniclesUtilBackend.Models.Agent;
using NineChroniclesUtilBackend.Services;
using NineChroniclesUtilBackend.Util;

namespace NineChroniclesUtilBackend.Repositories;

public class CpRepository
{
    private Codec _codec = new();
    private StateGetter _stateGetter;

    public CpRepository(IStateService stateService)
    {
        _stateGetter = new StateGetter(stateService);
    }

    public async Task<int?> CalculateCp(
        Avatar avatar,
        int characterId,
        IReadOnlyCollection<Costume> costumes,
        IReadOnlyCollection<Equipment> equipments,
        IEnumerable<(int id, int level)> runeOption
    )
    {
        try
        {
            var avatarAddress = new Address(avatar.AvatarAddress);

            var characterRow = await GetCharacterRow(characterId);
            var costumeStatSheet = await _stateGetter.GetSheet<CostumeStatSheet>();
            var collectionSheets = await _stateGetter.GetSheet<CollectionSheet>();
            
            var runes = runeOption.Select(x => GetRuneOptionInfo(x.id, x.level).Result).ToList();
            var collectionStates = await _stateGetter.GetCollectionStates([avatarAddress]);
            var modifiers = new Dictionary<Address, List<StatModifier>>
            {
                [avatarAddress] = [],
            };

            if (collectionStates.Count > 0)
            {
                foreach (var (address, state) in collectionStates)
                {
                    var modifier = modifiers[address];
                    foreach (var collectionId in state.Ids)
                    {
                        modifier.AddRange(collectionSheets[collectionId].StatModifiers);
                    }
                }
            }

            return CPHelper.TotalCP(
                equipments,
                costumes,
                runes,
                avatar.Level,
                characterRow,
                costumeStatSheet,
                modifiers[avatarAddress]
            );
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private async Task<RuneOptionSheet.Row.RuneOptionInfo> GetRuneOptionInfo(
        int id,
        int level
    )
    {
        var sheets = await _stateGetter.GetSheet<RuneOptionSheet>();

        if (!sheets.TryGetValue(id, out var optionRow))
        {
            throw new SheetRowNotFoundException("RuneOptionSheet", id);
        }

        if (!optionRow.LevelOptionMap.TryGetValue(level, out var option))
        {
            throw new SheetRowNotFoundException("RuneOptionSheet", level);
        }

        return option;
    }

    private async Task<CharacterSheet.Row> GetCharacterRow(int characterId)
    {
        var sheets = await _stateGetter.GetSheet<CharacterSheet>();

        if (!sheets.TryGetValue(characterId, out var row))
        {
            throw new SheetRowNotFoundException("CharacterSheet", characterId);
        }

        return row;
    }
}
