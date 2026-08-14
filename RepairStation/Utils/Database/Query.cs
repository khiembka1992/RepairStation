using System;
using System.Collections.Generic;
using System.Linq;
using AI_AOI.Config;
using HOLLYAOIREPAIRAIContext;

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
            try
            {
                string connStr = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIR_AIConnectionString;
                using (var db = new HOLLYAOIREPAIRAIDataContext(connStr))
                {
                    string line = lineName ?? string.Empty;
                    string barcode = barcodeKeyword ?? string.Empty;

                    var query = db.Inspections.AsQueryable();

                    if (!string.IsNullOrEmpty(line))
                    {
                        query = query.Where(i => i.Line == line);
                    }

                    if (fromTime.HasValue)
                    {
                        query = query.Where(i => i.InspectionDateTime >= fromTime.Value);
                    }

                    if (toTime.HasValue)
                    {
                        query = query.Where(i => i.InspectionDateTime <= toTime.Value);
                    }

                    if (!string.IsNullOrEmpty(barcode))
                    {
                        query = query.Where(i => i.Blocks.Any(b => b.Barcodes.Any(bc => bc.CodeText != null && bc.CodeText.Contains(barcode))));
                    }

                    query = query.OrderByDescending(i => i.InspectionDateTime);
                    if (topN > 0)
                    {
                        query = query.Take(topN);
                    }

                    int no = 1;
                    return query
                        .AsEnumerable()
                        .Select(i => new InspectionStatisticRow
                        {
                            No = no++,
                            InspectionID = i.ID,
                            InspectedDate = i.InspectionDateTime,
                            Barcode = i.Blocks.SelectMany(b => b.Barcodes).OrderBy(bc => bc.ID).Select(bc => bc.CodeText).FirstOrDefault() ?? string.Empty,
                            BlockCount = i.Blocks.Count,
                            AlarmComponentCount = i.Blocks
                                .SelectMany(b => b.Components)
                                .Where(c => c.Alarms.Any(a => (a.DefectType ?? string.Empty) != "OK"))
                                .Select(c => c.ID)
                                .Distinct()
                                .Count(),
                            TotalComponentCount = i.Blocks.Sum(b => b.TotalComponentCount),
                            //GlobalMatchingCount = 0,
                            MarkCount = i.Blocks.SelectMany(b => b.Marks).Count(),
                            //BadBlockCount = i.Blocks
                            //    .SelectMany(b => b.BadMarks)
                            //    .Select(bm => bm.BlockID)
                            //    .Distinct()
                            //    .Count(),
                            BoardName = i.BoardName ?? string.Empty,
                            //ProductLot = i.ProductLot ?? string.Empty,
                            Line = i.Line ?? string.Empty,
                            Station = i.Station ?? string.Empty,
                            Operator = i.Operator ?? string.Empty,
                            Rail = i.RailID,
                            Side = i.Side ?? string.Empty,
                            Status = i.Status
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "GetInspectionStatistics failed.");
                throw;
            }
        }

        public static QueryResult GetInspectionDetail(Guid inspectionId)
        {
            try
            {
                string connStr = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIR_AIConnectionString;
                using (var db = new HOLLYAOIREPAIRAIDataContext(connStr))
                {
                    var inspection = db.Inspections.FirstOrDefault(i => i.ID == inspectionId);
                    if (inspection == null)
                    {
                        return null;
                    }

                    var ret = new QueryResult
                    {
                        ID = inspection.ID,
                        SN = inspection.Blocks
                            .SelectMany(b => b.Barcodes)
                            .Where(bc => !string.IsNullOrEmpty(bc.CodeText))
                            .OrderBy(bc => bc.ID)
                            .Select(bc => bc.CodeText)
                            .FirstOrDefault(),
                        Time = inspection.InspectionDateTime,
                        BoardName = inspection.BoardName ?? string.Empty,
                        BoardImageBytes = inspection.BoardImage,
                        BoardWidth = inspection.BoardWidth,
                        BoardHeight = inspection.BoardHeight,
                        ProductLot = inspection.ProductLot ?? string.Empty,
                        Line = inspection.Line ?? string.Empty,
                        Station = inspection.Station ?? string.Empty,
                        RailID = inspection.RailID,
                        HasMark = inspection.Blocks.Any(b => b.Marks.Any()),
                        BlockNumbers = inspection.Blocks.Select(b => b.Number).Distinct().OrderBy(n => n).ToList(),
                        DefectLocations = GetDefectLocations(db, inspectionId)
                    };

                    if (string.IsNullOrWhiteSpace(ret.SN))
                    {
                        ret.SN = ret.ID.ToString();
                    }

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

        private static List<DefectLocation> GetDefectLocations(HOLLYAOIREPAIRAIDataContext db, Guid inspectionId)
        {
            return db.Components
                .Where(c => c.Block.InspectionID == inspectionId)
                .OrderBy(c => c.Block.Number)
                .ThenBy(c => c.Name)
                .ThenBy(c => c.ID)
                .AsEnumerable()
                .Select(c =>
                {
                    //var alarms = c.Alarms
                    //    .Where(a => !string.Equals(a.DefectType ?? string.Empty, "OK", StringComparison.OrdinalIgnoreCase))
                    //    .ToList();

                    //if (alarms.Count == 0)
                    //{
                    //    return null;
                    //}
                    var alarms = c.Alarms;

                    return new DefectLocation
                    {
                        ComponentID = c.ID,
                        Name = c.Name ?? string.Empty,
                        Catalog = c.Catalog ?? string.Empty,
                        X = c.X,
                        Y = c.Y,
                        Angle = c.Angle,
                        Width = c.ImageWidth,
                        Height = c.ImageHeight,
                        Block = c.Block.Number,
                        TopImageBytes = c.TopImage,
                        SideImageBytes = c.SideImage,
                        TopReferenceImageBytes = c.TopReferenceImage,
                        SideReferenceImageBytes = c.SideReferenceImage,
                        AlarmTopImageBytes = alarms.Select(a => a.TopImage).FirstOrDefault(img => img != null),
                        AlarmSideImageBytes = alarms.Select(a => a.SideImage).FirstOrDefault(img => img != null),
                        AlarmInfors = alarms
                            .Select(a => new AlarmImageInfo
                            {
                                AlarmID = a.ID,
                                AlarmType = a.AlarmType ?? string.Empty,
                                TopImageBytes = a.TopImage,
                                SideImageBytes = a.SideImage
                            })
                            .ToList(),
                        AlarmTypes = alarms
                            .Select(a => a.AlarmType)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .Select(t => t.Trim())
                            .Distinct()
                            .ToList()
                    };
                })
                .Where(d => d != null)
                .ToList();
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
        public List<int> BlockNumbers { get; set; } = new List<int>();
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
        public List<AlarmImageInfo> AlarmInfors { get; set; } = new List<AlarmImageInfo>();
        public List<string> AlarmTypes { get; set; } = new List<string>();
    }

    public class AlarmImageInfo
    {
        public Guid AlarmID { get; set; }
        public string AlarmType { get; set; }
        public byte[] TopImageBytes { get; set; }
        public byte[] SideImageBytes { get; set; }
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
        //public int GlobalMatchingCount { get; set; }
        public int MarkCount { get; set; }
        //public int BadBlockCount { get; set; }
        public string BoardName { get; set; }
        //public string ProductLot { get; set; }
        public string Line { get; set; }
        public string Station { get; set; }
        public string Operator { get; set; }
        public int Rail { get; set; }
        public string Side { get; set; }
        public int Status { get; set; }
        //public string NgBuffer => Status == 0 ? "■" : string.Empty;
    }
}
