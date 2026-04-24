using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AI_AOI.Utils
{
    class MyDateTime
    {
        public const string TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
        public static string GetCurShiftStartTime()
        {
            DateTime curTime = DateTime.Now;
            DateTime startTime;
            TimeSpan curTimeSpan = curTime.TimeOfDay;
            string strCurTime = curTime.ToString("HH:mm");

            if ((String.Compare(strCurTime, "07:30") >= 0) && (String.Compare(strCurTime, "19:30") <= 0))
            {
                startTime = new DateTime(curTime.Year, curTime.Month, curTime.Day, 7, 30, 0);

            }
            else
            {

                if ((String.Compare(curTime.ToString("HH:mm"), "23:59") <= 0) && (String.Compare(curTime.ToString("HH:mm"), "19:30") >= 0))
                {
                    startTime = new DateTime(curTime.Year, curTime.Month, curTime.Day, 19, 30, 0);
                }
                else
                {
                    startTime = new DateTime(curTime.Year, curTime.Month, curTime.AddDays(-1).Day, 19, 30, 0);
                }

            }
            return startTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
        public static string GetCurDayStartTime()
        { 
            DateTime curTime = DateTime.Now;
            string result = new DateTime(curTime.Year, curTime.Month, curTime.Day, 0, 0, 0).ToString("yyyy-MM-dd HH:mm:ss");
            return result;
        }
        public static string GetStringDateTime()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss");
        }
        public static string GetStringDateTimeMs()
        {
            return DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        }
        public static string GetStringDateTimeSN()
        {
            return DateTime.Now.ToString("yyyyMMddHHmmss");
        }
        public static string GetStringDate()
        {
            return DateTime.Now.ToString("yyyyMMdd");
        }
        public static string GetStringDateTimeMongo()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        public static string GetTimeMongo()
        {
            return DateTime.Now.ToString("HH_mm_ss");
        }
       
    }
}
