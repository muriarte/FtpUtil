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
        /// <param name="wildcard">A wildcard to specify the filenames to be searched for and listed. Example: "subfolder/*.txt"</param>
        /// <returns>A list with the filenames found</returns>
        public List<string> GetFileList(string wildcard) {
            if (wildcard == null) wildcard = "";
            if (wildcard.Length > 0 && !wildcard.StartsWith("/")) {
                wildcard = "/" + wildcard;
            }
            var ret = new List<string>();
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + wildcard);
            req.Proxy = null;
            req.EnableSsl = false;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.ListDirectory;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                while (!reader.EndOfStream) {
                    ret.Add(reader.ReadLine());
                };
            };
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
        /// <param name="wildcard">Wildcard filename specification. Can contain subfolders and *'s. Example: subfolder/*.txt</param>
        /// <returns>A detailed list of files found</returns>
        /// <remarks>If wildcard is specified the resulting file list is not cached</remarks>
        public List<myFTPFileInfo> GetFileListDetailed(string wildcard) {
            return GetFileListDetailed(wildcard, false);
        }

        /// <summary>
        /// Obtains a detailed list of the files in the current remote FTP folder according to a wildcard filename specification and allows to bypass the cached filelist
        /// </summary>
        /// <param name="wildcard">Wildcard filename specification. Can contain subfolders and *'s. Example: subfolder/*.txt</param>
        /// <param name="forceRefresh">If true the cache is ignored</param>
        /// <returns></returns>
        /// <remarks>If wildcard is specified or forceRefresh is set to true the resulting file list is not cached</remarks>
        public List<myFTPFileInfo> GetFileListDetailed(string wildcard, bool forceRefresh) {
            if (wildcard == null) {
                wildcard = "";
            }
            if (wildcard.Length > 0 && !wildcard.StartsWith("/")) {
                wildcard = "/" + wildcard;
            }

            List<myFTPFileInfo> fileList = new List<myFTPFileInfo>();
            if (forceRefresh ||
                        wildcard.Trim().Length > 0 ||
                        _fileInfoListDirty ||
                        _lastWorkingFolderListed != _workingFolder) {
                var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + wildcard);
                req.Proxy = null;
                req.EnableSsl = false;
                req.Credentials = new NetworkCredential(User, Password);
                req.Method = WebRequestMethods.Ftp.ListDirectoryDetails;

                myFTPFileInfo ftpFileInfo;
                _fileInfoList.Clear();
                using (WebResponse resp = req.GetResponse()) {
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                        while (!reader.EndOfStream) {
                            ftpFileInfo = new myFTPFileInfo(reader.ReadLine());
                            if (ftpFileInfo.Nombre != null && ftpFileInfo.Nombre.Trim().Length > 0) {
                                fileList.Add(ftpFileInfo);
                            }
                        };
                    };
                };
                if (wildcard.Trim().Length == 0) {
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
        public bool GetFile(string remoteFile, string localFile) {
            string contenido = GetFileString(remoteFile);
            if (contenido != null) {
                File.WriteAllText(localFile, contenido);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the contents of an FTP file
        /// </summary>
        /// <param name="remoteFile">The name of the remote file to be read</param>
        /// <returns>The contents of the file or Nothing if there was an error</returns>
        public string GetFileString(string remoteFile) {
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + remoteFile);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.DownloadFile;

            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                return reader.ReadToEnd();
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
            using (WebResponse resp = req.GetResponse())
                return resp.ContentLength;
            //using (WebResponse resp = req.GetResponse())
            //using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
            //    Console.WriteLine(reader.ReadToEnd());
            //return true;
        }

        /// <summary>
        /// Deletes a file on the FTP Server
        /// </summary>
        /// <param name="filename">File name</param>
        /// <returns>Allways returns True</returns>
        public bool DeleteFile(string filename) {
            _fileInfoListDirty = true;
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + filename);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.DeleteFile;

            //TODO: get the resulting error code to report success or failure
            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                Console.WriteLine(reader.ReadToEnd());
            return true;
        }

        public bool RenameFile(string filename, string newFilename) {
            _fileInfoListDirty = true;
            var req = (FtpWebRequest)WebRequest.Create("ftp://" + Server + RootFolder + _workingFolder + "/" + filename);
            req.Proxy = null;
            req.Credentials = new NetworkCredential(User, Password);
            req.Method = WebRequestMethods.Ftp.Rename;
            req.RenameTo = newFilename;

            //TODO: get the resulting error code to report success or failure
            using (WebResponse resp = req.GetResponse())
            using (StreamReader reader = new StreamReader(resp.GetResponseStream()))
                Console.WriteLine(reader.ReadToEnd());
            return true;
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
                using (WebResponse resp = req.GetResponse()) {
                    using (StreamReader reader = new StreamReader(resp.GetResponseStream())) {
                        try {
                            Console.WriteLine(resp.ContentType.ToString());
                        }
                        catch (System.NotImplementedException) {
                            //Al parecer el servidor no soporta el metodo para obtener la fecha de un archivo
                            // asi que usamos otro metodo
                            getFileTimeStampNotSupported = true;
                            return (GetFileDateTimeByListingFiles(filename));
                        };
                        Console.WriteLine(reader.ReadToEnd());
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
                Console.WriteLine("Error en UploadFile:" + GetExceptionMessage(ex));
                return false;
            }
        }

        public bool UploadFileBytes(string remoteFilename, byte[] fileContents) {
            try {
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

                string responseStatus = response.StatusDescription;
                response.Close();

                return responseStatus.StartsWith("226 ");
            }
            catch (Exception ex) {
                Console.WriteLine("Error en UploadFileBytes:" + GetExceptionMessage(ex));
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
    }

}
