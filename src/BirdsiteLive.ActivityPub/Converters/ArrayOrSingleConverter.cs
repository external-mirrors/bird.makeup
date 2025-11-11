using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BirdsiteLive.ActivityPub.Converters;

public class ArrayOrSingleConverter<T> : JsonConverter<T>
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            // If it's an array, read the first element
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    // Empty array, return default
                    return default;
                }
                
                // Return the first element
                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                
                // Skip remaining elements in the array
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    reader.Skip();
                }
                
                return item;
            }
            
            return default;
        }
        else
        {
            // If it's a single value, deserialize it directly
            return JsonSerializer.Deserialize<T>(ref reader, options);
        }
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        // Always write as a single value
        JsonSerializer.Serialize(writer, value, options);
    }
}

