using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace CBot
{
    class IWProxy : WebProxy
    {
        public IWProxy(string host, string port, string[] auth = null) : base(host,Int32.Parse(port))
        {
            if(auth != null)
                Credentials = new NetworkCredential(auth[0], auth[1]);
        }
    }

    class Instagram
    {
        public const string API_KEY = "25eace5393646842f0d0c3fb2ac7d3cfa15c052436ee86b5406a8433f54d24a5";

        static Instagram()
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => { return true; };
        }
        
        public static bool IsAvailable(CookieContainer cookies, IWProxy proxy, string UA, string username, string ID)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/si/fetch_headers/?challenge_type=signup&guid=" + ID);
            r.Method = "GET";
            r.UserAgent = UA;
            r.CookieContainer = cookies;
            r.Proxy = proxy;
            r.GetResponse().Close();
            string boundary = Utils.RandomStr();
            r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/users/check_username/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = cookies;
            r.Proxy = proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "username", "_uuid", "_csrftoken" },
                new[] { username, ID, "missing"}));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                using(var s = new StreamReader(r.GetResponse().GetResponseStream()))
                    return s.ReadToEnd().Contains("\"available\":true");
            }
            catch (Exception){}
            return false;
        }

        public static Account Register(CookieContainer cookies, IWProxy proxy, string UA, string ID, string username, string password, string email)
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/accounts/create/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = cookies;
            r.Proxy = proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "username", "_uuid", "_csrftoken", "password", "device_id", "email" },
                new[] { username, ID, cookies.GetCookie("csrftoken"), password, ID, email }));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                Console.WriteLine(new StreamReader(r.GetResponse().GetResponseStream()).ReadToEnd());
                ("Registered " + username).Log(LogType.INFO);
                return new Account(cookies, proxy, UA, ID, username);
            }
            catch (Exception)
            {
                ("Cannot register " + username).Log(LogType.ERROR);
            }
            return null;
        }

        public static Account Login(CookieContainer cookies, IWProxy proxy, string UA, string ID, string username, string password)
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/accounts/login/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary="+boundary;
            r.CookieContainer = cookies;
            r.Proxy = proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "username", "_uuid", "csrftoken", "password", "device_id", "from_reg" },
                new[] { username, ID, "missing", password, ID, "false" }));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                r.GetResponse().Close();
                ("Logged in with " + username).Log(LogType.INFO);
                return new Account(cookies,proxy,UA,ID,username);
            }
            catch (WebException e){
                ("Cannot login with " + username).Log(LogType.ERROR);
                if (new StreamReader(e.Response.GetResponseStream()).ReadToEnd().Contains("checkpoint_required"))
                    ("Checkpoint required for " + username).Log(LogType.WARNING);
            }
            return null;
        }

        public static string[] GetFollowing(string id, string max_id = "0", List<string> ret = null)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/friendships/" + id + "/following/?ig_sig_key_version=5&max_id=" + max_id);
            r.Method = "GET";
            r.UserAgent = Utils.RandomUA();
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    if (ret == null)
                        ret = new List<string>();
                    var res = s.ReadToEnd();
                    try
                    {
                        ret.AddRange(GetFollowing(id, res.Split(new[] { "\"next_max_id\":" }, StringSplitOptions.None)[1].Split('}')[0], ret));
                    }
                    catch (Exception) { }
                    foreach (Match m in Regex.Matches(res, "\"pk\":([0-9]{1,11}),"))
                        ret.Add(m.Value.Replace("\"pk\":", "").Replace(",", ""));
                    return ret.Distinct().ToArray();
                }
            }
            catch (Exception) { }
            return null;
        }

        public static string[] GetFollowers(string id, string max_id = "0", List<string> ret = null)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/friendships/" + id + "/followers/?ig_sig_key_version=5&max_id=" + max_id);
            r.Method = "GET";
            r.UserAgent = Utils.RandomUA();
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    if (ret == null)
                        ret = new List<string>();
                    var res = s.ReadToEnd();
                    try
                    {
                        ret.AddRange(GetFollowers(id, res.Split(new[] { "\"next_max_id\":" }, StringSplitOptions.None)[1].Split('}')[0], ret));
                    }
                    catch (Exception) { }
                    foreach (Match m in Regex.Matches(res, "\"pk\":([0-9]{1,11}),"))
                        ret.Add(m.Value.Replace("\"pk\":", "").Replace(",", ""));
                    return ret.Distinct().ToArray();
                }
            }
            catch (Exception) { }
            return null;
        }

        public static string[] GetLikers(string id)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/media/" + id + "/likers/?ig_sig_key_version=5");
            r.Method = "GET";
            r.UserAgent = Utils.RandomUA();
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    foreach (Match m in Regex.Matches(res, "\"pk\":([0-9]{1,11}),"))
                        ret.Add(m.Value.Replace("\"pk\":", "").Replace(",", ""));
                    return ret.Distinct().ToArray();
                }
            }
            catch (Exception) { }
            return null;
        }

        public static string GetMediaID(string url)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://api.instagram.com/oembed/?callback=&url=" + url);
            r.Method = "GET";
            r.UserAgent = Utils.RandomUA();
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    return s.ReadToEnd().Split(new[]{"\"media_id\":\""},StringSplitOptions.None)[1].Split('"')[0];
                }
            }
            catch (Exception) { }
            return null;
        }

        public static string GetUsernameID(string username)
        {
            ///api/v1/users/USERID/info/
            return null;
        }

        public static string GetIDUsername(string ID)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/users/" + ID + "/info/");
            r.Method = "GET";
            r.UserAgent = Utils.RandomUA();
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    return s.ReadToEnd().Split(new[] { "\"username\":\"" }, StringSplitOptions.None)[1].Split('"')[0];
                }
            }
            catch (Exception) { }
            return null;
        }

        public static long GetMediaCount(string tag)
        {
            var r = (HttpWebRequest)WebRequest.Create("http://i.instagram.com/api/v1/tags/" + tag + "/info/");
            r.Method = "GET";
            r.UserAgent = Utils.RandomUA();
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    return long.Parse(s.ReadToEnd().Split(new[] { "\"media_count\":" }, StringSplitOptions.None)[1].Split('}')[0]);
                }
            }
            catch (Exception) { }
            return 0;
        }
    }

    class Account
    {
        public readonly CookieContainer Auth;
        public readonly IWProxy Proxy;
        public readonly string UA, ID, Name;

        public Account(CookieContainer auth, IWProxy proxy, string ua, string id, string username)
        {
            Auth = auth;
            Proxy = proxy;
            UA = ua;
            ID = id;
            Name = username;
        }

        public string[] ExploreUsers(string max_id = null)
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/discover/ayml/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(cdata.Module);
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    foreach (Match m in Regex.Matches(res, "\"pk\":\"([0-9]{1,11})\","))
                        ret.Add(m.Value.Replace("\"pk\":\"", "").Replace("\",", ""));
                    return ret.Distinct().ToArray();
                }
            }
            catch (Exception){}
            return null;
        }

        public Tuple<string[], string> GetMediasLiked(string max_id = null)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/feed/liked/" + (max_id != null ? "?max_id=" + max_id : ""));
            r.Method = "GET";
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    try
                    {
                        max_id = res.Split(new[] { "\"next_max_id\":" }, StringSplitOptions.None)[1].Split('}')[0];
                    }
                    catch (Exception) { max_id = null; }
                    var medias = Regex.Matches(res, "\"media_id\":([0-9]+),");
                    var users = Regex.Matches(res, "\"pk\":([0-9]+),");
                    for (int i = 0; i < medias.Count; i++)
                        ret.Add(medias[i].Value.Replace("\"media_id\":", "").Replace(",", "") + '_' + users[i].Value.Replace("\"pk\":", "").Replace(",", ""));
                    return new Tuple<string[], string>(ret.Distinct().ToArray(), max_id);
                }
            }
            catch (Exception) { }
            return null;
        }

        public string[] GetPopulars(string max_id = "0")
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/feed/popular/?max_id=" + max_id);
            r.Method = "GET";
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    var medias = Regex.Matches(res, "\"media_id\":([0-9]+),");
                    var users = Regex.Matches(res, "\"pk\":([0-9]+),");
                    for (int i = 0; i < medias.Count; i++)
                        ret.Add(medias[i].Value.Replace("\"media_id\":", "").Replace(",", "") + '_' + users[i].Value.Replace("\"pk\":", "").Replace(",", ""));
                    return ret.Distinct().ToArray();
                }
            }
            catch (Exception) { }
            return null;
        }

        public string[] GetSuggestedUsers()
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/friendships/suggested/");
            r.Method = "POST";
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    foreach (Match m in Regex.Matches(res, "\"pk\":\"([0-9]{1,11})\","))
                        ret.Add(m.Value.Replace("\"pk\":\"", "").Replace("\",", ""));
                    return ret.Distinct().ToArray();
                }
            }
            catch (Exception) { }
            return null;
        }

        public Tuple<string[], string> GetMediasFromTag(string tag, string max_id = null)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/feed/tag/" + tag + "/" + (max_id != null ? "?max_id=" + max_id : ""));
            r.Method = "GET";
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    try
                    {
                        max_id = res.Split(new[] { "\"next_max_id\":" }, StringSplitOptions.None)[1].Split('}')[0];
                    }
                    catch (Exception) { max_id = null; }
                    var medias = Regex.Matches(res, "\"media_id\":([0-9]+),");
                    var users = Regex.Matches(res, "\"pk\":([0-9]+),");
                    for (int i = 0; i < medias.Count; i++)
                        ret.Add(medias[i].Value.Replace("\"media_id\":", "").Replace(",", "") + '_' + users[i].Value.Replace("\"pk\":", "").Replace(",", ""));
                    return new Tuple<string[], string>(ret.Distinct().ToArray(),max_id);
                }
            }
            catch (Exception) { }
            return null;
        }

        public Tuple<string[], string> GetFromTag(string tag, string max_id = null)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/feed/tag/" + tag + "/" + (max_id != null ? "?max_id=" + max_id : ""));
            r.Method = "GET";
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    try
                    {
                        max_id = res.Split(new[] { "\"next_max_id\":" }, StringSplitOptions.None)[1].Split('}')[0];
                    }
                    catch (Exception) { max_id = null; }
                    foreach (Match m in Regex.Matches(res, "\"pk\":([0-9]{1,11}),")) //for(;;) usernames
                        ret.Add(m.Value.Replace("\"pk\":", "").Replace(",", ""));
                    return new Tuple<string[], string>(ret.Distinct().ToArray(), max_id);
                }
            }
            catch (Exception) { }
            return null;
        }

        public Tuple<string[], string> GetMediasFromLocation(string location, string max_id = null)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/feed/location/" + location + "/" + (max_id != null ? "?max_id=" + max_id : ""));
            r.Method = "GET";
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    try
                    {
                        max_id = res.Split(new[] { "\"next_max_id\":" }, StringSplitOptions.None)[1].Split('}')[0];
                    }
                    catch (Exception) { max_id = null; }
                    var medias = Regex.Matches(res, "\"media_id\":([0-9]+),");
                    var users = Regex.Matches(res, "\"pk\":([0-9]+),");
                    for (int i = 0; i < medias.Count; i++)
                        ret.Add(medias[i].Value.Replace("\"media_id\":", "").Replace(",", "") + '_' + users[i].Value.Replace("\"pk\":", "").Replace(",", ""));
                    return new Tuple<string[], string>(ret.Distinct().ToArray(), max_id);
                }
            }
            catch (Exception) { }
            return null;
        }

        public Tuple<string[], string> GetFromLocation(string location, string max_id = null)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/feed/location/" + location + "/" + (max_id != null ? "?max_id=" + max_id : ""));
            r.Method = "GET";
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    try
                    {
                        max_id = res.Split(new[] { "\"next_max_id\":" }, StringSplitOptions.None)[1].Split('}')[0];
                    }
                    catch (Exception) { max_id = null; }
                    foreach (Match m in Regex.Matches(res, "\"pk\":([0-9]{1,11}),"))
                        ret.Add(m.Value.Replace("\"pk\":", "").Replace(",",""));
                    return new Tuple<string[], string>(ret.Distinct().ToArray(), max_id);
                }
            }
            catch (Exception) { }
            return null;
        }

        public string[] GetCommentators(string id)
        {
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/media/" + id + "/comments/");
            r.Method = "GET";
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            try
            {
                using (var s = new StreamReader(r.GetResponse().GetResponseStream()))
                {
                    var ret = new List<string>();
                    var res = s.ReadToEnd();
                    foreach (Match m in Regex.Matches(res, "\"pk\":([0-9]{1,11}),"))
                        ret.Add(m.Value.Replace("\"pk\":", "").Replace(",", ""));
                    return ret.Distinct().ToArray();
                }
            }
            catch (Exception) { }
            return null;
        }

        public bool Follow(string id)
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/friendships/create/"+id+"/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "_uuid", "_csrftoken", "_uid", "user_id" },
                new[] { ID, Auth.GetCookie("csrftoken"), Auth.GetCookie("ds_user_id"), id }));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                r.GetResponse().Close();
                (Name + " Followed " + id).Log(LogType.INFO);
                return true;
            }
            catch (Exception)
            {
                (Name + " Cannot follow " + id).Log(LogType.ERROR);
            }
            return false;
        }

        public bool Unfollow(string id)
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/friendships/destroy/" + id + "/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "_uuid", "_csrftoken", "_uid", "user_id" },
                new[] { ID, Auth.GetCookie("csrftoken"), Auth.GetCookie("ds_user_id"), id }));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                r.GetResponse().Close();
                (Name + " Unfollowed " + id).Log(LogType.INFO);
                return true;
            }
            catch (Exception)
            {
                (Name + " Cannot unfollow " + id).Log(LogType.ERROR);
            }
            return false;
        }

        public bool Like(string id) //PHOTO
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/media/" + id + "/like/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "_uuid", "_csrftoken", "_uid", "media_id" },
                new[] { ID, Auth.GetCookie("csrftoken"), Auth.GetCookie("ds_user_id"), id }));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                r.GetResponse().Close();
                (Name + " Liked " + id).Log(LogType.INFO);
                return true;
            }
            catch (Exception)
            {
                (Name + " Cannot like " + id).Log(LogType.ERROR);
            }
            return false;
        }

        public bool Unlike(string id) //PHOTO
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/media/" + id + "/unlike/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "_uuid", "_csrftoken", "_uid", "media_id" },
                new[] { ID, Auth.GetCookie("csrftoken"), Auth.GetCookie("ds_user_id"), id }));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                r.GetResponse().Close();
                (Name + " Unliked " + id).Log(LogType.INFO);
                return true;
            }
            catch (Exception)
            {
                (Name + " Cannot unlike " + id).Log(LogType.ERROR);
            }
            return false;
        }

        public bool Comment(string id, string comment) //PHOTO
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/media/" + id + "/comment/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "_uuid", "_csrftoken", "comment_text", "_uid" },
                new[] { ID, Auth.GetCookie("csrftoken"), comment, Auth.GetCookie("ds_user_id") }));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                r.GetResponse().Close();
                (Name + " Commented " + id).Log(LogType.INFO);
                return true;
            }
            catch (Exception)
            {
                (Name + " Cannot comment " + id).Log(LogType.ERROR);
            }
            return false;
        }

        /*
--99E23E9A-3D21-4331-BAA5-6D6122EDE131
Content-Disposition: form-data; name="upload_id"

1454510224
--99E23E9A-3D21-4331-BAA5-6D6122EDE131
Content-Disposition: form-data; name="photo"; filename="photo"
Content-Type: image/jpeg


--99E23E9A-3D21-4331-BAA5-6D6122EDE131--
 */
        public string Upload(string file, string caption)
        {
            string upid = Utils.Timestamp().ToString();
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/upload/photo/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            r.Timeout = 1000 * 120;
            var d = new List<byte>(Encoding.ASCII.GetBytes(
                cdata.Upload.Replace("%id%", upid)
                .Replace("%type%", "image/" + file.Split('.').Last().Replace("jpg", "jpeg"))
                .Replace("%boundary%", boundary)));
            d.AddRange(File.ReadAllBytes(file));
            d.AddRange(Encoding.ASCII.GetBytes("\r\n--"+boundary+"--"));
            byte[] sign = d.ToArray();
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                r.GetResponse().Close();
                boundary = Utils.RandomStr();
                r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/media/configure/");
                r.Method = "POST";
                r.ContentType = "multipart/form-data; boundary=" + boundary;
                r.CookieContainer = Auth;
                r.Proxy = Proxy;
                r.UserAgent = UA;
                sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                    new[] { "_uuid", "_csrftoken", "_uid", "caption", "camera_position", "geotag_enabled", "source_type", "upload_id", "waterfall_id" },
                    new[] { ID, Auth.GetCookie("csrftoken"), Auth.GetCookie("ds_user_id"), caption, "unknown", "false", "0", upid, Utils.WaterfallId() })); //waterfall?
                r.GetRequestStream().Write(sign, 0, sign.Length);
                try
                {
                    string id = null;
                    using(var s = new StreamReader(r.GetResponse().GetResponseStream()))
                        id = s.ReadToEnd().Split(new[]{"\"code\":\""},StringSplitOptions.None)[1].Split('"')[0];
                    /*string id = "";
                    using(var s = new StreamReader(r.GetResponse().GetResponseStream())) 
                        id = s.ReadToEnd().Split(new[]{"\"id\":\""},StringSplitOptions.None)[1].Split('"')[0];
                    r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/feed/timeline/?unseen_ad_media_id=" + id);
                    r.Method = "GET";
                    r.CookieContainer = Auth;
                    r.Proxy = Proxy;
                    r.UserAgent = UA;
                    r.GetResponse().Close();*/
                    (Name + " Uploaded " + file).Log(LogType.INFO);
                    return id;
                }
                catch (WebException e)
                {
                    Console.WriteLine("ERR" + new StreamReader(e.Response.GetResponseStream()).ReadToEnd());
                }
            }
            catch (Exception){
                (Name + " Cannot upload " + file).Log(LogType.ERROR);
            }
            return null;
        }

        public bool Delete(string id) //PHOTO
        {
            string boundary = Utils.RandomStr();
            var r = (HttpWebRequest)WebRequest.Create("https://i.instagram.com/api/v1/media/" + id + "/delete/");
            r.Method = "POST";
            r.ContentType = "multipart/form-data; boundary=" + boundary;
            r.CookieContainer = Auth;
            r.Proxy = Proxy;
            r.UserAgent = UA;
            byte[] sign = Encoding.ASCII.GetBytes(Utils.GenSignature(boundary,
                new[] { "_uuid", "_csrftoken", "_uid", "media_id" },
                new[] { ID, Auth.GetCookie("csrftoken"), Auth.GetCookie("ds_user_id"), id }));
            r.GetRequestStream().Write(sign, 0, sign.Length);
            try
            {
                r.GetResponse().Close();
                (Name + " Deleted " + id).Log(LogType.INFO);
                return true;
            }
            catch (Exception)
            {
                (Name + " Cannot delete " + id).Log(LogType.ERROR);
            }
            return false;
        }
    }

    public enum LogType
    {
        ERROR,
        WARNING,
        STARTING,
        FINISHED,
        INFO
    }

    static class Utils
    {
        static readonly string[] UA = 
        {
            "Instagram 6.21.2 Android (21/5.0.2; 480dpi; 1080x1776; LGE/Google; Nexus 5; hammerhead; hammerhead; en_US)" //ADD UAs
        };
        
        public static CookieContainer Init(this CookieContainer container)
        {
            container.Add(new Cookie("lmao", "test", "/", "i.instagram.com"));
            return container;
        }

        public static CookieCollection Get(this CookieContainer container)
        {
            return container.GetCookies(new Uri("http://i.instagram.com"));
        }

        public static string GetCookie(this CookieContainer container, string name)
        {
            foreach (Cookie k in container.Get())
                if (k.Name == name)
                    return k.Value;
            return "missing";
        }

        public static string RandomUA()
        {
            var rand = new Random();
            return UA[rand.Next(0, UA.Length - 1)];
        }

        public static string RandomStr(bool d = false)
        {
            var s = Guid.NewGuid().ToString();
            return d ? s.Replace("-", "") : s.ToUpper();
        }

        public static string WaterfallId()
        {
            string text = "qwertyuiopasdfghjklzxcvbnm0123456789";
            int num = 32;
            char[] array = new char[num];
            var rand = new Random();
            for (int i = 0; i < array.Length; i++)
                array[i] = text[rand.Next(text.Length)];
            return new string(array);
        }

        public static string GenSignature(string boundary, string[] keys, string[] values)
        {
            string data = "{";
            for (int i = 0; i < keys.Length; i++)
                data += "\""+keys[i]+"\":\"" + values[i].Replace("\"","\\\"") + "\",";
            data = data.Substring(0, data.Length - 1) + "}";
            return CBot.cdata.Request
                .Replace("%sign%", hmacSHA256(data, Instagram.API_KEY))
                .Replace("%body%", data)
                .Replace("%boundary%", boundary);
        }

        public static void Log(this string text, LogType type = LogType.INFO)
        {
            Console.WriteLine("[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString() + "][" + type.ToString() + "] " + text);
        }

        public static string hmacSHA256(string data, string key)
        {
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.ASCII.GetBytes(key)))
            {
                return BitConverter.ToString(hmac.ComputeHash(Encoding.ASCII.GetBytes(data))).Replace("-", "").ToLower();
            }
        }

        public static long Timestamp()
        {
            var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)timeSpan.TotalSeconds;
        }
    }
}
