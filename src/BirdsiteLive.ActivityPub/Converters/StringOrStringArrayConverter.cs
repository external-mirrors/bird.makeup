using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub.Converters;

public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
{
    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // If it's already an array, deserialize it as a list
            var result = new List<T>();
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }
                
                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                if (item != null)
                {
                    result.Add(item);
                }
            }
            
            return result;
        }
        else
        {
            // If it's a single value, deserialize it and create a list
            var item = JsonSerializer.Deserialize<T>(ref reader, options);
            return item != null ? new List<T> { item } : new List<T>();
        }
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        // Always write as an array
        writer.WriteStartArray();
        
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }
        
        writer.WriteEndArray();
    }
}
