#nullable disable

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine.Animation
{
    /// <summary>
    /// AnimationReference 的自定义 JSON 转换器
    /// 处理动态属性（speed、loop 等可以是静态值或表达式字符串）
    /// </summary>
    public class AnimationReferenceConverter : JsonConverter<AnimationReference>
    {
        public override AnimationReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var reference = new AnimationReference();

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                string propertyName = reader.GetString();
                reader.Read();

                switch (propertyName.ToLowerInvariant())
                {
                    case "source":
                        reference.Source = reader.GetString();
                        break;

                    case "speed":
                        reference.SpeedValue = ReadDynamicValue(ref reader);
                        break;

                    case "loop":
                        reference.LoopValue = ReadDynamicValue(ref reader);
                        break;

                    case "startphase":
                        reference.StartPhaseValue = ReadDynamicValue(ref reader);
                        break;

                    case "endphase":
                        reference.EndPhaseValue = ReadDynamicValue(ref reader);
                        break;

                    case "preservepose":
                        reference.PreservePose = reader.GetBoolean();
                        break;

                    case "blendduration":
                        reference.BlendDurationValue = ReadDynamicValue(ref reader);
                        break;

                    case "driverargs":
                        reference.DriverArgs = ReadDriverArgs(ref reader, options);
                        break;

                    case "oncomplete":
                        reference.OnComplete = JsonSerializer.Deserialize<OnCompleteAction>(ref reader, options);
                        break;

                    default:
                        // Skip unknown properties
                        reader.Skip();
                        break;
                }
            }

            return reference;
        }

        public override void Write(Utf8JsonWriter writer, AnimationReference value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            if (!string.IsNullOrEmpty(value.Source))
            {
                writer.WriteString("source", value.Source);
            }

            WriteDynamicValue(writer, "speed", value.SpeedValue);
            WriteDynamicValue(writer, "loop", value.LoopValue);
            WriteDynamicValue(writer, "startPhase", value.StartPhaseValue);
            WriteDynamicValue(writer, "endPhase", value.EndPhaseValue);
            if (value.PreservePose)
            {
                writer.WritePropertyName("preservePose");
                writer.WriteBooleanValue(true);
            }
            WriteDynamicValue(writer, "blendDuration", value.BlendDurationValue);

            if (value.DriverArgs != null && value.DriverArgs.Count > 0)
            {
                writer.WritePropertyName("driverArgs");
                JsonSerializer.Serialize(writer, value.DriverArgs, options);
            }

            if (value.OnComplete != null)
            {
                writer.WritePropertyName("onComplete");
                JsonSerializer.Serialize(writer, value.OnComplete, options);
            }

            writer.WriteEndObject();
        }

        private object ReadDynamicValue(ref Utf8JsonReader reader)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt32(out int intVal) ? intVal : reader.GetSingle(),
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Null => null,
                _ => null
            };
        }

        private void WriteDynamicValue(Utf8JsonWriter writer, string propertyName, object value)
        {
            if (value == null) return;

            writer.WritePropertyName(propertyName);

            switch (value)
            {
                case string s:
                    writer.WriteStringValue(s);
                    break;
                case float f:
                    writer.WriteNumberValue(f);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case double d:
                    writer.WriteNumberValue(d);
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                default:
                    writer.WriteStringValue(value.ToString());
                    break;
            }
        }

        private Dictionary<string, object> ReadDriverArgs(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            var dict = new Dictionary<string, object>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                string key = reader.GetString();
                reader.Read();

                object value = reader.TokenType switch
                {
                    JsonTokenType.String => reader.GetString(),
                    JsonTokenType.Number => reader.TryGetInt32(out int intVal) ? intVal : reader.GetSingle(),
                    JsonTokenType.True => true,
                    JsonTokenType.False => false,
                    JsonTokenType.Null => null,
                    JsonTokenType.StartObject => JsonElement.ParseValue(ref reader),
                    JsonTokenType.StartArray => JsonElement.ParseValue(ref reader),
                    _ => null
                };

                dict[key] = value;
            }

            return dict;
        }
    }
}
