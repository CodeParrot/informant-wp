﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using vplan;

namespace DataUpdate
{
    public class Fetcher
    {
        private WebClient webcl = new WebClient();
        private int Group;
        private ScheduledAgent sa;
        private bool silent;
        private ObservableCollection<Data> globData;
        // MODE 0: Nur heute, MODE 1: Nur Morgen, MODE 2: Beide
        int mode;
        public Fetcher(ScheduledAgent scha, int md) {
            sa = scha;
            silent = true;
            mode = md;
        }
        public async void getTimes(int group, bool follow)
        {
            webcl = new WebClient();
            Group = group;
            if (follow != true && mode != 1) {
                webcl.DownloadStringCompleted += new DownloadStringCompletedEventHandler(times_DownloadStringCompleted);
            }
            else
            {
                webcl.DownloadStringCompleted += new DownloadStringCompletedEventHandler(timesNext_DownloadStringCompleted);
            }
            string groupStr = "";
            string weekStr = "";
            if (group < 10)
            {
                groupStr = "w0000" + Convert.ToString(group);
            }
            else if (group < 100)
            {
                groupStr = "w000" + Convert.ToString(group);
            }
            else if (group < 1000)
            {
                groupStr = "w00" + Convert.ToString(group);
            }
            else if (group < 10000)
            {
                groupStr = "w0" + Convert.ToString(group);
            }
            else if (group < 100000)
            {
                groupStr = "w" + Convert.ToString(group);
            }
            DateTimeFormatInfo dfi = DateTimeFormatInfo.CurrentInfo;
            Calendar cal = dfi.Calendar;
            int week = cal.GetWeekOfYear(DateTime.Now, dfi.CalendarWeekRule, dfi.FirstDayOfWeek);
            if (follow == true)
                week++;
            if (week < 10)
            {
                weekStr = "0" + Convert.ToString(week);
            }
            else if (week < 100)
            {
                weekStr = Convert.ToString(week);
            }
            webcl.DownloadStringAsync(new Uri("http://www.cws-usingen.de/stupla/Schueler/" + weekStr + "/w/" + groupStr + ".htm"));
        }
        private void times_DownloadStringCompleted(object send, DownloadStringCompletedEventArgs e)
        {
            ObservableCollection<Data> timeObj = new ObservableCollection<Data>();
            if (e.Error != null)
            {
                sa.Stop();
                return;
            }
            //TO-DO: Parse VPlan
            string[] raw = new string[] { "", "", "", "", "" };
            string comp = e.Result;
            comp = comp.Replace(" ", string.Empty);
            for (int i = 0; i < 5; i++)
            {
                raw[i] = comp.Substring(comp.IndexOf("<table"), comp.IndexOf("</table>") - comp.IndexOf("<table") + 8);
                comp = comp.Substring(comp.IndexOf("</table>") + 8);
            }
            ObservableCollection<Data> v1 = new ObservableCollection<Data>();
            int iOuter = 0;
            int daysRec = 0;
            bool doNot = false;
            foreach (var item in raw)
            {
                string it = item;
                if (item.IndexOf("freigegeben") == -1)
                {
                    daysRec++;
                    it = item.Replace("&nbsp;", String.Empty);
                    Regex regex = new Regex("<t{1}d{1}.*?>.*?</td>");
                    MatchCollection mc;
                    if (item.IndexOf("KeineVertretungen") == -1)
                    {
                        int i = 0;
                        while (it.IndexOf("<trclass='list") != -1)
                        {
                            if (i == 0)
                                it = it.Substring(it.IndexOf("</tr>") + 5, it.Length - it.IndexOf("</tr>") - 5);
                            string w;
                            Data data = new Data();
                            w = it.Substring(it.IndexOf("<trclass='list"));
                            w = w.Substring(0, w.IndexOf("</tr>"));
                            it = it.Substring(it.IndexOf("</tr>") + 5, it.Length - it.IndexOf("</tr>") - 5);
                            mc = regex.Matches(w);
                            int iInner = 0;
                            foreach (var thing in mc)
                            {
                                string thingy = thing.ToString();
                                thingy = thingy.Substring(thingy.IndexOf(">") + 1, thingy.Length - thingy.IndexOf(">") - 1);
                                thingy = thingy.Substring(0, thingy.IndexOf("<"));
                                switch (iInner)
                                {
                                    case 0:
                                        if (thingy == "Veranst.")
                                        { data.Veranstaltung = true; }
                                        break;
                                    case 1:
                                        int day = Convert.ToInt16(thingy.Substring(0, thingy.IndexOf(".")));
                                        string dayStr = thingy.Substring(thingy.IndexOf(".") + 1);
                                        dayStr = dayStr.Replace(".", string.Empty);
                                        int month = Convert.ToInt16(dayStr);
                                        int year = DateTime.Now.Year;
                                        DateTime dt = new DateTime(year, month, day);
                                        data.Date = dt;
                                        break;
                                    case 2:
                                        data.Stunde = thingy;
                                        break;
                                    case 3:
                                        data.Vertreter = thingy;
                                        break;
                                    case 4:
                                        data.Fach = thingy;
                                        break;
                                    case 5:
                                        data.AltFach = thingy;
                                        break;
                                    case 6:
                                        data.Raum = thingy;
                                        break;
                                    case 7:
                                        data.Klasse = thingy;
                                        break;
                                    case 8:
                                        data.Lehrer = thingy;
                                        break;
                                    case 13:
                                        data.Notiz = thingy;
                                        break;
                                    case 14:
                                        data.EntfallStr = thingy;
                                        break;
                                    case 15:
                                        data.MitbeStr = thingy;
                                        break;
                                    default:
                                        break;
                                }
                                iInner++;
                            }
                            data.refresh();
                            v1.Add(data);
                            i++;
                        }
                    }
                    else
                    {
                    }
                }
                iOuter++;
                if (iOuter == 5 && daysRec == 1 && mode != 0)
                {
                    getTimes(Group, true);
                    globData = v1;
                    doNot = true;
                }
            }
            if (doNot != true)
                sa.Proceed(v1);
        }
        private void timesNext_DownloadStringCompleted(object send, DownloadStringCompletedEventArgs e)
        {
            try
            {
                ObservableCollection<Data> timeObj = new ObservableCollection<Data>();
                if (e.Error != null)
                {
                    sa.Proceed(globData);
                    return;
                }
                    //TO-DO: Parse VPlan
                string[] raw = new string[] { "", "", "", "", "" };
                string comp = e.Result;
                comp.Replace(" ", string.Empty);
                for (int i = 0; i < 5; i++)
                {
                    raw[i] = comp.Substring(comp.IndexOf("<table"), comp.IndexOf("</table>") - comp.IndexOf("<table") + 8);
                    comp = comp.Substring(comp.IndexOf("</table>") + 8);
                }
                int iOuter = 0;
                int daysRec = 0;
                foreach (var item in raw)
                {
                    string it = item;
                    if (item.IndexOf("freigegeben") == -1 && daysRec == 0)
                    {
                        daysRec++;
                        it = item.Replace("&nbsp;", String.Empty);
                        Regex regex = new Regex("<t{1}d{1}.*?>.*?</td>");
                        MatchCollection mc;
                        if (item.IndexOf("Keine Vertretungen") == -1)
                        {
                            int i = 0;
                            while (it.IndexOf("<trclass='list") != -1)
                            {
                                if (i == 0)
                                    it = it.Substring(it.IndexOf("</tr>") + 5, it.Length - it.IndexOf("</tr>") - 5);
                                string w;
                                Data data = new Data();
                                w = it.Substring(it.IndexOf("<trclass='list"));
                                w = w.Substring(0, w.IndexOf("</tr>"));
                                it = it.Substring(it.IndexOf("</tr>") + 5, it.Length - it.IndexOf("</tr>") - 5);
                                mc = regex.Matches(w);
                                int iInner = 0;
                                foreach (var thing in mc)
                                {
                                    string thingy = thing.ToString();
                                    thingy = thingy.Substring(thingy.IndexOf(">") + 1, thingy.Length - thingy.IndexOf(">") - 1);
                                    thingy = thingy.Substring(0, thingy.IndexOf("<"));
                                    switch (iInner)
                                    {
                                        case 0:
                                            if (thingy == "Veranst.")
                                            { data.Veranstaltung = true; }
                                            break;
                                        case 1:
                                            int day = Convert.ToInt16(thingy.Substring(0, thingy.IndexOf(".")));
                                            string dayStr = thingy.Substring(thingy.IndexOf(".") + 1);
                                            dayStr = dayStr.Replace(".", string.Empty);
                                            int month = Convert.ToInt16(dayStr);
                                            int year = DateTime.Now.Year;
                                            DateTime dt = new DateTime(year, month, day);
                                            data.Date = dt;
                                            break;
                                        case 2:
                                            data.Stunde = thingy;
                                            break;
                                        case 3:
                                            data.Vertreter = thingy;
                                            break;
                                        case 4:
                                            data.Fach = thingy;
                                            break;
                                        case 5:
                                            data.AltFach = thingy;
                                            break;
                                        case 6:
                                            data.Raum = thingy;
                                            break;
                                        case 7:
                                            data.Klasse = thingy;
                                            break;
                                        case 8:
                                            data.Lehrer = thingy;
                                            break;
                                        case 13:
                                            data.Notiz = thingy;
                                            break;
                                        case 14:
                                            data.EntfallStr = thingy;
                                            break;
                                        case 15:
                                            data.MitbeStr = thingy;
                                            break;
                                        default:
                                            break;
                                    }
                                    iInner++;
                                }
                                data.refresh();
                                globData.Add(data);
                                i++;
                            }
                        }
                    }
                    iOuter++;
                }
                sa.Proceed(globData);
            }
            catch {

            }
        }
    }
}
