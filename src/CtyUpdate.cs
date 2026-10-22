// Downloads the latest country file. Only ever runs when the user clicks the button.
//
// Source: AD1C's stable "latest" address for the (Big CTY) country file. It is the same address the WSJT-X
// project and other loggers point to. The file is validated by CallDb.Install before it is used.
using System;
using System.IO;
using System.Net;
using System.Text;

namespace N1TA.MofLof
{
    public static class CtyUpdate
    {
        public const string DefaultUrl = "https://www.country-files.com/bigcty/cty.dat";
        // N1TA_CTY_URL lets the self-test point this at a local test server; normal users never set it.
        public static string Url { get { string u = Environment.GetEnvironmentVariable("N1TA_CTY_URL"); return string.IsNullOrEmpty(u) ? DefaultUrl : u; } }

        // Returns the file text, or null with a plain-language reason.
        public static string Download(out string error)
        {
            error = "";
            try
            {
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192); } catch { }
                HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(Url);
                rq.Timeout = 20000; rq.ReadWriteTimeout = 20000;
                rq.UserAgent = "N1TA-MOFLOF/1.3 (amateur radio propagation tool; github.com/mfdechristopher/N1TA-MOF-LOF)";
                using (HttpWebResponse rs = (HttpWebResponse)rq.GetResponse())
                using (Stream st = rs.GetResponseStream())
                {
                    MemoryStream ms = new MemoryStream(); byte[] buf = new byte[65536]; int n;
                    while ((n = st.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, n);
                        if (ms.Length > 8 * 1024 * 1024) { error = "the server sent far more data than a country file"; return null; }
                    }
                    return Encoding.GetEncoding("ISO-8859-1").GetString(ms.ToArray());
                }
            }
            catch (WebException e) { error = e.Status == WebExceptionStatus.ProtocolError && e.Response != null ? "the server answered " + (int)((HttpWebResponse)e.Response).StatusCode + " " + ((HttpWebResponse)e.Response).StatusDescription : e.Message; return null; }
            catch (Exception e) { error = e.Message; return null; }
        }
    }
}
