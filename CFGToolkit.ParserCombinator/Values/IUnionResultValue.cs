﻿using System;
using System.Linq;
using CFGToolkit.ParserCombinator;
using CFGToolkit.ParserCombinator.Input;

namespace CFGToolkit.ParserCombinator.Values
{
    public interface IUnionResultValue<TToken> where TToken : IToken
    {
        int Position { get; set; }

        IInputStream<TToken> Reminder { get; set; }

        bool EmptyMatch { get; }

        bool Equals(object obj);

        int GetHashCode();

        object Value { get; }

        Type ValueType { get; }

        int ConsumedTokens { get; set; }

        T GetValue<T>();
    }

    public static class IUnionResultValueExtensions
    {
        public static string Text(this IUnionResultValue<CharToken> value)
        {
            if (value.EmptyMatch)
            {
                return string.Empty;
            }

            return new string(value.Reminder.Tokens.Skip(value.Position).Take(value.ConsumedTokens).Select(token => token.Value).ToArray());
        }
    }
}
