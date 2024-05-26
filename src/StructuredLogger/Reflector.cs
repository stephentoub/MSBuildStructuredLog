﻿using System;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Build.Framework;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Reflector
    {
#if NET8_0_OR_GREATER
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(LazyFormattedBuildEventArgs))]
#endif
        static Reflector()
        {
            string fieldName = "argumentsOrFormattedMessage";
            FieldInfo field = typeof(LazyFormattedBuildEventArgs).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null)
            {
                argumentsOrFormattedMessageGetter = GetFieldAccessor<LazyFormattedBuildEventArgs, object>(fieldName);
            }
            else
            {
                fieldName = "arguments";
                field = typeof(LazyFormattedBuildEventArgs).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field is not null)
                {
                    argumentsGetter = GetFieldAccessor<LazyFormattedBuildEventArgs, object[]>(fieldName);
                }
            }

            var rawArgumentsPropertyInfo =
                typeof(LazyFormattedBuildEventArgs).GetProperty("RawArguments", BindingFlags.Instance | BindingFlags.NonPublic);
            if (rawArgumentsPropertyInfo is not null)
            {
                lazyFormattedBuildEventArgs_RawArgumentsGetter = (Func<LazyFormattedBuildEventArgs, object[]>)Delegate.CreateDelegate(
                    typeof(Func<LazyFormattedBuildEventArgs, object[]>),
                    rawArgumentsPropertyInfo.GetMethod);
            }
        }

        private static Func<LazyFormattedBuildEventArgs, object[]> argumentsGetter;
        private static Func<LazyFormattedBuildEventArgs, object> argumentsOrFormattedMessageGetter;
        private static Func<LazyFormattedBuildEventArgs, object[]> lazyFormattedBuildEventArgs_RawArgumentsGetter;

        public static object[] GetArguments(LazyFormattedBuildEventArgs args)
        {
            if (lazyFormattedBuildEventArgs_RawArgumentsGetter is not null)
            {
                return lazyFormattedBuildEventArgs_RawArgumentsGetter(args);
            }
            else if (argumentsOrFormattedMessageGetter is not null)
            {
                return (object[])argumentsOrFormattedMessageGetter(args);
            }

            return argumentsGetter(args);
        }

        private static Func<BuildEventArgs, string> messageGetter = GetFieldAccessor<BuildEventArgs, string>("message");
        public static string GetMessage(BuildEventArgs args)
        {
            return messageGetter(args);
        }

        private static Action<BuildEventArgs, string> senderNameSetter = GetFieldSetter<BuildEventArgs, string>("senderName");
        public static void SetSenderName(BuildEventArgs args, string senderName)
        {
            senderNameSetter(args, senderName);
        }

        private static Action<BuildEventArgs, DateTime> timeStampSetter = GetFieldSetter<BuildEventArgs, DateTime>("timestamp");
        public static void SetTimestamp(BuildEventArgs args, DateTime timestamp)
        {
            timeStampSetter(args, timestamp);
        }

        private static MethodInfo enumerateItemsPerType;
        public static MethodInfo GetEnumerateItemsPerTypeMethod(Type itemDictionary)
        {
            if (enumerateItemsPerType == null)
            {
                enumerateItemsPerType = itemDictionary.GetMethod("EnumerateItemsPerType", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            return enumerateItemsPerType;
        }

        public static Func<T, R> GetFieldAccessor<T, R>(string fieldName)
        {
            ParameterExpression param = Expression.Parameter(typeof(T), "instance");
            MemberExpression member = Expression.Field(param, fieldName);
            LambdaExpression lambda = Expression.Lambda(typeof(Func<T, R>), member, param);
            Func<T, R> compiled = (Func<T, R>)lambda.Compile();
            return compiled;
        }

        public static Action<T, R> GetFieldSetter<T, R>(string fieldName)
        {
            ParameterExpression instance = Expression.Parameter(typeof(T), "instance");
            ParameterExpression value = Expression.Parameter(typeof(R), "value");
            MemberExpression member = Expression.Field(instance, fieldName);
            BinaryExpression assign = Expression.Assign(member, value);
            LambdaExpression lambda = Expression.Lambda<Action<T, R>>(assign, instance, value);
            Action<T, R> compiled = (Action<T, R>)lambda.Compile();
            return compiled;
        }
    }
}
