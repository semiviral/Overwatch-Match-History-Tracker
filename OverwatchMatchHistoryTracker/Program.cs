﻿#define UNIT_TEST

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OverwatchMatchHistoryTracker.AverageOption;
using OverwatchMatchHistoryTracker.DisplayOption;
using OverwatchMatchHistoryTracker.MatchOption;

#endregion

namespace OverwatchMatchHistoryTracker
{
    internal class Program
    {
        private const string PLAYER_NAME = "aaad";

        private static async Task Main(string[] args)
        {
            // todo create `adjust` command
            // todo add 'entropic' boolean column in db to signify whether to use for entropic queries (sr change, for example)
            // todo add verification step for any match commits with a change of >32
            // todo add peak functionality
            // todo add valley functionality
            // todo add entropic option to match verb

#if DEBUG && UNIT_TEST
            foreach (string[] unitTestArgs in _UnitTestArgs.Values.SelectMany(unitTestArgsCollection => unitTestArgsCollection))
            {
                await OverwatchTracker.Process(unitTestArgs);
            }
#else
            await OverwatchTracker.Process(args);
#endif
        }

#if DEBUG && UNIT_TEST
        private static readonly Dictionary<Type, string[][]> _UnitTestArgs = new Dictionary<Type, string[][]>
        {
            {
                typeof(Match), new[]
                {
                    new[]
                    {
                        "match",
                        PLAYER_NAME,
                        "support",
                        "2527",
                        "hanamura",
                        "unit test comment"
                    }
                }
            },
            {
                typeof(Average), new[]
                {
                    new[]
                    {
                        "average",
                        PLAYER_NAME,
                        "support"
                    },
                    new[]
                    {
                        "average",
                        PLAYER_NAME,
                        "support",
                        "win"
                    },
                    new[]
                    {
                        "average",
                        PLAYER_NAME,
                        "support",
                        "loss"
                    },
                    new[]
                    {
                        "average",
                        "-c",
                        PLAYER_NAME,
                        "support"
                    },
                    new[]
                    {
                        "average",
                        "-c",
                        PLAYER_NAME,
                        "support",
                        "win"
                    },
                    new[]
                    {
                        "average",
                        "-c",
                        PLAYER_NAME,
                        "support",
                        "loss"
                    }
                }
            },
            {
                typeof(Display), new[]
                {
                    new[]
                    {
                        "display",
                        PLAYER_NAME,
                        "support"
                    },
                    new[]
                    {
                        "display",
                        PLAYER_NAME,
                        "support",
                        "win"
                    },
                    new[]
                    {
                        "display",
                        PLAYER_NAME,
                        "support",
                        "loss"
                    }
                }
            }
        };
#endif
    }
}
