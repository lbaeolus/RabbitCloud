﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitCloud.Rpc.Abstractions;
using RabbitCloud.Rpc.Abstractions.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RabbitCloud.Rpc.Default
{
    public class RabbitCodec : ICodec
    {
        #region Implementation of ICodec

        /// <summary>
        /// 对消息进行编码。
        /// </summary>
        /// <param name="message">消息实例。</param>
        /// <returns>编码后的内容。</returns>
        public object Encode(object message)
        {
            if (message is IRpcRequestFeature)
            {
                var invocation = (IRpcRequestFeature)message;
                return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
                {
                    invocation.ServiceId,
                    Arguments = ((IEnumerable<object>)invocation.Body).Select(GetTypeParameter)
                }));
            }
            if (message is IRpcResponseFeature)
            {
                var responseFeature = (IRpcResponseFeature)message;
                var json = JsonConvert.SerializeObject(GetTypeParameter(responseFeature.Body));
                return Encoding.UTF8.GetBytes(json);
            }
            return null;
        }

        /// <summary>
        /// 对消息进行解码。
        /// </summary>
        /// <param name="message">消息内容。</param>
        /// <param name="type">内容类型。</param>
        /// <returns>解码后的内容。</returns>
        public object Decode(object message, Type type)
        {
            if (type.IsInstanceOfType(message))
                return message;

            JObject obj;
            if (message is JObject)
                obj = (JObject)message;
            else if (message is byte[])
                obj = GetObjByBytes((byte[])message);
            else if (message is string)
                obj = GetObjByString((string)message);
            else
                throw new NotSupportedException($"not support type: {message.GetType().FullName}");

            if (type == typeof(IRpcRequestFeature))
            {
                var arguments = ((JArray)obj.SelectToken("Arguments")).Select(GetArgument).ToArray();
                var invocation = new RpcRequestFeature
                {
                    ServiceId = obj.Value<string>("ServiceId"),
                    Body = arguments
                };
                return invocation;
            }
            if (type == typeof(IRpcResponseFeature))
            {
                return new RpcResponseFeature
                {
                    Body = GetArgument(obj)
                };
            }

            return null;
        }

        #endregion Implementation of ICodec

        private static JObject GetObjByBytes(byte[] buffer)
        {
            return GetObjByString(Encoding.UTF8.GetString(buffer));
        }

        private static JObject GetObjByString(string message)
        {
            return JObject.Parse(message);
        }

        private static object GetArgument(JToken token)
        {
            var typeString = token.Value<string>("Type");
            var type = Type.GetType(typeString);
            if (type == null)
                throw new Exception($"解析参数类型时发生了错误，找不到类型: {typeString}");
            return token["Content"].ToObject(type);
        }

        private static object GetTypeParameter(object obj)
        {
            return new { Type = obj.GetType().AssemblyQualifiedName, Content = obj };
        }
    }
}