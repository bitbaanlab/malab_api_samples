﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLib
{
    public class MALabLib
    {
        private const string USER_AGENT = "BitBaan-API-Sample-VBNet";

        private string server_address;
        private string api_key;
        JObject unknownerror_respone_json;

        public MALabLib(string server_address, string api_key = "")
        {
            this.server_address = server_address;
            this.api_key = api_key;

            this.unknownerror_respone_json = new JObject();
            this.unknownerror_respone_json.Add("error_code", 900);
            this.unknownerror_respone_json.Add("success", false);
        }

        public string get_sha256(string file_path) {
            using (var sha256_var = SHA256.Create())
            {
                using (var stream = File.OpenRead(file_path))
                {
                    byte[] file_sha256 = sha256_var.ComputeHash(stream);
                    StringBuilder Hex = new StringBuilder(file_sha256.Length * 2);
                    foreach (Byte b in file_sha256)
                        Hex.AppendFormat("{0:x2}", b);
                    return Hex.ToString();
                }
            }
        }

        private JObject call_api_with_json_input(string api, JObject json_input) {
            HttpWebRequest HttpWebRequest = (HttpWebRequest)WebRequest.Create(this.server_address + "/" + api);
            HttpWebRequest.ContentType = "application/json";
            HttpWebRequest.Method = "POST";
            HttpWebRequest.UserAgent = USER_AGENT;
            using (var streamWriter = new StreamWriter(HttpWebRequest.GetRequestStream()))
            {
                string parsedContent = json_input.ToString();
                streamWriter.Write(parsedContent);
                streamWriter.Flush();
                streamWriter.Close();
            }
            string result;
            try
            {
                HttpWebResponse httpResponse = (HttpWebResponse)HttpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    result = streamReader.ReadToEnd();
            }
            catch (WebException ex)
            {
                if (ex.Response == null)
                    return this.unknownerror_respone_json;
                using (var stream = ex.Response.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream))
                        result = reader.ReadToEnd();
                }
            }
            try
            {
                JObject srtr;
                srtr = JObject.Parse(result);
                return srtr;
            }
            catch (Exception)
            {
                return unknownerror_respone_json;
            }
        }

        private JObject call_api_with_form_input(string api, JObject data_input, string file_param_name, string file_path) {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");
            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(this.server_address + "/" + api);
            wr.ContentType = "multipart/form-data; boundary=" + boundary;
            wr.Method = "POST";
            wr.KeepAlive = true;
            wr.Credentials = System.Net.CredentialCache.DefaultCredentials;
            Stream rs = wr.GetRequestStream();
            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            JToken data_input_mover = data_input.First;
            for (int i = 0; i < data_input.Count; i++)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string key = ((JProperty)data_input_mover).Name;
                string value = data_input_mover.First.Value<string>();
                string formitem = String.Format(formdataTemplate, key, value);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
                data_input_mover = data_input_mover.Next;
            }

            rs.Write(boundarybytes, 0, boundarybytes.Length);
            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\n\r\n";
            string header = String.Format(headerTemplate, file_param_name, "file");
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            rs.Write(headerbytes, 0, headerbytes.Length);
            FileStream fs = new FileStream(file_path, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[4096];
            int bytesRead = 0;
            do
            {
                bytesRead = fs.Read(buffer, 0, buffer.Length);
                if (bytesRead != 0)
                    rs.Write(buffer, 0, bytesRead);
            } while (bytesRead != 0);
            fs.Close();

            byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();

            string result;
            try
            {
                HttpWebResponse httpResponse = (HttpWebResponse)wr.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    result = streamReader.ReadToEnd();
            }
            catch (WebException ex)
            {
                if (ex.Response == null)
                    return unknownerror_respone_json;
                using (var stream = ex.Response.GetResponseStream())
                {
                    using (var reader = new StreamReader(stream))
                        result = reader.ReadToEnd();
                }
            }
            try
            {
                JObject srtr;
                srtr = JObject.Parse(result);
                return srtr;
            }
            catch (Exception)
            {
                return unknownerror_respone_json;
            }
        }

        public JObject login(string email, string password)
        {
            JObject j_params = new JObject();
            j_params.Add("email", email);
            j_params.Add("password", password);
            JObject retValue = call_api_with_json_input("api/v1/user/login", j_params);
            if (retValue.SelectToken("success").ToObject<bool>() == true)
                this.api_key = retValue.SelectToken("apikey").ToObject<string>();
            return retValue;
        }

        public JObject scan(string file_path, string file_name, bool is_private = false, string file_origin = "")
        {
            JObject j_params = new JObject();
            j_params.Add("filename", file_name);
            j_params.Add("apikey", this.api_key);
            if (is_private == true)
                j_params.Add("is_private", true);
            if (file_origin.Length != 0)
                j_params.Add("fileorigin", file_origin);
            return call_api_with_form_input("api/v1/scan", j_params, "filedata", file_path);
        }

        public JObject rescan(string file_sha256)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("sha256", file_sha256);
            return call_api_with_json_input("api/v1/rescan", j_params);
        }

        public JObject results(string file_sha256, int scan_id)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("sha256", file_sha256);
            j_params.Add("scan_id", scan_id);
            return call_api_with_json_input("api/v1/search/scan/results", j_params);
        }

        public JObject search_by_hash(string hash, int ot = 0, int ob = 0, int page = 0, int per_page = 0)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("hash", hash);
            if (ot != 0)
                j_params.Add("ot", ot);
            if (ob != 0)
                j_params.Add("ob", ob);
            if (page != 0)
                j_params.Add("page", page);
            if (per_page != 0)
                j_params.Add("per_page", per_page);
            return call_api_with_json_input("api/v1/search/scan/hash", j_params);
        }

        public JObject search_by_malware_name(string malware_name, int ot = 0, int ob = 0, int page = 0, int per_page = 0)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("malware_name", malware_name);
            if (ot != 0)
                j_params.Add("ot", ot);
            if (ob != 0)
                j_params.Add("ob", ob);
            if (page != 0)
                j_params.Add("page", page);
            if (per_page != 0)
                j_params.Add("per_page", per_page);
            return call_api_with_json_input("api/v1/search/scan/malware-name", j_params);
        }

        public JObject download_file(string hash_value)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("hash", hash_value);
            return call_api_with_json_input("api/v1/file/download", j_params);
        }

        public JObject get_comments(string sha256, int page = 0, int per_page = 0)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("sha256", sha256);
            if (page != 0)
                j_params.Add("page", page);
            if (per_page != 0)
                j_params.Add("per_page", per_page);
            return call_api_with_json_input("api/v1/comment", j_params);
        }

        public JObject add_comment(string sha256, string description)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("sha256", sha256);
            j_params.Add("description", description);
            return call_api_with_json_input("api/v1/comment/add", j_params);
        }

        public JObject edit_comment(int comment_id, string new_description)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("comment_id", comment_id);
            j_params.Add("description", new_description);
            return call_api_with_json_input("api/v1/comment/edit", j_params);
        }

        public JObject delete_comment(int comment_id)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("comment_id", comment_id);
            return call_api_with_json_input("api/v1/comment/delete", j_params);
        }

        public JObject approve_comment(int comment_id)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            j_params.Add("comment_id", comment_id);
            return call_api_with_json_input("api/v1/comment/approve", j_params);
        }

        public JObject get_captcha()
        {
            JObject j_params = new JObject();
            return call_api_with_json_input("api/v1/captcha", j_params);
        }

        public JObject register(string first_name, string last_name, string username, string email, string password, string captcha)
        {
            JObject j_params = new JObject();
            j_params.Add("firstname", first_name);
            j_params.Add("lastname", last_name);
            j_params.Add("username", username);
            j_params.Add("email", email);
            j_params.Add("password", password);
            j_params.Add("captcha", captcha);
            return call_api_with_json_input("api/v1/user/register", j_params);
        }

        public JObject advanced_search(int scan_id = 0, string file_name = "", string malware_name = "", string hash = "", string origin = "", string analyzed = "", string has_origin = "", int ot = 0, int ob = 0, int page = 0,int per_page = 0)
        {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            if (scan_id != 0)
                j_params.Add("scan_id", scan_id);
            if (file_name.Length != 0)
                j_params.Add("filename", file_name);
            if (malware_name.Length != 0)
                j_params.Add("malware_name", malware_name);
            if (hash.Length != 0)
                j_params.Add("hash", hash);
            if (origin.Length != 0)
                j_params.Add("origin", origin);
            if (analyzed.Length != 0)
                j_params.Add("analyzed", analyzed);
            if (has_origin.Length != 0)
                j_params.Add("has_origin", has_origin);
            if (ot != 0)
                j_params.Add("ot", ot);
            if (ob != 0)
                j_params.Add("ob", ob);
            if (page != 0)
                j_params.Add("page", page);
            if (per_page != 0)
                j_params.Add("per_page", per_page);
            return call_api_with_json_input("api/v1/search/scan/advanced", j_params);
        }

        public JObject get_av_list() {
            JObject j_params = new JObject();
            j_params.Add("apikey", this.api_key);
            return call_api_with_json_input("api/v1/search/av_list", j_params);
        }

    }
}
