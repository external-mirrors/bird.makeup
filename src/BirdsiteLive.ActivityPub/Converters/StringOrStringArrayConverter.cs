using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub.Converters;

public class StringOrStringArrayConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            // If it's a single string, create a list with just that string
            return new List<string> { reader.GetString() };
        }
        else if (reader.TokenType == JsonTokenType.StartArray)
        {
            // If it's already an array, deserialize it as a list
            List<string> result = new List<string>();
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }
                
                if (reader.TokenType == JsonTokenType.String)
                {
                    result.Add(reader.GetString());
                }
            }
            
            return result;
        }
        
        throw new JsonException("Expected string or string array");
    }

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        // Always write as an array
        writer.WriteStartArray();
        
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }
        
        writer.WriteEndArray();
    }
}
