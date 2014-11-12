using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.IO;

namespace FtpUtil
{
    /// <summary>
    /// Connect to a FTP server to list, transfer, delete and rename files
    /// </summary>
    public class myFTP
    {
        public string Server { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string RootFolder { get; set; }
        public FtpStatusCode LastStatusCode { get; set; }
        public string LastStatusDescription { get; set; }
        public string LastErrorMessage { get; set; }
        private string _workingFolder = "";
        private string _lastWorkingFolderListed = null;
        private bool _fileInfoListDirty = true;
        private bool getFileTimeStampNotSupported = false;
        private List<myFTPFileInfo> _fileInfoList = new List<myFTPFileInfo>();

        public myFTP(string server, string user, string password, string folder) {
            Server = server;
            User = user;
            Password = password;
            RootFolder = folder.Trim();
            if (RootFolder == null) {
                RootFolder = "";
            };
            if (RootFolder.Length > 0 && !RootFolder.StartsWith("/")) {
                RootFolder = "/" + RootFolder;
            };
            if (RootFolder.EndsWith("/")) {
                RootFolder = RootFolder.Substring(0, RootFolder.Length - 1);
            }
        }

        /// <summary>
        /// Obtains a simple file list with the filenames on the FTP server using a wildcard starting at the current remote FTP folder
        /// </summary>
        /// <param name="fileSpec">A wildcard to specify the filenames to be searched for and listed. Example: "subfolder/*.txt"</param>
        /// <returns>A list with the filenames found</returns>
        public List<string> GetFileList(string fileSpec) {
            if (fileSpec == null) fileSpec = "";
            if (fileSpec.Length > 0 && !fileSpec.StartsWith("/")) {
                fileSpec = "/" + fileSpec;
            }
            var ret = new List<string>();
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + fileSpec);
            req.Proxy = null;
            req.EnableSsl = false;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.ListDirectory;
            using (WebResponse resp = req.GetResponse()) {
                using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                    while (!reader.EndOfStream) {
                        ret.Add(reader.ReadLine());
                    };
                };
                RecordResponseStatus((FtpWebResponse)resp);
            }
            return ret;
        }

        /// <summary>
        /// Obtains a detailed list of the filenames in the current remote FTP folder
        /// </summary>
        /// <returns>A detailed list of files found</returns>
        public List<myFTPFileInfo> GetFileListDetailed() {
            return GetFileListDetailed("", false);
        }

        /// <summary>
        /// Obtains a detailed list of the files in the current remote FTP folder according to a wildcard filename specification
        /// </summary>
        /// <param name="fileSpec">Wildcard filename specification. Can contain subfolders and *'s. Example: subfolder/*.txt</param>
        /// <returns>A detailed list of files found</returns>
        /// <remarks>If wildcard is specified the resulting file list is not cached</remarks>
        public List<myFTPFileInfo> GetFileListDetailed(string fileSpec) {
            return GetFileListDetailed(fileSpec, false);
        }

        /// <summary>
        /// Obtains a detailed list of the files in the current remote FTP folder according to a wildcard filename specification and allows to bypass the cached filelist
        /// </summary>
        /// <param name="fileSpec">Wildcard filename specification. Can contain subfolders and *'s. Example: subfolder/*.txt</param>
        /// <param name="forceRefresh">If true the cache is ignored</param>
        /// <returns></returns>
        /// <remarks>If wildcard is specified or forceRefresh is set to true the resulting file list is not cached</remarks>
        public List<myFTPFileInfo> GetFileListDetailed(string fileSpec, bool forceRefresh) {
            if (fileSpec == null || fileSpec.Trim().Length == 0) {
                fileSpec = "*";
            }
            if (fileSpec.Length > 0 && !fileSpec.StartsWith("/")) {
                fileSpec = "/" + fileSpec;
            }

            List<myFTPFileInfo> fileList = new List<myFTPFileInfo>();
            if (forceRefresh ||
                        fileSpec.Trim().Length > 0 ||
                        _fileInfoListDirty ||
                        _lastWorkingFolderListed != _workingFolder) {
                var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + fileSpec);
                req.Proxy = null;
                req.EnableSsl = false;
                req.Credentials = new NetworkCredential(User, Password);
                req.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

                myFTPFileInfo ftpFileInfo;
                _fileInfoList.Clear();
                try {
                    using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse()) {
                        using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                            while (!reader.EndOfStream) {
                                ftpFileInfo = new myFTPFileInfo(reader.ReadLine());
                                if (ftpFileInfo.Nombre != null && ftpFileInfo.Nombre.Trim().Length > 0) {
                                    fileList.Add(ftpFileInfo);
                                }
                            };
                        };
                        RecordResponseStatus(resp);
                    };
                }
                catch (System.Net.WebException ex) {
                    if (((FtpWebResponse)ex.Response).StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable) {
                        RecordResponseStatus((FtpWebResponse)ex.Response);
                    }
                    else {
                        throw ex;
                    }
                }
                if (fileSpec.Trim().Length == 0 || fileSpec.Trim() == "/*") {
                    //Solo conserva cache de la lista de archivos cuando se solicita sin comodines
                    _fileInfoList = fileList;
                    _lastWorkingFolderListed = _workingFolder;
                    _fileInfoListDirty = false;
                }
                return fileList;
            }
            else {
                return _fileInfoList;
            }
        }

        /// <summary>
        /// Sets the current remote folder
        /// </summary>
        /// <param name="folder">The folder to be considered the current remote folder</param>
        /// <returns>Allways return True</returns>
        /// <remarks>if folder begins with '/' it starts on the root FTP folder, otherwise it is relative to current remote folder</remarks>
        public bool ChangeFolder(string folder) {
            _fileInfoListDirty = true;
            folder = folder.Trim();
            if (folder == null || folder.Length == 0 || folder == "/") {
                _workingFolder = "";
                return true;
            }
            if (folder.StartsWith("/")) {
                //Sustitución completa de folder relativo
                _workingFolder = folder.Substring(1);
            }
            else {
                //Append de folder relativo
                _workingFolder += "/" + folder;
            }
            if (_workingFolder.EndsWith("/")) {
                _workingFolder = _workingFolder.Substring(0, _workingFolder.Length - 1);
            }
            if (_workingFolder.Length > 0 && !_workingFolder.StartsWith("/")) {
                _workingFolder = "/" + _workingFolder;
            }

            return true;
        }

        /// <summary>
        /// Gets the current remote folder
        /// </summary>
        /// <returns>The current remote folder name including the root folder</returns>
        public string GetCurrentFolder() {
            return RootFolder + _workingFolder;
        }

        /// <summary>
        /// Download an FTP file and saves it on a local file
        /// </summary>
        /// <param name="remoteFile">The name of the remote file to be downloaded</param>
        /// <param name="localFile">The name of the local file to where the remote file will be saved</param>
        /// <returns>True if successful, False if error</returns>
        public bool GetFile(string remoteFile, string localFile, bool binaryMode) {
            //antes lo leiamos en texto
            //string contenido = GetFileString(remoteFile, binaryMode);
            //if (contenido != null) {
            //    File.WriteAllText(localFile, contenido);
            //    return true;
            //}
            byte[] contenido = GetFileBinary(remoteFile);
            if (contenido != null) {
                File.WriteAllBytes(localFile, contenido);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the text contents of an FTP file
        /// </summary>
        /// <param name="remoteFile">The name of the remote file to be read</param>
        /// <returns>The contents of the file or Nothing if there was an error</returns>
        public string GetFileString(string remoteFile, bool binaryMode) {
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + remoteFile);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            req.UseBinary = binaryMode;

            using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse()) {
                RecordResponseStatus(resp);
                using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Gets the binary contents of an FTP file
        /// </summary>
        /// <param name="remoteFile">The name of the remote file to be read</param>
        /// <returns>The contents of the file or Nothing if there was an error</returns>
        public byte[] GetFileBinary(string remoteFile) {
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + remoteFile);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.DownloadFile;
            req.UseBinary = true;

            using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse()) {
                RecordResponseStatus(resp);
                byte[] buffer = new byte[32768];

                using (MemoryStream ms = new MemoryStream()) {
                    while (true) {
                        int read = resp.GetResponseStream().Read(buffer, 0, buffer.Length);
                        if (read <= 0) {
                            return ms.ToArray();
                        }
                        ms.Write(buffer, 0, read);
                    };
                };
            }
        }

        /// <summary>
        /// Calculate the time diff between local system and the FTP server
        /// </summary>
        /// <returns></returns>
        public System.TimeSpan GetTimeDiff() {
            string fileName = "integraasync.txt";
            DateTime current = DateTime.Now;
            DateTime fileDateTime = GetFileDateTime(fileName);
            //TimeSpan ret=TimeSpan.Zero;
            if (fileDateTime > DateTime.MinValue) {
                DeleteFile(fileName);
            }
            if (UploadFileString(fileName, "integraasync")) {
                fileDateTime = GetFileDateTime(fileName);
            }
            else {
                throw new ApplicationException("Problemas al subir el archivo para calculo de diferencia de tiempo con servidor FTP");
            }
            return current - fileDateTime;
        }

        /// <summary>
        /// Gets the size of a file on the FTP Server (this method does not use the filelist cache)
        /// </summary>
        /// <param name="filename">file name</param>
        /// <returns>The file size</returns>
        public long GetFileSize(string filename) {
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + filename);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.GetFileSize;

            //TODO: get the resulting error code to report success or failure
            using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse()) {
                RecordResponseStatus(resp);
                return resp.ContentLength;
            }
        }

        /// <summary>
        /// Deletes a file on the FTP Server
        /// </summary>
        /// <param name="filename">File name</param>
        /// <returns>Allways returns True</returns>
        public bool DeleteFile(string filename) {
            bool result = false;
            _fileInfoListDirty = true;
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + filename);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.DeleteFile;

            //TODO: get the resulting error code to report success or failure
            try {
                using (WebResponse resp = req.GetResponse()) {
                    RecordResponseStatus((FtpWebResponse)resp);
                    result = (((FtpWebResponse)resp).StatusCode == FtpStatusCode.FileActionOK);
                }
            }
            catch (System.Net.WebException ex) {
                if (ex.Response is FtpWebResponse) {
                    RecordResponseStatus((FtpWebResponse)ex.Response);
                    result = (((FtpWebResponse)ex.Response).StatusCode == FtpStatusCode.FileActionOK);
                }
                else {
                    throw ex;
                }
            };
            return result;
        }

        public bool DeleteFolder(string folderName) {
            bool result = false;
            _fileInfoListDirty = true;
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + folderName);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.RemoveDirectory;

            //TODO: get the resulting error code to report success or failure
            try {
                using (WebResponse resp = req.GetResponse()) {
                    RecordResponseStatus((FtpWebResponse)resp);
                    result = (((FtpWebResponse)resp).StatusCode == FtpStatusCode.FileActionOK);
                }
            }
            catch (System.Net.WebException ex) {
                if (ex.Response is FtpWebResponse) {
                    RecordResponseStatus((FtpWebResponse)ex.Response);
                    result = (((FtpWebResponse)ex.Response).StatusCode == FtpStatusCode.FileActionOK);
                }
                else {
                    throw ex;
                }
            };
            return result;
        }

        public bool RenameFile(string filename, string newFilename) {
            bool result = false;
            _fileInfoListDirty = true;
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + filename);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.Rename;
            req.RenameTo = newFilename;

            using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse()) {
                RecordResponseStatus(resp);
                result = (resp.StatusCode == FtpStatusCode.FileActionOK);
            }
            return result;
        }

        public DateTime GetFileDateTime(string filename) {
            DateTime ret = DateTime.MinValue;
            if (getFileTimeStampNotSupported) {
                return (GetFileDateTimeByListingFiles(filename));
            }

            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + filename);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.GetDateTimestamp;

            try {
                using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse()) {
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                        RecordResponseStatus(resp);
                        try {
                            Console.WriteLine(resp.ContentType.ToString());
                        }
                        catch (System.NotImplementedException) {
                            //Al parecer el servidor no soporta el metodo para obtener la fecha de un archivo
                            // asi que usamos otro metodo
                            getFileTimeStampNotSupported = true;
                            return (GetFileDateTimeByListingFiles(filename));
                        };
                    }
                }
            }
            catch (System.Net.WebException ex) {
                FtpWebResponse ftpResponse = (FtpWebResponse)ex.Response;
                if (ftpResponse.StatusCode != FtpStatusCode.ActionNotTakenFileUnavailable) {
                    throw ex;
                }
            }
            return ret;
        }

        public bool UploadFileString(string remoteFilename, string contents) {
            byte[] fileContents = Encoding.UTF8.GetBytes(contents);
            return UploadFileBytes(remoteFilename, fileContents);

        }

        public bool UploadFile(string localFilename, string remoteFilename) {
            //Lee el contenido del archivo
            try {
                StreamReader sourceStream = new StreamReader(localFilename);
                byte[] fileContents = Encoding.UTF8.GetBytes(sourceStream.ReadToEnd());
                sourceStream.Close();
                return UploadFileBytes(remoteFilename, fileContents);
            }
            catch (Exception ex) {
                LastErrorMessage = "Error en UploadFile:" + GetExceptionMessage(ex);
                return false;
            }
        }

        public bool UploadFileBytes(string remoteFilename, byte[] fileContents) {
            try {
                bool result = false;
                _fileInfoListDirty = true;
                var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + remoteFilename);
                req.Proxy = null;
                req.Credentials = new NetworkCredential(User, Password);
                req.Method = WebRequestMethods.Ftp.UploadFile;

                //Lee el contenido del archivo
                req.ContentLength = fileContents.Length;

                Stream requestStream = req.GetRequestStream();
                requestStream.Write(fileContents, 0, fileContents.Length);
                requestStream.Close();

                FtpWebResponse response = (FtpWebResponse)req.GetResponse();
                RecordResponseStatus(response);

                //string responseStatus = response.StatusDescription;
                result = (response.StatusCode == FtpStatusCode.ClosingData);
                response.Close();

                return result;
            }
            catch (Exception ex) {
                LastErrorMessage = "Error en UploadFileBytes:" + GetExceptionMessage(ex);
                return false;
            }
        }

        public bool CreateFolder(string newFolderName) {
            try {
                bool result = false;
                _fileInfoListDirty = true;
                var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + newFolderName);
                req.Proxy = null;
                req.Credentials = new NetworkCredential(User, Password);
                req.Method = WebRequestMethods.Ftp.MakeDirectory;

                FtpWebResponse response = (FtpWebResponse)req.GetResponse();
                RecordResponseStatus(response);

                result = response.StatusCode == FtpStatusCode.PathnameCreated;
                response.Close();
                return result;
            }
            catch (Exception ex) {
                LastErrorMessage = "Error en CreateFolder \r\n" + GetExceptionMessage(ex);
                return false;
            }
        }

        private DateTime GetFileDateTimeByListingFiles(string filename) {
            DateTime ret = DateTime.MinValue;
            //Si es necesario actualiza la lista de archivos de la carpeta actual
            if (_workingFolder != _lastWorkingFolderListed || _fileInfoListDirty) {
                GetFileListDetailed(null);
            }
            foreach (myFTPFileInfo fileInfo in _fileInfoList) {
                if (fileInfo.Nombre.ToLower() == filename.ToLower()) {
                    return fileInfo.Fecha;
                }
            }
            return ret;
        }

        private myFTPFileInfo GetFileInfo(string filename) {
            myFTPFileInfo ret = null;
            //Si es necesario actualiza la lista de archivos de la carpeta actual
            if (_workingFolder != _lastWorkingFolderListed || _fileInfoListDirty) {
                GetFileListDetailed(null);
            }
            foreach (myFTPFileInfo fileInfo in _fileInfoList) {
                if (fileInfo.Nombre.ToLower() == filename.ToLower()) {
                    return fileInfo;
                }
            }
            return ret;
        }

        private string GetExceptionMessage(Exception ex) {
            string ret = "";
            while (ex != null) {
                ret += "==>" + ex.Message + "\r\n";
                ex = ex.InnerException;
            }
            return ret;
        }

        private void RecordResponseStatus(FtpWebResponse response) {
            LastStatusCode = response.StatusCode;
            LastStatusDescription = response.StatusDescription;
        }
    }

}
