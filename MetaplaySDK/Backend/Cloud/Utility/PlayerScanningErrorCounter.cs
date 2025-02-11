// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metaplay.Cloud.Utility
{
    /// <summary>
    /// Collects the recent counts of players scanned and errors occurred during player scanning to show them on the dashboard.
    /// After reaching <see cref="MaxPlayerScanReports"/>, removes the oldest scan report. Use <see cref="TotalErrors"/> and
    /// <see cref="TotalPlayers"/> to get the current error and scanned player counts.
    /// </summary>
    public class PlayerScanningErrorCounter
    {
        public class PlayerScanReport
        {
            public PlayerScanReport(int playerCount, int errorCount)
            {
                PlayerCount = playerCount;
                ErrorCount  = errorCount;
            }

            public int PlayerCount;
            public int ErrorCount;
        }

        public int                    TotalPlayers => PlayerScanReports.Sum(report => report.PlayerCount);
        public int                    TotalErrors  => PlayerScanReports.Sum(report => report.ErrorCount);
        public int                    MaxPlayerScanReports;
        public List<PlayerScanReport> PlayerScanReports;

        /// <summary>
        /// Creates a new PlayerScanningErrorCounter with a maximum of <paramref name="maxPlayerScanReports"/> to keep at a time.
        /// </summary>
        /// <param name="maxPlayerScanReports">Maximum number of scan reports to keep.</param>
        public PlayerScanningErrorCounter(int maxPlayerScanReports)
        {
            PlayerScanReports    = new List<PlayerScanReport>();
            MaxPlayerScanReports = maxPlayerScanReports;
        }

        /// <summary>
        /// Adds a new player scan report and if there are more than <see cref="MaxPlayerScanReports"/> scan reports, removes the oldest.
        /// </summary>
        public void AddPlayerScanReport(int playerCount, int errorCount)
        {
            PlayerScanReports.Add(new PlayerScanReport(playerCount, errorCount));
            if (PlayerScanReports.Count > MaxPlayerScanReports)
            {
                PlayerScanReports.RemoveAt(0);
            }
        }

        /// <inheritdoc cref="AddPlayerScanReport(int,int)"/>
        public void AddPlayerScanReport(PlayerScanReport playerScanReport)
        {
            PlayerScanReports.Add(playerScanReport);
            if (PlayerScanReports.Count > MaxPlayerScanReports)
            {
                PlayerScanReports.RemoveAt(0);
            }
        }
        
        public static PlayerScanReport CreateNewPlayerScanReport(int playerCount, int errorCount)
        {
            return new PlayerScanReport(playerCount, errorCount);
        }
    }
}
