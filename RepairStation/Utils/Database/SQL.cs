using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using AI_AOI.Config;

namespace AI_AOI.Database
{
    class SQL
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger("debug");

        public class AlarmWithDetails
        {
            public string Alarm { get; set; }
            public Guid AlarmID { get; set; }
            public string Component { get; set; }
            public int Block { get; set; }
            public string Barcode { get; set; }
            public Guid InspectionID { get; set; }
        }

        public static bool IsDatabaseConnected()
        {
            try
            {
                string repairConn = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIRConnectionString;
                string repairAiConn = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIR_AIConnectionString;

                using (var repair = new SqlConnection(repairConn))
                using (var repairAi = new SqlConnection(repairAiConn))
                {
                    repair.Open();
                    repairAi.Open();

                    if (repair.State == System.Data.ConnectionState.Open && repairAi.State == System.Data.ConnectionState.Open)
                        return true;
                    else
                    {
                        Logger.Debug($"Repair={repair.State}, RepairAI={repairAi.State}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug(ex);
                return false;
            }
        }

        public static bool CommitAndMoveInspection(Guid inspectionId, Dictionary<Guid, string> componentDefectTypes, out string error)
        {
            return MoveInspectionInternal(inspectionId, componentDefectTypes, true, out error);
        }

        public static bool MoveInspectionToRepair(Guid inspectionId, out string error)
        {
            return MoveInspectionInternal(inspectionId, null, false, out error);
        }

        private static bool MoveInspectionInternal(
            Guid inspectionId,
            Dictionary<Guid, string> componentDefectTypes,
            bool updateDefectTypes,
            out string error)
        {
            error = null;
            try
            {
                if (updateDefectTypes)
                {
                    UpdateAlarmDefectTypes(inspectionId, componentDefectTypes);
                }

                string repairAiConn = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIR_AIConnectionString;
                string repairConn = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIRConnectionString;
                string sourceDb = QuoteName(new SqlConnectionStringBuilder(repairAiConn).InitialCatalog);
                string targetDb = QuoteName(new SqlConnectionStringBuilder(repairConn).InitialCatalog);

                using (var conn = new SqlConnection(repairAiConn))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "Inspection",
                                "s.ID = @InspectionID",
                                $"SELECT 1 FROM {targetDb}.dbo.Inspection t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "Block",
                                $"s.InspectionID = @InspectionID",
                                $"SELECT 1 FROM {targetDb}.dbo.Block t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "Component",
                                $"s.BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID)",
                                $"SELECT 1 FROM {targetDb}.dbo.Component t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "Alarm",
                                $@"s.ComponentID IN (
    SELECT c.ID
    FROM {sourceDb}.dbo.Component c
    INNER JOIN {sourceDb}.dbo.Block b ON b.ID = c.BlockID
    WHERE b.InspectionID = @InspectionID
)",
                                $"SELECT 1 FROM {targetDb}.dbo.Alarm t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "Barcode",
                                $"s.BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID)",
                                $"SELECT 1 FROM {targetDb}.dbo.Barcode t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "Mark",
                                $"s.BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID)",
                                $"SELECT 1 FROM {targetDb}.dbo.Mark t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "BadMark",
                                $"s.BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID)",
                                $"SELECT 1 FROM {targetDb}.dbo.BadMark t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "ComponentOffset",
                                $"s.BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID)",
                                $"SELECT 1 FROM {targetDb}.dbo.ComponentOffset t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "GlobalMatchingAlarm",
                                $"s.InspectionID = @InspectionID",
                                $"SELECT 1 FROM {targetDb}.dbo.GlobalMatchingAlarm t WHERE t.ID = s.ID",
                                inspectionId);

                            CopyRowsWithCommonColumns(conn, tx, sourceDb, targetDb, "CompAlarm",
                                $"s.InspectionID = @InspectionID",
                                $@"SELECT 1
FROM {targetDb}.dbo.CompAlarm t
WHERE t.InspectionID = s.InspectionID
  AND t.Name = s.Name
  AND t.Number = s.Number
  AND ISNULL(t.Library, '') = ISNULL(s.Library, '')",
                                inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.Alarm
WHERE ComponentID IN (
    SELECT c.ID
    FROM {sourceDb}.dbo.Component c
    INNER JOIN {sourceDb}.dbo.Block b ON b.ID = c.BlockID
    WHERE b.InspectionID = @InspectionID
);", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.Barcode
WHERE BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID);", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.Mark
WHERE BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID);", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.BadMark
WHERE BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID);", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.ComponentOffset
WHERE BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID);", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.Component
WHERE BlockID IN (SELECT ID FROM {sourceDb}.dbo.Block WHERE InspectionID = @InspectionID);", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.GlobalMatchingAlarm
WHERE InspectionID = @InspectionID;", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.CompAlarm
WHERE InspectionID = @InspectionID;", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.Block
WHERE InspectionID = @InspectionID;", inspectionId);

                            ExecuteNonQuery(conn, tx, $@"
DELETE FROM {sourceDb}.dbo.Inspection
WHERE ID = @InspectionID;", inspectionId);

                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Error(ex);
                return false;
            }
        }

        private static void UpdateAlarmDefectTypes(Guid inspectionId, Dictionary<Guid, string> componentDefectTypes)
        {
            string connStr = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIR_AIConnectionString;
            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // Default all alarms in this inspection to OK first.
                        using (var resetCmd = new SqlCommand(@"
UPDATE a
SET a.DefectType = 'OK'
FROM dbo.Alarm a
INNER JOIN dbo.Component c ON c.ID = a.ComponentID
INNER JOIN dbo.Block b ON b.ID = c.BlockID
WHERE b.InspectionID = @InspectionID;", conn, tx))
                        {
                            resetCmd.Parameters.AddWithValue("@InspectionID", inspectionId);
                            resetCmd.ExecuteNonQuery();
                        }

                        if (componentDefectTypes != null && componentDefectTypes.Count > 0)
                        {
                            using (var setCmd = new SqlCommand(@"
UPDATE a
SET a.DefectType = @DefectType
FROM dbo.Alarm a
INNER JOIN dbo.Component c ON c.ID = a.ComponentID
INNER JOIN dbo.Block b ON b.ID = c.BlockID
WHERE b.InspectionID = @InspectionID
  AND a.ComponentID = @ComponentID;", conn, tx))
                            {
                                setCmd.Parameters.AddWithValue("@InspectionID", inspectionId);
                                var defectParam = setCmd.Parameters.Add("@DefectType", System.Data.SqlDbType.NVarChar, 200);
                                var componentParam = setCmd.Parameters.Add("@ComponentID", System.Data.SqlDbType.UniqueIdentifier);

                                foreach (var kv in componentDefectTypes)
                                {
                                    if (kv.Key == Guid.Empty) continue;
                                    componentParam.Value = kv.Key;
                                    defectParam.Value = string.IsNullOrWhiteSpace(kv.Value) ? "OK" : kv.Value.Trim();
                                    setCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void ExecuteNonQuery(SqlConnection conn, SqlTransaction tx, string sql, Guid inspectionId)
        {
            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@InspectionID", inspectionId);
                cmd.ExecuteNonQuery();
            }
        }

        private static void CopyRowsWithCommonColumns(
            SqlConnection conn,
            SqlTransaction tx,
            string sourceDb,
            string targetDb,
            string tableName,
            string whereClause,
            string notExistsSubQuery,
            Guid inspectionId)
        {
            var columns = GetCommonInsertableColumns(conn, tx, sourceDb, targetDb, tableName);
            if (columns.Count == 0)
            {
                throw new InvalidOperationException($"No common columns to copy for table {tableName}.");
            }

            string table = QuoteName(tableName);
            string columnList = string.Join(", ", columns.Select(c => QuoteName(c.Name)));
            string selectList = string.Join(", ", columns.Select(BuildSelectExpression));
            string sql = $@"
INSERT INTO {targetDb}.dbo.{table} ({columnList})
SELECT {selectList}
FROM {sourceDb}.dbo.{table} s
WHERE {whereClause}
  AND NOT EXISTS ({notExistsSubQuery});";

            try
            {
                ExecuteNonQuery(conn, tx, sql, inspectionId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Copy table '{tableName}' failed: {ex.Message}", ex);
            }
        }

        private static string BuildSelectExpression(CopyColumn column)
        {
            if (ShouldNullImageColumn(column))
            {
                return $"NULL AS {QuoteName(column.Name)}";
            }

            return QuoteName(column.Name);
        }

        private static bool ShouldNullImageColumn(CopyColumn column)
        {
            if (column == null || string.IsNullOrWhiteSpace(column.Name) || !column.IsNullable)
            {
                return false;
            }

            // Drop binary payload columns to reduce storage when moving from RepairAI to Repair.
            return column.Name.EndsWith("Image", StringComparison.OrdinalIgnoreCase);
        }

        private static List<CopyColumn> GetCommonInsertableColumns(
            SqlConnection conn,
            SqlTransaction tx,
            string sourceDb,
            string targetDb,
            string tableName)
        {
            string sql = $@"
SELECT tcol.name, tcol.is_nullable
FROM {targetDb}.sys.columns tcol
INNER JOIN {targetDb}.sys.tables tt ON tcol.object_id = tt.object_id
INNER JOIN {targetDb}.sys.schemas ts ON tt.schema_id = ts.schema_id
INNER JOIN {sourceDb}.sys.tables st ON st.name = tt.name
INNER JOIN {sourceDb}.sys.schemas ss ON st.schema_id = ss.schema_id AND ss.name = ts.name
INNER JOIN {sourceDb}.sys.columns scol ON scol.object_id = st.object_id AND scol.name = tcol.name
WHERE ts.name = 'dbo'
  AND tt.name = @TableName
  AND tcol.is_computed = 0
  AND scol.is_computed = 0
  AND COLUMNPROPERTY(tcol.object_id, tcol.name, 'IsIdentity') = 0
ORDER BY tcol.column_id;";

            var result = new List<CopyColumn>();
            using (var cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new CopyColumn
                        {
                            Name = reader.GetString(0),
                            IsNullable = reader.GetBoolean(1)
                        });
                    }
                }
            }

            return result;
        }

        private sealed class CopyColumn
        {
            public string Name { get; set; }
            public bool IsNullable { get; set; }
        }

        private static string QuoteName(string name)
        {
            return $"[{(name ?? string.Empty).Replace("]", "]]")}]";
        }
    }
}
