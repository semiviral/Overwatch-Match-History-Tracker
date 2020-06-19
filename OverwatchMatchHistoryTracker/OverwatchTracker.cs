#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Data.Sqlite;

#endregion

namespace OverwatchMatchHistoryTracker
{
    public class OverwatchTracker
    {
        private static readonly HashSet<string> _ValidMaps = new HashSet<string>
        {
            "Blizzard World",
            "Busan",
            "Dorado",
            "Eichenwalde",
            "Hanamura",
            "Havana",
            "Hollywood",
            "Horizon Lunar Colony",
            "Ilios",
            "Junkertown",
            "King's Row",
            "Lijiang Tower",
            "Nepal",
            "Numbani",
            "Oasis",
            "Paris",
            "Rialto",
            "Route 66",
            "Temple of Anubis",
            "Volskaya Industries",
            "Watchpoint: Gibraltar",
        };

        private static readonly string _CurrentDirectory = Environment.CurrentDirectory;
        private static readonly string _DatabasePathFormat = $@"{_CurrentDirectory}/{{0}}.sqlite";

        private SqliteConnection? _Connection;
        private MatchInfo? _MatchInfo;

        public OverwatchTracker(IEnumerable<string> args)
        {
            ParserResult<MatchInfo> parserResult = Parser.Default.ParseArguments<MatchInfo>(args);
            parserResult.WithParsed(matchInfo => _MatchInfo = matchInfo);

            if (_MatchInfo is null)
            {
                // parser should have printed an error
                Environment.Exit(-1);
            }
        }

        public async ValueTask Process()
        {
            File.Delete(string.Format(_DatabasePathFormat, _MatchInfo.Name));

            try
            {
                VerifyArguments();
                await VerifyDatabase();
                await CommitMatchInfo();
            }
            finally
            {
                async ValueTask StatefulClose()
                {
                    if (_Connection is null)
                    {
                        return;
                    }

                    await _Connection.CloseAsync();
                }

                await StatefulClose();
            }
        }

        private void VerifyArguments()
        {
            Debug.Assert(!(_MatchInfo is null), "MatchInfo should be parsed prior to this point.");

            if (!File.Exists(string.Format(_DatabasePathFormat, _MatchInfo.Name)) && !_MatchInfo.NewPlayer)
            {
                throw new InvalidOperationException
                (
                    $"No match history database has been created for player '{_MatchInfo.Name}'. Use the '-n' flag to create it instead of throwing an error."
                );
            }
        }

        private async ValueTask VerifyDatabase()
        {
            Debug.Assert(!(_MatchInfo is null), "MatchInfo should be parsed prior to this point.");

            if (_MatchInfo is null)
            {
                throw new NullReferenceException(nameof(_MatchInfo));
            }

            _Connection = new SqliteConnection($"Data Source={string.Format(_DatabasePathFormat, _MatchInfo.Name)}");
            await _Connection.OpenAsync();
            await using SqliteCommand command = _Connection.CreateCommand();

            command.CommandText = $@"SELECT name FROM sqlite_master WHERE type = 'table' AND name = '{_MatchInfo.Role}_history'";
            if (await command.ExecuteScalarAsync() == null)
            {
                command.CommandText =
                    $@"
                        CREATE TABLE IF NOT EXISTS {_MatchInfo.Role}_history 
                            (
                                timestamp TEXT NOT NULL,
                                sr INT NOT NULL CHECK (sr >= 0 AND sr <= 6000),
                                map TEXT NOT NULL CHECK
                                    (
                                        {string.Join(" OR ", _ValidMaps.Select(validMap => $"map = \"{validMap}\""))}
                                    ),
                                comment TEXT
                            )
                    ";
                await command.ExecuteNonQueryAsync();
            }
        }

        private async ValueTask CommitMatchInfo()
        {
            Debug.Assert(!(_Connection is null), "Connection should be initialized prior to this point.");
            Debug.Assert(!(_MatchInfo is null), "MatchInfo should be parsed prior to this point.");

            await using SqliteCommand command = _Connection.CreateCommand();

            command.CommandText = $"INSERT INTO {_MatchInfo.Role}_history (timestamp, sr, map, comment) VALUES (datetime(), $sr, $map, $comment)";
            command.Parameters.AddWithValue("$sr", _MatchInfo.SR);
            command.Parameters.AddWithValue("$map", _MatchInfo.Map);
            command.Parameters.AddWithValue("$comment", _MatchInfo.Comment);
            await command.ExecuteNonQueryAsync();
        }
    }
}