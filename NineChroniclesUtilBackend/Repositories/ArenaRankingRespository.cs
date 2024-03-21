using System.Collections;
using Bencodex;
using Bencodex.Types;
using Microsoft.OpenApi.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using Nekoyume.Model.Elemental;
using Nekoyume.Model.Item;
using Nekoyume.Model.Stat;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChroniclesUtilBackend.Models.Agent;
using NineChroniclesUtilBackend.Models.Arena;
using NineChroniclesUtilBackend.Services;

namespace NineChroniclesUtilBackend.Repositories;

public class ArenaRankingRepository(MongoDBCollectionService mongoDBCollectionService, IStateService stateService)
{
    private CpRepository cpRepository = new CpRepository(stateService);

    private readonly IMongoCollection<dynamic> ArenaCollection =
        mongoDBCollectionService.GetCollection<dynamic>("arena");

    public async Task<long> GetRankByAvatarAddress(string avatarAddress)
    {
        var pipelines = new BsonDocument[]
        {
            new("$sort", new BsonDocument("Score.Score", -1)),
            new(
                "$group",
                new BsonDocument
                {
                    { "_id", BsonNull.Value },
                    { "docs", new BsonDocument("$push", "$$ROOT") },
                }
            ),
            new(
                "$unwind",
                new BsonDocument { { "path", "$docs" }, { "includeArrayIndex", "Rank" }, }
            ),
            new("$match", new BsonDocument("docs.AvatarAddress", avatarAddress)),
        };

        var aggregation = await ArenaCollection.Aggregate<dynamic>(pipelines).ToListAsync();

        return aggregation.First().Rank;
    }

    public async Task<List<ArenaRanking>> GetRanking(long limit, long offset)
    {
        var pipelines = new[]
        {
            @"{ $setWindowFields: { partitionBy: '', sortBy: { 'Score.Score': -1 }, output: { Rank: { $rank: {} } } } }",
            $@"{{ $skip: {offset} }}",
            $@"{{ $limit: {limit} }}",
            @"{ $lookup: { from: 'avatars', localField: 'AvatarAddress', foreignField: 'Avatar.address', as: 'Avatar' } }",
            @"{ $unwind: { path: '$Avatar', preserveNullAndEmptyArrays: true } }",
            @"{ $unset: ['Avatar.Avatar.mailBox', 'Avatar.Avatar.stageMap', 'Avatar.Avatar.monsterMap', 'Avatar.Avatar.itemMap', 'Avatar.Avatar.eventMap'] }",
        }.Select(BsonDocument.Parse).ToArray();

        var aggregation = ArenaCollection.Aggregate<BsonDocument>(pipelines).ToList();
        var arenaRankings =
            await Task.WhenAll(Enumerable.OfType<BsonDocument>(aggregation).Select(BuildArenaRankingFromDocument));

        return [.. arenaRankings];
    }

    private async Task<ArenaRanking> BuildArenaRankingFromDocument(BsonDocument document)
    {
        var arenaRanking = new ArenaRanking(
            document["AvatarAddress"].AsString,
            document["Information"]["Address"].AsString,
            document["Information"]["Win"].AsInt32,
            document["Information"]["Lose"].AsInt32,
            document["Rank"].AsInt32 + 1,
            document["Information"]["Ticket"].AsInt32,
            document["Information"]["TicketResetCount"].AsInt32,
            document["Information"]["PurchasedTicketCount"].AsInt32,
            document["Score"]["Score"].AsInt32
        );

        if (!document.Contains("Avatar")) return arenaRanking;

        var avatar = new Avatar(
            document["Avatar"]["Avatar"]["address"].AsString,
            document["Avatar"]["Avatar"]["name"].AsString,
            document["Avatar"]["Avatar"]["level"].AsInt32
        );
        arenaRanking.Avatar = avatar;

        var characterId = document["Avatar"]["Avatar"]["characterId"].AsInt32;
        var runeSlots = document["Avatar"]["RuneSlot"].AsBsonArray.Select(rune =>
            (rune["RuneId"].AsInt32, rune["Level"].AsInt32)
        );

        var costumes = ParseToCostumes(document["Avatar"]["Avatar"]["inventory"]["Costumes"].AsBsonArray).ToList();
        var equipments = ParseToEquipments(document["Avatar"]["Avatar"]["inventory"]["Equipments"].AsBsonArray)
            .ToList();

        var cp = await cpRepository.CalculateCp(avatar, characterId, costumes, equipments, runeSlots);
        arenaRanking.CP = cp;

        return arenaRanking;
    }

    public static IEnumerable<Costume> ParseToCostumes(IEnumerable<BsonValue> bsonValues)
    {
        return bsonValues.Select(value =>
        {
            var itemId = new Guid(value["ItemId"].AsString);
            var spineResourcePath = value["SpineResourcePath"].AsString;

            var rawValue = ParseToItemRowDictionary(value);
            rawValue = rawValue.Add("spine_resource_path", new Text(spineResourcePath));

            var raw = new CostumeItemSheet.Row(rawValue);
            return new Costume(raw, itemId);
        });
    }

    public static IEnumerable<Equipment> ParseToEquipments(IEnumerable<BsonValue> bsonValues)
    {
        return bsonValues.Select(value =>
        {
            var itemId = new Guid(value["ItemId"].AsString);
            var requiredBlockIndex = value["RequiredBlockIndex"].AsInt32;
            var madeWithMimisbrunnrRecipe = value["MadeWithMimisbrunnrRecipe"].AsBoolean;
            var setId = value["SetId"].AsInt32;
            var stat = new DecimalStat(
                (StatType)value["Stat"]["StatType"].AsInt32,
                new decimal(value["Stat"]["BaseValue"].AsDouble),
                new decimal(value["Stat"]["AdditionalValue"].AsDouble)
            );
            var spineResourcePath = value["SpineResourcePath"].AsString;

            var rawValue = ParseToItemRowDictionary(value);
            rawValue = rawValue.Add("set_id", new Integer(setId));
            rawValue = rawValue.Add("stat", stat.Serialize());
            rawValue = rawValue.Add("attack_range", new Text("0"));
            rawValue = rawValue.Add("spine_resource_path", new Text(spineResourcePath));

            var raw = new EquipmentItemSheet.Row(rawValue);
            return new Equipment(raw, itemId, requiredBlockIndex, madeWithMimisbrunnrRecipe);
        });
    }

    private static Dictionary ParseToItemRowDictionary(BsonValue value)
    {
        var id = value["Id"].AsInt32;
        var itemSubType = Enum.GetName(typeof(ItemSubType), value["ItemSubType"].AsInt32) ??
                          ItemSubType.FullCostume.GetDisplayName();
        var grade = value["Grade"].AsInt32;
        var elementalType = Enum.GetName(typeof(ElementalType), value["ElementalType"].AsInt32) ??
                            ElementalType.Normal.GetDisplayName();

        return new Dictionary(new[]
        {
            new KeyValuePair<string, IValue>("item_id", new Integer(id)),
            new KeyValuePair<string, IValue>("item_sub_type", new Text(itemSubType)),
            new KeyValuePair<string, IValue>("grade", new Integer(grade)),
            new KeyValuePair<string, IValue>("elemental_type", new Text(elementalType)),
        });
    }
}