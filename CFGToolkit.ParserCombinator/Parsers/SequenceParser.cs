﻿using System;
using System.Collections.Generic;
using System.Linq;
using CFGToolkit.ParserCombinator.Input;
using CFGToolkit.ParserCombinator.Parsers.Graphs;
using CFGToolkit.ParserCombinator.State;
using CFGToolkit.ParserCombinator.Values;

namespace CFGToolkit.ParserCombinator.Parsers
{
    public class SequenceParser<TToken, TResult> : BaseParser<TToken, TResult> where TToken : IToken
    {
        private readonly Func<(string, IUnionResultValue<TToken>)[], TResult> _factory;
        private readonly Lazy<IParser<TToken>>[] _parsers;

        public SequenceParser(string name, Func<(string, IUnionResultValue<TToken>)[], TResult> valueFactory, params Lazy<IParser<TToken>>[] parsers)
        {
            Name = name;

            _factory = valueFactory;
            _parsers = parsers;
        }

        protected override IUnionResult<TToken> ParseInternal(IInputStream<TToken> input, IGlobalState<TToken> globalState, IParserCallStack<TToken> parserCallStack)
        {
            if (_parsers.Length == 1)
            {
                var parser = _parsers[0].Value;
                var result = parser.Parse(input, globalState, parserCallStack.Call(parser, input));

                if (result.IsSuccessful)
                {
                    var values = new List<IUnionResultValue<TToken>>(result.Values.Count);
                    foreach (var value in result.Values)
                    {
                        var newValue = new UnionResultValue<TToken>(typeof(TResult));
                        newValue.Reminder = value.Reminder;
                        newValue.Value = _factory( new[] { (parser.Name, value) });
                        newValue.ConsumedTokens = value.ConsumedTokens;
                        newValue.Position = value.Position;
                        values.Add(newValue);
                    }

                    return UnionResultFactory.Success(this, values);
                }
                else
                {
                    return UnionResultFactory.Failure(this, "Parser failed", result.MaxConsumed, input.Position);
                }
            }

            var parsers = new IParser<TToken>[_parsers.Length];
            var nodes = new List<TreeNode<TToken>>[_parsers.Length];

            for (var i = 0; i < _parsers.Length; i++)
            {
                var parser = _parsers[i].Value;
                parsers[i] = parser;
                

                if (i == 0)
                {
                    var result = parser.Parse(input, globalState, parserCallStack.Call(parser, input));

                    if (!result.IsSuccessful)
                    {
                        return UnionResultFactory.Failure(this, $"Parser {parser.Name} was not successful", result.MaxConsumed, input.Position);
                    }
                    else
                    {
                        nodes[0] = new List<TreeNode<TToken>>(result.Values.Count);
                        foreach (IUnionResultValue<TToken> item in result.Values)
                        {
                            nodes[0].Add(new TreeNode<TToken>() { Depth = 0, Parent = null, Value = item, IsSuccess = true });
                        }
                    }
                }
                else
                {
                    nodes[i] = new List<TreeNode<TToken>>();

                    int max = 0;
                    foreach (var node in nodes[i - 1])
                    {
                        if (node.IsSuccess)
                        {
                            var tmp = parser.Parse(node.Value.Reminder, globalState, parserCallStack.Call(parser, node.Value.Reminder));

                            if (tmp.IsSuccessful && tmp.Values != null)
                            {
                                foreach (IUnionResultValue<TToken> secondItem in tmp.Values)
                                {
                                    nodes[i].Add(new TreeNode<TToken>() { Depth = i, Parent = node, Value = secondItem, IsSuccess = true });
                                }
                            }
                        }
                        else
                        {
                            max = Options.FullErrorReporting ? Math.Max(max, node.Value.ConsumedTokens) : 0;
                        }
                    }

                    if (nodes[i] == null || !nodes[i].Any())
                    {
                        return UnionResultFactory.Failure(this, "Parser failed", max, input.Position);
                    }
                }
            }

            var resultValues = new List<IUnionResultValue<TToken>>(nodes[parsers.Length - 1].Count);
            foreach (var leaf in nodes[parsers.Length - 1])
            {
                var paths = new TreeNode<TToken>[parsers.Length];
                paths[paths.Length - 1] = leaf;

                for (var k = paths.Length - 2; k >= 0; k--)
                {
                    paths[k] = paths[k + 1].Parent;
                }

                var value = new UnionResultValue<TToken>(typeof(TResult));
                value.Reminder = leaf.Value.Reminder;

                var args = new (string, IUnionResultValue<TToken>)[paths.Length];

                for (var i = 0; i < paths.Length; i++)
                {
                    args[i] = (parsers[i].Name, paths[i].Value);
                    value.ConsumedTokens += paths[i].Value.ConsumedTokens;
                }
                value.Value = _factory(args);
                value.Position = paths[0].Value.Position;

                resultValues.Add(value);
            }

            return UnionResultFactory.Success(this, resultValues);
        }
    }
}
