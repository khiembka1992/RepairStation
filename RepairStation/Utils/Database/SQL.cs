using System;
using System.Collections.Generic;
using System.Linq;
using AI_AOI.Config;
using Repair = HOLLYAOIREPAIRContext;
using RepairAI = HOLLYAOIREPAIRAIContext;

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

                using (var repair = new Repair.HOLLYAOIREPAIRDataContext(repairConn))
                using (var repairAi = new RepairAI.HOLLYAOIREPAIRAIDataContext(repairAiConn))
                {
                    repair.Connection.Open();
                    repairAi.Connection.Open();
                    return repair.Connection.State == System.Data.ConnectionState.Open
                        && repairAi.Connection.State == System.Data.ConnectionState.Open;
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

                using (var source = new RepairAI.HOLLYAOIREPAIRAIDataContext(repairAiConn))
                using (var target = new Repair.HOLLYAOIREPAIRDataContext(repairConn))
                {
                    var inspection = source.Inspections.FirstOrDefault(i => i.ID == inspectionId);
                    if (inspection == null)
                    {
                        error = "Inspection not found.";
                        return false;
                    }

                    CopyInspection(target, inspection);
                    DeleteInspection(source, inspection);
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
            if (componentDefectTypes == null || componentDefectTypes.Count == 0)
            {
                return;
            }

            string connStr = SoftwareSettingsManager.Current.HOLLY_AOI_REPAIR_AIConnectionString;
            using (var db = new RepairAI.HOLLYAOIREPAIRAIDataContext(connStr))
            {
                var defectByComponent = componentDefectTypes
                    .Where(kv => kv.Key != Guid.Empty && !string.IsNullOrWhiteSpace(kv.Value))
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Trim());

                if (defectByComponent.Count == 0)
                {
                    return;
                }

                var alarms = db.Alarms
                    .Where(a => a.Component.Block.InspectionID == inspectionId && defectByComponent.Keys.Contains(a.ComponentID))
                    .ToList();

                foreach (var alarm in alarms)
                {
                    if (defectByComponent.TryGetValue(alarm.ComponentID, out var defectType))
                    {
                        alarm.DefectType = defectType;
                    }
                }

                db.SubmitChanges();
            }
        }

        private static void CopyInspection(Repair.HOLLYAOIREPAIRDataContext target, RepairAI.Inspection inspection)
        {
            if (target.Inspections.Any(i => i.ID == inspection.ID))
            {
                return;
            }

            target.Inspections.InsertOnSubmit(CloneInspectionTree(inspection));
            target.SubmitChanges();
        }

        private static void DeleteInspection(RepairAI.HOLLYAOIREPAIRAIDataContext source, RepairAI.Inspection inspection)
        {
            source.Inspections.DeleteOnSubmit(inspection);
            source.SubmitChanges();
        }

        private static Repair.Inspection CloneInspection(RepairAI.Inspection source)
        {
            return new Repair.Inspection
            {
                ID = source.ID,
                BoardName = source.BoardName,
                BoardWidth = source.BoardWidth,
                BoardHeight = source.BoardHeight,
                BoardImage = null,
                InspectionDateTime = source.InspectionDateTime,
                GlobalMatchingSampleCount = source.GlobalMatchingSampleCount,
                LogonID = source.LogonID,
                Operator = source.Operator,
                Shift = source.Shift,
                Line = source.Line,
                ProductLot = source.ProductLot,
                Station = source.Station,
                Status = source.Status,
                RailID = source.RailID,
                Side = source.Side,
                CycleTime = source.CycleTime,
                IsSend = null
            };
        }

        private static Repair.Inspection CloneInspectionTree(RepairAI.Inspection source)
        {
            var inspection = CloneInspection(source);

            foreach (var sourceBlock in source.Blocks)
            {
                var block = CloneBlock(sourceBlock);

                foreach (var sourceBarcode in sourceBlock.Barcodes)
                {
                    block.Barcodes.Add(CloneBarcode(sourceBarcode));
                }

                foreach (var sourceMark in sourceBlock.Marks)
                {
                    block.Marks.Add(CloneMark(sourceMark));
                }

                foreach (var sourceBadMark in sourceBlock.BadMarks)
                {
                    block.BadMarks.Add(CloneBadMark(sourceBadMark));
                }

                foreach (var sourceComponent in sourceBlock.Components)
                {
                    var component = CloneComponent(sourceComponent);

                    foreach (var sourceAlarm in sourceComponent.Alarms)
                    {
                        component.Alarms.Add(CloneAlarm(sourceAlarm));
                    }

                    block.Components.Add(component);
                }

                inspection.Blocks.Add(block);
            }

            return inspection;
        }

        private static Repair.Block CloneBlock(RepairAI.Block source)
        {
            return new Repair.Block
            {
                ID = source.ID,
                Number = source.Number,
                Name = source.Name,
                Side = source.Side,
                TotalComponentCount = source.TotalComponentCount,
                InspectionID = source.InspectionID
            };
        }

        private static Repair.Component CloneComponent(RepairAI.Component source)
        {
            return new Repair.Component
            {
                ID = source.ID,
                Name = source.Name,
                X = source.X,
                Y = source.Y,
                Library = source.Library,
                IsVirtual = source.IsVirtual,
                Angle = source.Angle,
                ImageWidth = source.ImageWidth,
                ImageHeight = source.ImageHeight,
                TopImage = null,
                SideImage = null,
                TopReferenceImage = null,
                SideReferenceImage = null,
                Machine = source.Machine,
                BlockID = source.BlockID,
                Catalog = source.Catalog
            };
        }

        private static Repair.Alarm CloneAlarm(RepairAI.Alarm source)
        {
            return new Repair.Alarm
            {
                ID = source.ID,
                DefectType = source.DefectType,
                AlarmType = source.AlarmType,
                TopImage = null,
                SideImage = null,
                ImageWidth = source.ImageWidth,
                ImageHeight = source.ImageHeight,
                Width = source.Width,
                Height = source.Height,
                Angle = source.Angle,
                X = source.X,
                Y = source.Y,
                ComponentPart = source.ComponentPart,
                ComponentID = source.ComponentID
            };
        }

        private static Repair.Barcode CloneBarcode(RepairAI.Barcode source)
        {
            return new Repair.Barcode
            {
                ID = source.ID,
                Name = source.Name,
                X = source.X,
                Y = source.Y,
                CodeText = source.CodeText,
                Validator = source.Validator,
                TopImage = null,
                SideImage = null,
                Angle = source.Angle,
                BlockID = source.BlockID
            };
        }

        private static Repair.Mark CloneMark(RepairAI.Mark source)
        {
            return new Repair.Mark
            {
                ID = source.ID,
                Name = source.Name,
                X = source.X,
                Y = source.Y,
                Type = source.Type,
                BlockID = source.BlockID
            };
        }

        private static Repair.BadMark CloneBadMark(RepairAI.BadMark source)
        {
            return new Repair.BadMark
            {
                ID = source.ID,
                Name = source.Name,
                X = source.X,
                Y = source.Y,
                BlockID = source.BlockID
            };
        }
    }
}
