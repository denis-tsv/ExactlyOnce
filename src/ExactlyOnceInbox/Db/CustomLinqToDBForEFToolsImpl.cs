using System.Text.Json;
using LinqToDB;
using LinqToDB.EntityFrameworkCore;
using LinqToDB.Mapping;
using LinqToDB.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ExactlyOnceInbox.Db;

public class CustomLinqToDBForEFToolsImpl : LinqToDBForEFToolsImplDefault
{
    private readonly string _connectionsString;

    public CustomLinqToDBForEFToolsImpl(string connectionsString) => _connectionsString = connectionsString;

    public override EFConnectionInfo ExtractConnectionInfo(IDbContextOptions? options) =>
        new()
        {
            ConnectionString = _connectionsString,
        };

    public override MappingSchema CreateMappingSchema(IModel model, IMetadataReader? metadataReader, IValueConverterSelector? convertorSelector, DataOptions dataOptions)
    {
        var result = base.CreateMappingSchema(model, metadataReader, convertorSelector, dataOptions);

        result.SetConverter<string, Dictionary<string, string>>(str => JsonSerializer.Deserialize<Dictionary<string, string>>(str) ?? new Dictionary<string, string>());
        result.SetConverter<Dictionary<string, string>, string>(dict => JsonSerializer.Serialize(dict));
        
        return result;
    }
}