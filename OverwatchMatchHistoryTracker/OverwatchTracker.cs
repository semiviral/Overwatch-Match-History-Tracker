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
        private static readonly HashSet<string> _ValidRoles = new HashSet<string>
        {
            "tank",
            "dps",
            "support"
        };

        private static readonly HashSet<string> _ValidMaps = new HashSet<string>
        {
            MapNames.BLIZZARD_WORLD,
            MapNames.BUSAN,
            MapNames.DORADO,
            MapNames.EICHENWALDE,
            MapNames.HANAMURA,
            MapNames.HAVANA,
            MapNames.HOLLYWOOD,
            MapNames.HORIZON_LUNAR_COLONY,
            MapNames.ILIOS,
            MapNames.JUNKERTOWN,
            MapNames.KINGS_ROW,
            MapNames.LIJIANG_TOWER,
            MapNames.NEPAL,
            MapNames.NUMBANI,
            MapNames.OASIS,
            MapNames.PARIS,
            MapNames.RIALTO,
            MapNames.ROUTE66,
            MapNames.TEMPLE_OF_ANUBIS,
            MapNames.VOLSKAYA_INDUSTRIES,
            MapNames.WATCHPOINT_GIBRALTAR,
        };

        private static readonly IReadOnlyDictionary<string, string> _MapAliases = new Dictionary<string, string>
        {
            { "bw", MapNames.BLIZZARD_WORLD },
            { "bworld", MapNames.BLIZZARD_WORLD },
            { "hlc", MapNames.HORIZON_LUNAR_COLONY },
            { "horizon", MapNames.HORIZON_LUNAR_COLONY },
            { "krow", MapNames.KINGS_ROW },
            { "kingsrow", MapNames.KINGS_ROW },
            { "ltower", MapNames.LIJIANG_TOWER },
            { "lt", MapNames.LIJIANG_TOWER },
            { "lijiang", MapNames.LIJIANG_TOWER },
            { "r66", MapNames.ROUTE66 },
            { "route", MapNames.ROUTE66 },
            { "toa", MapNames.TEMPLE_OF_ANUBIS },
            { "temple", MapNames.TEMPLE_OF_ANUBIS },
            { "anubis", MapNames.TEMPLE_OF_ANUBIS },
            { "vi", MapNames.VOLSKAYA_INDUSTRIES },
            { "volskaya", MapNames.VOLSKAYA_INDUSTRIES },
            { "wg", MapNames.WATCHPOINT_GIBRALTAR },
            { "watchpoint", MapNames.WATCHPOINT_GIBRALTAR },
            { "gibraltar", MapNames.WATCHPOINT_GIBRALTAR },
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
            else if (!string.IsNullOrEmpty(_MatchInfo.Map) && _MapAliases.ContainsKey(_MatchInfo.Map))
            {
                _MatchInfo.Map = _MapAliases[_MatchInfo.Map];
            }
        }

        public async ValueTask Process()
        {
            try
            {
                VerifyArguments();
                await VerifyDatabase();
                await CommitMatchInfo();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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

            if (!_ValidRoles.Contains(_MatchInfo.Role))
            {
                throw new InvalidOperationException
                (
                    $"Invalid role provided: '{_MatchInfo.Role}' (valid roles are 'tank', 'dps', and 'support')."
                );
            }
            else if (!_ValidMaps.Contains(_MatchInfo.Map))
            {
                throw new InvalidOperationException
                (
                    $"Invalid map provided: '{_MatchInfo.Map}'"
                );
            }
            else if ((_MatchInfo.SR < 0) || (_MatchInfo.SR > 6000))
            {
                throw new InvalidOperationException
                (
                    "Provided SR value must be between 0 and 6000 (minimum and maximum as determined by Blizzard)."
                );
            }
            else if (!File.Exists(string.Format(_DatabasePathFormat, _MatchInfo.Name)) && !_MatchInfo.NewPlayer)
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

            _Connection = new SqliteConnection($"Data Source={string.Format(_DatabasePathFormat, _MatchInfo.Name)}");
            await _Connection.OpenAsync();
            await using SqliteCommand command = _Connection.CreateCommand();

            command.CommandText =
                $@"
                    CREATE TABLE IF NOT EXISTS {_MatchInfo.Role}
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

        private async ValueTask CommitMatchInfo()
        {
            Debug.Assert(!(_Connection is null), "Connection should be initialized prior to this point.");
            Debug.Assert(!(_MatchInfo is null), "MatchInfo should be parsed prior to this point.");

            await using SqliteCommand command = _Connection.CreateCommand();

            command.CommandText = $"INSERT INTO {_MatchInfo.Role} (timestamp, sr, map, comment) VALUES (datetime(), $sr, $map, $comment)";
            command.Parameters.AddWithValue("$sr", _MatchInfo.SR);
            command.Parameters.AddWithValue("$map", _MatchInfo.Map);
            command.Parameters.AddWithValue("$comment", _MatchInfo.Comment);
            await command.ExecuteNonQueryAsync();
        }
    }
}
