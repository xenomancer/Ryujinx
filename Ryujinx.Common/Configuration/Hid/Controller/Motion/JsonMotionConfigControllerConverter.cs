﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ryujinx.Common.Configuration.Hid.Controller.Motion
{
    class JsonMotionConfigControllerConverter : JsonConverter<MotionConfigController>
    {
        private static MotionInputBackendType GetMotionInputBackendType(ref Utf8JsonReader reader)
        {
            // Temporary reader to get the backend type
            Utf8JsonReader tempReader = reader;

            MotionInputBackendType result = MotionInputBackendType.Invalid;

            while (tempReader.Read())
            {
                // NOTE: We scan all properties ignoring the depth entirely in purpose.
                // The reason behind this is that we cannot track in a reliable way the depth of the object because Utf8JsonReader never emit the first TokenType == StartObject if the json start with an object.
                // As such, this code will try to parse very field named "motion_backend" to the correct enum.
                if (tempReader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = tempReader.GetString();

                    if (propertyName.Equals("motion_backend"))
                    {
                        tempReader.Read();

                        if (tempReader.TokenType == JsonTokenType.String)
                        {
                            string backendTypeRaw = tempReader.GetString();

                            if (!Enum.TryParse(backendTypeRaw, out result))
                            {
                                result = MotionInputBackendType.Invalid;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return result;
        }

        public override MotionConfigController Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            MotionInputBackendType motionBackendType = GetMotionInputBackendType(ref reader);

            switch (motionBackendType)
            {
                case MotionInputBackendType.GamepadDriver:
                    return (MotionConfigController)JsonSerializer.Deserialize(ref reader, typeof(StandardMotionConfigController), options);
                case MotionInputBackendType.CemuHooks:
                    return (MotionConfigController)JsonSerializer.Deserialize(ref reader, typeof(CemuHooksMotionConfigController), options);
                default:
                    throw new JsonException($"Unknown backend type {motionBackendType}");
            }
        }

        public override void Write(Utf8JsonWriter writer, MotionConfigController value, JsonSerializerOptions options)
        {
            switch (value.MotionBackend)
            {
                case MotionInputBackendType.GamepadDriver:
                    JsonSerializer.Serialize(writer, value as StandardMotionConfigController, options);
                    break;
                case MotionInputBackendType.CemuHooks:
                    JsonSerializer.Serialize(writer, value as CemuHooksMotionConfigController, options);
                    break;
                default:
                    throw new JsonException($"Unknown motion backend type {value.MotionBackend}");
            }
        }
    }
}
