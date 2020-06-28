#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OverwatchMatchHistoryTracker.Options;

#endregion

namespace OverwatchMatchHistoryTracker
{
    public class OverwatchTracker
    {
        private static readonly Type[] _OptionTypes =
        {
            typeof(MatchOption),
            typeof(AverageOption),
            typeof(DisplayOption),
            typeof(ExportOption)
        };

        public static async ValueTask Process(IEnumerable<string> args)
        {
            try
            {
                object? parsed = null;
                Parser.Default.ParseArguments(args, _OptionTypes).WithParsed(obj => parsed = obj);

                if (parsed is CommandOption commandOption)
                {
                    MatchHistoryContext matchHistoryContext = await MatchHistoryContext.GetMatchHistoryContext(commandOption.Name);
                    await commandOption.Process(matchHistoryContext);
                    await matchHistoryContext.SaveChangesAsync();

                    if (!string.IsNullOrWhiteSpace(commandOption.CompleteText))
                    {
                        Console.WriteLine(commandOption.CompleteText);
                    }

                    return;
                }

                throw new InvalidOperationException("Did not recognize given arguments.");
            }
            catch (DbUpdateException ex)
            {
                if (ex.InnerException is SqliteException sqliteException)
                {
                    switch (sqliteException.SqliteErrorCode)
                    {
                        case 1:
                            Console.WriteLine("A database format error has occurred. The referenced database may be from an older version, or corrupted.");
                            break;

                    }
                }
                else
                {
                    Console.WriteLine($"{ex.InnerException?.Message ?? "No inner exception."} Operation not completed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message} Operation not completed.");
            }
        }
    }
}
