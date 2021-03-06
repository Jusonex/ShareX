﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (C) 2007-2014 ShareX Developers

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using HelpersLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;

namespace UploadersLib.FileUploaders
{
    public sealed class OwnCloud : FileUploader
    {
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Path { get; set; }
        public bool CreateShare { get; set; }
        public bool DirectLink { get; set; }
        public bool IgnoreInvalidCert { get; set; }

        public OwnCloud(string host, string username, string password)
        {
            Host = host;
            Username = username;
            Password = password;
        }

        public override UploadResult Upload(Stream stream, string fileName)
        {
            if (string.IsNullOrEmpty(Host))
            {
                throw new Exception("ownCloud Host is empty.");
            }

            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                throw new Exception("ownCloud Username or Password is empty.");
            }

            if (string.IsNullOrEmpty(Path))
            {
                Path = "/";
            }

            string path = URLHelpers.CombineURL(Path, fileName);
            string url = URLHelpers.CombineURL(Host, "remote.php/webdav", path);
            url = URLHelpers.FixPrefix(url);
            NameValueCollection headers = CreateAuthenticationHeader(Username, Password);

            SSLBypassHelper sslBypassHelper = null;
            if (IgnoreInvalidCert)
                sslBypassHelper = new SSLBypassHelper();
            
            string response = SendRequestStream(url, stream, Helpers.GetMimeType(fileName), headers, method: HttpMethod.PUT);

            UploadResult result = new UploadResult(response);

            if (!IsError)
            {
                if (CreateShare)
                {
                    AllowReportProgress = false;
                    result.URL = ShareFile(path);
                }
                else
                {
                    result.IsURLExpected = false;
                }
            }

            if (sslBypassHelper != null)
                sslBypassHelper.Dispose();

            return result;
        }

        // http://doc.owncloud.org/server/7.0/developer_manual/core/ocs-share-api.html#create-a-new-share
        public string ShareFile(string path)
        {
            Dictionary<string, string> args = new Dictionary<string, string>();
            args.Add("path", path); // path to the file/folder which should be shared
            args.Add("shareType", "3"); // ‘0’ = user; ‘1’ = group; ‘3’ = public link
            // args.Add("shareWith", ""); // user / group id with which the file should be shared
            // args.Add("publicUpload", "false"); // allow public upload to a public shared folder (true/false)
            // args.Add("password", ""); // password to protect public link Share with
            args.Add("permissions", "1"); // 1 = read; 2 = update; 4 = create; 8 = delete; 16 = share; 31 = all (default: 31, for public shares: 1)

            string url = URLHelpers.CombineURL(Host, "ocs/v1.php/apps/files_sharing/api/v1/shares?format=json");
            NameValueCollection headers = CreateAuthenticationHeader(Username, Password);
            string response = SendRequest(HttpMethod.POST, url, args, headers);

            if (!string.IsNullOrEmpty(response))
            {
                OwnCloudShareResponse result = JsonConvert.DeserializeObject<OwnCloudShareResponse>(response);

                if (result != null && result.ocs != null && result.ocs.meta != null)
                {
                    if (result.ocs.data != null && result.ocs.meta.statuscode == 100)
                    {
                        string link = result.ocs.data.url;
                        if (DirectLink) link += "&download";
                        return link;
                    }
                    else
                    {
                        Errors.Add(string.Format("Status: {0}\r\nStatus code: {1}\r\nMessage: {2}", result.ocs.meta.status, result.ocs.meta.statuscode, result.ocs.meta.message));
                    }
                }
            }

            return null;
        }

        public class OwnCloudShareResponse
        {
            public OwnCloudShareResponseOcs ocs { get; set; }
        }

        public class OwnCloudShareResponseOcs
        {
            public OwnCloudShareResponseMeta meta { get; set; }
            public OwnCloudShareResponseData data { get; set; }
        }

        public class OwnCloudShareResponseMeta
        {
            public string status { get; set; }
            public int statuscode { get; set; }
            public string message { get; set; }
        }

        public class OwnCloudShareResponseData
        {
            public int id { get; set; }
            public string url { get; set; }
            public string token { get; set; }
        }
    }
}