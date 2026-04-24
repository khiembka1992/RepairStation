using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using AI_AOI.Config;

namespace AI_AOI.Database
{
    public class Query
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger("debug");

        public static List<InspectionStatisticRow> GetInspectionStatistics(
            string lineName,
            DateTime? fromTime,
            DateTime? toTime,
            string barcodeKeyword,
            int topN = 300)
        {
            var ret = new List<InspectionStatisticRow>();

            try
            {
                string connStr = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIR_AIConnectionString;
                using (var conn = new SqlConnection(connStr))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();

                    bool hasTop = topN > 0;
                    cmd.CommandText = BuildInspectionStatisticsSql(hasTop);
                    cmd.Parameters.AddWithValue("@LineName", (object)(lineName ?? string.Empty));
                    cmd.Parameters.AddWithValue("@FromTime", fromTime.HasValue ? (object)fromTime.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ToTime", toTime.HasValue ? (object)toTime.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@BarcodeKeyword", (object)(barcodeKeyword ?? string.Empty));
                    if (hasTop)
                    {
                        cmd.Parameters.AddWithValue("@TopN", topN);
                    }

                    using (var reader = cmd.ExecuteReader())
                    {
                        int no = 1;
                        while (reader.Read())
                        {
                            ret.Add(new InspectionStatisticRow
                            {
                                No = no++,
                                InspectionID = reader.GetGuid(reader.GetOrdinal("InspectionID")),
                                InspectedDate = reader.GetDateTime(reader.GetOrdinal("InspectedDate")),
                                Barcode = ReadString(reader, "Barcode"),
                                BlockCount = ReadInt(reader, "BlockCount"),
                                AlarmComponentCount = ReadInt(reader, "AlarmComponentCount"),
                                TotalComponentCount = ReadInt(reader, "TotalComponentCount"),
                                GlobalMatchingCount = ReadInt(reader, "GlobalMatchingCount"),
                                MarkCount = ReadInt(reader, "MarkCount"),
                                BadBlockCount = ReadInt(reader, "BadBlockCount"),
                                BoardName = ReadString(reader, "BoardName"),
                                ProductLot = ReadString(reader, "ProductLot"),
                                Line = ReadString(reader, "Line"),
                                Station = ReadString(reader, "Station"),
                                Operator = ReadString(reader, "Operator"),
                                Rail = ReadInt(reader, "Rail"),
                                Side = ReadString(reader, "Side"),
                                Status = ReadInt(reader, "Status")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetInspectionStatistics failed.");
                throw;
            }

            return ret;
        }

        public static QueryResult GetInspectionDetail(Guid inspectionId)
        {
            try
            {
                string connStr = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIR_AIConnectionString;
                using (var conn = new SqlConnection(connStr))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandText = @"
SELECT
    i.ID,
    i.InspectionDateTime,
    i.BoardName,
    i.BoardImage,
    i.BoardWidth,
    i.BoardHeight,
    i.ProductLot,
    i.Line,
    i.Station,
    i.RailID,
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM dbo.Block b
            INNER JOIN dbo.Mark m ON m.BlockID = b.ID
            WHERE b.InspectionID = i.ID
        ) THEN 1
        ELSE 0
    END AS HasMark,
    (
        SELECT TOP (1) bc.CodeText
        FROM dbo.Block b
        INNER JOIN dbo.Barcode bc ON bc.BlockID = b.ID
        WHERE b.InspectionID = i.ID
          AND ISNULL(bc.CodeText, '') <> ''
        ORDER BY bc.ID
    ) AS Barcode
FROM dbo.Inspection i
WHERE i.ID = @InspectionID;";
                    cmd.Parameters.AddWithValue("@InspectionID", inspectionId);

                    QueryResult ret = null;
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        ret = new QueryResult
                        {
                            ID = reader.GetGuid(reader.GetOrdinal("ID")),
                            SN = ReadString(reader, "Barcode"),
                            Time = reader.GetDateTime(reader.GetOrdinal("InspectionDateTime")),
                            BoardName = ReadString(reader, "BoardName"),
                            BoardImageBytes = ReadBytes(reader, "BoardImage"),
                            BoardWidth = ReadDouble(reader, "BoardWidth"),
                            BoardHeight = ReadDouble(reader, "BoardHeight"),
                            ProductLot = ReadString(reader, "ProductLot"),
                            Line = ReadString(reader, "Line"),
                            Station = ReadString(reader, "Station"),
                            RailID = ReadInt(reader, "RailID"),
                            HasMark = ReadBool(reader, "HasMark"),
                            DefectLocations = new List<DefectLocation>()
                        };
                    }

                    if (string.IsNullOrWhiteSpace(ret.SN))
                    {
                        ret.SN = ret.ID.ToString();
                    }

                    ret.DefectLocations = GetDefectLocations(conn, inspectionId);
                    ret.Status = ret.DefectLocations.Count == 0;

                    return ret;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetInspectionDetail failed.");
                throw;
            }
        }

        private static List<DefectLocation> GetDefectLocations(SqlConnection conn, Guid inspectionId)
        {
            var componentMap = new Dictionary<Guid, DefectLocation>();
            var order = new List<Guid>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT
    c.ID AS ComponentID,
    c.Name,
    c.Catalog,
    c.X,
    c.Y,
    c.Angle,
    c.ImageWidth,
    c.ImageHeight,
    b.Number AS BlockNumber,
    c.TopImage,
    c.SideImage,
    c.TopReferenceImage,
    c.SideReferenceImage,
    a.AlarmType,
    a.TopImage AS AlarmTopImage,
    a.SideImage AS AlarmSideImage
FROM dbo.Component c
INNER JOIN dbo.Block b ON b.ID = c.BlockID
LEFT JOIN dbo.Alarm a ON a.ComponentID = c.ID
WHERE b.InspectionID = @InspectionID
ORDER BY b.Number, c.Name, c.ID;";
                cmd.Parameters.AddWithValue("@InspectionID", inspectionId);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var componentId = reader.GetGuid(reader.GetOrdinal("ComponentID"));
                        if (!componentMap.TryGetValue(componentId, out var defect))
                        {
                            defect = new DefectLocation
                            {
                                ComponentID = componentId,
                                Name = ReadString(reader, "Name"),
                                Catalog = ReadString(reader, "Catalog"),
                                X = ReadDouble(reader, "X"),
                                Y = ReadDouble(reader, "Y"),
                                Angle = ReadDouble(reader, "Angle"),
                                Width = ReadDouble(reader, "ImageWidth"),
                                Height = ReadDouble(reader, "ImageHeight"),
                                Block = ReadInt(reader, "BlockNumber"),
                                TopImageBytes = ReadBytes(reader, "TopImage"),
                                SideImageBytes = ReadBytes(reader, "SideImage"),
                                TopReferenceImageBytes = ReadBytes(reader, "TopReferenceImage"),
                                SideReferenceImageBytes = ReadBytes(reader, "SideReferenceImage"),
                                AlarmTypes = new List<string>()
                            };

                            componentMap[componentId] = defect;
                            order.Add(componentId);
                        }

                        var alarmType = ReadString(reader, "AlarmType");
                        if (!string.IsNullOrWhiteSpace(alarmType))
                        {
                            var normalized = alarmType.Trim();
                            if (!defect.AlarmTypes.Contains(normalized))
                            {
                                defect.AlarmTypes.Add(normalized);
                            }
                        }

                        if (defect.AlarmSideImageBytes == null)
                        {
                            defect.AlarmSideImageBytes = ReadBytes(reader, "AlarmSideImage");
                        }
                        if (defect.AlarmTopImageBytes == null)
                        {
                            defect.AlarmTopImageBytes = ReadBytes(reader, "AlarmTopImage");
                        }
                    }
                }
            }

            return order
                .Select(id => componentMap[id])
                .Where(d => d.AlarmTypes.Count > 0)
                .ToList();
        }

        private static string BuildInspectionStatisticsSql(bool hasTop)
        {
            var topClause = hasTop ? "TOP (@TopN)" : string.Empty;
            return $@"
SELECT {topClause}
    i.ID AS InspectionID,
    i.InspectionDateTime AS InspectedDate,
    i.BoardName,
    i.ProductLot,
    i.Line,
    i.Station,
    i.Operator,
    i.RailID AS Rail,
    i.Side,
    i.Status,
    (
        SELECT COUNT(*)
        FROM dbo.Block b
        WHERE b.InspectionID = i.ID
    ) AS BlockCount,
    (
        SELECT TOP (1) bc.CodeText
        FROM dbo.Block b
        INNER JOIN dbo.Barcode bc ON bc.BlockID = b.ID
        WHERE b.InspectionID = i.ID
        ORDER BY bc.ID
    ) AS Barcode,
    ISNULL((
        SELECT SUM(ISNULL(b.TotalComponentCount, 0))
        FROM dbo.Block b
        WHERE b.InspectionID = i.ID
    ), 0) AS TotalComponentCount,
    (
        SELECT COUNT(*)
        FROM (
            SELECT DISTINCT a.ComponentID
            FROM dbo.Alarm a
            INNER JOIN dbo.Component c ON c.ID = a.ComponentID
            INNER JOIN dbo.Block b ON b.ID = c.BlockID
            WHERE b.InspectionID = i.ID
              AND ISNULL(a.DefectType, '') <> 'OK'
        ) q
    ) AS AlarmComponentCount,
    (
        SELECT COUNT(*)
        FROM dbo.GlobalMatchingAlarm g
        WHERE g.InspectionID = i.ID
          AND ISNULL(g.DefectType, '') <> 'OK'
    ) AS GlobalMatchingCount,
    (
        SELECT COUNT(*)
        FROM dbo.Mark m
        INNER JOIN dbo.Block b ON b.ID = m.BlockID
        WHERE b.InspectionID = i.ID
    ) AS MarkCount,
    (
        SELECT COUNT(*)
        FROM (
            SELECT DISTINCT bm.BlockID
            FROM dbo.BadMark bm
            INNER JOIN dbo.Block b ON b.ID = bm.BlockID
            WHERE b.InspectionID = i.ID
        ) q
    ) AS BadBlockCount
FROM dbo.Inspection i
WHERE (@LineName = '' OR i.Line = @LineName)
  AND (@FromTime IS NULL OR i.InspectionDateTime >= @FromTime)
  AND (@ToTime IS NULL OR i.InspectionDateTime <= @ToTime)
  AND (
        @BarcodeKeyword = ''
        OR EXISTS (
            SELECT 1
            FROM dbo.Block b
            INNER JOIN dbo.Barcode bc ON bc.BlockID = b.ID
            WHERE b.InspectionID = i.ID
              AND bc.CodeText LIKE '%' + @BarcodeKeyword + '%'
        )
  )
ORDER BY i.InspectionDateTime DESC;";
        }

        private static string ReadString(IDataRecord r, string name)
        {
            int i = r.GetOrdinal(name);
            return r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i));
        }

        private static int ReadInt(IDataRecord r, string name)
        {
            int i = r.GetOrdinal(name);
            return r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i));
        }

        private static double ReadDouble(IDataRecord r, string name)
        {
            int i = r.GetOrdinal(name);
            return r.IsDBNull(i) ? 0d : Convert.ToDouble(r.GetValue(i));
        }

        private static bool ReadBool(IDataRecord r, string name)
        {
            int i = r.GetOrdinal(name);
            return !r.IsDBNull(i) && Convert.ToBoolean(r.GetValue(i));
        }

        private static byte[] ReadBytes(IDataRecord r, string name)
        {
            int i = r.GetOrdinal(name);
            if (r.IsDBNull(i))
            {
                return null;
            }

            return r[name] as byte[];
        }
    }

    public class QueryResult
    {
        public Guid ID { get; set; }
        public string SN { get; set; }
        public DateTime Time { get; set; }
        public string BoardName { get; set; }
        public byte[] BoardImageBytes { get; set; }
        public double BoardWidth { get; set; }
        public double BoardHeight { get; set; }
        public bool HasMark { get; set; }
        public bool Status { get; set; }
        public int RailID { get; set; }
        public string Station { get; set; }
        public string ProductLot { get; set; }
        public string Line { get; set; }
        public List<DefectLocation> DefectLocations { get; set; }
    }

    public class DefectLocation
    {
        public Guid ComponentID { get; set; }
        public string Name { get; set; }
        public string Catalog { get; set; }
        public int Block { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Angle { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public byte[] TopImageBytes { get; set; }
        public byte[] SideImageBytes { get; set; }
        public byte[] TopReferenceImageBytes { get; set; }
        public byte[] SideReferenceImageBytes { get; set; }
        public byte[] AlarmTopImageBytes { get; set; }
        public byte[] AlarmSideImageBytes { get; set; }
        public List<string> AlarmTypes { get; set; } = new List<string>();
    }

    public class InspectionStatisticRow
    {
        public int No { get; set; }
        public Guid InspectionID { get; set; }
        public DateTime InspectedDate { get; set; }
        public string Barcode { get; set; }
        public int BlockCount { get; set; }
        public int AlarmComponentCount { get; set; }
        public int TotalComponentCount { get; set; }
        public string ComponentCountDisplay => $"{AlarmComponentCount} / {TotalComponentCount}";
        public int GlobalMatchingCount { get; set; }
        public int MarkCount { get; set; }
        public int BadBlockCount { get; set; }
        public string BoardName { get; set; }
        public string ProductLot { get; set; }
        public string Line { get; set; }
        public string Station { get; set; }
        public string Operator { get; set; }
        public int Rail { get; set; }
        public string Side { get; set; }
        public int Status { get; set; }
        public string NgBuffer => Status == 0 ? "■" : string.Empty;
    }
}

