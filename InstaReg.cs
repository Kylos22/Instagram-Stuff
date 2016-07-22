using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace InstaReg
{
    class InstaReg
    {
        public static string CreateAccount(string email, string username, string password, string name, string ua, CookieContainer cookies, string proxy=null)
        {
            try
            {
                cookies.Add(new Cookie("x", "v", "/", "instagram.com"));
                var wproxy = new WebProxy(proxy);
                var web = (HttpWebRequest)WebRequest.Create("https://www.instagram.com");
                web.Method = "GET";
                web.CookieContainer = cookies;
                web.Proxy = wproxy;
                web.UserAgent = ua;
                string csrf = "";
                using (var res = new StreamReader(web.GetResponse().GetResponseStream()))
                    csrf = res.ReadToEnd().Split(new[] { "\"csrf_token\":\"" }, StringSplitOptions.None)[1].Split('"')[0];
                string guid = "";
                foreach (Cookie cookie in cookies.GetCookies(new Uri("https://www.instagram.com")))
                {
                    if (cookie.Name == "mid")
                    {
                        guid = cookie.Value;
                        break;
                    }
                }
                web = (HttpWebRequest)WebRequest.Create("https://www.instagram.com/accounts/web_create_ajax/");
                web.Method = "POST";
                web.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
                web.CookieContainer = cookies;
                web.Proxy = wproxy;
                web.UserAgent = ua;
                web.Headers["X-CSRFToken"] = csrf;
                web.Headers["X-Requested-With"] = "XMLHttpRequest";
                web.Headers["X-Instagram-AJAX"] = "1";
                web.Headers["Accept-Language"] = "en-US,en;q=0.8,en-US;q=0.6,en;q=0.4";
                web.Referer = "https://www.instagram.com/";
                byte[] data = Encoding.UTF8.GetBytes("email=" + HttpUtility.UrlEncode(email) + "&password=" + HttpUtility.UrlEncode(password) 
                    + "&username=" + HttpUtility.UrlEncode(username) + "&first_name=" + HttpUtility.UrlEncode(name) + "&guid=" + guid);
                web.ContentLength = data.Length;
                using (var w = web.GetRequestStream())
                    w.Write(data, 0, data.Length);
                using (var res = new StreamReader(web.GetResponse().GetResponseStream()))
                    return res.ReadToEnd();
            }
            catch { }
            return "";
        }

        public static bool SetProfile(string username, string name, string email, string bio, string website, string ua, CookieContainer cookies, string proxy=null)
        {
            try
            {
                var wproxy = new WebProxy(proxy);
                var web = (HttpWebRequest)WebRequest.Create("https://www.instagram.com/accounts/edit/");
                web.Method = "GET";
                web.CookieContainer = cookies;
                web.Proxy = wproxy;
                web.UserAgent = ua;
                string csrf = "";
                using (var res = new StreamReader(web.GetResponse().GetResponseStream()))
                    csrf = res.ReadToEnd().Split(new[] { "\"csrf_token\":\"" }, StringSplitOptions.None)[1].Split('"')[0];
                web = (HttpWebRequest)WebRequest.Create("https://www.instagram.com/accounts/edit/");
                web.Method = "POST";
                web.ContentType = "application/x-www-form-urlencoded";
                web.CookieContainer = cookies;
                web.Proxy = wproxy;
                web.UserAgent = ua;
                web.Referer = "https://www.instagram.com/accounts/edit/";
                byte[] data = Encoding.UTF8.GetBytes("csrfmiddlewaretoken="+csrf+"&first_name="+ HttpUtility.UrlEncode(name) +"&email="
                    + HttpUtility.UrlEncode(email) +"&username="+HttpUtility.UrlEncode(username)+"&phone_number=&gender=3&biography="+HttpUtility.UrlEncode(bio)
                    +"&external_url="+HttpUtility.UrlEncode(website)+"&chaining_enabled=on");
                web.ContentLength = data.Length;
                using (var w = web.GetRequestStream())
                    w.Write(data, 0, data.Length);
                web.GetResponse();
                return true;
            }
            catch { }
            return false;
        }

        public static bool Upload(string username, string pic, string ua, CookieContainer cookies, string proxy = null)
        {
            try
            {
                var wproxy = new WebProxy(proxy);
                var web = (HttpWebRequest)WebRequest.Create("https://www.instagram.com/"+username+"/");
                web.Method = "GET";
                web.CookieContainer = cookies;
                web.Proxy = wproxy;
                web.UserAgent = ua;
                string csrf = "";
                using (var res = new StreamReader(web.GetResponse().GetResponseStream()))
                    csrf = res.ReadToEnd().Split(new[] { "\"csrf_token\":\"" }, StringSplitOptions.None)[1].Split('"')[0];
                web = (HttpWebRequest)WebRequest.Create("https://www.instagram.com/accounts/web_change_profile_picture/");
                web.Method = "POST";
                web.ContentType = "multipart/form-data; boundary=----WebKitFormBoundary0ajPSMhebdZCGOl2";
                web.CookieContainer = cookies;
                web.Proxy = wproxy;
                web.UserAgent = ua;
                web.Headers["X-CSRFToken"] = csrf;
                web.Headers["X-Requested-With"] = "XMLHttpRequest";
                web.Headers["X-Instagram-AJAX"] = "1";
                web.Referer = "https://www.instagram.com/"+username+"/";
                List<byte> req = new List<byte>();
                req.AddRange(Encoding.UTF8.GetBytes("------WebKitFormBoundary0ajPSMhebdZCGOl2\r\n" +
"Content-Disposition: form-data; name=\"profile_pic\"; filename=\"" + pic.Split('\\').Last() + "\"\r\n" +
"Content-Type: image/"+pic.Split('.').Last().Replace("jpg","jpeg")+"\r\n\r\n"));
                req.AddRange(File.ReadAllBytes(pic));
                req.AddRange(Encoding.UTF8.GetBytes("\r\n------WebKitFormBoundary0ajPSMhebdZCGOl2--\r\n"));
                byte[] data = req.ToArray();
                web.ContentLength = data.Length;
                using (var w = web.GetRequestStream())
                    w.Write(data, 0, data.Length);
                web.GetResponse();
                return true;
            }
            catch { }
            return false;
        }

        public static string GetUA()
        {
            return new[] {
                "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0.2623.112 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_10_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2227.1 Safari/537.36",
                "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2227.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2227.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1",
                "Mozilla/5.0 (Windows NT 5.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2224.3 Safari/537.36",
                "Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/40.0.2214.93 Safari/537.36",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_10_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/37.0.2062.124 Safari/537.36",
                "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/37.0.2049.0 Safari/537.36",
                "Mozilla/5.0 (Windows NT 4.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/37.0.2049.0 Safari/537.36"
            }[new Random().Next(0,10)];
        }
    }
}
